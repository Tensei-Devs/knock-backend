# Tareas 062 — Flujo privado de fotos de solicitud

> **Estado:** conciliado con el código de `sprint-05` el 2026-11-23 (PR #294 y #332 para 060; #287 para 062); cada tarea cita su evidencia y la que no tiene prueba con ID queda parcial o pendiente. **Prioridad máxima de la Fase 4** (decisión de José, 2026-11-23): sin este flujo nadie puede crear una solicitud válida.

| Tarea | Qué se hace | Cubre | Depende de | Estado |
|---|---|---|---|---|
| T-01 | `PropositoImagen.Solicitud` y validador del propósito (`perfil`, `trabajo`, `solicitud`) con unitarias | R-01, R-02 · CA-05 | — | hecha (KNOCK-185): `PedirSubidaImagenRequestValidator` y `PropositoImagen` aceptan `perfil`, `trabajo` y `solicitud`; `Spec062FotosPrivadasTests` (CA_062_05) |
| T-02 | Traductor de `MediaOutcome` a los códigos de la spec 032 y `limit_exceeded` (429 con `Retry-After`), con unitarias | R-03, R-06 · CA-04 | — | hecha (KNOCK-185): traductor de `MediaOutcome` con `limit_exceeded` (429 y `Retry-After`); `Spec062FotosPrivadasTests` y `Spec062FotosPrivadasHttpTests` (CA_062_04) |
| T-03 | `ImagenesController`: despachar `solicitud` a `MediaService.CreateUploadAsync` con `uso = solicitud` y devolver la misma forma de respuesta | R-01 · CA-01 | T-01, T-02 | hecha (KNOCK-185): `ImagenesController` despacha `solicitud` a `MediaService` y devuelve la misma forma, `id` de 32 hexadecimales; `Spec062FotosPrivadasTests` y `Spec062FotosPrivadasHttpTests` (CA_062_01) |
| T-04 | Confirmación privada: dueño, vigencia, descarga, limpieza de metadatos, moderación con falla cerrada y mover a `archivos/{blobKey}/…`; respuesta sin `url` | R-04, R-05 · CA-02, CA-03, CA-06, CA-07 | T-03 | parcial (KNOCK-185): `MediaService.ConfirmAsync` y la confirmación privada en `ImagenesController` (respuesta sin `url`, cuarentena a `archivos`); prueba con ID solo de CA-06 (`Confirmar_Sin_Haber_Subido_...CA_062_06`); faltan pruebas con ID de CA-02, CA-03 y CA-07 (`sdd_lint --trace 062` las marca FALTA) |
| T-05 | Prueba de integración del flujo completo con Azurite y MongoDB, incluida la creación de la solicitud con el `id` | CA-01, CA-02, CA-03, CA-08 | T-04 | parcial (KNOCK-185): `Spec062FotosPrivadasHttpTests` cubre CA-01, CA-04 y CA-06 (escrita, la corre el CI); falta el flujo completo con Azurite y MongoDB (CA-02, CA-03, CA-08) |
| T-06 | Pruebas de seguridad negativa (tipo, tamaño, dueño, tope) y de registros sin datos personales | CA-04, CA-05, CA-09 | T-04 | parcial (KNOCK-185): validación de tipo y tamaño y tope de subidas pendientes probados (CA_062_04, CA_062_05); falta la prueba de dueño y de registros sin datos personales (CA-09) |
| T-07 | OpenAPI de Mercado regenerado con `scripts/export_openapi.py`, prueba de contrato, `docs/endpoints.md`, `docs/rate-limit-matrix.md`; enmienda de las specs 032 (R-04, CA-02) y 058 (§7) | R-09 · CA-10 | T-04 | parcial (KNOCK-185): `docs/openapi.v1.json` ya trae `/imagenes/subidas` y `docs/endpoints.md` documenta el flujo; falta la prueba de contrato de CA-10 y la enmienda de las specs 032 (R-04, CA-02) y 058 (§7) |
| T-08 | Gates de `AGENTS.md §5`, par revisor, `security-reviewer`, `Memoria.md` y `python scripts/sdd_lint.py --trace 062` | §12 | todas | pendiente: `python scripts/sdd_lint.py --trace 062` marca FALTA en CA-02, 03, 07, 08, 09 y 10; par revisor y `security-reviewer` sin hacer |

## Definición de terminado
- Los casos CA de `spec.md` tienen prueba con su ID; `python scripts/sdd_lint.py --trace 062` en OK.
- Gates de `AGENTS.md §5` en verde, par revisor y `Memoria.md` al día.
