# Tareas 044 — Avisos dentro de la app y preferencias de avisos

> **Estado:** pendientes. En esta etapa **no se ejecutan**: se activan cuando la spec esté Aprobada y una persona lo indique.
> «Depende de» = tareas que deben estar fusionadas antes; «Paralelo con» = tareas que se pueden trabajar a la vez.

| Tarea | Qué se hace | Cubre | Depende de | Paralelo con | Responsable sugerido | Estado |
|---|---|---|---|---|---|---|
| T-00 | Prerrequisitos que hoy no existen y no son parte de esta spec: Despachador de avisos de la spec 037, único que crea avisos (hoy no implementado) | §8 | — | T-01 | por asignar | pendiente |
| T-01 | Script de `db/` con colecciones e índices de §6 y prueba de base de datos | §6 | — | T-02 en adelante (con datos de prueba) | por asignar | hecha (KNOCK-105): `db/044-avisos-dentro-de-la-app.js` (idempotente, base `KNOCK_DB_MESSAGING`) crea `avisos` (índice `usuarioId`+`leido`+`creadoEn` descendente y TTL en `venceEn`) y `preferencias_avisos` (`usuarioId` único). Prueba: `Spec044AvisosIndicesTests` (CA-20 y CA-24; corre solo en CI, usa Docker) |
| T-02 | `GET /api/v1/avisos` (AVI-01): DTO, validador, servicio, almacén, controlador y pruebas | R-01 a R-03 · CA-01 a CA-04 | T-01 | otras rutas | por asignar | hecha (KNOCK-105): `AvisosController.List`, `NoticesService`, `NoticesStore` (único punto con `IMongoCollection`), `ListNoticesQueryValidator`, `NoticeRules` y `NoticeCursor` (cursor opaco por fecha e `_id`) en `Modules/Notices`; política `avisos-leer` (120 por minuto por `sub`). Pruebas: `Spec044AvisosTests` (CA-01 a CA-04 con almacén de mentira). La ruta no está en el gateway hasta T-09 |
| T-03 | `GET /api/v1/avisos/no-leidos/contador` (AVI-02): DTO, validador, servicio, almacén, controlador y pruebas | R-04 · CA-05 a CA-06 | T-01 | otras rutas | por asignar | hecha (KNOCK-105): `AvisosController.CountUnread`, `NoticesService.CountUnreadAsync` y `NoticesStore.CountUnreadAsync` (`CountDocuments` con tope de 100 sobre el índice `usuarioId`+`leido`); responde `{ cantidad, masDe99 }` sin caché. Pruebas: `Spec044AvisosTests` (CA-05 y CA-06) |
| T-04 | `POST /api/v1/avisos/{id}/leido` (AVI-03): DTO, validador, servicio, almacén, controlador y pruebas | R-05 a R-07 · CA-07 a CA-11 | T-01 | otras rutas | por asignar | hecha (KNOCK-105): `AvisosController.MarkRead` (204 idempotente; 404 `notice_not_found` uniforme para ajeno e inexistente), `MarkNoticeReadRequest` y su validador (`ObjectId` de 24 hexadecimales), `NoticesStore.MarkReadAsync` con filtro por `_id` y `usuarioId`; política `avisos-marcar` (60 por minuto por `sub`) y tope de cuerpo de 1 KiB. Pruebas: `Spec044AvisosTests` (CA-07 a CA-11, CA-17). **Decisión:** CA-09 pide 403 y CA-11/R-12 piden 404 sin revelar; se aplica 404 uniforme (PARA VALIDACIÓN DE JOSÉ) |
| T-05 | `POST /api/v1/avisos/leidos` (AVI-04): DTO, validador, servicio, almacén, controlador y pruebas | R-08 · CA-12 a CA-13 | T-01 | otras rutas | por asignar | hecha (KNOCK-105): `AvisosController.MarkAllRead` y `NoticesStore.MarkAllReadAsync` (`UpdateMany` solo sobre los avisos sin leer de quien llama; 204 aunque no haya ninguno). Pruebas: `Spec044AvisosTests` (CA-12 y CA-13) |
| T-06 | `GET /api/v1/me/preferencias-avisos` (PRE-01): DTO, validador, servicio, almacén, controlador y pruebas | R-09 · CA-14 a CA-15 | T-01 | otras rutas | por asignar | pendiente |
| T-07 | Reglas de negocio específicas de la spec (estados, plazos y límites) | R-10 a R-14 · CA-16 a CA-20 | T-01 | otras rutas | por asignar | pendiente |
| T-08 | Transversales: `sub` del token, registros sin datos personales, 429 y 413, sin `Idempotency-Key` | R-15 a R-18 · CA-21 a CA-24 | T-02 a T-07 | T-09 | por asignar | pendiente |
| T-09 | OpenAPI, `docs/endpoints.md` y `docs/rate-limit-matrix.md` | §5 | T-02 a T-07 | T-08 | por asignar | pendiente |
| T-10 | Gates, `Memoria.md` y revisión de par | §12 | todas | — | por asignar | pendiente |

## Definición de terminado
- Los criterios de aceptación de `spec.md` están cubiertos por pruebas en verde.
- El CI está en verde, un par revisó el cambio y `Memoria.md` está al día.
