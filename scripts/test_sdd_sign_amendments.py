#!/usr/bin/env python3
"""Pruebas de scripts/sdd_sign_amendments.py (KNOCK-266). Uso: python scripts/test_sdd_sign_amendments.py.

No toca git, ni el repositorio real, ni firma nada: usa textos y un directorio temporal.
"""
from __future__ import annotations

import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import sdd_sign_amendments as firmas  # noqa: E402

SPEC = """# Spec de prueba

**Estado:** Aprobada

## 8. Plan de pruebas

| Caso | Qué prueba |
|---|---|
| TC-039-01 | algo |

## Nota de enmienda (2026-11-23, decisión de José)
- Ya firmada.

## Nota de enmienda (2026-11-24, pendiente de firma)
- {detalle}

## Aprobación
- [x] Revisor 1 — nombre: José Julián Hernández Hernández fecha: 2026-11-14
- [x] Enmienda 2026-11-23 — nombre: José Julián Hernández Hernández fecha: 2026-11-24
- [ ] Enmienda 2026-11-24 — nombre: ________ fecha: ________
- [x] Sin conflicto con la constitución
- **Estados:** Borrador → Aprobada
"""

PATH = Path("specs/039-cuenta-y-datos-personales/spec.md")


def spec(detalle: str = "R-22 cambia el contenido del ZIP.", estado: str = "Aprobada") -> str:
    return SPEC.replace("- {detalle}", f"- {detalle}").replace("**Estado:** Aprobada", f"**Estado:** {estado}")


class ParsingTests(unittest.TestCase):
    def test_state_comes_from_the_state_line(self) -> None:
        self.assertEqual("Aprobada", firmas.spec_state(spec()))

    def test_pending_amendment_is_firmable_when_the_note_is_complete(self) -> None:
        items = firmas.pending(spec(), "039-cuenta-y-datos-personales", PATH)

        self.assertEqual(1, len(items))
        self.assertEqual("Enmienda 2026-11-24", items[0].label)
        self.assertEqual(firmas.FIRMABLE, items[0].classification)
        self.assertIn("2026-11-24", items[0].note_heading or "")
        self.assertEqual((), items[0].reasons)

    def test_signed_amendments_are_not_pending(self) -> None:
        items = firmas.pending(spec(), "039-cuenta-y-datos-personales", PATH)

        self.assertTrue(all(item.label != "Enmienda 2026-11-23" for item in items))

    def test_note_with_open_question_needs_a_decision(self) -> None:
        items = firmas.pending(
            spec("¿Se agrega el filtro `tipo` a la ruta de avisos?"), "039-cuenta-y-datos-personales", PATH
        )

        self.assertEqual(firmas.NEEDS_DECISION, items[0].classification)
        self.assertIn("pregunta sin cerrar (¿)", items[0].markers)

    def test_note_with_pending_marker_needs_a_decision(self) -> None:
        items = firmas.pending(spec("El nombre queda a validar con José."), "039-cuenta-y-datos-personales", PATH)

        self.assertEqual(firmas.NEEDS_DECISION, items[0].classification)
        self.assertIn("a validar", items[0].markers)

    def test_question_mark_inside_a_route_does_not_block_the_signature(self) -> None:
        items = firmas.pending(
            spec("La ruta `GET /api/v1/avisos?tipo=sistema` no se agrega en esta enmienda."),
            "039-cuenta-y-datos-personales",
            PATH,
        )

        self.assertEqual(firmas.FIRMABLE, items[0].classification)

    def test_amendment_without_note_is_incomplete(self) -> None:
        text = spec().replace("## Nota de enmienda (2026-11-24, pendiente de firma)", "## Otra cosa")

        items = firmas.pending(text, "039-cuenta-y-datos-personales", PATH)

        self.assertEqual(firmas.INCOMPLETE, items[0].classification)
        self.assertIn("no hay «## Nota de enmienda» de esa fecha", items[0].reasons)

    def test_draft_spec_is_not_signable(self) -> None:
        items = firmas.pending(spec(estado="Borrador"), "039-cuenta-y-datos-personales", PATH)

        self.assertEqual(firmas.NOT_SIGNABLE, items[0].classification)
        self.assertIn("Borrador", items[0].reasons[0])

    def test_note_is_matched_by_date_not_by_position(self) -> None:
        heading, body = firmas.note_for("Enmienda 2026-11-24", firmas.notes(spec()))

        self.assertIn("2026-11-24", heading)
        self.assertIn("R-22", body)


