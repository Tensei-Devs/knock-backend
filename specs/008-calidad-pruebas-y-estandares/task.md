# Tareas 008 — Calidad, pruebas y estándares

> **Estado:** T-01, T-02 y T-06 hechas (conciliado el 2026-11-23, con evidencia por fila); T-03, T-04 y T-05 siguen pendientes de verificar.

| Tarea | Qué se hace | Cubre | Estado |
|---|---|---|---|
| T-01 | Crear `.editorconfig` y reglas de analizadores | R-01, R-02 · CA-01 | hecha: `.editorconfig` en la raíz |
| T-02 | Configurar warnings como errores | R-01 | hecha: `TreatWarningsAsErrors` en `Directory.Build.props` |
| T-03 | Definir el esquema de nombres de pruebas con el ID del criterio | R-04 | pendiente |
| T-04 | Configurar la cobertura y su umbral | R-05 · CA-02 | pendiente |
| T-05 | Documentar la definición de terminado y el registro de dependencias | R-03, R-06, R-07 · CA-03 | pendiente |
| T-06 | Agregar el script de trazabilidad criterio ↔ prueba y correrlo en el CI | R-08 · CA-04 | hecha: `scripts/sdd_lint.py --trace` corre en `.github/workflows/_governance.yml` |

## Definición de terminado
- Los criterios de aceptación de `spec.md` están cubiertos por pruebas en verde.
- El CI está en verde, un par revisó el cambio y `Memoria.md` está al día.
