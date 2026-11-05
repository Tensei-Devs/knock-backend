#!/usr/bin/env python3
"""Pruebas de scripts/ci/version.py (spec 012, CA-01 a CA-05): versión por puntos y etiqueta de puntos.

Uso: python scripts/ci/test_version.py (sin dependencias).
"""
from __future__ import annotations

import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import version  # noqa: E402


class BumpTests(unittest.TestCase):
    def test_one_point_fix_plus_three_point_endpoint_gives_1_0_4_CA_012_01(self) -> None:
        points = version.POINTS["fix"] + version.POINTS["endpoint"]
        self.assertEqual(4, points)
        self.assertEqual("1.0.4", version.bump("1.0.0", points))

    def test_eight_more_points_carry_z_into_y_CA_012_02(self) -> None:
        self.assertEqual("1.1.2", version.bump("1.0.4", 8))

    def test_exactly_ten_points_reset_z_and_raise_y_CA_012_02(self) -> None:
        self.assertEqual("1.1.0", version.bump("1.0.0", 10))
        self.assertEqual("1.1.0", version.bump("1.0.9", 1))

    def test_y_carries_into_x_like_z_into_y_CA_012_02(self) -> None:
        self.assertEqual("2.0.0", version.bump("1.9.9", 1))
        self.assertEqual("2.0.5", version.bump("1.9.8", 7))
        self.assertEqual("2.0.0", version.bump("1.0.0", 100))

    def test_breaking_change_raises_x_and_zeroes_y_and_z_CA_012_03(self) -> None:
        self.assertEqual("2.0.0", version.bump("1.4.7", 3, breaking=True))

    def test_breaking_change_with_zero_points_still_raises_x_CA_012_03(self) -> None:
        self.assertEqual("2.0.0", version.bump("1.4.7", 0, breaking=True))

    def test_zero_points_do_not_change_the_version_CA_012_05(self) -> None:
        for current in ("1.0.0", "1.4.7", "3.9.9"):
            self.assertEqual(current, version.bump(current, 0))

    def test_first_version_is_1_0_0_CA_012_01(self) -> None:
        self.assertEqual("1.0.0", version.FIRST_VERSION)

    def test_negative_points_and_bad_versions_are_rejected_CA_012_02(self) -> None:
        with self.assertRaises(ValueError):
            version.bump("1.0.0", -1)
        for bad in ("1.0", "v1.0.0", "1.10.0", "1.0.10", "01.0.0", "1.0.0-rc.1", ""):
            with self.assertRaises(ValueError, msg=bad):
                version.bump(bad, 1)

    def test_points_table_matches_the_spec_R_03_CA_012_01(self) -> None:
        expected = {1: {"fix", "adjustment", "index"}, 2: {"new-field", "rule-change"}, 3: {"endpoint", "screen"},
                    5: {"collection", "full-feature"}, 10: {"service", "external-integration", "permission"}}
        for value, kinds in expected.items():
            self.assertEqual(kinds, {k for k, v in version.POINTS.items() if v == value})


class LabelTests(unittest.TestCase):
    def test_pr_without_points_label_fails_CA_012_04(self) -> None:
        self.assertTrue(version.check_labels([]))
        self.assertTrue(version.check_labels(["bug", "documentation"]))

    def test_pr_with_valid_points_label_passes_CA_012_04(self) -> None:
        self.assertEqual([], version.check_labels(["bug", "puntos-gateway-3"]))
        self.assertEqual([], version.check_labels(["puntos-app-0"]))

    def test_malformed_or_unknown_label_fails_CA_012_04(self) -> None:
        for bad in ("puntos-gateway", "puntos-gateway-", "puntos-gateway-03", "puntos-gateway--1", "puntos-backend-3",
                    "puntos-gateway-3x", "puntos-gateway-1000"):
            self.assertTrue(version.check_labels([bad]), bad)

    def test_valid_label_does_not_hide_a_malformed_one_CA_012_04(self) -> None:
        self.assertTrue(version.check_labels(["puntos-gateway-3", "puntos-market-x"]))

    def test_two_labels_for_the_same_component_fail_CA_012_04(self) -> None:
        self.assertTrue(version.check_labels(["puntos-gateway-3", "puntos-gateway-1"]))

    def test_points_are_kept_per_component_R_01(self) -> None:
        found, errors = version.parse_labels(["puntos-gateway-3", "puntos-landing-1"])
        self.assertEqual([], errors)
        self.assertEqual({"gateway": 3, "landing": 1}, found)

    def test_bot_branches_are_exempt_CA_012_04(self) -> None:
        self.assertEqual([], version.check_labels([], "release-please--branches--main"))
        self.assertEqual([], version.check_labels([], "dependabot/nuget/x"))
        self.assertTrue(version.check_labels([], "feature/KNOCK-1-x"))

    def test_labels_json_must_be_a_list_of_strings_CA_012_04(self) -> None:
        self.assertEqual(["puntos-app-1"], version.load_labels('["puntos-app-1"]'))
        with self.assertRaises(ValueError):
            version.load_labels('{"a": 1}')
        with self.assertRaises(ValueError):
            version.load_labels("[1, 2]")


if __name__ == "__main__":
    unittest.main()