class DecisionTests(unittest.TestCase):
    TABLE = (
        "| ID | Spec | Tema | Estado | Decisión o pregunta | Evidencia |\n"
        "|---|---|---|---|---|---|\n"
        "| D-1 | 020-x | Transiciones | pendiente de decisión | ¿Qué se acepta? | plan |\n"
        "| D-2 | 046-x | Índices | decidida el 2026-11-25 | — | plan |\n"
    )

    def test_decided_rows_are_excluded(self) -> None:
        rows = firmas.decision_rows(self.TABLE)

        self.assertEqual(["D-1"], [row.key for row in rows])

    def test_header_and_separator_are_ignored(self) -> None:
        self.assertEqual(1, len(firmas.decision_rows(self.TABLE)))


class SignatureTests(unittest.TestCase):
    def test_signature_line_marks_the_checkbox(self) -> None:
        line = firmas.signature_line("Enmienda 2026-11-24", "José Julián Hernández Hernández", "2026-11-25")

        self.assertEqual(
            "- [x] Enmienda 2026-11-24 — nombre: José Julián Hernández Hernández fecha: 2026-11-25", line
        )

    def test_apply_signature_changes_only_that_line(self) -> None:
        text = spec()
        item = firmas.pending(text, "039-cuenta-y-datos-personales", PATH)[0]
        new_line = firmas.signature_line(item.label, "José", "2026-11-25")

        updated = firmas.apply_signature(text, item.line, new_line)

        self.assertEqual(text.replace(item.line, new_line, 1), updated)
        self.assertEqual(1, updated.count("- [x] Enmienda 2026-11-24"))

    def test_apply_signature_refuses_a_missing_or_duplicated_line(self) -> None:
        with self.assertRaises(ValueError):
            firmas.apply_signature("sin la línea", "- [ ] Enmienda", "- [x] Enmienda")

    def test_ledger_round_trip(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / "enmiendas.jsonl"
            firmas.append_ledger(path, {"schema": 1, "spec": "039-x"})
            firmas.append_ledger(path, {"schema": 1, "spec": "044-y"})

            entries = firmas.load_ledger(path)

        self.assertEqual(["039-x", "044-y"], [entry["spec"] for entry in entries])
        self.assertIn('"spec": "039-x"', json.dumps(entries[0]))

    def test_normalize_ignores_accents_case_and_spaces(self) -> None:
        self.assertEqual(firmas.normalize("José  Julián"), firmas.normalize("jose julian"))


class RenderTests(unittest.TestCase):
    def test_inventory_summary_counts_each_classification(self) -> None:
        items = firmas.pending(spec(), "039-cuenta-y-datos-personales", PATH)
        blocked = firmas.pending(spec("¿Falta decidir?"), "044-avisos-dentro-de-la-app", PATH)
        decisions = firmas.decision_rows(DecisionTests.TABLE)

        text = firmas.render_text(items + blocked, decisions, "origin/sprint-05", None)

        self.assertIn("1 firmables", text)
        self.assertIn("1 requieren decisión", text)
        self.assertIn("1 decisiones abiertas", text)
        self.assertIn("[D-1]", text)

    def test_bundle_contains_the_command_and_says_it_does_not_sign(self) -> None:
        items = firmas.pending(spec(), "039-cuenta-y-datos-personales", PATH)
        decisions = firmas.decision_rows(DecisionTests.TABLE)

        bundle = firmas.render_bundle(items, decisions, "origin/sprint-05", None, "José Julián Hernández Hernández",
                                      "2026-11-25")

        self.assertIn("--spec 039-cuenta-y-datos-personales", bundle)
        self.assertIn("**Este archivo no firma nada.**", bundle)
        self.assertIn("Aprobación conjunta propuesta", bundle)
        self.assertIn("D-1", bundle)

    def test_bundle_lists_blocked_amendments(self) -> None:
        items = firmas.pending(spec("¿Falta decidir?"), "039-cuenta-y-datos-personales", PATH)

        bundle = firmas.render_bundle(items, [], "origin/sprint-05", None, "José", "2026-11-25")

        self.assertIn("No firmables todavía", bundle)
        self.assertIn("requiere-decisión", bundle)


if __name__ == "__main__":
    unittest.main()
