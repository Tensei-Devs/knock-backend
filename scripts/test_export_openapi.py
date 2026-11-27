#!/usr/bin/env python3
"""Pruebas de export_openapi.py: ruta de salida por servicio, --service inválido y configuración sintética de Identity.
Uso: python scripts/test_export_openapi.py (sin dependencias; no arranca ninguna API)."""
from __future__ import annotations

import base64
import contextlib
import io
import json
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import export_openapi  # noqa: E402


class OutputPathTests(unittest.TestCase):
    def test_market_keeps_its_historical_path(self) -> None:
        self.assertEqual(export_openapi.ROOT / "docs" / "openapi.v1.json", export_openapi.default_output("market"))

    def test_identity_has_its_own_file(self) -> None:
        self.assertEqual(export_openapi.ROOT / "docs" / "openapi.identity.v1.json", export_openapi.default_output("identity"))

    def test_project_path_per_service(self) -> None:
        self.assertEqual("Knock.Market.Api.csproj", export_openapi.project_path("market").name)
        self.assertEqual("Knock.Identity.Api.csproj", export_openapi.project_path("identity").name)

    def test_outputs_are_distinct(self) -> None:
        outputs = {export_openapi.default_output(service) for service in export_openapi.SERVICES}
        self.assertEqual(len(export_openapi.SERVICES), len(outputs))


class ArgumentTests(unittest.TestCase):
    def test_default_service_is_market(self) -> None:
        args = export_openapi.parse_args([])
        self.assertEqual("market", args.service)
        self.assertIsNone(args.output)

    def test_identity_is_accepted(self) -> None:
        self.assertEqual("identity", export_openapi.parse_args(["--service", "identity"]).service)

    def test_invalid_service_fails(self) -> None:
        with contextlib.redirect_stderr(io.StringIO()), self.assertRaises(SystemExit) as raised:
            export_openapi.parse_args(["--service", "gateway"])
        self.assertEqual(2, raised.exception.code)


class NormalizeTests(unittest.TestCase):
    def test_crlf_is_unified_and_servers_removed(self) -> None:
        raw = json.dumps({"servers": [{"url": "http://127.0.0.1:1"}], "info": {"summary": "a\r\nb"}, "list": ["x\r\ny"]}).encode()
        text = export_openapi.normalize(raw)
        self.assertNotIn("servers", text)
        self.assertNotIn("\\r", text)
        self.assertIn("a\\nb", text)
        self.assertTrue(text.endswith("\n"))


class SyntheticEnvironmentTests(unittest.TestCase):
    def test_market_cursor_key_is_random_and_valid(self) -> None:
        first = export_openapi.service_environment("market")["Busqueda__Cursor__Key"]
        second = export_openapi.service_environment("market")["Busqueda__Cursor__Key"]
        self.assertEqual(32, len(base64.b64decode(first, validate=True)))
        self.assertNotEqual(first, second)

    def test_identity_secrets_are_random_and_valid(self) -> None:
        first = export_openapi.service_environment("identity")
        second = export_openapi.service_environment("identity")
        for key in ("Jwt__SigningKey", "Identity__PhoneProtection__EncryptionKeys__1", "Identity__PhoneProtection__HmacKeys__1"):
            self.assertGreaterEqual(len(base64.b64decode(first[key], validate=True)), 32, key)
            self.assertNotEqual(first[key], second[key], key)
        self.assertNotEqual(first["ConnectionStrings__KnockRedis"], second["ConnectionStrings__KnockRedis"])
        self.assertTrue(first["ConnectionStrings__KnockRedis"].startswith("127.0.0.1:6379,password="))
        self.assertEqual("demo-knock", first["Firebase__ProjectId"])


def _service_doc(paths: dict, schemas: dict) -> dict:
    return {"openapi": "3.1.1", "info": {"title": "x", "version": "1.0.0"}, "paths": paths, "components": {"schemas": schemas}}


_GATEWAY = {"ReverseProxy": {"Routes": {
    "a": {"ClusterId": "identidad", "Match": {"Path": "/api/v1/auth/{**resto}"}},
    "b": {"ClusterId": "mercado", "Match": {"Path": "/api/v1/oficios/{**resto}", "Methods": ["GET"]}},
    "hub": {"ClusterId": "mensajeria", "Match": {"Path": "/hubs/chat/{**resto}"}},
}}}


