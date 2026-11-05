# Tareas 012 — Versionamiento por puntos

> **Estado:** en curso (KNOCK-207). T-01, T-02, T-04 y T-05 hechas; T-03 pendiente porque depende del workflow de despliegue (KNOCK-202); CA-05 queda parcial por eso.

| Tarea | Qué se hace | Cubre | Estado |
|---|---|---|---|
| T-01 | Escribir el cálculo de la versión con los ejemplos de la descripción como pruebas | R-02, R-03, R-05, R-06 · CA-01, CA-02, CA-03 | hecha (`scripts/ci/version.py`, `scripts/ci/test_version.py`) |
| T-02 | Agregar la comprobación de la etiqueta de puntos al CI | R-04 · CA-04 | hecha (job `pr-points` en `ci-pr.yml`) |
| T-03 | Integrar el cálculo, la etiqueta de Git y la imagen en el despliegue | R-07 · CA-05 | pendiente (depende de KNOCK-202); la regla de cero puntos ya está probada en `version.py` |
| T-04 | Crear el registro de versiones desplegadas | R-08 | hecha la estructura (`docs/VERSIONES_DESPLEGADAS.md`); el llenado automático va con T-03 |
| T-05 | Documentar la tabla de puntos en `CONTRIBUTING.md` | R-01, R-03 | hecha (`CONTRIBUTING.md` §9) |

## Definición de terminado
- Los criterios de aceptación de `spec.md` están cubiertos por pruebas en verde.
- El CI está en verde, un par revisó el cambio y `Memoria.md` está al día.
