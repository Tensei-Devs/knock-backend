#!/usr/bin/env python3
"""Genera las cifras de evidencia del backend (docs/evaluacion/generado/cifras.json y cifras.md).

Solo lectura del repositorio. Sin red por omisión. Opciones:
  --gh      consulta el estado de GitHub con `gh` (protección de ramas, rulesets, seguridad, ejecuciones de ci-deploy)
  --sonar   lee el estado del quality gate de SonarCloud por la API pública (sin token)
Cada cifra lleva su método: «estático» (se cuenta en archivos del repositorio, NO es una corrida de pruebas) o «consulta».
Uso: python docs/evaluacion/generar_evidencia.py [--gh] [--sonar] [--sonar-project Tensei-Devs_knock-backend]
"""
from __future__ import annotations

import argparse
import json
import re
import subprocess
import sys
import urllib.error
import urllib.request
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = Path(__file__).resolve().parent / "generado"
METHODS = {"get", "put", "post", "delete", "patch", "head", "options"}


def run(*args: str) -> str:
    result = subprocess.run(args, cwd=ROOT, capture_output=True, text=True, encoding="utf-8", errors="replace")
    return result.stdout.strip() if result.returncode == 0 else ""


def read(path: Path) -> str:
    return path.read_text(encoding="utf-8", errors="replace")


def endpoints() -> dict:
    result: dict = {"metodo": "estático: operaciones de los openapi*.json (ruta × método)", "por_contrato": {}}
    total = 0
    for spec in sorted((ROOT / "docs").glob("openapi*.json")):
        paths = json.loads(read(spec)).get("paths", {})
        count = sum(1 for item in paths.values() for method in item if method.lower() in METHODS)
        result["por_contrato"][spec.name] = count
        total += count
    result["total"] = total
    return result


def tests() -> dict:
    """Recuento ESTÁTICO de [Fact]/[Theory] por proyecto de pruebas. Una Theory con varios datos cuenta una vez: no es el número de casos de una corrida."""
    result: dict = {"metodo": "estático: atributos [Fact] y [Theory] en tests/<Tipo> (NO es una corrida; una Theory cuenta una vez)", "por_tipo": {}}
    for kind in ("UnitTest", "IntegrationTest", "SecurityTest", "DatabaseTest", "ArchitectureTest", "ContractTest", "PerformanceTest"):
        folder = ROOT / "tests" / kind
        facts = theories = 0
        if folder.is_dir():
            for file in folder.rglob("*.cs"):
                if "obj" in file.parts or "bin" in file.parts:
                    continue
                text = read(file)
                facts += len(re.findall(r"\[Fact(?:\(|\])", text))
                theories += len(re.findall(r"\[Theory(?:\(|\])", text))
        result["por_tipo"][kind] = {"facts": facts, "theories": theories}
    return result


def agentes_y_skills() -> dict:
    def count_files(folder: str, pattern: str) -> int:
        path = ROOT / folder
        return len(list(path.glob(pattern))) if path.is_dir() else 0

    def count_dirs(folder: str) -> int:
        path = ROOT / folder
        return len([p for p in path.iterdir() if p.is_dir()]) if path.is_dir() else 0

    return {
        "metodo": "estático: archivos por carpeta. La canónica es .agents/ (las demás son reflejos); hay que fijar y documentar cuál cuenta",
        "agentes": {
            ".agents/agents": count_files(".agents/agents", "*.md"),
            ".claude/agents": count_files(".claude/agents", "*.md"),
            ".github/agents": count_files(".github/agents", "*.agent.md"),
            ".opencode/agents": count_files(".opencode/agents", "*.md"),
        },
        "skills": {
            ".agents/skills": count_dirs(".agents/skills"),
            ".claude/skills": count_dirs(".claude/skills"),
            ".opencode/skills": count_dirs(".opencode/skills"),
        },
    }


def memoria() -> dict:
    historial = ROOT / "Memoria.md"
    entradas = [p for p in (ROOT / "docs" / "memoria").glob("*.md") if p.name not in ("README.md", "INDEX.md")] if (ROOT / "docs" / "memoria").is_dir() else []
    text = read(historial) if historial.exists() else ""
    return {
        "metodo": "estático: Memoria.md (historial hasta 2026-11-20) y docs/memoria/ (un archivo por PR desde 2026-11-21)",
        "memoria_md_lineas": len(text.splitlines()),
        "memoria_md_secciones": len(re.findall(r"^## ", text, re.M)),
        "entradas_por_tarea": len(entradas),
    }


