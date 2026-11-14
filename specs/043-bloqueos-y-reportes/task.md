# Tareas 043 — Bloqueos entre personas y reportes con apelación

> **Estado:** pendientes. En esta etapa **no se ejecutan**: se activan cuando la spec esté Aprobada y una persona lo indique.
> «Depende de» = tareas que deben estar fusionadas antes; «Paralelo con» = tareas que se pueden trabajar a la vez.

| Tarea | Qué se hace | Cubre | Depende de | Paralelo con | Responsable sugerido | Estado |
|---|---|---|---|---|---|---|
| T-01 | Script de `db/` con colecciones e índices de §6 y prueba de base de datos | §6 | — | T-02 en adelante (con datos de prueba) | por asignar | pendiente |
| T-02 | `POST /api/v1/usuarios/{id}/bloqueo` (BLO-01): DTO, validador, servicio, almacén, controlador y pruebas | R-01 a R-03 · CA-01 a CA-05 | T-01 | otras rutas | por asignar | pendiente |
| T-03 | `DELETE /api/v1/usuarios/{id}/bloqueo` (BLO-02): DTO, validador, servicio, almacén, controlador y pruebas | R-04 a R-06 · CA-06 a CA-09 | T-01 | otras rutas | por asignar | pendiente |
| T-04 | `GET /api/v1/me/bloqueos` (BLO-03): DTO, validador, servicio, almacén, controlador y pruebas | R-07 a R-08 · CA-10 a CA-12 | T-01 | otras rutas | por asignar | pendiente |
| T-05 | `POST /api/v1/reportes` (REP-01): DTO, validador, servicio, almacén, controlador y pruebas | R-09 a R-10 · CA-13 a CA-16 | T-01 | otras rutas | por asignar | pendiente |
| T-06 | `GET /api/v1/reportes/me` (REP-02): DTO, validador, servicio, almacén, controlador y pruebas | R-11 a R-12 · CA-17 a CA-19 | T-01 | otras rutas | por asignar | pendiente |
| T-07 | `POST /api/v1/reportes/{id}/apelacion` (REP-03): DTO, validador, servicio, almacén, controlador y pruebas | R-13 a R-15 · CA-20 a CA-25 | T-01 | otras rutas | por asignar | pendiente |
| T-08 | Reglas de negocio específicas de la spec (estados, plazos y límites) | R-16 a R-22 · CA-26 a CA-32 | T-01 | otras rutas | por asignar | pendiente |
| T-09 | Transversales: `sub` del token, registros sin datos personales, 429 y 413, sin `Idempotency-Key` | R-23 a R-26 · CA-33 a CA-36 | T-02 a T-08 | T-10 | por asignar | pendiente |
| T-10 | OpenAPI, `docs/endpoints.md` y `docs/rate-limit-matrix.md` | §5 | T-02 a T-08 | T-09 | por asignar | pendiente |
| T-11 | Gates, `Memoria.md` y revisión de par | §12 | todas | — | por asignar | pendiente |

## Definición de terminado
- Los criterios de aceptación de `spec.md` están cubiertos por pruebas en verde.
- El CI está en verde, un par revisó el cambio y `Memoria.md` está al día.
