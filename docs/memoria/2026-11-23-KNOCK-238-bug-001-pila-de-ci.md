# BUG-001: pila de CI para el humo y el DAST

## Qué cambió
- `deploy/docker-compose.ci.yml`: gateway, Identidad, Mercado, Mensajería, documentdb-local, Redis y Azurite (imágenes por digest); solo se publica el 8080 del gateway y cada servicio tiene su sonda de `/health/ready`. Credenciales solo por variables `CI_*`.
- `deploy/docker-compose.local.yml`: Azurite 3.37.0 por digest.
- `tests/smoke/smoke.sh` y su autoprueba negativa `tests/smoke/smoke_test.sh` (TC-BUG-001-03 y 04); `security/zap-rules.tsv` sin reglas.
- `ci-pr.yml`: credenciales efímeras, espera `--wait` con registros al fallar, autoprueba del humo, y el `gate` falla si `docker=true` y `dast=false` (TC-BUG-001-06).

## Por qué
El job «Smoke + DAST» salía siempre `skipped` porque faltaban esos archivos, y el `gate` lo aceptaba como bueno.

## A evitar
No agregar un perfil `Ci` en `src/`: los servicios corren como Development con valores sintéticos. Si el primer run del job no arranca algún servicio, se corrige la configuración del compose, no se debilita un control. No ignorar alertas de ZAP sin `EXC-NNN`.
