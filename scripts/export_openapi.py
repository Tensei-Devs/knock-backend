#!/usr/bin/env python3
"""Exporta el contrato OpenAPI de un servicio de Knock (Market o Identity) a docs/.

Arranca la API en Development en un puerto libre de 127.0.0.1, lee /openapi/v1.json, quita lo que cambia entre ejecuciones
(la lista «servers», que lleva el puerto) y guarda el JSON con formato estable y salto de línea LF. No agrega paquetes
ni toca la base de datos: solo lee el documento que la API ya publica en Development. La API no arranca sin
ConnectionStrings:KnockMongo (TT-BD-06), así que se le da una cadena sintética hacia un destino que no responde, igual que
KnockApiFactory en las pruebas: la exportación no se conecta a MongoDB y nunca usa una cadena real del entorno.
Identity además valida al arrancar Redis, la clave del JWT, Firebase y las llaves del celular: se le dan valores sintéticos
generados al azar en cada ejecución (nada se escribe en disco ni se lee del entorno real; ver identity_environment).

Uso:
  python scripts/export_openapi.py                         Market -> docs/openapi.v1.json (compila en Release)
  python scripts/export_openapi.py --service identity      Identity -> docs/openapi.identity.v1.json
  python scripts/export_openapi.py --no-build              usa la compilación existente (así lo llama el CI)
  python scripts/export_openapi.py --output ruta.json
  python scripts/export_openapi.py --publico               contrato público docs/openapi.publico.v1.json (spec 034 R-13): une los
                                                           contratos de servicio de docs/ con las rutas del gateway; no compila ni arranca nada
  python scripts/export_openapi.py --publico --check       falla (nombrando el archivo) si el público del repositorio está desactualizado
  python scripts/export_openapi.py --comprobar-publico     R-19: falla si el público trae servers, hosts internos o ejemplos con datos personales

CI (ci-pr.yml): después de exportar los dos servicios, `git diff --exit-code` sobre ambos archivos falla si el contrato
cambió sin regenerarse. Nunca se edita ningún docs/openapi*.json a mano.
"""
from __future__ import annotations

import argparse
import base64
import json
import os
import re
import secrets
import signal
import socket
import subprocess
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DEFAULT_SERVICE = "market"
# Servicio -> (carpeta del proyecto en src/, archivo del contrato en docs/). El de Market conserva su nombre histórico
# (docs/openapi.v1.json) porque el frontend y la publicación del contrato dependen de esa ruta.
SERVICES: dict[str, tuple[str, str]] = {
    "market": ("Knock.Market.Api", "openapi.v1.json"),
    "identity": ("Knock.Identity.Api", "openapi.identity.v1.json"),
    "messaging": ("Knock.Messaging.Api", "openapi.messaging.v1.json"),
}
PUBLIC_FILE = "openapi.publico.v1.json"
GATEWAY_SETTINGS = ROOT / "src" / "Knock.Gateway" / "appsettings.json"
# Cluster del gateway -> servicio cuyo contrato atiende esas rutas.
CLUSTER_SERVICE = {"identidad": "identity", "mercado": "market", "mensajeria": "messaging"}
HTTP_METHODS = {"get", "put", "post", "delete", "options", "head", "patch", "trace"}
INTERNAL_HOSTS = ("knock-identity", "knock-market", "knock-messaging", "knock-redis", ".internal.")
PII_PATTERNS = {
    "celular +52": re.compile(r"\+52"),
    "CURP": re.compile(r"\b[A-Z]{4}\d{6}[HM][A-Z]{5}[A-Z0-9]\d\b"),
    "correo": re.compile(r"[\w.+-]+@[\w-]+\.[\w.-]+"),
}
# Cadena sintética: puerto 9 (descarte) en loopback, sin credenciales. La API solo la valida al arrancar; el cliente no conecta.
UNREACHABLE_MONGO = "mongodb://127.0.0.1:9/?directConnection=true"


def project_path(service: str) -> Path:
    return ROOT / "src" / SERVICES[service][0] / f"{SERVICES[service][0]}.csproj"


def default_output(service: str) -> Path:
    return ROOT / "docs" / SERVICES[service][1]


def random_base64(size: int = 32) -> str:
    return base64.b64encode(secrets.token_bytes(size)).decode("ascii")


