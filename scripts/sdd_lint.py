#!/usr/bin/env python3
"""Lint de specs SDD de Knock.

Revisa cada carpeta specs/<ID>-<slug>/ (ID = HU-NNN, TT-ÁREA-NN o BUG-NNN):
  - spec.md con las secciones 1 a 8 y «Aprobación», y un estado válido (Borrador, Aprobada, Firmada, Reemplazada)
  - plan.md, task.md y checklists/gates.md presentes
  - casos de prueba TC-<ID>-KK únicos, consecutivos y del mismo ID que la carpeta
  - una spec Aprobada, Firmada o Reemplazada tiene su clave de Jira (KNOCK-N) y revisor
  - constitution.md (raíz) idéntico a .specify/memory/constitution.md

Con --trace <ID> busca en el código de pruebas un test por cada caso:
  C#: TC_HU_001_01 en el nombre · Dart/TS: 'TC-HU-001-01' en la descripción.
  Specs numeradas (--trace 003, siempre con tres dígitos): los casos son los criterios «CA-NN» de spec.md; la prueba
  lleva en el nombre CA_003_01 (el número de la spec es obligatorio: un CA_01 suelto no distingue una spec de otra
  y daba por cubierta una spec sin pruebas).

Uso:
  python scripts/sdd_lint.py
  python scripts/sdd_lint.py --trace HU-001
"""
from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SPECS = ROOT / "specs" / "_legacy"  # migración: valida el formato TT/HU; las specs NNN-tema aún no tienen validador
CONFIG = json.loads((ROOT / ".agents" / "knock.json").read_text(encoding="utf-8")) if (ROOT / ".agents" / "knock.json").exists() else {}
REQUIRED_FILES = ("spec.md", "plan.md", "task.md", "checklists/gates.md")
SECTIONS = [rf"^## {n}\. " for n in range(1, 9)] + [r"^## Aprobación"]
FOLDER = re.compile(r"^(HU-\d{3}|TT-[A-Z]+-\d{2}|BUG-\d{3})(-[a-z0-9-]+)?$")
CASE_ROW = re.compile(r"^\|\s*(TC-(HU-\d{3}|TT-[A-Z]+-\d{2}|BUG-\d{3})-(\d{2}))\s*\|", re.M)
STATES = ("Borrador", "Aprobada", "Firmada")
# «Reemplazada» exige el formato exacto de la constitución §11: «Reemplazada (por <ID>)» (el «por» es opcional)
REPLACED = re.compile(r"Reemplazada(?: \(por (?:HU-\d{3}|TT-[A-Z]+-\d{2}|BUG-\d{3})\))?")
NUMBERED = re.compile(r"^(\d{3})-[a-z0-9]")
NUMBERED_CASE = re.compile(r"^- \*\*(CA-\d{2}):\*\*", re.M)
STATE_LINE = re.compile(r"\*\*Estado:\*\*\s*([^\n]+)")


def spec_state(text: str) -> str | None:
    """Estado de la spec: la primera línea «**Estado:**» (la misma que usa el lint), sin espacios sobrantes."""
    match = STATE_LINE.search(text)
    return match.group(1).strip() if match else None


def is_replaced(state: str | None) -> bool:
    return bool(state and REPLACED.fullmatch(state))
TEST_GLOBS = CONFIG.get("test_globs", ["tests/**/*.cs", "**/test/**/*.dart", "**/integration_test/**/*.dart", "**/*.spec.ts", "**/e2e/**/*.ts"])


def spec_dirs() -> list[Path]:
    return sorted(p for p in SPECS.glob("*") if p.is_dir() and not p.name.startswith(("_", ".")))


def cases_of(text: str) -> list[str]:
    return [m.group(1) for m in CASE_ROW.finditer(text)]


def lint_spec(folder: Path) -> list[str]:
    errors = []
    m = FOLDER.match(folder.name)
    if not m:
        return [f"{folder.name}: nombre inválido (usa HU-NNN-slug, TT-AREA-NN-slug o BUG-NNN-slug)"]
    sid = m.group(1)
    for name in REQUIRED_FILES:
        if not (folder / name).exists():
            errors.append(f"{folder.name}: falta {name}")
    spec = folder / "spec.md"
    if not spec.exists():
        return errors
    text = spec.read_text(encoding="utf-8")
    for pattern in SECTIONS:
        if not re.search(pattern, text, re.M):
            errors.append(f"{folder.name}/spec.md: falta la sección «## {pattern[4:].replace(chr(92), '').strip()}»")
    state = spec_state(text)
    if not state or not (any(state.startswith(s) for s in STATES) or is_replaced(state)):
        errors.append(f"{folder.name}/spec.md: estado inválido (usa {', '.join(STATES)} o «Reemplazada (por <ID>)»)")
    cases = cases_of(text)
    if not cases:
        errors.append(f"{folder.name}/spec.md: sin casos de prueba TC-{sid}-KK en la tabla de §2")
    dup = {c for c in cases if cases.count(c) > 1}
    if dup:
        errors.append(f"{folder.name}/spec.md: casos duplicados {sorted(dup)}")
    wrong = [c for c in cases if not c.startswith(f"TC-{sid}-")]
    if wrong:
        errors.append(f"{folder.name}/spec.md: casos de otro ID {wrong}")
    nums = [int(c[-2:]) for c in cases if c.startswith(f"TC-{sid}-")]
    if nums and sorted(nums) != list(range(1, len(nums) + 1)):
        errors.append(f"{folder.name}/spec.md: casos no consecutivos {sorted(nums)}")
    if state and (state.startswith(("Aprobada", "Firmada")) or is_replaced(state)):
        if not re.search(r"\bKNOCK-\d+\b", text):
            errors.append(f"{folder.name}/spec.md: está {state} pero no tiene clave de Jira (KNOCK-N)")
        if re.search(r"- \[ \] Revisor 1", text):
            errors.append(f"{folder.name}/spec.md: está {state} sin revisor marcado")
    return errors


