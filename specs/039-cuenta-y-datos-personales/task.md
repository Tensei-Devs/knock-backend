# Tareas 039 — Cuenta y datos personales: cambio de número, recuperación de acceso, exportación, consentimientos y eliminación

> **Estado:** pendientes. En esta etapa **no se ejecutan**: se activan cuando la spec esté Aprobada y una persona lo indique.
> «Depende de» = tareas que deben estar fusionadas antes; «Paralelo con» = tareas que se pueden trabajar a la vez.

| Tarea | Qué se hace | Cubre | Depende de | Paralelo con | Responsable sugerido | Estado |
|---|---|---|---|---|---|---|
| T-01 | Script de `db/` con colecciones e índices de §6 y prueba de base de datos | §6 | — | T-02 en adelante (con datos de prueba) | por asignar | pendiente |
| T-02 | `POST /api/v1/me/cambio-numero` (USR-03): DTO, validador, servicio, almacén, controlador y pruebas | R-01 a R-03 · CA-01 a CA-05 | T-01 | otras rutas | por asignar | pendiente |
| T-03 | `POST /api/v1/me/cambio-numero/confirmar` (USR-04): DTO, validador, servicio, almacén, controlador y pruebas | R-04 a R-05 · CA-06 a CA-09 | T-01 | otras rutas | por asignar | pendiente |
| T-04 | `POST /api/v1/recuperacion-acceso` (USR-05): DTO, validador, servicio, almacén, controlador y pruebas | R-06 a R-07 · CA-10 a CA-12 | T-01 | otras rutas | por asignar | pendiente |
| T-05 | `POST /api/v1/me/exportaciones` (USR-06): DTO, validador, servicio, almacén, controlador y pruebas | R-08 a R-09 · CA-13 a CA-15 | T-01 | otras rutas | por asignar | pendiente |
| T-06 | `GET /api/v1/me/consentimientos` (USR-07): DTO, validador, servicio, almacén, controlador y pruebas | R-10 · CA-16 a CA-17 | T-01 | otras rutas | por asignar | pendiente |
| T-07 | `PUT /api/v1/me/consentimientos` (USR-08): DTO, validador, servicio, almacén, controlador y pruebas | R-11 a R-12 · CA-18 a CA-21 | T-01 | otras rutas | por asignar | pendiente |
| T-08 | `GET /api/v1/me/pendientes` (USR-09): DTO, validador, servicio, almacén, controlador y pruebas | R-13 · CA-22 a CA-23 | T-01 | otras rutas | por asignar | pendiente |
| T-09 | `POST /api/v1/me/eliminacion` (USR-10): DTO, validador, servicio, almacén, controlador y pruebas | R-14 a R-16 · CA-24 a CA-28 | T-01 | otras rutas | por asignar | pendiente |
| T-10 | `POST /api/v1/me/eliminacion/cancelar` (USR-11): DTO, validador, servicio, almacén, controlador y pruebas | R-17 a R-18 · CA-29 a CA-31 | T-01 | otras rutas | por asignar | pendiente |
| T-11 | Reglas de negocio específicas de la spec (estados, plazos y límites) | R-19 a R-25 · CA-32 a CA-38 | T-01 | otras rutas | por asignar | pendiente |
| T-12 | Transversales: `sub` del token, registros sin datos personales, 429 y 413, sin `Idempotency-Key` | R-26 a R-29 · CA-39 a CA-42 | T-02 a T-11 | T-13 | por asignar | pendiente |
| T-13 | OpenAPI, `docs/endpoints.md` y `docs/rate-limit-matrix.md` | §5 | T-02 a T-11 | T-12 | por asignar | pendiente |
| T-14 | Gates, `Memoria.md` y revisión de par | §12 | todas | — | por asignar | pendiente |

## Definición de terminado
- Los criterios de aceptación de `spec.md` están cubiertos por pruebas en verde.
- El CI está en verde, un par revisó el cambio y `Memoria.md` está al día.