def specs() -> dict:
    folders = [p for p in (ROOT / "specs").iterdir() if p.is_dir()] if (ROOT / "specs").is_dir() else []
    states: dict[str, int] = {}
    for folder in folders:
        task = folder / "task.md"
        if not task.exists():
            continue
        for line in read(task).splitlines():
            if re.match(r"\| T-\d+ ", line):
                cells = [c.strip() for c in line.strip().strip("|").split("|")]
                state = cells[-1].lower()
                key = "hecha" if state.startswith("hecha") else "parcial" if state.startswith(("parcial", "en revisión", "en revision")) else "pendiente" if state.startswith("pendiente") else "otro"
                states[key] = states.get(key, 0) + 1
    return {"metodo": "estático: filas de tabla de specs/*/task.md por la palabra con que empieza el estado (task.md puede estar desactualizado)", "specs": len(folders), "tareas_por_estado": states}


def replicas() -> dict:
    result: dict = {"metodo": "estático: bloque replicas de deploy/apps/envs/*.tfvars (NO es el estado desplegado; para eso, az containerapp replica list)", "entornos": {}}
    for tfvars in sorted((ROOT / "deploy" / "apps" / "envs").glob("*.tfvars")):
        text = read(tfvars)
        result["entornos"][tfvars.stem] = {name: {"min": int(a), "max": int(b)} for name, a, b in re.findall(r"(\w+)\s*=\s*\{\s*min\s*=\s*(\d+)\s*,\s*max\s*=\s*(\d+)\s*\}", text)}
    return result


def github() -> dict:
    result: dict = {"metodo": "consulta: gh api (solo lectura)"}
    repo = run("gh", "repo", "view", "--json", "nameWithOwner", "--jq", ".nameWithOwner")
    if not repo:
        return {**result, "disponible": False}
    result["repositorio"] = repo
    result["rama_predeterminada"] = run("gh", "repo", "view", "--json", "defaultBranchRef", "--jq", ".defaultBranchRef.name")
    result["rulesets"] = run("gh", "api", f"repos/{repo}/rulesets", "--jq", "length")
    result["seguridad"] = run("gh", "api", f"repos/{repo}", "--jq", ".security_and_analysis | with_entries(.value |= .status)")
    result["ci_deploy_ultimas"] = run("gh", "run", "list", "--workflow", "ci-deploy", "--limit", "5", "--json", "conclusion,createdAt", "--jq", "[.[]|{conclusion,createdAt}]")
    return result


def sonar(project: str) -> dict:
    """Estado del quality gate por la API pública de SonarCloud (no pide token en proyectos públicos).
    `quality_gate_manual` queda vacío para que lo capture una persona si la API no responde."""
    result: dict = {"metodo": "consulta: https://sonarcloud.io/api/qualitygates/project_status (rama principal)", "proyecto": project, "quality_gate_manual": ""}
    url = f"https://sonarcloud.io/api/qualitygates/project_status?projectKey={project}"
    try:
        with urllib.request.urlopen(url, timeout=15) as response:  # noqa: S310 (URL fija de SonarCloud)
            data = json.load(response)
        status = data.get("projectStatus", {})
        result["quality_gate"] = status.get("status", "DESCONOCIDO")
        result["condiciones"] = [{"metrica": c.get("metricKey"), "estado": c.get("status")} for c in status.get("conditions", [])]
        result["nota"] = "NONE = todavía no hay análisis" if result["quality_gate"] == "NONE" else ""
    except (urllib.error.URLError, TimeoutError, json.JSONDecodeError) as exc:
        result["quality_gate"] = "NO DISPONIBLE"
        result["error"] = type(exc).__name__
        result["nota"] = "No se pudo leer la API pública: captura el estado a mano en quality_gate_manual"
    return result


