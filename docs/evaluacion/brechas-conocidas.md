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
- No se verificó en vivo el estado de los Container Apps ni las réplicas desplegadas: `deploy/apps/envs/evaluacion.tfvars` pide 2 por servicio (solo si se aplica para la evaluación del lunes, durante pocos días; hoy se usa `dev.tfvars`), pero no hay captura de `az containerapp replica list` ni de `terraform output`.
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

## G. Ramas y PR que quedan fuera del primer ciclo (pendientes, no perdidos)
Nada se descarta por no haber llegado a `sprint-05`: lo que no entre en el segundo ciclo (empieza el domingo antes de las 15:00) sigue como trabajo pendiente. Estado a 2026-11-21 por la tarde. Ninguna de estas ramas se ha borrado. Las ramas «solo local» tienen respaldo en `_respaldos
oche-2026-11-21\` (bundle) y pueden empujarse cuando se decida.

### G.1 PR abiertos (en GitHub)
| PR | Rama · SHA | Tarjeta | Estado |
|---|---|---|---|
| #239 | `feature/KNOCK-70-cancelar-solicitud-paquete` · `67660c31` | KNOCK-70 (Alexis) | Abierto, CI rojo real: formato (`CierreDeSolicitudStore.cs`, prefijo `_`) y Semgrep (`Task.Delay` en `Modules/**`). Lo corrige su autor; luego debe traer `sprint-05` y regenerar `docs/openapi.v1.json` |
| #240 | `feature/KNOCK-71-plazo-configurable-paquete` · `11e42167` | KNOCK-71 (Alexis) | Abierto, **en conflicto** con `sprint-05`; lo resuelve su autor |
| #243 | `feature/KNOCK-280-limitador-hosting` · `780f76bd` | KNOCK-280 (Erick) | Abierto, CI rojo por formato (`FINALNEWLINE` en `GatewayRateLimits.cs`, `IMPORTS` en `AuditoriaPanel.cs`); lo corrige su autor. Choque conocido con KNOCK-70 en `RateLimitingExtensions.cs` de Market |
| #229 | `feature/KNOCK-90-tecnicos-lista` · `d9e555bb` | KNOCK-90 (Erick) | Abierto; falta traer `sprint-05` (conflictos en `TechnicianProfileStore.cs`, `task.md` y archivos generados) y mover su entrada de `Memoria.md` a `docs/memoria/`. Lo hace Erick |
| #237 | `feature/KNOCK-60-notifications-indexes` · `c076c891` | KNOCK-60 (responsable sin confirmar: `task.md` dice Alexis, Jira dice Guillermo) | Abierto, sin fusionar a propósito; faltan responsable y validar nombres de campo y TTL |
| #236 | `feature/KNOCK-86-moderation-verifications` · `58b9df7b` | KNOCK-86 (José) | Abierto, sin fusionar a propósito (PII y seguridad); CI en curso tras el contrato de Identity regenerado |
| #245 | `docs/KNOCK-298-ajuste-plazo-evaluacion` · `3c0bcf47` | KNOCK-298 (José) | Abierto, solo documentación; falta su CI |

### G.2 Ramas solo locales (sin PR, con respaldo en bundle)
| Rama · SHA | Tarjeta | Estado |
|---|---|---|
| `feature/KNOCK-79-curp-hmac-cifrado` · `aea95003` | KNOCK-79 (Erick en Jira) | Terminada y verificada en local; **criptografía y PII**, pendiente de decisión del Tech Lead y de repartir las tareas de la spec 025 |
| `feature/KNOCK-79-foto-verificacion-store` · `74639bac`, `…-verificacion-confirmar-enviar` · `f0a6d820`, `…-verificacion-docs-contrato` · `11f8a182`, `…-verificacion-azure-AUTORIZACION` · `d12a627b` | KNOCK-79 | Apiladas; sin verificar de punta a punta; la última toca el cliente de Azure y Key Vault |
| `feature/KNOCK-87-vencimiento-fotos-verificacion` · `fea68c0a`, `feature/KNOCK-87-verification-photo-purge` · `056c34c6` | KNOCK-87 (Guillermo en Jira) | Incompletas; dependen de la spec 025 y de la 054 |
| `feature/KNOCK-86-photo-links-adapter-codemap` · `464c1329` | KNOCK-86 | Apilada sobre la del PR #236 y sobre KNOCK-79; verificada en local (codemap 0 violaciones), sin PR |
| `feature/KNOCK-172-almacen-blob` · `34265bed` → `…-limites-redis` · `a4c0e19e` → `…-subidas-endpoints` · `42d5cdbd` → `…-openapi-AUTORIZACION` · `26ea0d98` | KNOCK-172 (José) | Verificadas en local (unitarias, arquitectura y contrato); **seguridad y criptografía (SAS, HMAC)**; la última regenera OpenAPI (archivo protegido) |
| `feature/KNOCK-171-moderacion-contenido` · `c4d9b93e`, `…-content-safety-infra-AUTORIZACION` · `dc7dd600` | KNOCK-171 (José) | **No verificadas**; la segunda toca Terraform y gasto de Azure |
| `feature/KNOCK-63-cliente-ia-azure-openai` · `01a6de4d`, `feature/KNOCK-99-asistente-ia` · `6b789df5` | KNOCK-63 (Alexis en Jira) y KNOCK-99 (Erick) | **No verificadas**; el tope de tokens y el ADR-008 los decide el Tech Lead |
| `feature/KNOCK-185-archivos-store` · `ddc2c9ca` | KNOCK-185 (Alexis) | **No verificada, no revisar todavía** (spec 058 T-02); hay trabajo sin commitear respaldado en `wip2\` |
| `feature/KNOCK-91-confianza-script-prueba` · `460a9507` | KNOCK-91 (Guillermo) | Prueba de BD de la spec 042; sin PR; responsable por confirmar |
| `feature/KNOCK-95-bloqueo-eventos` · `64b5482d` | KNOCK-95 (Alexis) | Puerto de eventos de bloqueo; sin PR |
| `feature/KNOCK-57-servicios-tecnico` · `af745950` | KNOCK-57 (Erick) | Spec 047, 5 rutas; falta asignar tareas en `task.md`; sin PR |
| `chore/KNOCK-280-retirar-exc-025-AUTORIZACION` · `50f43978` | KNOCK-280 (Erick) | Archivo protegido (`security/exceptions.yaml`); autorizar junto con #243 |
| `feature/SIN-TARJETA-cotizacion-final-ajustes` · `ee0a7240` | sin tarjeta (spec 049) | **Incompleta, no compilada ni probada**; sin pruebas, sin script `db/` ni documentación |
| `feature/KNOCK-281-catalogo-hosting` (base `cd5c452d`), `feature/KNOCK-105-in-app-notices` (base `7bd8046c`) | KNOCK-281 (sin responsable), KNOCK-105 (Guillermo) | Solo trabajo sin commitear, respaldado como parches en `wip2\`; ninguna pieza completa |
| `chore/SIN-TARJETA-cdn-front-door-AUTORIZACION` · `53ae082f`, `chore/SIN-TARJETA-sonarcloud-AUTORIZACION` · `8ae5a2c4` | KNOCK-296, KNOCK-297 | Versiones anteriores de lo ya fusionado en #231 y #232; se conservan hasta confirmar que ya no hacen falta |

### G.3 Frontend (KNOCK-288, conexión de la maqueta a la API real)
Nueve ramas locales sin PR: `chore/KNOCK-288-contrato-api-c87ab21` (base), las de repositorios (direcciones, casa y auto, técnicos de confianza, mis datos, zona, búsqueda, trabajo), `fix/KNOCK-288-rutas-protegidas-por-omision` (**hallazgo de seguridad: lista de rutas públicas, la revisa el Tech Lead antes de abrir PR**) y `docs/KNOCK-288-task-states-app`. Verificadas en un árbol combinado (formato, análisis y 1 026 pruebas). Respaldo en `_respaldos
oche-2026-11-21ront-*.bundle`.

### Detalle de la moderación del panel (spec 053, KNOCK-86)
Las rutas `/api/v1/admin/verificaciones` y `/api/v1/admin/reportes` están en el PR #236 (abierto, sin fusionar). Exigen el permiso `verification:review` (roles `administrador` y `verificador`) o `reports:resolve` (roles `administrador` y `moderador`), con la sesión del panel. Tres límites que hay que declarar:
1. **Las fotos de verificación no se entregan todavía.** Abrir el detalle de una verificación depende de `IVerificationPhotoLinks`; en esta rama queda `UnavailableVerificationPhotoLinks` (falla cerrado) hasta que llegue el adaptador real de la pila de KNOCK-79 y KNOCK-86.
2. **La `descripcion` de un reporte es texto libre y no se enmascara.** La cola muestra motivo y texto escritos por el usuario; podrían incluir datos personales que el usuario haya puesto ahí. No se devuelven teléfono ni dirección.
3. **El límite de peticiones de las rutas `/admin` lo aplica el gateway** (contador por el `sub` del panel). Identity no tiene una política de límite propia para esas rutas; depende de que el tráfico pase por el gateway.

