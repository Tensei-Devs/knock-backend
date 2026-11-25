# Specs 031 y 052: pruebas del canal HTTP, de Redis y de MongoDB reales, sin tocar Azure

## Qué cambió
- `Spec052HttpTests` (host sin contenedores, dobles de IA, cuentas, conversación, referencia y bitácoras) cubre el canal HTTP de ASI-01 a ASI-06: 401, 403 `not_a_technician`, 400, 422 sin repetir el valor, 404 `log_not_found`, 503 sin IA, 429 con `Retry-After`, 413, `sub` del token y registros sin datos personales (CA-02 a CA-22 y CA-24 a CA-38 de la 052).
- `Spec052ModeloTests` (CA-31): lo que llega al modelo no trae teléfono ni dirección.
- `Spec052BitacoraMongoTests` (CA-39) y `Spec031CuotaRedisTests` (CA-08) usan MongoDB y Redis reales y corren solo en el CI.
- `BitacoraStore.UpsertAsync` se serializa por par solicitud y técnico con `KeyedGate` (mismo remedio que los demás almacenes con índice único).
- `task.md` de 031 y 052 actualizados solo con lo verificado.

## Por qué
- `sdd_lint --trace` mostraba 23 casos de la 052 y 1 de la 031 sin prueba. Todo se hace con la IA y Content Safety apagados (`Asistente:Habilitado` y `Moderacion:Habilitado` en falso); ningún interruptor ni recurso de Azure se tocó.

## A evitar
- CA-23 de la 052 («recurso no disponible» responde 201 con ProblemDetails) contradice R-12 y R-15: no se probó ni se implementó; espera decisión de José.
- Lo que sigue sin validarse en Azure: llamadas reales a Azure OpenAI y Content Safety, identidad administrada, recurso en Terraform (031 T-10, 052 T-00), dependencias de las specs 030 y 048.
- Autorrevisión de seguridad: ninguna ruta nueva; los dobles solo viven en pruebas, sin llaves ni valores literales de secretos; el cambio de código es solo el turno por clave en una escritura propia de la persona del token. Revisión por par y `security-reviewer` omitidas por decisión de José (2026-11-24).
