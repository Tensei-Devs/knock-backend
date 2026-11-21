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

## F. Trabajo empezado por agentes y NO terminado (no está en `sprint-05` ni en ningún PR)
Se declara aquí para que no se infiera de las specs como si existiera. Todo está solo en ramas locales sin verificar.
| Qué | Estado | Por qué no se terminó |
|---|---|---|
| Spec 049 (cotización final y ajuste de precio: COT-06, COT-07, AJU-01, AJU-02) | Un commit local de trabajo en curso (12 archivos); **no se compiló ni se probó**; sin pruebas `CA_049_*`, sin script `db/` de `ajustes_precio`, sin documentación ni OpenAPI. No tiene tarjeta de Jira. | La cola de compilación se detuvo por poca memoria y no alcanzó el tiempo |
| Spec 050 (visita, agenda y cierre) | No se inició | Sin tiempo; tarjetas HU-034, HU-039, HU-040, HU-041 y HU-023 en «Por hacer» |
| Spec 058 T-02 a T-09 (almacén de archivos de Media en MongoDB) | Store parcial sin verificar y trabajo sin commitear | El agente se cortó por límite de uso |
| Spec 055 etapa 3 (KNOCK-281: logging, observabilidad, Redis/Health, Mongo, seguridad HTTP a `Knock.Hosting`) | Solo 47 archivos sin commitear; ninguna pieza completa | Sin responsable en Jira y sin tiempo |
| Spec 039: exportación de datos, eliminación de cuenta al vencer la gracia, cola del panel de recuperación, ruta interna de Mercado y aviso de sesión revocada | Los endpoints existen; los procesos de fondo no | Dependen del ejecutor y la bandeja de la spec 054 (no existe) |
| Spec 054 (bandeja de salida y tareas programadas) | No iniciada | Prerrequisito de 037, 039, 044 y 057 |

### Detalle de la spec 039 (cuenta y datos personales)
Los 9 endpoints (USR-03 a USR-11) existen y tienen pruebas; quedan **cinco tareas parciales** que dependen del ejecutor y la bandeja de la spec 054 (que no existe) y de infraestructura:
1. `PrepararExportacion`: el ZIP, el contenedor `exportaciones` y el aviso con enlace.
2. `EliminarCuenta`: el borrado real cuando vence el plazo de gracia.
3. La cola del panel (spec 053) para la recuperación de acceso.
4. La ruta interna de Mercado: hoy `UnavailableMarketAccountClient` responde 503.
5. El aviso `sesion_revocada`: solo se registra en `PendingAccountOutbox`.

**Diferencia entre la historia y la spec (HU-006, KNOCK-104):** la historia habla de un «código SMS» para eliminar la cuenta; la spec 039 lo sustituyó por **reautenticación con Firebase**. Lo que vale es la spec; el texto de la historia debe corregirse.
