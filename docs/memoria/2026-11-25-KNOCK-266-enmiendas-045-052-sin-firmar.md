# Enmiendas sin firmar de 045 (R-20) y 052 (CA-23)

## Qué cambió
- 045: nota de enmienda que describe la auditoría de oficios por bandeja de salida ya fusionada (#394). Sin firmar.
- 052: nota con dos opciones para CA-23. Sin firmar y no firmable hasta que José elija.

## Por qué
- El comportamiento de 045 cambió y la spec no lo decía. CA-23 es contradictorio y la salida propuesta antes (503 `ai_unavailable`) no coincide con el código: la bitácora no usa IA y su contrato no declara 503.

## A evitar
- Recomendar un estado HTTP para una ruta sin comprobar antes qué dependencias usa y qué declara su contrato.
