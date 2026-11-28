#!/usr/bin/env bash
# BUG-001 (TC-BUG-001-03): prueba de humo del gateway. Uso: smoke.sh <url-base>
# Comprueba /health 200, ruta protegida sin token = 401 application/problem+json, cuatro encabezados de seguridad y ausencia de `Server: Kestrel`.
# Sale con 0 solo si todo se cumple; imprime una línea por comprobación fallida, sin cuerpos ni valores de encabezados.
set -uo pipefail

base="${1:?uso: smoke.sh <url-base>}"
base="${base%/}"
fallos=0
fallo() { echo "FALLO: $1"; fallos=$((fallos + 1)); }

estado() { curl -s -o /dev/null -w '%{http_code}' --max-time 15 "$1" || echo 000; }
encabezados() { curl -s -D - -o /dev/null --max-time 15 "$1" | tr -d '\r'; }

[ "$(estado "$base/health")" = "200" ] || fallo "GET /health no responde 200"

protegida="$base/api/v1/me"
[ "$(estado "$protegida")" = "401" ] || fallo "GET /api/v1/me sin token no responde 401"
cabeceras="$(encabezados "$protegida")"
printf '%s\n' "$cabeceras" | grep -qi '^content-type: *application/problem+json' || fallo "401 sin application/problem+json"

for h in x-content-type-options x-frame-options referrer-policy content-security-policy; do
  printf '%s\n' "$cabeceras" | grep -qi "^$h:" || fallo "falta el encabezado $h"
done
printf '%s\n' "$cabeceras" | grep -qi '^server: *kestrel' && fallo "se envía Server: Kestrel"

if [ "$fallos" -ne 0 ]; then
  echo "Prueba de humo: $fallos comprobación(es) fallida(s)"
  exit 1
fi
echo "Prueba de humo: todas las comprobaciones pasaron"
