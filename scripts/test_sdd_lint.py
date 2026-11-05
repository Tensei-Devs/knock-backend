#!/usr/bin/env python3
"""Pruebas mínimas del estado «Reemplazada» de sdd_lint.py. Uso: python scripts/test_sdd_lint.py (sin dependencias; no corre en el CI)."""
from __future__ import annotations

import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import sdd_lint  # noqa: E402


class ReplacedStateTests(unittest.TestCase):
    def test_header_line_replaced_is_accepted(self) -> None:
        text = "> **Estado:** Reemplazada (por TT-BD-06)\n\n## 8\nTexto.\n"
        self.assertTrue(sdd_lint.is_replaced(sdd_lint.spec_state(text)))

    def test_body_that_quotes_the_state_does_not_replace_the_header(self) -> None:
        text = "> **Estado:** Aprobada\n\n## 8\nPasará a **Estado:** Reemplazada (por TT-BD-06) cuando se fusione.\n"
        self.assertEqual("Aprobada", sdd_lint.spec_state(text))
        self.assertFalse(sdd_lint.is_replaced(sdd_lint.spec_state(text)))

    def test_state_with_extra_characters_is_not_replaced(self) -> None:
        for state in ("Reemplazadaxyz", "Reemplazada por TT-BD-06", "Reemplazada (por algo)", "reemplazada"):
            self.assertFalse(sdd_lint.is_replaced(state), state)

    def test_plain_replaced_and_missing_state(self) -> None:
        self.assertTrue(sdd_lint.is_replaced("Reemplazada"))
        self.assertFalse(sdd_lint.is_replaced(None))


class NumberedSpecTests(unittest.TestCase):
    def test_numbered_folders_are_recognised(self) -> None:
        self.assertTrue(sdd_lint.NUMBERED.match("003-bases-de-datos-mongodb-y-redis"))
        self.assertFalse(sdd_lint.NUMBERED.match("TT-BD-06-catalogo"))
        self.assertFalse(sdd_lint.NUMBERED.match("_legacy"))

    def test_cases_are_the_ca_criteria_of_the_spec(self) -> None:
        text = "## 4. Criterios de aceptación\n- **CA-01:** Dado A.\n- **CA-02:** Dado B.\nTexto con **CA-09:** en medio.\n"
        self.assertEqual(["CA-01", "CA-02"], sdd_lint.numbered_cases(text))

    def test_a_test_must_cite_the_case_with_the_spec_number(self) -> None:
        self.assertEqual(["CA_003_01"], sdd_lint.needles("CA-01", "003"))
        self.assertEqual(["TC_HU_001_01"], sdd_lint.needles("TC-HU-001-01", None))

    def test_a_bare_case_name_does_not_cover_a_numbered_spec(self) -> None:
        # Antes `CA_01` suelto daba por cubierta cualquier spec con un CA-01: falso verde.
        corpus = "def test_x_CA_01(): ...".replace("-", "_")
        self.assertFalse(any(n in corpus for n in sdd_lint.needles("CA-01", "006")))
        self.assertTrue(any(n in "def test_x_CA_006_01(): ..." for n in sdd_lint.needles("CA-01", "006")))


class FindSpecTests(unittest.TestCase):
    DIRS = [Path("001-arquitectura"), Path("010-cache"), Path("015-registro")]

    def test_a_numeric_id_needs_exactly_three_digits(self) -> None:
        self.assertEqual("015-registro", sdd_lint.find_spec(self.DIRS, "015").name)
        for ambiguous in ("0", "00", "01", "1", "15", "0151"):
            self.assertIsNone(sdd_lint.find_spec(self.DIRS, ambiguous), ambiguous)

    def test_a_non_numeric_id_still_matches_by_prefix(self) -> None:
        self.assertEqual(Path("HU-001-login"), sdd_lint.find_spec([Path("HU-001-login")], "HU-001"))


if __name__ == "__main__":
    unittest.main()