def markdown(data: dict) -> str:
    lines = [f"# Cifras de evidencia del backend", "", f"Generado el {data['fecha']} sobre el commit `{data['sha']}` (rama `{data['rama']}`). **No edites este archivo a mano:** corre `python docs/evaluacion/generar_evidencia.py`.", ""]
    ep = data["endpoints"]
    lines += ["## Endpoints", f"_{ep['metodo']}_", "", "| Contrato | Operaciones |", "|---|---|"]
    lines += [f"| `{name}` | {n} |" for name, n in ep["por_contrato"].items()]
    lines += [f"| **Total** | **{ep['total']}** |", ""]
    t = data["pruebas"]
    lines += ["## Pruebas (recuento estático)", f"_{t['metodo']}_", "", "| Tipo | `[Fact]` | `[Theory]` |", "|---|---|---|"]
    lines += [f"| {k} | {v['facts']} | {v['theories']} |" for k, v in t["por_tipo"].items()]
    lines += ["", "Las cifras de una corrida real (casos ejecutados, cobertura) salen del artefacto `api-reports` y del log del job «API — build, formato y pruebas» de `ci-pr`; no se mezclan con este recuento.", ""]
    a = data["agentes_y_skills"]
    lines += ["## Agentes y skills", f"_{a['metodo']}_", "", "| Carpeta | Cantidad |", "|---|---|"]
    lines += [f"| agentes · `{k}` | {v} |" for k, v in a["agentes"].items()] + [f"| skills · `{k}` | {v} |" for k, v in a["skills"].items()]
    m = data["memoria"]
    lines += ["", "## Memoria", f"_{m['metodo']}_", "", f"- `Memoria.md`: {m['memoria_md_lineas']} líneas y {m['memoria_md_secciones']} secciones.", f"- `docs/memoria/`: {m['entradas_por_tarea']} entradas por tarea.", ""]
    s = data["specs"]
    lines += ["## Specs", f"_{s['metodo']}_", "", f"- Specs: {s['specs']}.", "- Tareas por estado: " + ", ".join(f"{k} {v}" for k, v in sorted(s["tareas_por_estado"].items())), ""]
    r = data["replicas"]
    lines += ["## Réplicas por entorno", f"_{r['metodo']}_", ""]
    for env, services in r["entornos"].items():
        lines.append(f"- `{env}`: " + ", ".join(f"{n} {v['min']}-{v['max']}" for n, v in services.items()))
    if "github" in data:
        g = data["github"]
        lines += ["", "## GitHub", f"_{g['metodo']}_", "", f"```\n{json.dumps({k: v for k, v in g.items() if k != 'metodo'}, ensure_ascii=False, indent=2)}\n```"]
    if "sonarcloud" in data:
        q = data["sonarcloud"]
        lines += ["", "## SonarCloud", f"_{q['metodo']}_", "", f"- Proyecto: `{q['proyecto']}`", f"- **Quality gate: {q.get('quality_gate')}**" + (f" ({q['nota']})" if q.get("nota") else ""), f"- Captura manual (si la API no responde): {q['quality_gate_manual'] or '_vacío_'}"]
    lines.append("")
    return "\n".join(lines)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--gh", action="store_true")
    parser.add_argument("--sonar", action="store_true")
    parser.add_argument("--sonar-project", default="Tensei-Devs_knock-backend")
    args = parser.parse_args()
    data = {
        "fecha": datetime.now(timezone.utc).strftime("%Y-%m-%d"),
        "sha": run("git", "rev-parse", "--short", "HEAD") or "desconocido",
        "rama": run("git", "rev-parse", "--abbrev-ref", "HEAD") or "desconocida",
        "endpoints": endpoints(),
        "pruebas": tests(),
        "agentes_y_skills": agentes_y_skills(),
        "memoria": memoria(),
        "specs": specs(),
        "replicas": replicas(),
    }
    if args.gh:
        data["github"] = github()
    if args.sonar:
        data["sonarcloud"] = sonar(args.sonar_project)
    OUT.mkdir(parents=True, exist_ok=True)
    (OUT / "cifras.json").write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    (OUT / "cifras.md").write_text(markdown(data), encoding="utf-8", newline="\n")
    print(f"Escrito {OUT.relative_to(ROOT)}/cifras.json y cifras.md ({data['endpoints']['total']} operaciones)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
