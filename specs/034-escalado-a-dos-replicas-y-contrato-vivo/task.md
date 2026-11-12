# Tareas 034 — Escalado a dos réplicas y contrato vivo

> **Estado:** pendientes. En esta etapa **no se ejecutan**: se activan cuando la spec esté Aprobada y una persona lo indique.
> **Paralelo:** el frente A (T-01 a T-05) y el frente B (T-06 a T-10) no dependen entre sí. Dentro de cada frente, T-01, T-02 y T-03 van a la vez; T-06, T-07 y T-08 también. T-11 junta los dos frentes.

| Tarea | Qué se hace | Cubre | Depende de | ¿En paralelo? | Responsable sugerido | Estado |
|---|---|---|---|---|---|---|
| T-01 | Pruebas de Terraform de perfiles (`evaluacion` 2, `dev` 1, Redis 1) y de sondas; gracia de 30 s y sonda de vida en el módulo `service` | R-01, R-02, R-11 · CA-01, CA-02, CA-10 | — | Sí (con T-02, T-03 y frente B) | José Julián | pendiente |
| T-02 | Apagado ordenado en gateway e Identidad, Mensajería y Mercado: disponibilidad en 503, drenado de 25 s, cierre del canal de tiempo real; pruebas | R-03 · CA-03, CA-04 | — | Sí | Alexis | pendiente |
| T-03 | `ci-deploy.yml`: `workflow_dispatch` con `image_tag` validado contra el registro y entorno `evaluacion` con aprobación | R-07, R-08 · CA-07, CA-08 | — | Sí | José Julián | pendiente |
| T-04 | Script k6 de despliegue sin corte y consulta de reparto por instancia en `docs/OBSERVABILIDAD.md` | R-05, R-06 · CA-05, CA-06 | T-02 | Sí (con T-03) | Erick | pendiente |
| T-05 | Ensayo de reversión, humo con dos réplicas (incluye CA-01 y CA-02 de la spec 006), evidencia y pasos en `deploy/README.md` | R-04, R-09, R-10 · CA-05, CA-06, CA-09 | T-01, T-03, T-04 y spec 035 desplegada | No (cierre del frente A) | José Julián | pendiente |
| T-06 | Declarar en el contrato de cada servicio `ProblemDetails` completo, `X-Correlation-Id` y `Retry-After`; prueba sobre el contrato | R-14 · CA-15 | — | Sí (con T-07 y T-08) | Guillermo | pendiente |
| T-07 | `export_openapi.py`: Mensajería, `--publico` (unión filtrada por rutas del gateway, conflictos) y comprobación de datos internos; pruebas | R-12, R-13, R-19 · CA-14, CA-18 | — | Sí | José Julián | pendiente |
| T-08 | `tests/ContractTest`: fábricas por servicio, lector del contrato, validador de esquemas y comprobación de rutas en ambos sentidos | R-16 · CA-13 | — | Sí | Guillermo | pendiente |
| T-09 | `ci-pr.yml`: regenerar todos los contratos y `oasdiff` del público con la etiqueta `contrato-incompatible` | R-15 · CA-11, CA-12 | T-07 | Sí (con T-08) | José Julián | pendiente |
| T-10 | GitHub App solo en knock-frontend, entorno `contrato` y `contract-publish.yml` (un PR a la vez, sin PR ni bifurcaciones, acciones por SHA); primera ejecución real | R-17, R-18 · CA-16, CA-17 | T-07, T-09 | No (cierre del frente B) | José Julián | pendiente |
| T-11 | Coordinación con frontend (contrato combinado), `Memoria.md` y revisión final de evidencias | DoD | T-05, T-10 | No | José Julián | pendiente |

## Definición de terminado
- Los criterios de aceptación de `spec.md` están cubiertos por pruebas o comandos en verde, con su salida en el PR.
- El CI está en verde, un par revisó el cambio y `Memoria.md` está al día.
