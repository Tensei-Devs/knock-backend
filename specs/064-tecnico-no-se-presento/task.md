# Tareas 064 — El técnico no se presentó

> **Estado:** pendientes. La spec está en Borrador sin firmar: no se programa hasta que una persona la firme y decida D-01.

| Tarea | Qué se hace | Cubre | Depende de | Estado |
|---|---|---|---|---|
| T-01 | Regla de ventana y estado en el dominio, con pruebas unitarias | R-02 · CA-03 | — | pendiente |
| T-02 | Almacén: actualización condicionada y evento `TecnicoNoSePresento` en la misma transacción | R-03, R-05 · CA-01, CA-02, CA-05, CA-07 | T-01 | pendiente |
| T-03 | Servicio y ruta `POST /api/v1/visitas/{id}/tecnico-ausente` | R-01, R-04, R-06 · CA-01, CA-04, CA-06 | T-02 | pendiente |
| T-04 | Límite `visitas-cambios` y aviso de texto fijo al técnico | R-07, R-08 · CA-08 | T-03 | pendiente |
| T-05 | OpenAPI, `docs/endpoints.md`, `docs/events.md`, matriz de límites y prueba de contrato | R-09 · CA-09 | T-03 | pendiente |
| T-06 | Gates de `AGENTS.md §5` y memoria | §12 | T-05 | pendiente |
