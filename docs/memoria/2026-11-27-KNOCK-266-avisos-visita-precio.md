# Faltaban los avisos de la visita, del precio y de la cotización vencida

## Qué cambió
- Mensajería suma al catálogo seis avisos de la visita y cuatro de cotización final y ajuste de precio, y Mercado los entrega (enmiendas del 2026-11-27 de las specs 049 y 054).
- `SolicitudVencida` avisa además `cotizacion_vencida` a cada técnico de `tecnicosAvisar` (enmienda de la 037).
- Tres eventos de reprogramación llevan `propuestaPor` y marcar al cliente ausente escribe `ClienteAusente`.

## Por qué
- Los eventos se quedaban `pendiente` porque el catálogo cerrado no tenía sus tipos.
- La propuesta se borra de la visita al responderla, así que quién propuso tiene que viajar en el evento.

## A evitar
- Dar un solo tipo y rol por evento: cada destino de `AvisoDeEvento` lleva los suyos.
- Avisar con `VisitaCerrada`: la ausencia del cliente tiene su propio evento.

## Pendiente
- Los nombres nuevos de aviso en la spec 024 y en el código de la app.
- Los eventos de reprogramación anteriores sin `propuestaPor` quedan `fallido`.
- Cuando la visita termina por rechazar o vencer la cotización final no se escribe `VisitaTerminada`, así que no hay aviso `visita_terminada`; la enmienda no lo pide.
