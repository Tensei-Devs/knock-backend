# KNOCK-266 · Enmiendas firmadas en lote

## Qué cambió

- 039, 044, 054 (#391): exportación de mensajes, tipo «sistema» y avisos sin rol. Desbloquea #390
- 045 R-20 (#399): constancia de la auditoría ya fusionada
- 052: retirar CA-23
- 020 y 029: cómo la solicitud llega a terminada o cancelada (requiere código)
- 046 §6: índices y búsqueda tal como están implementados

## Por qué

- Había decisiones de negocio sin escribir y specs que no decían lo que hace el código.

## A evitar

- Dar por implementada una enmienda firmada: las transiciones de la spec 020 siguen sin código.