class PublicContractTests(unittest.TestCase):
    def test_contrato_publico_conflictos_CA_034_14(self) -> None:
        identity = _service_doc({"/api/v{version}/auth/login": {"post": {"responses": {"200": {"description": "ok"}}}}}, {"Dto": {"type": "string"}})
        market = _service_doc({"/api/v{version}/oficios": {"get": {"responses": {"200": {"description": "ok"}}}}}, {"Dto": {"type": "integer"}})
        with self.assertRaises(export_openapi.PublicContractError) as raised:
            export_openapi.build_public({"identity": identity, "market": market}, _GATEWAY)
        self.assertIn("schemas/Dto", str(raised.exception))

    def test_contrato_publico_solo_rutas_del_gateway_CA_034_14(self) -> None:
        ok = {"responses": {"200": {"description": "ok"}}}
        identity = _service_doc({"/api/v{version}/auth/login": {"post": ok}, "/api/v{version}/interno/x": {"post": ok}}, {})
        market = _service_doc({"/api/v{version}/oficios/1": {"get": ok, "delete": ok}}, {"Dto": {"type": "string"}})
        public = export_openapi.build_public({"identity": identity, "market": market}, _GATEWAY)
        self.assertEqual(["/api/v{version}/auth/login", "/api/v{version}/oficios/1"], list(public["paths"]))
        self.assertEqual(["get"], list(public["paths"]["/api/v{version}/oficios/1"]))
        self.assertNotIn("servers", public)
        self.assertEqual("Knock API", public["info"]["title"])

    def test_contrato_publico_incluye_la_raiz_de_un_comodin_CA_034_14(self) -> None:
        # En YARP «{**resto}» acepta cero o más segmentos: la ruta raíz también la atiende el gateway.
        ok = {"responses": {"200": {"description": "ok"}}}
        market = _service_doc({
            "/api/v{version}/oficios": {"get": ok, "post": ok},
            "/api/v{version}/oficios/{clave}": {"get": ok},
            "/api/v{version}/oficios-pedidos": {"get": ok},
            "/api/v{version}/oficiosx/1": {"get": ok},
        }, {})
        public = export_openapi.build_public({"market": market}, _GATEWAY)
        self.assertEqual(["/api/v{version}/oficios", "/api/v{version}/oficios/{clave}"], list(public["paths"]))
        self.assertEqual(["get"], list(public["paths"]["/api/v{version}/oficios"]))

    def test_contrato_publico_misma_ruta_y_verbo_falla_CA_034_14(self) -> None:
        ok = {"responses": {"200": {"description": "ok"}}}
        doc = _service_doc({"/api/v{version}/oficios/1": {"get": ok}}, {})
        gateway = {"ReverseProxy": {"Routes": {"b": {"ClusterId": "mercado", "Match": {"Path": "/api/v1/oficios/{**r}"}}, "c": {"ClusterId": "identidad", "Match": {"Path": "/api/v1/oficios/{**r}"}}}}}
        with self.assertRaises(export_openapi.PublicContractError) as raised:
            export_openapi.build_public({"identity": doc, "market": doc}, gateway)
        self.assertIn("GET /api/v{version}/oficios/1", str(raised.exception))

    def test_publicacion_rechaza_datos_internos_CA_034_18(self) -> None:
        self.assertEqual([], export_openapi.check_public({"paths": {}}))
        self.assertTrue(export_openapi.check_public({"servers": [{"url": "https://x"}]}))
        self.assertTrue(export_openapi.check_public({"description": "http://knock-market/api"}))
        self.assertTrue(export_openapi.check_public({"a": {"example": "+525512345678"}}))
        self.assertTrue(export_openapi.check_public({"a": {"examples": {"x": {"value": "ana@example.com"}}}}))
        self.assertTrue(export_openapi.check_public({"a": {"example": "GOMC800101HDFRRL09"}}))

    def test_contrato_desactualizado_falla_CA_034_11(self) -> None:
        import tempfile

        with tempfile.TemporaryDirectory() as folder:
            target = Path(folder) / "openapi.publico.v1.json"
            target.write_text("{}\n", encoding="utf-8")
            args = export_openapi.parse_args(["--publico", "--check", "--output", str(target)])
            error = io.StringIO()
            with contextlib.redirect_stderr(error):
                self.assertEqual(1, export_openapi.run_public(args))
            self.assertIn("openapi.publico.v1.json", error.getvalue())
            self.assertEqual(0, export_openapi.run_public(export_openapi.parse_args(["--publico", "--output", str(target)])))
            self.assertEqual(0, export_openapi.run_public(args))


if __name__ == "__main__":
    unittest.main()
