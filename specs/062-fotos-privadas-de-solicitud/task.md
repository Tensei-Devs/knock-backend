# Tareas 062 — Flujo privado de fotos de solicitud

> **Estado:** pendientes. En esta etapa **no se ejecutan**: se activan cuando la spec esté Aprobada (firma de José) y una persona lo indique. **Prioridad máxima de la Fase 4** (decisión de José, 2026-11-23): sin este flujo nadie puede crear una solicitud válida.

| Tarea | Qué se hace | Cubre | Depende de | Estado |
|---|---|---|---|---|
| T-01 | `PropositoImagen.Solicitud` y validador del propósito (`perfil`, `trabajo`, `solicitud`) con unitarias | R-01, R-02 · CA-05 | — | pendiente |
| T-02 | Traductor de `MediaOutcome` a los códigos de la spec 032 y `limit_exceeded` (429 con `Retry-After`), con unitarias | R-03, R-06 · CA-04 | — | pendiente |
| T-03 | `ImagenesController`: despachar `solicitud` a `MediaService.CreateUploadAsync` con `uso = solicitud` y devolver la misma forma de respuesta | R-01 · CA-01 | T-01, T-02 | pendiente |
| T-04 | Confirmación privada: dueño, vigencia, descarga, limpieza de metadatos, moderación con falla cerrada y mover a `archivos/{blobKey}/…`; respuesta sin `url` | R-04, R-05 · CA-02, CA-03, CA-06, CA-07 | T-03 | pendiente |
| T-05 | Prueba de integración del flujo completo con Azurite y MongoDB, incluida la creación de la solicitud con el `id` | CA-01, CA-02, CA-03, CA-08 | T-04 | pendiente |
| T-06 | Pruebas de seguridad negativa (tipo, tamaño, dueño, tope) y de registros sin datos personales | CA-04, CA-05, CA-09 | T-04 | pendiente |
| T-07 | OpenAPI de Mercado regenerado con `scripts/export_openapi.py`, prueba de contrato, `docs/endpoints.md`, `docs/rate-limit-matrix.md`; enmienda de las specs 032 (R-04, CA-02) y 058 (§7) | R-09 · CA-10 | T-04 | pendiente |
| T-08 | Gates de `AGENTS.md §5`, par revisor, `security-reviewer`, `Memoria.md` y `python scripts/sdd_lint.py --trace 062` | §12 | todas | pendiente |

## Definición de terminado
- Los casos CA de `spec.md` tienen prueba con su ID; `python scripts/sdd_lint.py --trace 062` en OK.
- Gates de `AGENTS.md §5` en verde, par revisor y `Memoria.md` al día.
