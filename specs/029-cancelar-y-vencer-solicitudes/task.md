# Tareas 029 — Cancelar y vencer solicitudes

> **Estado:** en curso; conciliado con el código el 2026-11-23 (cada fila cita su evidencia: archivo o prueba). Lo que dice «pendiente» no tiene aún evidencia verificable.
> **Responsable sugerido de las historias:** Guillermo (KNOCK-70 y KNOCK-71). Revisión de par: Erick.

| Tarea | Qué se hace | Cubre | Depende de | En paralelo con | Responsable sugerido | Estado |
|---|---|---|---|---|---|---|
| T-01 | `TransicionesDeSolicitud` pura con unitarias y mutación | R-01, R-05 · CA-02, CA-11 | — | T-02, T-05 | Alexis | hecha |
| T-02 | `CierreDeSolicitudService`: cascada transaccional (solicitud, cotizaciones, envíos, evento) | R-03, R-07, R-09 · CA-01, CA-06, CA-15 | T-01; specs 020, 021 y 028 (envíos) | T-05 | Alexis | parcial: cascada y reversión probadas; faltan cobertura completa de criterios e integración con envíos de 028 |
| T-03 | Ruta de cancelación: controller, validador, idempotencia, `no-store`, límite de 10 por hora | R-02, R-04, R-06, R-14, R-15 · CA-03, CA-04, CA-05, CA-12, CA-13 | T-02 | T-04 | Alexis | hecha (KNOCK-70): `CancelacionController`, `CancelRequestBodyValidator`, límite `solicitudes-cancelar` (10/h), reintento ante carrera. Pruebas: `Spec029CancelarTests` (integración, solo CI) y `Spec029CancelValidatorTests` |
| T-04 | Manejadores `VencerSolicitud` y `BarrerVencidas`; programación en la creación; opciones del plazo validadas | R-08, R-10, R-11 · CA-06, CA-07, CA-08, CA-09, CA-14 | T-02; ejecutor (T-02 de la 028) | T-03 | Guillermo | hecha (KNOCK-71): `VencerSolicitud`, `BarrerVencidas` (lotes de 100, reprograma con clave nueva), programación en la transacción de creación y `SolicitudesOptions` validadas al arrancar. Pruebas: `Spec029OptionsTests` y `Spec029VencerTests` (CA-06 a CA-09, CA-14; integración solo en CI). El ejecutor de la 028 ya está en sprint-05 |
| T-05 | `puedeVolverAPedir` en el detalle y 404 para técnicos no elegidos en solicitudes cerradas | R-12, R-13 · CA-10, CA-11 | — | T-01, T-02 | Alexis | hecha (KNOCK-70): campo solo en la vista del cliente y 404 al técnico con la solicitud cancelada o vencida; prueba `Spec029CancelarTests` (solo CI) |
| T-06 | Script de `db/` (índices y programación idempotente del barrido) | R-10 · CA-09 | T-04 | T-07 | Guillermo | hecha (KNOCK-71): `db/029-requests-vencimiento.js` (índices y barrido inicial idempotente); prueba `Spec029ExpiryScriptTests` (solo CI) |
| T-07 | Pruebas de concurrencia (cancelar contra elegir; dos ejecutores) y seguridad negativa; registros sin datos | R-06, R-11, R-15 · CA-05, CA-08, CA-13, CA-15 | T-03, T-04 | T-06 | Guillermo | hecha (KNOCK-71): `Spec029ConcurrenciaTests` (cancelar contra elegir CA-05; tarea y barrido a la vez CA-08; registros y eventos sin datos personales CA-15); CA-13 ya en `Spec029CancelarTests`. Integración solo en CI |
| T-08 | OpenAPI regenerado, `docs/endpoints.md`, `docs/events.md`, `docs/rate-limit-matrix.md`, `Memoria.md` | R-02, R-07 | T-03, T-05 | T-07 | Erick | parcial (KNOCK-71): OpenAPI ya incluye la ruta (sin endpoints nuevos), `docs/events.md` y `Memoria` hechos; faltan `docs/endpoints.md` y `docs/rate-limit-matrix.md` (cierre F3) |

**Qué bloquea:** el ejecutor de la 028 bloquea T-04; T-02 bloquea T-03 y T-04. **Qué va en paralelo:** T-01, T-05 y la parte de la cascada sin envíos arrancan sin esperar a la 028.

## Definición de terminado
- Los criterios de aceptación de `spec.md` están cubiertos por pruebas en verde.
- El CI está en verde, un par revisó el cambio y `Memoria.md` está al día.
