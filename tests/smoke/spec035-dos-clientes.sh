#!/usr/bin/env bash
# Spec 035, CA-15 y R-15: humo de dos clientes en el entorno de evaluación (NO se corre contra Azure desde el CI).
# Uso: spec035-dos-clientes.sh <url-base> <origen-cliente-A> <origen-cliente-B>
#   <origen-*> es lo que `curl --interface` acepta (una dirección local, un nombre de interfaz); los dos deben salir a internet con IP pública DISTINTA
#   (por ejemplo, la red de la casa y el punto de acceso del teléfono). Con dos orígenes que comparten IP el script falla a propósito: no prueba nada.
# Comprueba: el cliente A agota el límite estricto de /api/v1/auth/* (10 por minuto por IP) y recibe 429; el cliente B, enseguida, sigue sin recibir 429.
# Envía cuerpos vacíos a POST /api/v1/auth/firebase (se rechazan sin tocar a Firebase) y no imprime cuerpos ni direcciones.
# Sale con 0 solo si se cumple todo.
set -uo pipefail

base="${1:?uso: spec035-dos-clientes.sh <url-base> <origen-A> <origen-B>}"
origen_a="${2:?falta el origen del cliente A}"
origen_b="${3:?falta el origen del cliente B}"
base="${base%/}"
ruta="$base/api/v1/auth/firebase"

peticion() {
  curl -s -o /dev/null -w '%{http_code}' --max-time 15 --interface "$1" -X POST -H 'Content-Type: application/json' -d '{}' "$ruta" || echo 000
}

limitado=0
for _ in $(seq 1 15); do
  if [ "$(peticion "$origen_a")" = "429" ]; then
    limitado=1
    break
  fi
done
if [ "$limitado" != "1" ]; then
  echo "FALLO: el cliente A no llegó a 429 tras 15 peticiones (¿límite distinto o confianza mal configurada?)"
  exit 1
fi

estado_b="$(peticion "$origen_b")"
if [ "$estado_b" = "429" ]; then
  echo "FALLO: el cliente B también recibe 429: comparten partición (IP igual o ForwardedHeaders:KnownNetworks sin el rango de la entrada)"
  exit 1
fi
if [ "$estado_b" = "000" ]; then
  echo "FALLO: el cliente B no obtuvo respuesta"
  exit 1
fi

echo "Humo de dos clientes: A llegó a 429 y B no se vio afectado"
