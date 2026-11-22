# Tareas 045 — Catálogo de oficios, oficios pedidos y lista de espera (consulta interna)

> **Estado:** pendientes. En esta etapa **no se ejecutan**: se activan cuando la spec esté Aprobada y una persona lo indique.
> «Depende de» = tareas que deben estar fusionadas antes; «Paralelo con» = tareas que se pueden trabajar a la vez.

| Tarea | Qué se hace | Cubre | Depende de | Paralelo con | Responsable sugerido | Estado |
|---|---|---|---|---|---|---|
| T-00 | Prerrequisitos que hoy no existen y no son parte de esta spec: Panel con doble factor, permisos y auditoría de la spec 033 (hoy no implementados) | §8 | — | T-01 | por asignar | el panel de la 033 ya existe en Identity; falta decidir cómo autorizan las rutas `/admin` en Mercado (ver Memoria, KNOCK-111) |
| T-01 | Script de `db/` con colecciones e índices de §6 y prueba de base de datos | §6 | — | T-02 en adelante (con datos de prueba) | por asignar | hecha (KNOCK-111): `db/045-catalogos-y-lista-de-espera.js`; la lista de espera sigue en `listas_espera` (spec 023) |
| T-02 | `GET /api/v1/oficios/{clave}` (OFI-02): DTO, validador, servicio, almacén, controlador y pruebas | R-01 a R-04 · CA-01 a CA-04 | T-01 | otras rutas | por asignar | hecha (KNOCK-134): `OficiosController.Detail` (`GET /api/v1/oficios/{clave}`, público, con ETag), `OficioQueries.GetActiveAsync` sobre la lectura con caché de 24 h de oficios activos (`IActiveOficios`, que implementa `CatalogStore`; ahora lee también `frasesSugeridas`), `TradeKeyRoute` con su validador y `TradeRules`. 404 `trade_not_found` para inexistente o inactivo. Pruebas: `Spec045OficiosTests` (CA-01 a CA-03) y `Spec045OficioCacheTests` (CA-04 y CA-34, base de datos con Redis: solo CI). Sin ruta nueva en el gateway: `mercado-oficios` ya cubre `/api/v1/oficios/**` |
| T-03 | `GET /api/v1/admin/oficios` (OFI-03): DTO, validador, servicio, almacén, controlador y pruebas | R-05 · CA-05 a CA-07 | T-01 | otras rutas | por asignar | hecha (KNOCK-134): `AdminTradesController.List` (`GET /api/v1/admin/oficios`, token del panel y política `catalog:manage`, sin caché), `TradeAdminService` y `TradeAdminStore.ListAllAsync` (activos e inactivos, tope de seguridad de 500). Hoy responde 403 a todos porque la puerta de permisos del panel es `DenyAllPanelPermissionGate` (T-00 de la spec). Pruebas: `Spec045OficiosTests` (CA-05 a CA-07); HTTP real en `Spec045OficiosAdminHttpTests` (solo CI). **Se commitea junto con T-02** (comparten `TradeDtos`, `IActiveOficios` y el registro del módulo) |
| T-04 | `POST /api/v1/admin/oficios` (OFI-04): DTO, validador, servicio, almacén, controlador y pruebas | R-06 a R-08 · CA-08 a CA-13 | T-01 | otras rutas | por asignar | pendiente |
| T-05 | `PUT /api/v1/admin/oficios/{clave}` (OFI-05): DTO, validador, servicio, almacén, controlador y pruebas | R-09 a R-11 · CA-14 a CA-19 | T-01 | otras rutas | por asignar | pendiente |
| T-06 | `POST /api/v1/oficios-pedidos` (OFP-01): DTO, validador, servicio, almacén, controlador y pruebas | R-12 a R-13 · CA-20 a CA-24 | T-01 | otras rutas | por asignar | pendiente |
| T-07 | `GET /api/v1/admin/oficios-pedidos` (OFP-02): DTO, validador, servicio, almacén, controlador y pruebas | R-14 a R-15 · CA-25 a CA-28 | T-01 | otras rutas | por asignar | parcial (KNOCK-111): `GET /api/v1/admin/oficios-pedidos` en `AdminCatalogController` con `CatalogAdminService`/`CatalogAdminStore` y autorización provisional `PanelBearer` + `metrics:read` (puerta `IPanelPermissionGate`, falla cerrado); pruebas `Spec045AdminTests` y `Spec045AdminHttpTests` (integración solo en CI); sin `catalog:manage` ni auditoría hasta T-00 |
| T-08 | `GET /api/v1/admin/lista-espera` (LIS-02): DTO, validador, servicio, almacén, controlador y pruebas | R-16 a R-17 · CA-29 a CA-32 | T-01 | otras rutas | por asignar | parcial (KNOCK-111): `GET /api/v1/admin/lista-espera` (conteos por código postal sobre `listas_espera`, sin celulares); mismas piezas y pruebas que T-07 |
| T-09 | Reglas de negocio específicas de la spec (estados, plazos y límites) | R-18 a R-22 · CA-33 a CA-37 | T-01 | otras rutas | por asignar | pendiente |
| T-10 | Transversales: `sub` del token, registros sin datos personales, 429 y 413, sin `Idempotency-Key` | R-23 a R-26 · CA-38 a CA-41 | T-02 a T-09 | T-11 | por asignar | pendiente |
| T-11 | OpenAPI, `docs/endpoints.md` y `docs/rate-limit-matrix.md` | §5 | T-02 a T-09 | T-10 | por asignar | pendiente |
| T-12 | Gates, `Memoria.md` y revisión de par | §12 | todas | — | por asignar | pendiente |

## Definición de terminado
- Los criterios de aceptación de `spec.md` están cubiertos por pruebas en verde.
- El CI está en verde, un par revisó el cambio y `Memoria.md` está al día.
