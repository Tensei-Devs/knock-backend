#!/usr/bin/env python3
"""Firma verificable de las enmiendas de spec de Knock (KNOCK-266).

Subcomandos:
  inventory  Lista las enmiendas pendientes y las separa en firmables y con decisiones abiertas,
             con las diferencias de cada spec contra una versión anterior (`--base`).
  prepare    Escribe el paquete de aprobación conjunta en `docs/firmas/enmiendas-pendientes.md`.
  sign       Firma UNA enmienda con confirmación interactiva. No firma por nadie: exige que quien
             ejecuta sea la persona real nombrada en la spec y escriba la frase exacta.
  verify     Comprueba que cada firma registrada sigue coincidiendo con el texto y con el commit.

Nada de esto reescribe el historial: firmar cambia `- [ ]` por `- [x]` en la línea de la sección
Aprobación y añade una línea al libro append-only `docs/firmas/enmiendas.jsonl`; el commit lo hace
una persona. El agente no firma: solo prepara e inventaría.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import subprocess
import sys
from dataclasses import dataclass, replace
from datetime import date, datetime, timezone
from pathlib import Path

TOOL = "sdd_sign_amendments"
VERSION = "1.0.0"
SCHEMA = 1
LEDGER = Path("docs/firmas/enmiendas.jsonl")
BUNDLE = Path("docs/firmas/enmiendas-pendientes.md")
DECISIONS = Path("docs/firmas/decisiones-pendientes.md")

STATE_LINE = re.compile(r"\*\*Estado:\*\*\s*([^\n]+)")
APPROVAL = re.compile(r"^## Aprobación\s*$", re.M)
NOTE = re.compile(r"^## Nota de enmienda \((?P<inner>[^)\n]+)\)\s*$(?P<body>.*?)(?=^## |\Z)", re.M | re.S)
SIGNATURE = re.compile(
    r"^- \[(?P<mark>[ xX])\] (?P<label>Enmienda[^—\n]*?)\s*—\s*nombre:\s*(?P<name>[^\n]*?)\s*fecha:\s*(?P<date>[^\n]*)$",
    re.M,
)
SIGNED = re.compile(r"^- \[[xX]\] Enmienda", re.M)
DATE = re.compile(r"\d{4}-\d{2}-\d{2}")

# Marcadores de una decisión sin resolver dentro de la nota de enmienda. Se listan todos los que
# aparezcan: la clasificación es conservadora y explica por qué una enmienda no se puede firmar.
DECISION_MARKERS = (
    ("sin resolver", r"sin resolver"),
    ("pendiente de decisión", r"pendiente de decisi[oó]n"),
    ("por decidir", r"por decidir"),
    ("falta decisión", r"faltan? decisi[oó]n"),
    ("a validar", r"\ba validar\b"),
    ("pendiente de José", r"pendiente de jos[eé]"),
    ("pregunta abierta", r"pregunta abierta"),
    ("se confirma al firmar", r"confirm(?:a|ar|arlo|arla) al firmar"),
    ("pregunta sin cerrar (¿)", r"¿"),
)

FIRMABLE = "firmable"
NEEDS_DECISION = "requiere-decisión"
INCOMPLETE = "incompleta"
NOT_SIGNABLE = "no-firmable"


@dataclass(frozen=True)
class Amendment:
    """Una línea de firma pendiente de una spec y su nota de enmienda emparejada."""

    spec_id: str
    path: str
    label: str
    line: str
    state: str
    note_heading: str | None
    note_text: str
    markers: tuple[str, ...]
    classification: str
    reasons: tuple[str, ...]
    diff_stat: str | None = None
    diff_text: str | None = None

    @property
    def signed(self) -> bool:
        return not self.line.startswith("- [ ]")

    @property
    def note_sha256(self) -> str | None:
        return sha256(self.note_text.encode("utf-8")) if self.note_text else None


@dataclass(frozen=True)
class Decision:
    """Fila de `docs/firmas/decisiones-pendientes.md` que todavía no se puede convertir en enmienda."""

    key: str
    spec: str
    topic: str
    state: str
    question: str
    evidence: str


def sha256(text: str) -> str:
    return hashlib.sha256(text.encode("utf-8")).hexdigest()


def normalize(text: str) -> str:
    """Compara nombres sin depender de espacios, mayúsculas ni acentos."""
    folded = text.strip().casefold()
    for source, target in (("á", "a"), ("é", "e"), ("í", "i"), ("ó", "o"), ("ú", "u"), ("ñ", "n")):
        folded = folded.replace(source, target)
    return re.sub(r"\s+", " ", folded)


class Repo:
    """Acceso de solo lectura al repositorio; el trabajo pesado lo hace `git`, sin tocar el índice."""

    def __init__(self, root: Path) -> None:
        self.root = root

    def git(self, *args: str) -> str:
        result = subprocess.run(
            ["git", *args], cwd=self.root, capture_output=True, text=True, encoding="utf-8", check=False
        )
        if result.returncode != 0:
            raise RuntimeError(f"git {' '.join(args)}: {result.stderr.strip()}")
        return result.stdout

    def is_git(self) -> bool:
        try:
            self.git("rev-parse", "--git-dir")
            return True
        except (RuntimeError, FileNotFoundError):
            return False

    def head(self) -> str | None:
        try:
            return self.git("rev-parse", "--short", "HEAD").strip()
        except (RuntimeError, FileNotFoundError):
            return None

    def spec_paths(self, ref: str | None) -> list[Path]:
        if ref:
            listed = self.git("ls-tree", "-r", "--name-only", ref, "--", "specs")
            return sorted(Path(line) for line in listed.splitlines() if line.endswith("/spec.md"))
        return sorted(path.relative_to(self.root) for path in (self.root / "specs").glob("*/spec.md"))

    def read(self, path: Path, ref: str | None) -> str:
        if ref:
            return self.git("show", f"{ref}:{path.as_posix()}")
        return (self.root / path).read_text(encoding="utf-8")

    def diff(self, path: Path, base: str | None, ref: str | None, max_lines: int) -> tuple[str, str] | None:
        if not base:
            return None
        spec = f"{base}..{ref}" if ref else base
        try:
            text = self.git("diff", "--unified=2", spec, "--", path.as_posix())
        except RuntimeError:
            return None
        if not text.strip():
            return "sin diferencias"
        added = sum(1 for line in text.splitlines() if line.startswith("+") and not line.startswith("+++"))
        removed = sum(1 for line in text.splitlines() if line.startswith("-") and not line.startswith("---"))
        lines = text.splitlines()
        clipped = "\n".join(lines[:max_lines])
        if len(lines) > max_lines:
            clipped += f"\n… ({len(lines) - max_lines} líneas más; usa --full para verlas)"
        return f"+{added} −{removed}", clipped


def spec_state(text: str) -> str:
    match = STATE_LINE.search(text)
    if not match:
        return "sin estado"
    return match.group(1).split("·")[0].strip()


def notes(text: str) -> list[tuple[str, str]]:
    return [(m.group("inner").strip(), m.group("body").strip()) for m in NOTE.finditer(text)]


def note_for(label: str, found: list[tuple[str, str]]) -> tuple[str, str] | None:
    """Empareja la línea «Enmienda <fecha>» con la nota de enmienda de esa misma fecha."""
    match = DATE.search(label)
    if not match:
        return None
    wanted = match.group(0)
    candidates = [(heading, body) for heading, body in found if wanted in heading]
    return candidates[-1] if candidates else None


def markers_of(text: str) -> tuple[str, ...]:
    return tuple(name for name, pattern in DECISION_MARKERS if re.search(pattern, text, re.I))


def pending(text: str, spec_id: str, path: Path) -> list[Amendment]:
    """Enmiendas con la línea de firma sin marcar, ya clasificadas."""
    approval = APPROVAL.search(text)
    region = text[approval.end():] if approval else text
    found = notes(text)
    state = spec_state(text)
    items = []
    for match in SIGNATURE.finditer(region):
        label = " ".join(match.group("label").split())
        if not match.group("mark").strip():
            note = note_for(label, found)
            note_heading, note_text = note if note else (None, "")
            found_markers = markers_of(note_text) if note_text else ()
            reasons: list[str] = []
            if not state.startswith(("Aprobada", "Firmada")):
                classification = NOT_SIGNABLE
                reasons.append(f"la spec está en estado «{state}»")
            elif note is None:
                classification = INCOMPLETE
                reasons.append("no hay «## Nota de enmienda» de esa fecha")
            elif found_markers:
                classification = NEEDS_DECISION
                reasons.extend(f"marcador: {marker}" for marker in found_markers)
            else:
                classification = FIRMABLE
            items.append(
                Amendment(
                    spec_id=spec_id,
                    path=path.as_posix(),
                    label=label,
                    line=match.group(0),
                    state=state,
                    note_heading=note_heading,
                    note_text=note_text,
                    markers=found_markers,
                    classification=classification,
                    reasons=tuple(reasons),
                )
            )
    return items


def decision_rows(markdown: str) -> list[Decision]:
    """Filas de la tabla de decisiones abiertas; las marcadas como decididas no cuentan."""
    rows = []
    for line in markdown.splitlines():
        cells = [cell.strip() for cell in line.strip().strip("|").split("|")]
        if len(cells) != 6 or cells[0] in {"ID", "---"} or set(cells[0]) == {"-"}:
            continue
        key, spec, topic, state, question, evidence = cells
        if normalize(state).startswith(("decidida", "resuelta")):
            continue
        rows.append(Decision(key, spec, topic, state, question, evidence))
    return rows


def read_decisions(root: Path) -> list[Decision]:
    path = root / DECISIONS
    return decision_rows(path.read_text(encoding="utf-8")) if path.exists() else []


def inventory(repo: Repo, base: str | None, ref: str | None, max_lines: int) -> list[Amendment]:
    items: list[Amendment] = []
    for path in repo.spec_paths(ref):
        if not path.name == "spec.md":
            continue
        spec_id = path.parent.name
        text = repo.read(path, ref)
        for item in pending(text, spec_id, path):
            diff = repo.diff(path, base, ref, max_lines)
            items.append(replace(item, diff_stat=diff[0] if diff else None, diff_text=diff[1] if diff else None))
    return items


def render_text(items: list[Amendment], decisions: list[Decision], base: str | None, ref: str | None) -> str:
    origin = f"ref {ref}" if ref else "árbol de trabajo"
    lines = [f"Enmiendas pendientes ({origin}; diferencias contra {base or 'sin base'}):", ""]
    if not items:
        lines.append("  (ninguna)")
    for item in items:
        lines.append(f"- [{item.classification}] {item.path} ({item.state}) — {item.label}")
        lines.append(f"    nota: {item.note_heading or 'sin nota de enmienda'}")
        if item.diff_stat:
            lines.append(f"    diferencias: {item.diff_stat}")
        for reason in item.reasons:
            lines.append(f"    motivo: {reason}")
    counts = {kind: sum(1 for item in items if item.classification == kind) for kind in
              (FIRMABLE, NEEDS_DECISION, INCOMPLETE, NOT_SIGNABLE)}
    lines += ["", "Decisiones abiertas (no firmables):", ""]
    if not decisions:
        lines.append("  (ninguna)")
    for decision in decisions:
        lines.append(f"- [{decision.key}] {decision.spec} — {decision.topic} ({decision.state})")
        lines.append(f"    {decision.question}")
    lines += [
        "",
        f"Resumen: {counts[FIRMABLE]} firmables, {counts[NEEDS_DECISION]} requieren decisión, "
        f"{counts[INCOMPLETE]} incompletas, {counts[NOT_SIGNABLE]} no firmables, {len(decisions)} decisiones abiertas.",
    ]
    return "\n".join(lines)


def sign_command(item: Amendment, signer: str) -> str:
    return (
        "python -X utf8 scripts/sdd_sign_amendments.py sign "
        f"--spec {item.spec_id} --label \"{item.label}\" --signer \"{signer}\""
    )


def render_bundle(items: list[Amendment], decisions: list[Decision], base: str | None, ref: str | None,
                  signer: str, today: str) -> str:
    firmables = [item for item in items if item.classification == FIRMABLE]
    blocked = [item for item in items if item.classification != FIRMABLE]
    lines = [
        "# Enmiendas pendientes de firma",
        "",
        f"Generado por `scripts/sdd_sign_amendments.py` v{VERSION} el {today}.",
        f"Origen: {'ref `' + ref + '`' if ref else 'árbol de trabajo'}. Base de comparación: `{base or 'ninguna'}`.",
        "",
        "**Este archivo no firma nada.** Lo genera el mecanismo; la firma la hace una persona con `sign`, "
        "en su terminal, escribiendo su nombre real y la frase de confirmación. Un agente no firma.",
        "",
        "## Aprobación conjunta propuesta",
        "",
        "Firmar en este orden, una por una (cada `sign` vuelve a pedir confirmación interactiva):",
        "",
    ]
    if firmables:
        for number, item in enumerate(firmables, start=1):
            lines += [
                f"{number}. `{item.path}` — {item.label} ({item.diff_stat or 'sin diferencias'})",
                "",
                "   ```",
                f"   {sign_command(item, signer)}",
                "   ```",
                "",
            ]
        lines += [
            "Después de firmar todas, revisar y subir el cambio como un commit normal "
            "(sin reescribir historial, sin `--amend` de commits ya empujados):",
            "",
            "```",
            "git diff --stat",
            "git add specs docs/firmas",
            "git commit -m \"docs(specs): sign the pending amendments (KNOCK-<N de la spec>)\"",
            "python -X utf8 scripts/sdd_sign_amendments.py verify",
            "```",
            "",
        ]
    else:
        lines += ["(No hay enmiendas firmables ahora mismo.)", ""]
    lines += [
        "## No firmables todavía",
        "",
        "| Spec | Enmienda | Clasificación | Motivo |",
        "|---|---|---|---|",
    ]
    for item in blocked:
        lines.append(f"| `{item.path}` | {item.label} | {item.classification} | {'; '.join(item.reasons) or '—'} |")
    lines += ["", "## Decisiones abiertas (antes de escribir la enmienda)", "",
              "| ID | Spec | Tema | Estado | Decisión o pregunta | Evidencia |", "|---|---|---|---|---|---|"]
    for decision in decisions:
        lines.append(
            f"| {decision.key} | {decision.spec} | {decision.topic} | {decision.state} | "
            f"{decision.question} | {decision.evidence} |"
        )
    lines += ["", "## Diferencias contra la versión anterior", ""]
    for item in items:
        lines += [f"### {item.path} — {item.label}", "", f"`{item.diff_stat or 'sin base de comparación'}`", ""]
        if item.diff_text:
            lines += ["```diff", item.diff_text, "```", ""]
    return "\n".join(lines)


def require_tty() -> None:
    if os.environ.get("CI"):
        raise SystemExit("abortado: en CI no se firma nada (variable CI definida)")
    if not sys.stdin.isatty():
        raise SystemExit("abortado: `sign` exige una terminal interactiva; no se puede firmar por tubería")


def atomic_write(path: Path, text: str) -> None:
    temporary = path.with_name(path.name + ".tmp")
    temporary.write_text(text, encoding="utf-8", newline="")
    os.replace(temporary, path)


def signature_line(label: str, signer: str, signed_date: str) -> str:
    return f"- [x] {label} — nombre: {signer} fecha: {signed_date}"


def apply_signature(text: str, line: str, new_line: str) -> str:
    """Cambia solo esa línea de firma; el resto del archivo queda byte a byte igual."""
    if text.count(line) != 1:
        raise ValueError("la línea de firma no aparece exactamente una vez")
    return text.replace(line, new_line, 1)


def append_ledger(path: Path, entry: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("a", encoding="utf-8", newline="\n") as handle:
        handle.write(json.dumps(entry, ensure_ascii=False, sort_keys=True) + "\n")


def load_ledger(path: Path) -> list[dict]:
    if not path.exists():
        return []
    entries = []
    for number, line in enumerate(path.read_text(encoding="utf-8").splitlines(), start=1):
        if not line.strip():
            continue
        try:
            entries.append(json.loads(line))
        except json.JSONDecodeError as error:
            raise SystemExit(f"{path}:{number}: JSON inválido ({error})") from error
    return entries


def cmd_inventory(args: argparse.Namespace) -> int:
    repo = Repo(args.root)
    items = inventory(repo, args.base, args.ref, args.max_diff_lines)
    decisions = read_decisions(args.root)
    if args.json:
        print(json.dumps({"base": args.base, "ref": args.ref, "head": repo.head(), "amendments": [
            {
                "spec": item.spec_id,
                "path": item.path,
                "label": item.label,
                "state": item.state,
                "note_heading": item.note_heading,
                "note_sha256": item.note_sha256,
                "markers": list(item.markers),
                "classification": item.classification,
                "reasons": list(item.reasons),
                "diff": item.diff_stat,
            } for item in items
        ], "decisions": [decision.__dict__ for decision in decisions]}, ensure_ascii=False, indent=2))
        return 0
    print(render_text(items, decisions, args.base, args.ref))
    if args.full:
        for item in items:
            if item.diff_text:
                print(f"\n### {item.path} — {item.label}\n\n{item.diff_text}")
    return 0


def cmd_prepare(args: argparse.Namespace) -> int:
    repo = Repo(args.root)
    items = inventory(repo, args.base, args.ref, args.max_diff_lines)
    decisions = read_decisions(args.root)
    bundle = render_bundle(items, decisions, args.base, args.ref, args.signer, date.today().isoformat())
    output = args.output if args.output.is_absolute() else args.root / args.output
    output.parent.mkdir(parents=True, exist_ok=True)
    atomic_write(output, bundle + "\n")
    print(f"escrito {output} — {sum(1 for i in items if i.classification == FIRMABLE)} firmables, "
          f"{len(decisions)} decisiones abiertas")
    print("no se firmó nada: la firma la hace la persona con `sign`")
    return 0


def cmd_sign(args: argparse.Namespace) -> int:
    require_tty()
    repo = Repo(args.root)
    if not re.fullmatch(r"[0-9]{3}-[a-z0-9-]+", args.spec):
        raise SystemExit(f"abortado: «{args.spec}» no tiene forma de carpeta de spec (NNN-slug)")
    path = args.root / "specs" / args.spec / "spec.md"
    if not path.is_file():
        raise SystemExit(f"abortado: no existe {path.relative_to(args.root)}")
    text = path.read_text(encoding="utf-8")
    relative = path.relative_to(args.root)
    candidates = [item for item in pending(text, args.spec, relative) if normalize(item.label) == normalize(args.label)]
    if not candidates:
        raise SystemExit(f"abortado: no hay una firma pendiente «{args.label}» en {relative}")
    item = candidates[0]
    if item.classification != FIRMABLE:
        raise SystemExit(
            f"abortado: la enmienda no es firmable ({item.classification}): " + "; ".join(item.reasons)
        )
    known = {normalize(match.group("name")) for match in SIGNATURE.finditer(text) if "___" not in match.group("name")}
    if normalize(args.signer) not in known:
        raise SystemExit(
            "abortado: el firmante no es una persona nombrada en la spec; la identidad debe ser real y constar ya "
            "en la sección Aprobación"
        )
    if args.date and not re.fullmatch(r"\d{4}-\d{2}-\d{2}", args.date):
        raise SystemExit("abortado: --date debe ser YYYY-MM-DD")
    signed_date = args.date or date.today().isoformat()
    note_date = DATE.search(item.label)
    if note_date and signed_date < note_date.group(0):
        raise SystemExit("abortado: la fecha de firma no puede ser anterior a la de la enmienda")
    if signed_date > date.today().isoformat():
        raise SystemExit("abortado: no se firma con fecha futura")
    for staged in (["diff", "--quiet"], ["diff", "--cached", "--quiet"]):
        if subprocess.run(["git", *staged, "--", str(relative)], cwd=args.root).returncode != 0:
            raise SystemExit(
                "abortado: la spec tiene cambios sin commitear; firma sobre el texto ya commiteado "
                "(haz commit de la nota de enmienda primero)"
            )
    entry = {
        "schema": SCHEMA,
        "tool": TOOL,
        "version": VERSION,
        "signed_at": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
        "signed_date": signed_date,
        "signer": args.signer,
        "spec": args.spec,
        "path": relative.as_posix(),
        "label": item.label,
        "note_heading": item.note_heading,
        "note_sha256": item.note_sha256,
        "spec_sha256_before": sha256(text),
        "head": repo.head(),
    }
    print(f"Enmienda: {item.label} — {relative}")
    print(f"Nota: {item.note_heading}")
    print(f"Texto de la nota (SHA-256): {item.note_sha256}")
    print(f"Firmante: {args.signer}   Fecha: {signed_date}")
    print("\nLa firma quedará como una línea marcada y una entrada nueva en docs/firmas/enmiendas.jsonl.")
    phrase = f"FIRMO {args.spec} {item.label}"
    if input(f"\nEscribe exactamente «{phrase}» para continuar: ").strip() != phrase:
        raise SystemExit("abortado: frase de confirmación incorrecta; no se firmó nada")
    if normalize(input("Repite tu nombre completo para confirmar: ")) != normalize(args.signer):
        raise SystemExit("abortado: el nombre no coincide; no se firmó nada")
    new_line = signature_line(item.label, args.signer, signed_date)
    try:
        updated = apply_signature(text, item.line, new_line)
    except ValueError as error:
        raise SystemExit(f"abortado: {error}; no se firmó nada") from error
    atomic_write(path, updated)
    entry["signature_line"] = new_line
    entry["spec_sha256_after"] = sha256(updated)
    append_ledger(args.root / LEDGER, entry)
    print("\nFirmado. Nada se ha commiteado ni empujado.")
    print("Siguiente: git diff, commit normal (sin reescribir historial) y `verify`.")
    return 0


def cmd_verify(args: argparse.Namespace) -> int:
    repo = Repo(args.root)
    entries = load_ledger(args.root / LEDGER)
    problems = 0
    for entry in entries:
        path = args.root / entry["path"]
        work = path.read_text(encoding="utf-8") if path.exists() else ""
        work_hash = sha256(work) if work else None
        try:
            head_hash = sha256(repo.git("show", f"HEAD:{entry['path']}")) if repo.is_git() else None
        except (RuntimeError, FileNotFoundError):
            head_hash = None
        if work_hash == entry["spec_sha256_after"] and head_hash == work_hash:
            status = "committed"
        elif work_hash == entry["spec_sha256_after"]:
            status = "pendiente-de-commit"
        else:
            status = "deriva"
            problems += 1
        current_note = None
        for heading, body in notes(work):
            if heading == entry["note_heading"]:
                current_note = sha256(body.encode("utf-8"))
        note_ok = current_note == entry["note_sha256"]
        line_ok = entry["signature_line"] in work
        if not note_ok or not line_ok:
            problems += 1
        print(
            f"{entry['spec']} — {entry['label']}: {status}; nota {'igual' if note_ok else 'CAMBIADA'}; "
            f"firma {'presente' if line_ok else 'FALTA'} ({entry['signer']}, {entry['signed_at'][:10]})"
        )
    registered = {(entry["path"], entry["label"]) for entry in entries}
    unregistered = 0
    for path in repo.spec_paths(None):
        text = (args.root / path).read_text(encoding="utf-8")
        for match in SIGNED.finditer(text):
            label = " ".join(match.group(0).split())[len("- [x] "):]
            label = label.split("—")[0].strip()
            if (path.as_posix(), label) not in registered:
                unregistered += 1
                print(f"aviso: firma sin registro en el libro: {path} — {label}")
    print(f"\n{len(entries)} firmas registradas, {unregistered} sin registro, {problems} problemas.")
    if problems or (args.strict and unregistered):
        return 1
    return 0


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1], help="raíz del repositorio")
    sub = parser.add_subparsers(dest="command", required=True)

    def common(item: argparse.ArgumentParser) -> None:
        item.add_argument("--base", default="origin/sprint-05", help="versión anterior para las diferencias")
        item.add_argument("--ref", default=None, help="ref de git a inventariar (por omisión, el árbol de trabajo)")
        item.add_argument("--max-diff-lines", type=int, default=40)

    inventory_parser = sub.add_parser("inventory", help="lista enmiendas pendientes y decisiones abiertas")
    common(inventory_parser)
    inventory_parser.add_argument("--json", action="store_true")
    inventory_parser.add_argument("--full", action="store_true", help="muestra también el texto de las notas y los diffs")
    inventory_parser.set_defaults(func=cmd_inventory)

    prepare = sub.add_parser("prepare", help="escribe el paquete de aprobación conjunta")
    common(prepare)
    prepare.add_argument("--signer", default="José Julián Hernández Hernández")
    prepare.add_argument("--output", type=Path, default=BUNDLE)
    prepare.set_defaults(func=cmd_prepare)

    sign = sub.add_parser("sign", help="firma UNA enmienda (solo la persona real, en su terminal)")
    sign.add_argument("--spec", required=True, help="carpeta de la spec, p. ej. 039-cuenta-y-datos-personales")
    sign.add_argument("--label", required=True, help="texto de la línea, p. ej. \"Enmienda 2026-11-24\"")
    sign.add_argument("--signer", required=True, help="nombre real, tal como consta en la spec")
    sign.add_argument("--date", default=None)
    sign.set_defaults(func=cmd_sign)

    verify = sub.add_parser("verify", help="comprueba las firmas registradas")
    verify.add_argument("--strict", action="store_true", help="falla también por firmas anteriores sin registro")
    verify.set_defaults(func=cmd_verify)
    return parser


def main(argv: list[str] | None = None) -> int:
    args = build_parser().parse_args(argv)
    try:
        return args.func(args)
    except RuntimeError as error:
        print(f"error: {error}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    sys.exit(main())
