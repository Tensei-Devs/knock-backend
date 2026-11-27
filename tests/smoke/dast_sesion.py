#!/usr/bin/env python3
"""BUG-001 (KNOCK-266): sesiones ficticias para el segundo paso de ZAP, solo contra la pila efímera del runner.

Firma con la llave HMAC efímera del job (`CI_JWT_KEY`) dos JWT de cuentas de demostración (`db/seeds/demo`), de 30 minutos,
y los deja en un archivo del workspace que lee `zap_rutas.py`. La llave muere con el job y la pila se destruye al terminar.
Uso: dast_sesion.py <archivo-de-salida>
"""
import base64
import hashlib
import hmac
import json
import os
import secrets
import sys
import time
import uuid

# Mismo cálculo que UserIdDerivation (Identidad): UUID v8 con SHA-256 sobre el namespace fijo y el uid de demostración.
NAMESPACE = uuid.UUID("bcfea58b-95dd-4c0e-80d1-baf839f2e36e")
CUENTAS = {"cliente": "demo-cliente-01", "tecnico": "demo-tecnico-01"}


def sub_de(uid):
    digest = bytearray(hashlib.sha256(NAMESPACE.bytes + uid.encode()).digest()[:16])
    digest[6] = (digest[6] & 0x0F) | 0x80
    digest[8] = (digest[8] & 0x3F) | 0x80
    return str(uuid.UUID(bytes=bytes(digest)))


def b64url(data):
    return base64.urlsafe_b64encode(data).rstrip(b"=").decode()


def firmar(claims, llave):
    cabecera = b64url(json.dumps({"alg": "HS256", "typ": "JWT"}).encode())
    cuerpo = b64url(json.dumps(claims).encode())
    firma = hmac.new(llave, f"{cabecera}.{cuerpo}".encode(), hashlib.sha256).digest()
    return f"{cabecera}.{cuerpo}.{b64url(firma)}"


def main():
    llave = base64.b64decode(os.environ["CI_JWT_KEY"])
    ahora = int(time.time())
    tokens = {}
    for nombre, uid in CUENTAS.items():
        claims = {"iss": "knock-api", "aud": "knock-app", "sub": sub_de(uid), "sid": secrets.token_hex(12), "jti": str(uuid.uuid4()),
                  "iat": ahora, "nbf": ahora, "exp": ahora + 1800}
        tokens[nombre] = firmar(claims, llave)
        print(f"::add-mask::{tokens[nombre]}")
    with open(sys.argv[1], "w", encoding="utf-8") as salida:
        json.dump(tokens, salida)


if __name__ == "__main__":
    main()
