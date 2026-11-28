#!/usr/bin/env bash
# BUG-001 (TC-BUG-001-04): autoprueba negativa de smoke.sh contra un servidor de prueba (python3) que omite un control a la vez.
set -uo pipefail
aqui="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
puerto=18089
servidor_pid=""
trap '[ -n "$servidor_pid" ] && kill "$servidor_pid" 2>/dev/null' EXIT

arrancar() { # $1 = encabezado a omitir, "server" para enviar Server: Kestrel, "sintoken" para responder 200 sin token
  [ -n "$servidor_pid" ] && kill "$servidor_pid" 2>/dev/null && wait "$servidor_pid" 2>/dev/null
  MODO="$1" PUERTO="$puerto" python3 - <<'PY' &
import os
from http.server import BaseHTTPRequestHandler, HTTPServer
modo = os.environ["MODO"]
class H(BaseHTTPRequestHandler):
    def log_message(self, *a): pass
    def do_GET(self):
        ok = self.path == "/health" or modo == "sintoken"
        self.send_response(200 if ok else 401)
        self.send_header("Content-Type", "application/problem+json")
        for h in ["X-Content-Type-Options", "X-Frame-Options", "Referrer-Policy", "Content-Security-Policy"]:
            if h != modo:
                self.send_header(h, "x")
        if modo == "server":
            self.send_header("Server", "Kestrel")
        self.end_headers()
HTTPServer(("127.0.0.1", int(os.environ["PUERTO"])), H).serve_forever()
PY
  servidor_pid=$!
  for _ in $(seq 1 20); do curl -s -o /dev/null "http://127.0.0.1:$puerto/health" && return; sleep 0.25; done
}

fallos=0
arrancar ninguno
bash "$aqui/smoke.sh" "http://127.0.0.1:$puerto" >/dev/null || { echo "TC-BUG-001-04: el caso sano debía pasar"; fallos=$((fallos + 1)); }

for caso in X-Content-Type-Options X-Frame-Options Referrer-Policy Content-Security-Policy server sintoken; do
  arrancar "$caso"
  salida="$(bash "$aqui/smoke.sh" "http://127.0.0.1:$puerto")" && { echo "TC-BUG-001-04: $caso debía fallar"; fallos=$((fallos + 1)); continue; }
  printf '%s\n' "$salida" | grep -q '^FALLO:' || { echo "TC-BUG-001-04: $caso no nombra la comprobación"; fallos=$((fallos + 1)); }
done

[ "$fallos" -eq 0 ] && echo "TC-BUG-001-04 OK" || exit 1
