# Tareas 048 — Solicitudes y cotizaciones: ampliar, archivos, referencia de mercado, cotizaciones del técnico y cierre por elección

> **Estado:** pendientes. En esta etapa **no se ejecutan**: se activan cuando la spec esté Aprobada y una persona lo indique.
> «Depende de» = tareas que deben estar fusionadas antes; «Paralelo con» = tareas que se pueden trabajar a la vez.

| Tarea | Qué se hace | Cubre | Depende de | Paralelo con | Responsable sugerido | Estado |
|---|---|---|---|---|---|---|
| T-01 | Script de `db/` con colecciones e índices de §6 y prueba de base de datos | §6 | — | T-02 en adelante (con datos de prueba) | por asignar | pendiente |
| T-02 | `POST /api/v1/solicitudes/{id}/ampliar` (SOL-05): DTO, validador, servicio, almacén, controlador y pruebas | R-01 a R-03 · CA-01 a CA-06 | T-01 | otras rutas | por asignar | pendiente |
| T-03 | `GET /api/v1/solicitudes/{id}/archivos` (SOL-08): DTO, validador, servicio, almacén, controlador y pruebas | R-04 a R-06 · CA-07 a CA-11 | T-01 | otras rutas | por asignar | pendiente |
| T-04 | `GET /api/v1/cotizaciones/referencia-mercado` (COT-03): DTO, validador, servicio, almacén, controlador y pruebas | R-07 a R-09 · CA-12 a CA-16 | T-01 | otras rutas | por asignar | pendiente |
| T-05 | `GET /api/v1/tecnicos/me/cotizaciones` (COT-05): DTO, validador, servicio, almacén, controlador y pruebas | R-10 a R-12 · CA-17 a CA-21 | T-01 | otras rutas | por asignar | pendiente |
| T-06 | `POST /api/v1/conversaciones/{id}/cerrar-por-eleccion` (CNV-05): DTO, validador, servicio, almacén, controlador y pruebas | R-13 a R-14 · CA-22 a CA-25 | T-01 | otras rutas | por asignar | pendiente |
| T-07 | Transversales: `sub` del token, registros sin datos personales, 429 y 413, sin `Idempotency-Key` | R-15 a R-18 · CA-26 a CA-29 | T-02 a T-06 | T-08 | por asignar | pendiente |
| T-08 | OpenAPI, `docs/endpoints.md` y `docs/rate-limit-matrix.md` | §5 | T-02 a T-06 | T-07 | por asignar | pendiente |
| T-09 | Gates, `Memoria.md` y revisión de par | §12 | todas | — | por asignar | pendiente |

## Definición de terminado
- Los criterios de aceptación de `spec.md` están cubiertos por pruebas en verde.
- El CI está en verde, un par revisó el cambio y `Memoria.md` está al día.
