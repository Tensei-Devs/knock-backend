# Spec 061 — Ocultar una opinión desde el panel

> **Estado:** Borrador (agente) — **sin firmar: la prueba y la firma son de José** · **Repositorio:** knock-backend · **Rector:** `constitution.md`
> **Jira:** por crear (KNOCK-266 paraguas) · **Responsable:** por asignar
> **Fuentes:** spec 051 (R-14, R-16 y R-17: «una opinión reportada se atiende por el panel y puede ocultarse sin borrarse»), spec 053 (panel de revisión, `reviews:moderate`), spec 045 (rutas `/admin` de Mercado con `PanelBearer`), spec 033 (panel, doble factor), spec 054 (bandeja de salida), decisión de José del 2026-11-23 («ocultar opinión (051): sí, spec y tarea nuevas en Fase 4, después de 053»).
> **Servicios:** Mercado (`Knock.Market.Api`, módulo `Reviews`) e Identidad (auditoría del panel) · **Colecciones:** `opiniones` (campo `oculta`, ya existe), `tecnicos` (reputación), `panel_auditoria`
> **Skills:** `security-core`, `events-outbox`, `vertical-slice-api`

## 1. Objetivo
Que una persona del panel con permiso pueda **ocultar** (y volver a mostrar) una opinión reportada sin borrarla, con la reputación del técnico recalculada, la caché de lectura invalidada y la decisión auditada. Hoy la spec 051 dice que una opinión «puede ocultarse sin borrarse» pero no hay ruta ni regla que lo haga: el campo `oculta` existe y las lecturas ya lo respetan (`oculta != true`).

## 2. Historia y alcance
**2.1 Historia.** Como moderadora del panel, quiero ocultar una opinión que viola las reglas, para que deje de verse y de contar en la reputación sin perder la evidencia.

**2.2 Dentro**
- `POST /api/v1/admin/opiniones/{id}/ocultar` y `POST /api/v1/admin/opiniones/{id}/mostrar` en Mercado (esquema `PanelBearer`, permiso `reviews:moderate`, doble factor).
- Recalcular la reputación del técnico (promedio y distribución sin las ocultas) y sacar la opinión de la caché de 1 minuto.
- Auditoría en `panel_auditoria` (Identidad) por evento de la bandeja.

**2.3 Fuera**
- Borrar opiniones, editar su texto, sancionar al autor (specs 043, 053 y 059).
- El reporte de una opinión por una persona (spec 043: `referencia.tipo = "opinion"`): esta spec solo define el efecto de ocultar.

## 3. Requisitos
- **R-01:** `POST /api/v1/admin/opiniones/{id}/ocultar` con `{ motivo }` (8 a 300 caracteres, igual que el motivo de ajuste de la spec 049) pone `oculta = true`, guarda `ocultaPor` (id de la persona del panel), `ocultaEn` y `ocultaMotivo`, y responde 200. Repetirla sobre una ya oculta responde 200 sin cambios (idempotente).
- **R-02:** `POST /api/v1/admin/opiniones/{id}/mostrar` pone `oculta = false` y borra `ocultaPor`, `ocultaEn` y `ocultaMotivo`; sobre una visible responde 200 sin cambios.
- **R-03:** Ambas exigen el token del panel con `reviews:moderate` y doble factor (spec 033). Sin token, 401; sin el permiso, 403; una opinión inexistente, 404 `review_not_found`; un `id` mal formado, 400; un cuerpo inválido, 422 con `errors.motivo` sin repetirlo.
- **R-04:** Al ocultar o mostrar, en la **misma transacción** se recalcula la reputación del técnico con las opiniones no ocultas (spec 051 R-14) y se escribe `OpinionModerada` (`{ eventoId, opinionId, tecnicoId, accion, porId }`, sin texto) en la bandeja de salida de Mercado.
- **R-05:** La caché de lectura del técnico (`GET /api/v1/tecnicos/{id}/opiniones`, 1 minuto) se invalida por la generación del técnico (spec 051 R-16): el cambio se ve en la siguiente lectura.
- **R-06:** Identidad consume `OpinionModerada` y escribe `opinion_ocultada` u `opinion_mostrada` en `panel_auditoria` (con la persona del panel y la opinión; nunca el texto de la opinión), de forma idempotente por `eventoId`.
- **R-07:** Una opinión oculta **no se borra**: su texto, fotos y calificación se conservan para la apelación y la evidencia, y no aparecen en ninguna respuesta pública.
- **R-07a:** **Aviso al técnico (DECIDIDO, José, 2026-11-23 noche).** Al ocultar una opinión suya, el técnico recibe un aviso de **texto fijo**, sin el motivo ni datos de la opinión (misma regla que `ReporteDecidido`; amplía la spec 037 con el tipo `opinion_oculta`, rol `tecnico`, categoría `sistema`). Mostrar de nuevo no avisa. El autor de la opinión no recibe aviso.
- **R-08:** Límite de 30 peticiones por minuto por persona del panel (política `admin-opiniones`).

