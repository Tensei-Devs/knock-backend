# Brechas conocidas del backend

Lista honesta de lo que **no** está o **no actúa** hoy, para que nadie (persona o IA) lo infiera de las specs como si existiera. **Una spec aprobada no es un control implementado.** Fecha de la lista: 2026-11-21. Las cifras salen de `generado/cifras.md`.

## A. Controles que existen en el repositorio pero no actúan hoy
| Brecha | Estado real | Dónde se sigue |
|---|---|---|
| DAST (ZAP baseline) | El job nunca corre: faltan `tests/smoke/smoke.sh` y `deploy/docker-compose.ci.yml` (BUG-001). ZAP baseline se corrió a mano (KNOCK-78). | BUG-001 |
| Revisión con IA (`ai-review`) | Apagada: la variable `AI_REVIEW` no existe. No es un control activo. | — |
| Reglas de rama y commits (`pr-meta`) | Apagadas: la variable `ENFORCE_BRANCH_RULES` no existe. | — |
| Mutación con Stryker | Informativa y rota con la herramienta 5.1.0. | KNOCK-282 |
| Revisiones obligatorias de PR | Deshabilitadas por decisión del Tech Lead; el control es la revisión del dueño más el CI (`gate`). | `docs/BRANCH_PROTECTION.md` |

## B. Controles que faltan
- **Aprobación humana para producción:** `ci-deploy` despliega solo tras un `ci-pr` verde en `main`; no usa `environment:` con revisores. Sin SBOM ni firma de imágenes.
- **SonarCloud:** el análisis está preparado en una rama aparte (KNOCK-297) y no actúa hasta estar en `main`; hasta entonces el quality gate figura como `NONE`.
- **Dependabot:** la configuración del backend es antigua (agrupa paquetes de EF y Npgsql, que el proyecto no usa, y apunta Docker a una ruta que ya no existe).
- **Versionamiento por etiquetas:** `docs/VERSIONES_DESPLEGADAS.md` no tiene filas ni hay etiquetas `<componente>-vX.Y.Z` publicadas, aunque la spec 012 las promete.

## C. Funciones que las specs describen y no tienen código completo
| Función | Estado |
|---|---|
| Mensajería (push, bandeja, chat) | Solo `/health`; sin controladores. Specs 030 y 037 aprobadas. |
| Asistente de IA (`Knock.Assistant.Api`) | No existe el servicio; el cliente de Azure OpenAI está en una rama sin verificar. |
| Moderación de contenido (Azure Content Safety) | En una rama sin verificar; no está en `sprint-05`. |
| Envío por Firebase Cloud Messaging | Sin código de envío. No debe listarse como servicio externo activo. |
| Panel de revisión, imágenes públicas con SAS, verificación con fotos privadas | Parte en ramas locales o PR pendientes; no están completos en `sprint-05`. |
| CDN | El módulo de Terraform de Front Door está preparado (KNOCK-296) y **sin aplicar**; hoy las imágenes públicas salen del Blob con `Cache-Control` largo (ADR-007, opción D). El punto 15c sigue parcial. |

## D. Evidencia que falta o que hay que verificar en vivo
- No se verificó en vivo el estado de los Container Apps ni las réplicas desplegadas: `deploy/apps/envs/evaluacion.tfvars` pide 2 por servicio, pero no hay captura de `az containerapp replica list` ni de `terraform output`.
- Los números de pruebas de una corrida real salen de `ci-pr`; el recuento de `generado/cifras.md` es **estático** y no sustituye a esa corrida.
- Endpoints sin cita en alguna suite de pruebas (por ruta literal): ver `docs/endpoints.md` y la tabla de la autoevaluación; la heurística puede fallar si la ruta se arma por partes.
- Un porcentaje de «seguridad según DevSecOps» o una comparación con banca o fintech **no se responde**: el repositorio no define un criterio verificable (la alternativa es la lista de controles de `docs/QA_DEVSECOPS.md`).

## E. Conteos que hay que fijar
- **Agentes y skills:** `.agents/` tiene 7 agentes y 17 skills (canónica); `.opencode/` tiene 11 y 25. `docs/EVALUACION.md` aún muestra cifras anteriores y debe regenerarse desde `generado/cifras.md`.
