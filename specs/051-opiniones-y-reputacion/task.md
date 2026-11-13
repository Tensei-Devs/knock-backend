# Tareas 051 — Opiniones y reputación del técnico

> **Estado:** pendientes. En esta etapa **no se ejecutan**: se activan cuando la spec esté Aprobada y una persona lo indique.
> «Depende de» = tareas que deben estar fusionadas antes; «Paralelo con» = tareas que se pueden trabajar a la vez.

| Tarea | Qué se hace | Cubre | Depende de | Paralelo con | Responsable sugerido | Estado |
|---|---|---|---|---|---|---|
| T-01 | Script de `db/` con colecciones e índices de §6 y prueba de base de datos | §6 | — | T-02 en adelante (con datos de prueba) | por asignar | pendiente |
| T-02 | `GET /api/v1/tecnicos/{id}/opiniones` (OPI-01): DTO, validador, servicio, almacén, controlador y pruebas | R-01 a R-05 · CA-01 a CA-06 | T-01 | otras rutas | por asignar | pendiente |
| T-03 | `POST /api/v1/visitas/{id}/opinion` (OPI-02): DTO, validador, servicio, almacén, controlador y pruebas | R-06 a R-08 · CA-07 a CA-12 | T-01 | otras rutas | por asignar | pendiente |
| T-04 | `POST /api/v1/opiniones/{id}/respuesta` (OPI-03): DTO, validador, servicio, almacén, controlador y pruebas | R-09 a R-11 · CA-13 a CA-18 | T-01 | otras rutas | por asignar | pendiente |
| T-05 | Transversales: `sub` del token, registros sin datos personales, 429 y 413, sin `Idempotency-Key` | R-12 a R-15 · CA-19 a CA-22 | T-02 a T-04 | T-06 | por asignar | pendiente |
| T-06 | OpenAPI, `docs/endpoints.md` y `docs/rate-limit-matrix.md` | §5 | T-02 a T-04 | T-05 | por asignar | pendiente |
| T-07 | Gates, `Memoria.md` y revisión de par | §12 | todas | — | por asignar | pendiente |

## Definición de terminado
- Los criterios de aceptación de `spec.md` están cubiertos por pruebas en verde.
- El CI está en verde, un par revisó el cambio y `Memoria.md` está al día.
