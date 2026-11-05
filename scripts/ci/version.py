#!/usr/bin/env python3
"""Versionamiento por puntos de Knock (spec 012): cálculo de la versión X.Y.Z y comprobación de la etiqueta de puntos.

Reglas (spec 012):
  R-02  X sube con un cambio que rompe lo anterior, Y con funciones nuevas acumuladas y Z con cambios pequeños.
  R-03  Puntos por tipo de cambio: ver POINTS (se documentan en CONTRIBUTING.md).
  R-04  Cada PR declara sus puntos por componente con la etiqueta `puntos-<componente>-N`.
  R-05  Al desplegar se suman los puntos y se aplican a Z; al llegar a 10, Z vuelve a cero y Y sube; lo mismo entre Y y X.
  R-06  Un cambio que rompe compatibilidad sube X y deja Y y Z en cero.
  R-07  Un despliegue con cero puntos no cambia la versión.

Uso (sin dependencias):
  python scripts/ci/version.py bump --current 1.0.0 --points 4 [--breaking]       # imprime la versión nueva
  python scripts/ci/version.py points --component gateway --labels '["puntos-gateway-3","puntos-app-1"]'
  PR_LABELS='["puntos-gateway-3"]' python scripts/ci/version.py check-labels [--head <rama>]   # CA-04: falla sin etiqueta válida
"""
from __future__ import annotations

import argparse
import json
import os
import re
import sys

FIRST_VERSION = "1.0.0"  # R-01
COMPONENTS = ("gateway", "identity", "messaging", "market", "assistant", "contracts", "infra", "app", "panel", "landing")
# R-03: puntos por tipo de cambio (la tabla de CONTRIBUTING.md debe coincidir).
POINTS = {
    "fix": 1, "adjustment": 1, "index": 1,
    "new-field": 2, "rule-change": 2,
    "endpoint": 3, "screen": 3,
    "collection": 5, "full-feature": 5,
    "service": 10, "external-integration": 10, "permission": 10,
}
LABEL_PREFIX = "puntos-"
LABEL = re.compile(rf"^{LABEL_PREFIX}({'|'.join(COMPONENTS)})-(0|[1-9][0-9]{{0,2}})$")
VERSION = re.compile(r"^(0|[1-9][0-9]*)\.([0-9])\.([0-9])$")
# Ramas de bots que no declaran puntos (release-please y Dependabot).
EXEMPT_HEAD = re.compile(r"^(release-please--|dependabot/)")


def parse_version(text: str) -> tuple[int, int, int]:
    m = VERSION.match(text.strip())
    if not m:
        raise ValueError(f"Versión inválida «{text}»: se espera X.Y.Z con Y y Z de un dígito (0 a 9).")
    return int(m.group(1)), int(m.group(2)), int(m.group(3))


def bump(current: str, points: int, breaking: bool = False) -> str:
    """Versión que resulta de desplegar `points` puntos sobre `current` (R-05, R-06, R-07)."""
    if points < 0:
        raise ValueError("Los puntos no pueden ser negativos.")
    x, y, z = parse_version(current)
    if breaking:
        return f"{x + 1}.0.0"
    if points == 0:
        return f"{x}.{y}.{z}"
    total = x * 100 + y * 10 + z + points  # los acarreos Z→Y e Y→X son los de un número en base 10
    return f"{total // 100}.{(total // 10) % 10}.{total % 10}"


def parse_labels(labels: list[str]) -> tuple[dict[str, int], list[str]]:
    """Devuelve (puntos por componente, errores). Una etiqueta `puntos-…` mal formada o repetida por componente es error."""
    found: dict[str, int] = {}
    errors: list[str] = []
    for name in labels:
        if not name.startswith(LABEL_PREFIX):
            continue
        m = LABEL.match(name)
        if not m:
            errors.append(f"Etiqueta «{name}» inválida: usa puntos-<componente>-N con componente en {', '.join(COMPONENTS)} y N entero.")
        elif m.group(1) in found:
            errors.append(f"El componente «{m.group(1)}» tiene más de una etiqueta de puntos.")
        else:
            found[m.group(1)] = int(m.group(2))
    return found, errors


def check_labels(labels: list[str], head: str = "") -> list[str]:
    """CA-04: un PR sin etiqueta de puntos válida falla."""
    if head and EXEMPT_HEAD.match(head):
        return []
    found, errors = parse_labels(labels)
    if not found and not errors:
        errors.append("El PR no tiene etiqueta de puntos: agrega puntos-<componente>-N (p. ej. puntos-gateway-3; 0 si no suma). Ver CONTRIBUTING.md.")
    return errors


def load_labels(raw: str) -> list[str]:
    data = json.loads(raw or "[]")
    if not isinstance(data, list) or not all(isinstance(i, str) for i in data):
        raise ValueError("Las etiquetas deben ser una lista JSON de textos.")
    return data


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = parser.add_subparsers(dest="cmd", required=True)
    b = sub.add_parser("bump"); b.add_argument("--current", default=FIRST_VERSION); b.add_argument("--points", type=int, required=True); b.add_argument("--breaking", action="store_true")
    p = sub.add_parser("points"); p.add_argument("--component", required=True, choices=COMPONENTS); p.add_argument("--labels", required=True, help="lista JSON de etiquetas")
    c = sub.add_parser("check-labels"); c.add_argument("--labels", default=os.environ.get("PR_LABELS", "[]")); c.add_argument("--head", default=os.environ.get("HEAD_REF", ""))
    args = parser.parse_args()
    try:
        if args.cmd == "bump":
            print(bump(args.current, args.points, args.breaking))
            return 0
        if args.cmd == "points":
            found, errors = parse_labels(load_labels(args.labels))
            for e in errors:
                print(f"✗ {e}", file=sys.stderr)
            print(found.get(args.component, 0))
            return 1 if errors else 0
        errors = check_labels(load_labels(args.labels), args.head)
    except ValueError as exc:  # json.JSONDecodeError es un ValueError
        errors = [str(exc)]
    for e in errors:
        print(f"::error::{e}" if os.environ.get("GITHUB_ACTIONS") else f"✗ {e}")
    if not errors:
        print("OK")
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
