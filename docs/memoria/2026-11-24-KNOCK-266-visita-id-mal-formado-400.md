# Specs 063 y 064: un id de visita mal formado responde 400

## Qué cambió
- `BusinessRuleController.MalformedId` responde 400 con `code = invalid_id` (sin repetir el valor) cuando el `id` de la ruta no es un `ObjectId`. Lo usan `FinalQuoteController` (`cotizacion-final` y `ajustes-pendientes`, spec 063) y `VisitScheduleController.TechnicianAbsent` (spec 064).
- Se retiró `VisitRouteValidator`: ya no hay 422 en estas rutas. `VisitRoute` sigue enlazando el `id`.
- Pruebas ajustadas: `CA_063_08` y `CA_064_06` esperan 400 y el `code`; los contratos de las dos specs esperan 400 en lugar de 422.

## Por qué
- Enmiendas firmadas el 2026-11-24 (063 R-06, R-08, CA-08, CA-11; 064 R-06, R-09, CA-06, CA-09): la convención de la spec 002, R-10, reserva el 400 para lo que no se puede interpretar, como un identificador de ruta mal formado. No se tocó la 049, que no se enmendó.

## A evitar
- No reponer un validador de FluentValidation para el `id` de estas rutas: el filtro de validación responde 422.
- El OpenAPI no cambió porque la base de los controladores de Requests ya declara 400 y 422 en todas las rutas; el 422 sigue apareciendo en el contrato aunque estas tres rutas ya no lo devuelvan (huecos de la base, no de las rutas).
