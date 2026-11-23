# Spec 064: el técnico no se presentó

## Qué cambió
- Ruta `POST /api/v1/visitas/{id}/tecnico-ausente` en Mercado, módulo `Requests`: el cliente dueño marca la visita `confirmada` como `no_presentado` con `quienFalto = tecnico`, entre 15 minutos y 2 horas después de la cita (la misma ventana de «cliente ausente»).
- `VisitScheduleStore.MarkTechnicianAbsentAsync`: actualización condicionada (`confirmada` y del cliente) y evento `TecnicoNoSePresento` (solo identificadores) en la misma transacción.
- `VisitScheduleService.TechnicianAbsentAsync`: las llamadas de la misma visita se turnan con `KeyedGate` y la visita se relee dentro del turno; repetir con la visita ya marcada por el técnico responde 200 sin otro evento.
- `VisitRecord.WhoMissed` (campo `quienFalto`) y las reglas `CanReportTechnicianAbsent` e `IsTechnicianAbsentRecorded`.
- Documentación: `docs/endpoints.md`, `docs/events.md` y `docs/rate-limit-matrix.md` (la política `visitas-cambios` pasa a 8 rutas).

## Por qué
El cliente que espera en vano no tenía ruta: la visita quedaba `confirmada` hasta vencer. El hecho solo se registra y se avisa; no hay penalización ni cambio de reputación porque el reporte lo hace una sola parte (D-01 de la spec).

## A evitar
- No sancionar al técnico a partir de este evento: lo decidirá una spec futura (053 o 059).
- No responder 409 al repetir: la repetición es idempotente (200) y no escribe otro evento.
- El gateway declara las rutas de `visitas` una por una: esta ruta necesita su entrada propia antes de que la app la alcance por el gateway.