def identity_environment() -> dict[str, str]:
    """Configuración mínima y sintética para que Identity arranque en Development (mismas claves que IdentityApiFactory).

    Todo secreto se genera al azar en cada ejecución y solo vive en el entorno del proceso hijo: no hay ninguno literal.
    Mongo y Redis apuntan a destinos que no responden; Firebase usa un proyecto de demostración."""
    return {
        "ConnectionStrings__KnockRedis": "127.0.0.1:6379,password=" + secrets.token_hex(16),
        "Jwt__SigningKey": random_base64(),
        "Firebase__ProjectId": "demo-knock",
        "Identity__PhoneProtection__EncryptionKeys__1": random_base64(),
        "Identity__PhoneProtection__HmacKeys__1": random_base64(),
        "Identity__PhoneProtection__EncryptionKeyVersion": "1",
        "Identity__PhoneProtection__HmacKeyVersion": "1",
    }


def messaging_environment() -> dict[str, str]:
    """Configuración sintética para que Messaging arranque en Development: Redis hacia un destino que no responde y llave del JWT al azar."""
    return {
        "ConnectionStrings__KnockRedis": "127.0.0.1:6379,password=" + secrets.token_hex(16),
        "Jwt__SigningKey": random_base64(),
    }


def service_environment(service: str) -> dict[str, str]:
    if service == "identity":
        return identity_environment()
    if service == "messaging":
        return messaging_environment()
    return {"Busqueda__Cursor__Key": random_base64()}


def parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--service", choices=sorted(SERVICES), default=DEFAULT_SERVICE, help="servicio a exportar (por defecto market)")
    parser.add_argument("--no-build", action="store_true", help="no compila: usa la compilación Release existente")
    parser.add_argument("--output", type=Path, default=None, help="archivo de salida (por defecto docs/openapi.v1.json o docs/openapi.identity.v1.json)")
    parser.add_argument("--publico", action="store_true", help="genera el contrato público docs/openapi.publico.v1.json a partir de docs/ y el gateway")
    parser.add_argument("--check", action="store_true", help="con --publico: no escribe; falla si el archivo del repositorio difiere")
    parser.add_argument("--comprobar-publico", action="store_true", help="comprueba que el contrato público no revele datos internos (R-19)")
    parser.add_argument("--timeout", type=int, default=90, help="segundos de espera a que la API responda")
    return parser.parse_args(argv)


def free_port() -> int:
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as sock:
        sock.bind(("127.0.0.1", 0))
        return sock.getsockname()[1]


def stop(process: subprocess.Popen[bytes]) -> None:
    """Detiene la API y los procesos que `dotnet run` haya creado."""
    if process.poll() is not None:
        return
    if os.name == "nt":
        subprocess.run(["taskkill", "/PID", str(process.pid), "/T", "/F"], capture_output=True, check=False)
    else:
        os.killpg(os.getpgid(process.pid), signal.SIGTERM)
    try:
        process.wait(timeout=15)
    except subprocess.TimeoutExpired:
        process.kill()


def fetch(url: str, process: subprocess.Popen[bytes], timeout: int) -> bytes:
    deadline = time.monotonic() + timeout
    last_error = "sin respuesta"
    while time.monotonic() < deadline:
        if process.poll() is not None:
            raise RuntimeError(f"la API terminó antes de responder (código {process.returncode})")
        try:
            with urllib.request.urlopen(url, timeout=5) as response:
                return response.read()
        except (urllib.error.URLError, ConnectionError, TimeoutError) as error:
            last_error = type(error).__name__
            time.sleep(1)
    raise RuntimeError(f"la API no respondió en {timeout} s ({last_error})")


def unify_newlines(value: object) -> object:
    """Los comentarios XML de los .cs traen CRLF cuando el código se descargó en Windows y LF en Linux (CI): se unifican a LF
    para que el contrato salga igual en cualquier sistema."""
    if isinstance(value, str):
        return value.replace("\r\n", "\n")
    if isinstance(value, list):
        return [unify_newlines(item) for item in value]
    if isinstance(value, dict):
        return {key: unify_newlines(item) for key, item in value.items()}
    return value


def normalize(raw: bytes) -> str:
    document = unify_newlines(json.loads(raw))
    assert isinstance(document, dict)
    document.pop("servers", None)
    return json.dumps(document, indent=2, ensure_ascii=False) + "\n"


