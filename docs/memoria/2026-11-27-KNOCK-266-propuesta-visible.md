# KNOCK-266 · La propuesta de reprogramación se ve en la visita

## Qué cambió

- `GET /api/v1/visitas/{id}` y las respuestas con la misma vista agregan `propuestaReprogramacion` (`fechaHora`, `propuestaPor`, `venceEn`), igual para las dos partes.
- `reprogramacionPendiente` es verdadero solo cuando ese objeto está presente: visita `confirmada` (también bloqueada) y propuesta sin vencer (spec 050, enmienda del 2026-11-27).
- El contrato (`docs/openapi.v1.json` y el público) se editó a mano porque no se compiló en local; el CI lo regenera y compara.

## Por qué

- Quien responde no veía qué fecha le proponían ni cuándo vencía, y la bandera seguía en verdadero fuera de `confirmada` aunque responder diera 409.

## A evitar

- Volver a calcular la bandera aparte del objeto: pueden contradecirse.
- Borrar la propuesta guardada al leer.
