# Tareas 042 — Contactos de confianza y técnicos de confianza

> **Estado:** pendientes. En esta etapa **no se ejecutan**: se activan cuando la spec esté Aprobada y una persona lo indique.
> «Depende de» = tareas que deben estar fusionadas antes; «Paralelo con» = tareas que se pueden trabajar a la vez.

| Tarea | Qué se hace | Cubre | Depende de | Paralelo con | Responsable sugerido | Estado |
|---|---|---|---|---|---|---|
| T-01 | Script de `db/` con colecciones e índices de §6 y prueba de base de datos | §6 | — | T-02 en adelante (con datos de prueba) | por asignar | pendiente |
| T-02 | `GET /api/v1/me/contactos-confianza` (CON-01): DTO, validador, servicio, almacén, controlador y pruebas | R-01 · CA-01 a CA-02 | T-01 | otras rutas | por asignar | pendiente |
| T-03 | `POST /api/v1/me/contactos-confianza` (CON-02): DTO, validador, servicio, almacén, controlador y pruebas | R-02 a R-03 · CA-03 a CA-06 | T-01 | otras rutas | por asignar | pendiente |
| T-04 | `DELETE /api/v1/me/contactos-confianza/{id}` (CON-03): DTO, validador, servicio, almacén, controlador y pruebas | R-04 a R-06 · CA-07 a CA-11 | T-01 | otras rutas | por asignar | pendiente |
| T-05 | `GET /api/v1/me/tecnicos-confianza` (TCO-01): DTO, validador, servicio, almacén, controlador y pruebas | R-07 a R-08 · CA-12 a CA-14 | T-01 | otras rutas | por asignar | pendiente |
| T-06 | `PUT /api/v1/me/tecnicos-confianza/{tecnicoId}` (TCO-02): DTO, validador, servicio, almacén, controlador y pruebas | R-09 a R-11 · CA-15 a CA-19 | T-01 | otras rutas | por asignar | pendiente |
| T-07 | `DELETE /api/v1/me/tecnicos-confianza/{tecnicoId}` (TCO-03): DTO, validador, servicio, almacén, controlador y pruebas | R-12 a R-14 · CA-20 a CA-23 | T-01 | otras rutas | por asignar | pendiente |
| T-08 | Transversales: `sub` del token, registros sin datos personales, 429 y 413, sin `Idempotency-Key` | R-15 a R-18 · CA-24 a CA-27 | T-02 a T-07 | T-09 | por asignar | pendiente |
| T-09 | OpenAPI, `docs/endpoints.md` y `docs/rate-limit-matrix.md` | §5 | T-02 a T-07 | T-08 | por asignar | pendiente |
| T-10 | Gates, `Memoria.md` y revisión de par | §12 | todas | — | por asignar | pendiente |

## Definición de terminado
- Los criterios de aceptación de `spec.md` están cubiertos por pruebas en verde.
- El CI está en verde, un par revisó el cambio y `Memoria.md` está al día.
