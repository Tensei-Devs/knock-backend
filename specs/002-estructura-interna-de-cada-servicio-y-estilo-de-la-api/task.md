# Tareas 002 — Estructura interna de cada servicio y estilo de la API

> **Estado:** conciliado con el código el 2026-11-23: T-02 a T-06 hechas, T-01 parcial. `sdd_lint --trace 002` marca sin prueba CA-05, CA-06 y CA-08 (validación 422, 400 y 409): se prueban en las specs de cada endpoint pero no llevan el caso `CA_002_NN` en el nombre; queda pendiente nombrarlas o registrarlas.

| Tarea | Qué se hace | Cubre | Estado |
|---|---|---|---|
| T-01 | Crear la plantilla de módulo y documentarla | R-01 · CA-02 | parcial: la estructura por módulos (`Modules/<Nombre>/Controllers, Services, Data, Domain, Dtos`) existe en Identidad y Mercado y sus fronteras se prueban (`ModuleBoundaryTests`, `ModuleSurfaceTests`); falta documentar la plantilla del módulo |
| T-02 | Implementar el manejo de excepciones a ProblemDetails | R-02, R-03 · CA-01 | hecha: `DomainExceptionHandler` y `ModelStateProblems` en los hosts; pruebas `DomainExceptionMappingTests` y `Spec002ApiStyleTests` (CA_002_01) |
| T-03 | Implementar la paginación por cursor común | R-05 | hecha: `Knock.Contracts/Pagination/CursorPage.cs`; pruebas `PageLimitTests` (20 por omisión, 50 máximo, última página sin cursor) |
| T-04 | Configurar `/api/v1` y la generación del OpenAPI | R-04, R-07 · CA-04 | hecha: rutas bajo `/api/v1` y contratos `docs/openapi*.json` generados con `scripts/export_openapi.py`; prueba `Spec002ApiStyleTests` (CA_002_04) |
| T-05 | Implementar las rutas de salud | R-06 · CA-03 | hecha: `HealthExtensions` (`/health/ready`) en los servicios; prueba `Spec002ApiStyleTests` (CA_002_03, 503 con MongoDB caído) |
| T-06 | Escribir las pruebas de arquitectura de capas y de módulos | R-01, R-02 · CA-02 | hecha: `Spec002ServiceStructureTests` (CA_002_02), `Spec002ErrorCodesTests` (CA_002_07), `ModuleBoundaryTests` y `MongoAccessTests` |

## Definición de terminado
- Los criterios de aceptación de `spec.md` están cubiertos por pruebas en verde.
- El CI está en verde, un par revisó el cambio y `Memoria.md` está al día.
