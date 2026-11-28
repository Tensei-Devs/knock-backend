# Tareas 021 — Comparar y elegir cotización

> **Estado:** conciliado con el código el 2026-11-23: T-01 a T-08 hechas; T-09 pendiente (revisión de par y gates).
> «Depende de» = tareas que deben estar fusionadas antes; «Paralelo con» = tareas que se pueden trabajar a la vez.

| Tarea | Qué se hace | Cubre | Depende de | Paralelo con | Responsable sugerido | Estado |
|---|---|---|---|---|---|---|
| T-01 | Script de `db/` con `visitas` (índice único `{ solicitudId }`) y campos nuevos de `solicitudes`, `cotizaciones` y `tecnicos`; prueba de base de datos | §6 · R-10, R-17 | spec 020 T-03 | T-02, T-03 | Alexis | hecha: `db/022-visitas.js` (colección `visitas`, índice único `solicitudId`); prueba `Spec021VisitsScriptTests` |
| T-02 | `ComparadorDeCotizaciones`: orden, desempates, etiquetas, `cubre`, `tiposMixtos`, `puedeElegir` y `motivo`, con unitarias y mutación | R-02 a R-08 · CA-01, CA-02, CA-11, CA-14 | — | T-01, T-03 | Guillermo | hecha: `QuoteComparer` (orden, desempates, etiquetas, `puedeElegir`); pruebas `Spec021QuoteComparerTests` y `Spec021OrdenTests` (CA_021_02, CA_021_14); la mutación no se ha medido |
| T-03 | `VisitaFactory`: código criptográfico, cifrado con llave versionada y `codigoVenceEn` | R-17 · CA-10 | — | T-01, T-02 | Guillermo | hecha: `VisitCodeFactory`; prueba `Spec021VisitCodeFactoryTests` y `Spec021SeguridadTests` (CA_021_10) |
| T-04 | `EleccionService`: transacción condicionada, rechazo de las demás, visita, eventos y reintento ante conflicto transitorio | R-09 a R-13, R-18, R-20 · CA-04 a CA-08, CA-17 | T-01, T-03, spec 020 fusionada | T-05 | Guillermo | hecha: `ChooseQuoteService` y `ChoiceStore` (transacción condicionada); pruebas `Spec021EleccionTests` (CA_021_04, 06, 07, 08, 17 y R-20) |
| T-05 | Listado: `TecnicosStore` (proyección pública) y `CotizacionesController` con validación de `orden` | R-01, R-02, R-04, R-08 · CA-01, CA-03, CA-15, CA-18 | T-02, spec 020 fusionada | T-04 | Alexis (revisa Guillermo) | hecha: `QuoteComparisonService` y `QuoteComparisonController`; pruebas `Spec021ListadoTests` (CA_021_01, 11), `Spec021OrdenTests` y `Spec021SeguridadTests` (CA_021_03, 18) |
| T-06 | `EleccionController` con respuesta de cita, contacto público y lugar | R-14 a R-16, R-21 · CA-09, CA-12 | T-04 | T-07 | Guillermo | hecha: `ChooseQuoteController`; pruebas `Spec021EleccionTests` y `Spec021SeguridadTests` (CA_021_09: el contacto se revela solo después de elegir; CA_021_12) |
| T-07 | Pruebas de integración, concurrencia (dos elecciones reales), seguridad negativa y registros | CA-01 a CA-18 (salvo CA-16, retirado) · R-19 | T-05, T-06 | T-08 | Guillermo | hecha: integración y seguridad negativa en `Spec021SeguridadTests` (dos elecciones simultáneas con `Task.WhenAll`, CA_021_05; sin datos personales en registros, CA_021_13); `sdd_lint --trace 021` sin casos faltantes |
| T-08 | OpenAPI, `docs/endpoints.md`, `docs/rate-limit-matrix.md`; semillas con `contactoPublico`, `tallerDireccion` y `nivelVerificacion` | §5 | T-05, T-06 | T-07 | Alexis | hecha: OpenAPI, `docs/endpoints.md` y `docs/rate-limit-matrix.md` en el PR #138; semillas de demo: el cuarto técnico (`demo-tecnico-04`) trae `contactoPublico`, `nivelVerificacion` y `tallerDireccion`, y el cargador de técnicos los valida |
| T-09 | Gates, `Memoria.md` y revisión de par | §12 | T-07, T-08 | — | Guillermo | pendiente: gates de la rama, `Memoria.md` y revisión de par |

## Definición de terminado
- Los criterios de aceptación de `spec.md` están cubiertos por pruebas en verde.
- El CI está en verde, un par revisó el cambio y `Memoria.md` está al día.