def lint_constitution() -> list[str]:
    mirror, source = ROOT / "constitution.md", ROOT / ".specify/memory/constitution.md"
    if not mirror.exists():
        return ["constitution.md espejo no existe (corre python scripts/sync_agents.py)"]
    if mirror.read_text(encoding="utf-8") != source.read_text(encoding="utf-8"):
        return ["constitution.md difiere de .specify/memory/constitution.md (corre sync_agents.py)"]
    return []


def marked(spec_text: str, case: str, tag: str) -> bool:
    """El caso tiene el marcador «(tag)» en su fila de la tabla de §2."""
    return bool(re.search(rf"\|\s*{re.escape(case)}\s*\|[^\n]*\({re.escape(tag)}\)", spec_text))


def numbered_dirs() -> list[Path]:
    """Specs numeradas (specs/NNN-tema), las vigentes de Demo Base; las HU/TT viven en specs/_legacy."""
    return sorted(p for p in (ROOT / "specs").glob("*") if p.is_dir() and NUMBERED.match(p.name))


def numbered_cases(text: str) -> list[str]:
    return NUMBERED_CASE.findall(text)


def needles(case: str, spec_number: str | None) -> list[str]:
    """Nombres con los que una prueba puede citar el caso (guiones como guiones bajos)."""
    base = case.replace("-", "_")
    if spec_number is None:
        return [base]
    return [f"CA_{spec_number}_{base.rsplit('_', 1)[1]}"]


def find_spec(dirs: list[Path], spec_id: str) -> Path | None:
    """Carpeta de una spec por su ID. Un ID numérico exige tres dígitos exactos (`015`, no `01` ni `0`); los demás por prefijo."""
    if spec_id.isdigit():
        return next((d for d in dirs if re.fullmatch(r"\d{3}", spec_id) and d.name.startswith(f"{spec_id}-")), None)
    return next((d for d in dirs if d.name.startswith(spec_id)), None)


def trace(spec_id: str) -> int:
    folder = find_spec(spec_dirs(), spec_id)
    number = None
    if not folder:
        folder = find_spec(numbered_dirs(), spec_id)
        number = NUMBERED.match(folder.name).group(1) if folder else None
    if not folder:
        print(f"ERROR: no existe specs/{spec_id}* ni specs/_legacy/{spec_id}*")
        return 1
    spec_text = (folder / "spec.md").read_text(encoding="utf-8")
    if is_replaced(spec_state(spec_text)):
        print(f"AVISO: {folder.name} está Reemplazada por otra spec; sus casos ya no se verifican")
        return 0
    cases = numbered_cases(spec_text) if number else cases_of(spec_text)
    files = {f for g in TEST_GLOBS for f in ROOT.glob(g) if f.is_file() and "node_modules" not in f.parts}
    if not files:
        print(f"AVISO: aún no hay pruebas; {len(cases)} casos de {folder.name} sin verificar")
        return 0
    corpus = "\n".join(f.read_text(encoding="utf-8", errors="ignore") for f in files).replace("-", "_")
    missing = []
    print("| Caso | Prueba |\n|---|---|")
    for case in cases:
        found = any(n in corpus for n in needles(case, number))
        by_command = not found and marked(spec_text, case, "comando")
        print(f"| {case} | {'OK' if found else 'COMANDO (evidencia en el PR)' if by_command else 'FALTA'} |")
        if not found and not by_command:
            missing.append(case)
    if missing:
        print(f"\nDRIFT: {len(missing)} caso(s) sin prueba. Si un caso no aplica a este repositorio, márcalo «(otro repo)»; si se verifica solo con un comando (build, formato, auditoría), márcalo «(comando)» y pega su salida en el PR; ambos en la columna Verificación.")
        other = [c for c in missing if marked(spec_text, c, "otro repo")]
        return 0 if len(other) == len(missing) else 1
    print("\nOK — todos los casos tienen prueba")
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--trace", metavar="ID", help="verifica casos ↔ nombres de pruebas")
    args = parser.parse_args()
    if args.trace:
        return trace(args.trace)
    errors = lint_constitution()
    folders = spec_dirs()
    for folder in folders:
        errors += lint_spec(folder)
    if errors:
        print("ERRORES:")
        print("\n".join(f"  - {e}" for e in errors))
        return 1
    print(f"OK — {len(folders)} specs válidas")
    return 0


if __name__ == "__main__":
    sys.exit(main())