## 4. Criterios de aceptación
- **CA-01:** Dada una opinión visible y una persona del panel con `reviews:moderate` y doble factor, cuando la oculta con un motivo válido, entonces responde 200, `oculta = true` y la opinión deja de aparecer en `GET /api/v1/tecnicos/{id}/opiniones`.
- **CA-02:** Dada la misma operación, cuando se lee la reputación del técnico, entonces el promedio y la distribución ya no cuentan esa opinión.
- **CA-03:** Dada una opinión oculta, cuando se repite `ocultar`, entonces 200 sin cambios ni evento nuevo.
- **CA-04:** Dada una opinión oculta, cuando se llama `mostrar`, entonces vuelve a aparecer y a contar; sobre una visible, 200 sin cambios.
- **CA-05:** Dada una petición sin token, con token de otro esquema o sin `reviews:moderate`, cuando llama las rutas, entonces 401 o 403 y no cambia nada.
- **CA-06:** Dado un motivo de 7 o de 301 caracteres, cuando se oculta, entonces 422 con `errors.motivo` sin repetir el valor.
- **CA-07:** Dada una opinión inexistente, cuando se oculta, entonces 404 `review_not_found`.
- **CA-08:** Dadas dos peticiones simultáneas de ocultar la misma opinión, cuando terminan, entonces ambas responden 200 y hay un solo evento `OpinionModerada`.
- **CA-09:** Dado un evento `OpinionModerada` entregado dos veces a Identidad, cuando se consume, entonces hay un solo registro de auditoría y ninguno lleva el texto de la opinión.
- **CA-09a:** Dada una opinión que se oculta, cuando el evento llega a Mensajería, entonces el técnico recibe un aviso `opinion_oculta` de texto fijo que no contiene el motivo, la calificación ni el texto de la opinión; al mostrarla de nuevo no hay aviso.
- **CA-10:** Dada una opinión oculta, cuando se consulta en la base, entonces su texto, fotos y calificación siguen guardados.

**4.1 Criterio → prueba prevista**

| Criterio | Prueba prevista |
|---|---|
| CA-01 a CA-04, CA-07, CA-08, CA-10 | `IntegrationTest/Spec061OcultarOpinionTests` |
| CA-05, CA-06 | `SecurityTest/Spec061OcultarOpinionSeguridadTests` |
| CA-09 | `IntegrationTest/Spec061AuditoriaOpinionTests` |
| CA-09a | `IntegrationTest/Spec061AvisoTecnicoTests` |

## 5. Contrato
| Ruta | Quién | Entrada | Éxito | Errores |
|---|---|---|---|---|
| `POST /api/v1/admin/opiniones/{id}/ocultar` | panel (`reviews:moderate`, doble factor) | `{ motivo }` | `200` sin cuerpo | `400`, `401`, `403`, `404 review_not_found`, `422`, `429` |
| `POST /api/v1/admin/opiniones/{id}/mostrar` | igual | — | `200` sin cuerpo | `400`, `401`, `403`, `404 review_not_found`, `429` |

Las dos rutas van en el gateway dentro de `/api/v1/admin/**` (spec 045). Evento nuevo: `OpinionModerada` (bandeja de Mercado → Identidad por `POST /api/v1/interno/eventos`, permiso `opinion-moderada`, y → Mensajería por `POST /api/v1/interno/avisos` con el tipo `opinion_oculta` para el técnico, R-07a).

