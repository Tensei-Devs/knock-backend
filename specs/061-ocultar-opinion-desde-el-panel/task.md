# Tareas 061 — Ocultar una opinión desde el panel

> **Estado:** pendientes. En esta etapa **no se ejecutan**: se activan cuando la spec esté Aprobada (firma de José) y una persona lo indique.

| Tarea | Qué se hace | Cubre | Depende de | Estado |
|---|---|---|---|---|
| T-01 | Validador del motivo (8 a 300) y regla de idempotencia de ocultar y mostrar, con unitarias | R-01, R-02 · CA-03, CA-04, CA-06 | — | pendiente |
| T-02 | `ReviewStore.SetHiddenAsync` con relectura y `KeyedGate`, recálculo de reputación y generación de caché en una transacción | R-01, R-04, R-05 · CA-01, CA-02, CA-08, CA-10 | T-01 | pendiente |
| T-03 | `AdminReviewsController` (`ocultar` y `mostrar`) con `PanelBearer`, `reviews:moderate` y límite `admin-opiniones` | R-03, R-08 · CA-05, CA-07 | T-02 | pendiente |
| T-04 | Evento `OpinionModerada` en la bandeja de Mercado y su entrega a `POST /api/v1/interno/eventos` (permiso `opinion-moderada`) | R-04 | T-02 | pendiente |
| T-05 | Consumidor en Identidad: `opinion_ocultada` y `opinion_mostrada` en `panel_auditoria`, idempotente por `eventoId` y sin texto | R-06, R-07 · CA-09 | T-04 | pendiente |
| T-06 | Rutas en el gateway dentro de `/api/v1/admin/**` y prueba de enrutamiento | §5 | T-03 | pendiente |
| T-07 | Pruebas de seguridad negativa (sin token, sin permiso, sin doble factor) | CA-05, CA-06 | T-03 | pendiente |
| T-08 | OpenAPI de Mercado regenerado, `docs/endpoints.md`, `docs/rate-limit-matrix.md`, `docs/events.md` y `Memoria.md` | §12 | T-06 | pendiente |
| T-09 | Gates de `AGENTS.md §5`, par revisor, `security-reviewer` y `python scripts/sdd_lint.py --trace 061` | §12 | todas | pendiente |

## Definición de terminado
- Los casos CA de `spec.md` tienen prueba con su ID; `python scripts/sdd_lint.py --trace 061` en OK.
- Gates de `AGENTS.md §5` en verde, par revisor y `Memoria.md` al día.