class PublicContractError(Exception):
    """Conflicto en el contrato público; el mensaje nombra la ruta o el esquema."""


def route_regex(pattern: str) -> re.Pattern[str]:
    """Patrón de ruta de YARP -> expresión regular: `{**x}` = cero o más segmentos al final (incluida la ruta raíz, como en
    el enrutamiento de ASP.NET Core), `{x}` = un segmento."""
    expression = ""
    for segment in pattern.strip("/").split("/"):
        if segment.startswith("{**"):
            expression += "(?:/.*)?"
        elif segment.startswith("{") and segment.endswith("}"):
            expression += "/[^/]+"
        else:
            expression += "/" + re.escape(segment)
    return re.compile("^" + expression + "$")


def gateway_routes(settings: dict) -> list[tuple[str, re.Pattern[str], set[str] | None]]:
    """(servicio, regex de la ruta, verbos permitidos o None) por cada ruta del gateway; el canal de tiempo real queda fuera."""
    result = []
    for route in settings["ReverseProxy"]["Routes"].values():
        service = CLUSTER_SERVICE.get(route["ClusterId"])
        path = route["Match"]["Path"]
        if service is None or path.startswith("/hubs/"):
            continue
        methods = route["Match"].get("Methods")
        result.append((service, route_regex(path), {m.lower() for m in methods} if methods else None))
    return result


def exposed(service: str, path: str, method: str, routes: list[tuple[str, re.Pattern[str], set[str] | None]]) -> bool:
    concrete = path.replace("{version}", "1")
    return any(svc == service and regex.match(concrete) and (methods is None or method in methods) for svc, regex, methods in routes)


def collect_refs(node: object, found: set[tuple[str, str]]) -> None:
    if isinstance(node, dict):
        ref = node.get("$ref")
        if isinstance(ref, str) and ref.startswith("#/components/"):
            section, _, name = ref[len("#/components/"):].partition("/")
            found.add((section, name))
        for value in node.values():
            collect_refs(value, found)
    elif isinstance(node, list):
        for value in node:
            collect_refs(value, found)


def merge_paths(documents: dict[str, dict], routes: list[tuple[str, re.Pattern[str], set[str] | None]]) -> dict[str, dict]:
    paths: dict[str, dict] = {}
    owners: dict[tuple[str, str], str] = {}
    for service in sorted(documents):
        for path, item in documents[service]["paths"].items():
            for method, operation in item.items():
                if method not in HTTP_METHODS or not exposed(service, path, method, routes):
                    continue
                if (path, method) in owners:
                    raise PublicContractError(f"conflicto: {method.upper()} {path} la definen {owners[(path, method)]} y {service}")
                owners[(path, method)] = service
                paths.setdefault(path, {})[method] = operation
    return paths


def merge_components(documents: dict[str, dict]) -> dict[str, dict[str, tuple[str, object]]]:
    components: dict[str, dict[str, tuple[str, object]]] = {}
    for service in sorted(documents):
        for section, items in documents[service].get("components", {}).items():
            for name, value in items.items():
                previous = components.setdefault(section, {}).get(name)
                if previous is not None and previous[1] != value:
                    raise PublicContractError(f"conflicto: el esquema {section}/{name} difiere entre {previous[0]} y {service}")
                components[section].setdefault(name, (service, value))
    return components


def reachable(paths: dict[str, dict], components: dict[str, dict[str, tuple[str, object]]]) -> dict[str, dict[str, object]]:
    pending: set[tuple[str, str]] = set()
    collect_refs(paths, pending)
    kept: dict[str, dict[str, object]] = {}
    while pending:
        section, name = pending.pop()
        if name in kept.get(section, {}) or name not in components.get(section, {}):
            continue
        value = components[section][name][1]
        kept.setdefault(section, {})[name] = value
        collect_refs(value, pending)
    for name, (_, value) in components.get("securitySchemes", {}).items():
        kept.setdefault("securitySchemes", {})[name] = value
    return kept