## 6. Modelo de datos
`opiniones` gana `ocultaPor`, `ocultaEn` y `ocultaMotivo` (opcionales). `oculta` ya existe. `panel_auditoria` gana los tipos `opinion_ocultada` y `opinion_mostrada`. Sin índices nuevos.

## 7. Arquitectura y componentes
`AdminReviewsController` y `ReviewModerationService` en el módulo `Reviews` de Mercado (el módulo ya calcula la reputación y la generación de caché); `ReviewStore.SetHiddenAsync` con relectura y `KeyedGate` por opinión. Un consumidor `OpinionModeradaConsumer` en el módulo `Panel` de Identidad. Sin dependencias nuevas.

## 8. Dependencias
Specs 051 (reputación, caché y campo `oculta`), 053 (panel y `reviews:moderate`), 045 (`PanelBearer` en Mercado), 054 (bandeja) y 033 (doble factor).

## 9. Amenazas y controles
| Amenaza | Control | Caso |
|---|---|---|
| Una persona sin permiso oculta opiniones | Esquema `PanelBearer`, permiso y doble factor | CA-05 |
| Ocultar para manipular la reputación sin dejar rastro | Auditoría por evento con quién, cuándo y motivo | CA-09 |
| Perder la evidencia al ocultar | No se borra nada | CA-10 |
| Doble ejecución por reintentos | Idempotencia y turnos por opinión | CA-03, CA-08 |
| Filtrar el texto de la opinión en eventos o auditoría | El evento y la auditoría no llevan texto | CA-09 |

## 10. Plan de pruebas
Unitarias (validador del motivo y regla de idempotencia), integración con MongoDB real (reputación y caché), seguridad negativa (token, permiso, doble factor), arquitectura, contrato (OpenAPI de Mercado) y registros sin texto de la opinión.

## 11. Escenarios
**Positivo:** una moderadora oculta una opinión reportada por lenguaje ofensivo: desaparece de la lista, el promedio del técnico se recalcula y queda auditado. **Negativo:** una persona del panel sin `reviews:moderate` intenta ocultarla (403). **Error esperado:** motivo de 3 caracteres (422). **Límite:** dos moderadoras ocultan la misma opinión a la vez (ambas 200, un solo evento).

## 12. Definición de terminado
CA-01 a CA-10 con prueba; `python scripts/sdd_lint.py --trace 061` en OK; OpenAPI de Mercado regenerado; `docs/endpoints.md`, `docs/rate-limit-matrix.md` y `docs/events.md` al día; gates de `AGENTS.md §5` en verde; `Memoria.md` con la entrada.

## 13. Decisiones
- **D-04 (DECIDIDO, José, 2026-11-23 noche):** sí se avisa al técnico al ocultar una opinión suya, con aviso de texto fijo y sin motivo ni datos de la opinión (R-07a).
- **D-01:** ocultar no borra (spec 051 R-17). **D-02:** el permiso es `reviews:moderate`, que ya existe en el panel. **D-03:** decisión de José del 2026-11-23: spec y tarea nuevas, después de la 053.

## 14. Decisiones tomadas sin consulta
1. **El motivo de 8 a 300 caracteres** (como la spec 049). Alternativa: sin motivo obligatorio.
2. **Sin aviso al autor** de la opinión (el aviso al técnico está decidido, D-04).
3. **La auditoría va por evento a Identidad** y no por una colección de Mercado, porque `panel_auditoria` vive en Identidad. Alternativa: auditar en Mercado.

## 15. Riesgos y dudas abiertas
- La duda del aviso al técnico quedó **decidida** (D-04).
- **Duda abierta (alcance):** el reporte de una opinión por una persona (spec 043, `referencia.tipo = "opinion"`) y su cola en el panel no están definidos en esta spec.

## Aprobación
- [ ] Revisor 1 — nombre: ______ fecha: ______
- [ ] Sin conflicto con la constitución
- **Estados:** Borrador (agente) → Aprobada (persona; habilita el código) → Implementada (tras el merge)
