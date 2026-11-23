# Tareas 063 — Lectura de la cotización final y de los ajustes pendientes

> **Estado:** pendientes. La spec está en Borrador sin firmar: no se programa hasta que una persona la firme.

| Tarea | Qué se hace | Cubre | Depende de | Estado |
|---|---|---|---|---|
| T-01 | Cálculo de vigencia al leer (`enviada` vencida, ajuste vencido) y pruebas unitarias | R-02, R-05 · CA-04, CA-05 | — | pendiente |
| T-02 | `GET /api/v1/visitas/{id}/cotizacion-final`: DTO, servicio, lectura en el almacén, controlador y pruebas | R-01 a R-03 · CA-01 a CA-05 | T-01 | pendiente |
| T-03 | `GET /api/v1/visitas/{id}/ajustes-pendientes`: DTO, servicio, lectura en el almacén, controlador y pruebas | R-04, R-05 · CA-06, CA-07 | T-01 | pendiente |
| T-04 | Validación del `id`, `Cache-Control` y seguridad negativa | R-06, R-07 · CA-08 a CA-10 | T-02, T-03 | pendiente |
| T-05 | OpenAPI regenerado, `docs/endpoints.md` y prueba de contrato | R-08 · CA-11 | T-02, T-03 | pendiente |
| T-06 | Gates de `AGENTS.md §5` y memoria | §12 | T-05 | pendiente |
