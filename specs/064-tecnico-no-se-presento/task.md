# Tareas 064 — El técnico no se presentó

> **Estado:** implementada salvo el aviso de texto fijo al técnico (T-04, parcial). Las pruebas de integración y de seguridad las corre el CI.

| Tarea | Qué se hace | Cubre | Depende de | Estado |
|---|---|---|---|---|
| T-01 | Regla de ventana y estado en el dominio, con pruebas unitarias | R-02 · CA-03 | — | hecho (KNOCK-266): `VisitScheduleRules.CanReportTechnicianAbsent` e `IsTechnicianAbsentRecorded`; unitarias `Spec064VentanaTests` en verde en local |
| T-02 | Almacén: actualización condicionada y evento `TecnicoNoSePresento` en la misma transacción | R-03, R-05 · CA-01, CA-02, CA-05, CA-07 | T-01 | hecho (KNOCK-266): `VisitScheduleStore.MarkTechnicianAbsentAsync` (actualización condicionada y `TecnicoNoSePresento` en la misma transacción); integración `Spec064TecnicoAusenteTests` escrita, la corre el CI |
| T-03 | Servicio y ruta `POST /api/v1/visitas/{id}/tecnico-ausente` | R-01, R-04, R-06 · CA-01, CA-04, CA-06 | T-02 | hecho (KNOCK-266): `VisitScheduleService.TechnicianAbsentAsync` (con `KeyedGate`) y `VisitScheduleController.TechnicianAbsent` con `VisitRoute` (422 `errors.id`); integración escrita, la corre el CI |
| T-04 | Límite `visitas-cambios` y aviso de texto fijo al técnico | R-07, R-08 · CA-08 | T-03 | parcial (KNOCK-266): política `visitas-cambios` aplicada y probada (`Spec064TecnicoAusenteSeguridadTests`, escrita, la corre el CI); el aviso de texto fijo al técnico NO está: no existe entrega de este evento a Mensajería (ver el reporte del PR) |
| T-05 | OpenAPI, `docs/endpoints.md`, `docs/events.md`, matriz de límites y prueba de contrato | R-09 · CA-09 | T-03 | hecho (KNOCK-266): `docs/openapi.v1.json` regenerado con el script, `docs/endpoints.md`, `docs/events.md`, `docs/rate-limit-matrix.md` y `Spec064ContractTests` |
| T-06 | Gates de `AGENTS.md §5` y memoria | §12 | T-05 | hecho (KNOCK-266): build `-warnaserror`, format, arquitectura y unitarias de 064 en verde en local; memoria en `docs/memoria/2026-11-23-KNOCK-266-spec-064-tecnico-no-se-presento.md` |
