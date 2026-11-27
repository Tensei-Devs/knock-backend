# Los avisos de solicitudes de Mercado no llegaban a nadie

## Qué cambió
- Mercado registra la entrega a `/interno/avisos` de `SolicitudEnviadaATecnico`, `SolicitudSinTecnicos`, `CotizacionEnviada`, `CotizacionAceptada`, `SolicitudCancelada` y `SolicitudVencida` (spec 037, R-11; spec 054, R-03).
- La persona y la visita se leen de las colecciones de Mercado al entregar; la forma de los eventos no cambia.
- `CotizacionEnviada` se escribe como los demás eventos (`_id` de texto y `datos`); la bandeja no reserva los documentos antiguos con `_id` ObjectId.

## Por qué
- Mercado escribía los eventos, pero sin entrega registrada se quedaban `pendiente`.
- Resolver al entregar respeta las cargas que fijan las specs 028 y 029 y sirve para lo que ya estaba `pendiente`.

## A evitar
- Mandar un `tecnicoId` como destinatario: el aviso es para la persona (`sub`), no para el documento del técnico.
- Escribir en `outbox` a mano con otra forma: la bandeja espera `_id` de texto y `datos`.

## Pendiente
- `cotizacion_vencida`: falta que una spec del servidor diga qué evento lo produce.
- La spec 037 remite a la spec 024 del frontend para el tipo y el recurso, y esa usa `request_cancelled` y `request_expired` en vez de `solicitud_cancelada` y `solicitud_vencida`.
- Al encender `Bandeja:Habilitada` se entregan también los eventos antiguos que sigan `pendiente`.
