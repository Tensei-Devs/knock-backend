# Tareas 061 — Ocultar una opinión desde el panel

> **Estado:** pendientes. En esta etapa **no se ejecutan**: se activan cuando la spec esté Aprobada (firma de José) y una persona lo indique.

| Tarea | Qué se hace | Cubre | Depende de | Estado |
|---|---|---|---|---|
| T-01 | Validador del motivo (8 a 300) y regla de idempotencia de ocultar y mostrar, con unitarias | R-01, R-02 · CA-03, CA-04, CA-06 | — | hecho (KNOCK-93): `HideReviewBodyValidator` y `ReviewRules.IsValidModerationReason`; unitarias `Spec061OcultarOpinionTests` en verde en local |
| T-02 | `ReviewStore.SetHiddenAsync` con relectura y `KeyedGate`, recálculo de reputación y generación de caché en una transacción | R-01, R-04, R-05 · CA-01, CA-02, CA-08, CA-10 | T-01 | hecho (KNOCK-93): `ReviewStore.SetHiddenAsync` (`KeyedGate`, relectura, reputación y `OpinionModerada` en una transacción) y generación de caché por técnico; integración `Spec061OcultarOpinionTests` escrita, la corre el CI |
| T-03 | `AdminReviewsController` (`ocultar` y `mostrar`) con `PanelBearer`, `reviews:moderate` y límite `admin-opiniones` | R-03, R-08 · CA-05, CA-07 | T-02 | hecho (KNOCK-93): `AdminReviewsController`, política `panel-reviews-moderate` y límite `admin-opiniones` (por IP, ver Memoria); integración escrita, la corre el CI. La puerta de permisos de Mercado sigue siendo `DenyAll` (hueco heredado de la 045) |
| T-04 | Evento `OpinionModerada` en la bandeja de Mercado y su entrega a `POST /api/v1/interno/eventos` (permiso `opinion-moderada`) | R-04 | T-02 | hecho (KNOCK-93): `EntregaOpinionModerada` registrada en la bandeja; entrega a Identidad `POST /api/v1/interno/opiniones-moderadas` (permiso `opinion-moderada`); unitarias en verde en local |
| T-05 | Consumidor en Identidad: `opinion_ocultada` y `opinion_mostrada` en `panel_auditoria`, idempotente por `eventoId` y sin texto | R-06, R-07 · CA-09 | T-04 | hecho (KNOCK-93): `OpinionModeradaConsumer` y ruta interna en Identidad, idempotente por `eventoId` (`_id`), sin texto; integración `Spec061AuditoriaOpinionTests` escrita, la corre el CI |
| T-05a | Aviso al técnico: tipo `opinion_oculta` de texto fijo en el catálogo de avisos de la spec 037 (enmienda), entrega a `POST /api/v1/interno/avisos` y prueba de que no lleva motivo ni datos de la opinión | R-07a · CA-09a | T-04 | parcial (KNOCK-93): el tipo `opinion_oculta` ya está en el catálogo (PR 296) y Mercado entrega el aviso con `rol` `tecnico` (unitarias en verde); falta la prueba de integración `Spec061AvisoTecnicoTests` contra Mensajería |
| T-06 | Rutas en el gateway dentro de `/api/v1/admin/**` y prueba de enrutamiento | §5 | T-03 | hecho (KNOCK-93): ruta `panel-admin-opiniones` en el gateway y casos en `Spec033GatewayTests`; la corre el CI |
| T-07 | Pruebas de seguridad negativa (sin token, sin permiso, sin doble factor) | CA-05, CA-06 | T-03 | parcial (KNOCK-93): `Spec061OcultarOpinionSeguridadTests` cubre 401 sin token y con token de la app; el 403 y el 422 están en `Spec061OcultarOpinionTests` (puerta de mentira); falta una prueba del doble factor real (depende de la puerta real) |
| T-08 | OpenAPI de Mercado regenerado, `docs/endpoints.md`, `docs/rate-limit-matrix.md`, `docs/events.md` y `Memoria.md` | §12 | T-06 | parcial (KNOCK-93): `docs/endpoints.md`, `docs/rate-limit-matrix.md`, `docs/events.md` y Memoria al día; OpenAPI de Mercado e Identidad regenerados en commit aparte |
| T-09 | Gates de `AGENTS.md §5`, par revisor, `security-reviewer` y `python scripts/sdd_lint.py --trace 061` | §12 | todas | pendiente |

## Definición de terminado
- Los casos CA de `spec.md` tienen prueba con su ID; `python scripts/sdd_lint.py --trace 061` en OK.
- Gates de `AGENTS.md §5` en verde, par revisor y `Memoria.md` al día.