def build_public(documents: dict[str, dict], settings: dict) -> dict:
    """Une los contratos de servicio limitados a las rutas del gateway (R-13). Falla si dos servicios definen la misma ruta y verbo
    o un esquema con el mismo nombre y distinto contenido."""
    paths = merge_paths(documents, gateway_routes(settings))
    kept = reachable(paths, merge_components(documents))
    first = documents[sorted(documents)[0]]
    public: dict = {
        "openapi": first["openapi"],
        "info": {"title": "Knock API", "version": first.get("info", {}).get("version", "1.0.0")},
        "paths": {path: {m: paths[path][m] for m in sorted(paths[path])} for path in sorted(paths)},
        "components": {section: {n: kept[section][n] for n in sorted(kept[section])} for section in sorted(kept)},
    }
    tags = sorted({tag["name"] for document in documents.values() for tag in document.get("tags", [])})
    if tags:
        public["tags"] = [{"name": tag} for tag in tags]
    return public


def examples(node: object) -> list[object]:
    found: list[object] = []
    if isinstance(node, dict):
        for key, value in node.items():
            if key in ("example", "examples"):
                found.append(value)
            else:
                found.extend(examples(value))
    elif isinstance(node, list):
        for value in node:
            found.extend(examples(value))
    return found


def check_public(document: dict) -> list[str]:
    """R-19: problemas que impiden publicar el contrato (vacío = limpio)."""
    problems = []
    if "servers" in document:
        problems.append("el contrato público trae «servers»")
    text = json.dumps(document, ensure_ascii=False)
    problems.extend(f"host interno «{host}»" for host in INTERNAL_HOSTS if host in text)
    for example in examples(document):
        sample = json.dumps(example, ensure_ascii=False)
        problems.extend(f"ejemplo con {label}" for label, pattern in PII_PATTERNS.items() if pattern.search(sample))
    return problems


def render(document: dict) -> str:
    return json.dumps(document, indent=2, ensure_ascii=False) + "\n"


def run_public(args: argparse.Namespace) -> int:
    output = args.output if args.output is not None else ROOT / "docs" / PUBLIC_FILE
    if args.comprobar_publico:
        problems = check_public(json.loads(output.read_text(encoding="utf-8")))
        for problem in problems:
            print(f"ERROR: {output.name}: {problem}", file=sys.stderr)
        return 1 if problems else 0
    documents = {service: json.loads(default_output(service).read_text(encoding="utf-8")) for service in SERVICES}
    try:
        text = render(build_public(documents, json.loads(GATEWAY_SETTINGS.read_text(encoding="utf-8"))))
    except PublicContractError as error:
        print(f"ERROR: {error}", file=sys.stderr)
        return 1
    if args.check:
        current = output.read_text(encoding="utf-8") if output.exists() else ""
        if current.replace("\r\n", "\n") != text:
            print(f"ERROR: {output.name} está desactualizado: regéneralo con scripts/export_openapi.py --publico y súbelo con el código.", file=sys.stderr)
            return 1
        return 0
    output.write_text(text, encoding="utf-8", newline="\n")
    print(f"OK — contrato público exportado a {output.name} ({len(text.splitlines())} líneas)")
    return 0


def main() -> int:
    args = parse_args()
    if args.publico or args.comprobar_publico:
        return run_public(args)
    output = args.output if args.output is not None else default_output(args.service)

    port = free_port()
    command = ["dotnet", "run", "--project", str(project_path(args.service)), "-c", "Release", "--no-launch-profile"]
    if args.no_build:
        command.append("--no-build")
    environment = {
        **os.environ,
        "ASPNETCORE_ENVIRONMENT": "Development",
        "ASPNETCORE_URLS": f"http://127.0.0.1:{port}",
        "ConnectionStrings__KnockMongo": UNREACHABLE_MONGO,
        **service_environment(args.service),
    }
    popen_options: dict[str, object] = {"start_new_session": True} if os.name != "nt" else {}
    process = subprocess.Popen(command, cwd=ROOT, env=environment, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, **popen_options)
    try:
        raw = fetch(f"http://127.0.0.1:{port}/openapi/v1.json", process, args.timeout)
    except RuntimeError as error:
        print(f"ERROR: {error}", file=sys.stderr)
        return 1
    finally:
        stop(process)

    try:
        text = normalize(raw)
    except json.JSONDecodeError as error:
        print(f"ERROR: la respuesta no es JSON válido ({error.msg})", file=sys.stderr)
        return 1
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(text, encoding="utf-8", newline="\n")
    print(f"OK — contrato exportado a {output.relative_to(ROOT) if output.is_relative_to(ROOT) else output} ({len(text.splitlines())} líneas)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
