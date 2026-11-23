# Tareas 063 — Lectura de la cotización final y de los ajustes pendientes

> **Estado:** implementada (KNOCK-266). Las pruebas de integración y de seguridad las corre el CI.

| Tarea | Qué se hace | Cubre | Depende de | Estado |
|---|---|---|---|---|
| T-01 | Cálculo de vigencia al leer (`enviada` vencida, ajuste vencido) y pruebas unitarias | R-02, R-05 · CA-04, CA-05 | — | hecho (KNOCK-266): `FinalQuoteRules.EffectiveState` e `IsAdjustmentLive`; unitarias `Spec063VigenciaTests` (12 casos) en verde en local |
| T-02 | `GET /api/v1/visitas/{id}/cotizacion-final`: DTO, servicio, lectura en el almacén, controlador y pruebas | R-01 a R-03 · CA-01 a CA-05 | T-01 | hecho (KNOCK-266): `FinalQuoteStore.FindFinalQuoteAsync`, `FinalQuoteService.ReadFinalQuoteAsync` y `FinalQuoteController.ReadFinalQuote`; integración `Spec063LecturaTests` escrita, la corre el CI |
| T-03 | `GET /api/v1/visitas/{id}/ajustes-pendientes`: DTO, servicio, lectura en el almacén, controlador y pruebas | R-04, R-05 · CA-06, CA-07 | T-01 | hecho (KNOCK-266): `FinalQuoteStore.ListPendingAdjustmentsAsync`, `FinalQuoteService.ReadPendingAdjustmentsAsync` y `FinalQuoteController.ReadPendingAdjustments`; integración `Spec063LecturaTests` escrita, la corre el CI |
| T-04 | Validación del `id`, `Cache-Control` y seguridad negativa | R-06, R-07 · CA-08 a CA-10 | T-02, T-03 | hecho (KNOCK-266): `VisitRoute` y `VisitRouteValidator` (422 `errors.id`), `Cache-Control: private, no-store` por la base del controlador; seguridad `Spec063LecturaSeguridadTests` escrita, la corre el CI |
| T-05 | OpenAPI regenerado, `docs/endpoints.md` y prueba de contrato | R-08 · CA-11 | T-02, T-03 | hecho (KNOCK-266): `docs/openapi.v1.json` regenerado con el script, `docs/endpoints.md` y `Spec063ContractTests` |
| T-06 | Gates de `AGENTS.md §5` y memoria | §12 | T-05 | hecho (KNOCK-266): build `-warnaserror`, format, arquitectura y unitarias de 063 en verde en local; memoria en `docs/memoria/2026-11-23-KNOCK-266-spec-063-lectura-cotizacion-final.md` |
