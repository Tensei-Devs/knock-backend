# La bandeja de Mercado no entregaba dos eventos ni limpiaba lo terminado

## Qué cambió
- Mercado entrega `CotizacionElegida` a `/interno/eventos` y `visita_afectada` a `/interno/avisos` de Mensajería (specs 048, R-14; 059, R-11; 054, R-03).
- Los eventos y las tareas de Mercado escriben `limpiarEn` al quedar en un estado terminal: 14 días lo terminado y 90 lo fallido (spec 054, R-11).
- `docs/events.md` se puso al día con lo que hay en `sprint-05`.

## Por qué
- Los consumidores de Mensajería ya existían, pero nadie los llamaba: los eventos se quedaban `pendiente`.
- `db/054` crea el TTL sobre `limpiarEn` y solo Identidad lo escribía, así que en Mercado no se borraba nada fallido ni entregado.

## A evitar
- Dar por conectado un evento porque existe su consumidor: hace falta la entrega registrada en el emisor.
- Escribir en `docs/events.md` que algo espera un PR sin volver a comprobarlo en la rama.

## Pendiente
- Siguen sin entrega los demás tipos de Mercado, incluidos los de los avisos de la spec 037. Varios no llevan destinatario o recurso en sus datos y falta definir la correspondencia en el backend.
- Los documentos que ya estaban en un estado terminal sin `limpiarEn` no se rellenan.
