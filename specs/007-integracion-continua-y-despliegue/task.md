# Tareas 007 — Integración continua y despliegue

> **Estado:** implementada: los workflows `ci-pr.yml`, `ci-deploy.yml`, `_governance.yml` y `dependabot.yml` están activos y se ejecutan en cada PR (PR #26, #65, #74 y #96).

| Tarea | Qué se hace | Cubre | Estado |
|---|---|---|---|
| T-01 | Crear `.github/workflows/ci-pr.yml` | R-02 · CA-02 | hecha (workflows activos; PR #26, #65, #74, #96) |
| T-02 | Agregar la detección de servicio cambiado | R-01 · CA-01 | hecha (workflows activos; PR #26, #65, #74, #96) |
| T-03 | Agregar los jobs de secretos y dependencias | R-03, R-04 | hecha (workflows activos; PR #26, #65, #74, #96) |
| T-04 | Crear el workflow de construcción y despliegue | R-05, R-06 · CA-03 | hecha (workflows activos; PR #26, #65, #74, #96) |
| T-05 | Registrar los checks obligatorios | R-02 | hecha (workflows activos; PR #26, #65, #74, #96) |
| T-06 | Agregar al workflow Semgrep, Gitleaks, Trivy y Dependabot, y una prueba negativa con un secreto de ejemplo | R-07 · CA-04 | hecha (workflows activos; PR #26, #65, #74, #96) |

## Evidencia de CI y despliegue (KNOCK-272)

| Criterio | Ejecución verificada | Resultado |
|---|---|---|
| CA-01 | [CI del PR, corrida 45995527280](https://github.com/Tensei-Devs/knock-backend/actions/runs/45995527280) | La matriz de imágenes ejecutó el build de Mercado y omitió los builds de Identidad, Mensajería y Gateway. El build .NET del job de pruebas sigue compilando `Knock.slnx` completo. |
| CA-02 | [Prueba fallida](https://github.com/Tensei-Devs/knock-backend/actions/runs/45057817060/job/137316914646) y [gate fallido](https://github.com/Tensei-Devs/knock-backend/actions/runs/45057817060/job/137322463186) | En la integración, `Dos_Creaciones_Simultaneas_Dejan_Un_Solo_Documento_CA_030_15` esperaba 7 y recibió 6. El gate bloqueante falló en esa misma corrida. |
| CA-03 | [Publicación y despliegue](https://github.com/Tensei-Devs/knock-backend/actions/runs/44951355609) y [humo en verde](https://github.com/Tensei-Devs/knock-backend/actions/runs/44951355609/job/136990833018) | El 21 de noviembre de 2026 se publicaron las cuatro imágenes con la etiqueta `5e7b65c46f05117889bf07ed23f9bf25fddd8da2` (SHA completo del commit en `main`). El humo comprobó revisiones sanas de los cuatro componentes y las respuestas de `/health` y `/health/ready` por el gateway. |

CA-01 queda comprobado para las imágenes. Si el criterio abarca también la compilación .NET, falta acotarla al servicio afectado. CA-03 registra una corrida histórica en `main`. La comparación con la imagen activa tras varias fusiones seguidas queda en el seguimiento separado indicado en Jira.

## Definición de terminado
- Los criterios de aceptación de `spec.md` están cubiertos por pruebas en verde.
- El CI está en verde, un par revisó el cambio y `Memoria.md` está al día.
