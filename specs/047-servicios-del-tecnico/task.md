# Tareas 047 — Servicios y precios de cada técnico

> **Estado:** en curso; conciliado con el código el 2026-11-23 (cada fila cita su evidencia: archivo o prueba). Lo que dice «pendiente» no tiene aún evidencia verificable.
> «Depende de» = tareas que deben estar fusionadas antes; «Paralelo con» = tareas que se pueden trabajar a la vez.

| Tarea | Qué se hace | Cubre | Depende de | Paralelo con | Responsable sugerido | Estado |
|---|---|---|---|---|---|---|
| T-00 | Prerrequisitos que hoy no existen y no son parte de esta spec: Perfil del técnico y catálogo de oficios activos (spec 046, hoy no implementada) | §8 | — | T-01 | por asignar | pendiente |
| T-01 | Script de `db/` con colecciones e índices de §6 y prueba de base de datos | §6 | — | T-02 en adelante (con datos de prueba) | por asignar | parcial (KNOCK-57): `db/047-servicios-tecnico.js` con índices `tecnicoId+oficio` y de texto; prueba de BD `Spec047ServiciosScriptTests` (solo CI) |
| T-02 | `GET /api/v1/tecnicos/{id}/servicios` (SRV-01): DTO, validador, servicio, almacén, controlador y pruebas | R-01 a R-04 · CA-01 a CA-05 | T-01 | otras rutas | por asignar | hecha (KNOCK-57): `ServiciosController.List`; caché de 1 minuto con invalidación al escribir (`ServiceService`, `IServiceStore`); pruebas `Spec047ServiciosTests` y `Spec047ServiciosHttpTests` (integración solo en CI) |
| T-03 | `POST /api/v1/tecnicos/me/servicios` (SRV-02): DTO, validador, servicio, almacén, controlador y pruebas | R-05 a R-06 · CA-06 a CA-10 | T-01 | otras rutas | por asignar | hecha (KNOCK-57): `ServiciosController.Create`, límite de 50 con 409; mismas pruebas que T-02 y `Spec047ValidacionTests` (CA_047_09, 10, 13, 16, 18 y 21; las corre el CI) |
| T-04 | `PUT /api/v1/tecnicos/me/servicios/{id}` (SRV-03): DTO, validador, servicio, almacén, controlador y pruebas | R-07 a R-09 · CA-11 a CA-16 | T-01 | otras rutas | por asignar | hecha (KNOCK-57): `ServiciosController.Update`; mismas pruebas que T-02 y `Spec047ValidacionTests` (CA_047_09, 10, 13, 16, 18 y 21; las corre el CI) |
| T-05 | `DELETE /api/v1/tecnicos/me/servicios/{id}` (SRV-04): DTO, validador, servicio, almacén, controlador y pruebas | R-10 a R-12 · CA-17 a CA-21 | T-01 | otras rutas | por asignar | hecha (KNOCK-57): `ServiciosController.Delete`; mismas pruebas que T-02 y `Spec047ValidacionTests` (CA_047_09, 10, 13, 16, 18 y 21; las corre el CI) |
| T-06 | Reglas de negocio específicas de la spec (estados, plazos y límites) | R-13 a R-17 · CA-22 a CA-26 | T-01 | otras rutas | por asignar | hecha: límite de 50, oficio activo y propio, propiedad y caché cubiertos por `Spec047CacheYReglasTests`; CA-26 (cotización distinta del precio de referencia) ya se cumple porque la cotización de 049 no consulta el precio de referencia |
| T-07 | Transversales: `sub` del token, registros sin datos personales, 429 y 413, sin `Idempotency-Key` | R-18 a R-21 · CA-27 a CA-30 | T-02 a T-06 | T-08 | por asignar | hecha: `Spec047TransversalesTests` (CA_047_27 a CA_047_29; las corre el CI); el trazador de la 047 queda en OK con `Spec047ValidacionTests` |
| T-08 | OpenAPI, `docs/endpoints.md` y `docs/rate-limit-matrix.md` | §5 | T-02 a T-06 | T-07 | por asignar | pendiente |
| T-09 | Gates, `Memoria.md` y revisión de par | §12 | todas | — | por asignar | pendiente |

## Definición de terminado
- Los criterios de aceptación de `spec.md` están cubiertos por pruebas en verde.
- El CI está en verde, un par revisó el cambio y `Memoria.md` está al día.
