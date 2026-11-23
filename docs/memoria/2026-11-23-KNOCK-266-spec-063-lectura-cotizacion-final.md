# Spec 063: lectura de la cotización final y de los ajustes pendientes

## Qué cambió
- Dos rutas de solo lectura en Mercado, módulo `Requests`: `GET /api/v1/visitas/{id}/cotizacion-final` y `GET /api/v1/visitas/{id}/ajustes-pendientes`, para el cliente dueño y el técnico de la visita.
- `FinalQuoteRules.EffectiveState` e `IsAdjustmentLive` calculan al leer el estado `vencida` y la vigencia del ajuste, sin escribir.
- Lecturas nuevas en `FinalQuoteStore` (proyección de `cotizacionFinal` y lista de `ajustes_precio` por `visitaId`); acciones nuevas en `FinalQuoteController` y `FinalQuoteService`.
- `VisitRoute` y su validador: un `id` que no es `ObjectId` responde 422 con `errors.id`.

## Por qué
La app recibía el resultado de cada POST de la spec 049 pero, al reabrir la visita, no tenía dónde leer el precio, la descripción ni el vencimiento. Los ajustes se leen por visita porque la app abre la visita y no la cotización (D-01 de la spec).

## A evitar
- No escribir al leer: el estado `vencida` se calcula con el reloj; marcarlo sigue siendo del ejecutor de la spec 054.
- No distinguir en la respuesta entre visita ajena, inexistente o sin cotización: es el mismo 404.
- No agregar identificadores de personas a estas respuestas ni registrar `descripcion` o `motivo`.
