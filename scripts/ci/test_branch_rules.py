#!/usr/bin/env python3
"""Pruebas de scripts/ci/branch_rules.py (spec 004, CA-01 y CA-04): formato de commits, ramas y PR.

Uso: python scripts/ci/test_branch_rules.py (sin dependencias; no corre en el CI todavía).
"""
from __future__ import annotations

import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import branch_rules  # noqa: E402

BODY = """## Criterios
| Caso | Prueba | Resultado |

## Pruebas
- [x] build

## Uso de IA
- Herramienta: Claude Code
- Qué generó: el código
"""


class CommitFormatTests(unittest.TestCase):
    def test_a_commit_with_another_format_is_rejected_CA_01(self) -> None:
        for subject in (
            "Add gateway",
            "feat: add gateway (KNOCK-196)",
            "feat(gateway): Add gateway (KNOCK-196)",  # mayúscula inicial
            "feat(gateway): add gateway",  # sin clave de Jira
            "feat(gateway): add gateway. (KNOCK-196)",  # punto final
            "wip(gateway): add gateway (KNOCK-196)",  # tipo desconocido
        ):
            self.assertTrue(branch_rules.check_message(subject), subject)

    def test_a_valid_commit_is_accepted_CA_01(self) -> None:
        self.assertEqual([], branch_rules.check_message("feat(gateway): route the three services with YARP (KNOCK-196)"))

    def test_the_scopes_of_sprint_3_are_known(self) -> None:
        for scope in ("gateway", "contracts", "deploy", "docker", "scripts", "architecture", "security", "db", "infra", "ci", "docs"):
            self.assertEqual([], branch_rules.check_message(f"chore({scope}): adjust the thing (KNOCK-199)"), scope)

    def test_an_unknown_scope_is_rejected(self) -> None:
        self.assertTrue(branch_rules.check_message("feat(inventado): add the thing (KNOCK-199)"))

    def test_ai_attribution_is_rejected(self) -> None:
        self.assertTrue(branch_rules.check_trailers("body\n\nCo-Authored-By: Claude <noreply@anthropic.com>"))
        self.assertEqual([], branch_rules.check_trailers("body\n\nCo-Authored-By: Ana Perez <ana@example.com>"))


class BranchTests(unittest.TestCase):
    def test_personal_branches_go_to_the_sprint_branch(self) -> None:
        self.assertTrue(branch_rules.allowed("sprint-03", "feature/KNOCK-199-repo-structure"))
        self.assertFalse(branch_rules.allowed("develop", "feature/KNOCK-199-repo-structure"))
        self.assertFalse(branch_rules.allowed("main", "feature/KNOCK-199-repo-structure"))

    def test_promotion_path(self) -> None:
        self.assertTrue(branch_rules.allowed("develop", "sprint-03"))
        self.assertTrue(branch_rules.allowed("release", "develop"))
        self.assertTrue(branch_rules.allowed("main", "release"))
        self.assertFalse(branch_rules.allowed("main", "develop"))

    def test_a_branch_without_the_jira_key_is_rejected(self) -> None:
        self.assertTrue(branch_rules.check_branch("feature/gateway-routes", "sprint-03"))
        self.assertEqual([], branch_rules.check_branch("feature/KNOCK-196-gateway-routes", "sprint-03"))


class PullRequestTests(unittest.TestCase):
    def test_a_pr_without_the_ai_section_fails_CA_04(self) -> None:
        body = BODY.split("## Uso de IA")[0]
        errors = branch_rules.check_pr("feature/KNOCK-199-repo-structure", "KNOCK-199 · B-004 Estructura del repositorio", body)
        self.assertTrue(any("Uso de IA" in e for e in errors))

    def test_a_pr_with_an_empty_ai_section_fails_CA_04(self) -> None:
        body = BODY.split("## Uso de IA")[0] + "## Uso de IA\n- Herramienta:\n- Qué generó:\n"
        self.assertTrue(branch_rules.check_pr("feature/KNOCK-199-repo-structure", "KNOCK-199 · B-004 Estructura", body))

    def test_a_complete_pr_passes_CA_04(self) -> None:
        self.assertEqual([], branch_rules.check_pr("feature/KNOCK-199-repo-structure", "KNOCK-199 · B-004 Estructura del repositorio", BODY))

    def test_the_spec_id_in_the_title_is_optional_and_accepts_the_new_ids(self) -> None:
        for title in (
            "KNOCK-199 · Estructura del repositorio",
            "KNOCK-199 · B-004 Estructura del repositorio",
            "KNOCK-199 · F-005 Estructura del repositorio",
            "KNOCK-199 · HU-024 Pedir una cotización",
            "KNOCK-199 · TT-REPO-02 Ramas y plantillas",
        ):
            self.assertEqual([], branch_rules.check_pr("feature/KNOCK-199-repo-structure", title, BODY), title)

    def test_the_title_must_carry_the_key_of_the_branch(self) -> None:
        errors = branch_rules.check_pr("feature/KNOCK-199-repo-structure", "KNOCK-200 · B-005 Terraform", BODY)
        self.assertTrue(any("KNOCK-199" in e for e in errors))

    def test_a_title_without_the_key_is_rejected(self) -> None:
        self.assertTrue(branch_rules.check_pr("feature/KNOCK-199-repo-structure", "Estructura del repositorio", BODY))


if __name__ == "__main__":
    unittest.main()
