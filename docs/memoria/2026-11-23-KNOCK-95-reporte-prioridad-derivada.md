# 2026-11-23 · KNOCK-95 · Prioridad derivada de un reporte sin tocar la gravedad

## Qué cambió
Al decidir un reporte (spec 053, R-20), `ReportPriorities.For` deriva la `prioridad`: `alta` si la gravedad asignada es `critica` y la persona reportada no es técnica (la suspensión no aplica); `normal` en los demás casos. `ISafetyApi.ApplyReportDecisionAsync` y `IReportDecisionEffects.ApplyAsync` ahora devuelven `ReportActionResult(SessionIds, ReportedIsClient)`. El almacén guarda `prioridad` en la misma transacción, sin tocar `gravedad` ni `decision.gravedad`. La decisión y la cola de `GET /admin/reportes` devuelven `prioridad` (la cola solo en reportes ya decididos). Sin índice ni script de `db/`; el orden de la cola no cambia.

## Por qué
Decisión de José del 2026-11-23: una crítica contra un cliente no suspende la cuenta, pero debe quedar como crítica y atenderse con prioridad alta.

## A evitar
- Bajar o reescribir la gravedad para expresar la prioridad.
- Deducir «cliente» de la gravedad o de un campo del cuerpo: sale del resultado de la suspensión (spec 059, R-03).
- Ordenar la cola por prioridad sin una enmienda de la spec.
