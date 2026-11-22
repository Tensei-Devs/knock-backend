# Bloqueos: corte de mensajes, cotizaciones y reparto, y entrega interna firmada (spec 043 CA-26/27/28)

## Qué cambió
Con un bloqueo en cualquier sentido, Mensajería no envía mensajes ni crea o reabre conversaciones, Mercado no recibe cotizaciones y el reparto de solicitudes salta a quien tiene bloqueo con quien pidió; todo consulta la proyección con `AreBlockedAsync` (en Mercado, `BlockedWithAsync` para el reparto en una sola consulta). Quien fue bloqueado recibe la misma respuesta 404 que ante un técnico o conversación no disponible. Al crearse el bloqueo, la conversación abierta de la pareja queda cerrada (solo lectura) con el motivo ya existente `dismissed`, sin borrar mensajes; desbloquear deja volver a escribir. La bandeja de Identidad entrega ahora los eventos por HTTP privado (`EntregaInternaHttp`) con token firmado: Mercado con el permiso `bloqueos` y Mensajería con el tipo de evento.

## Por qué
R-16 a R-18, CA-26 a CA-28: el bloqueo debe cortar el contacto sin delatarse y sin que el evento viva solo en la bandeja.

## A evitar
Responder 403 o un código propio que revele el bloqueo; cerrar con un motivo distinto de los existentes; borrar mensajes al cerrar; consultar un bloqueo por técnico dentro de un ciclo en el reparto; registrar el token, la clave o la carga del evento; comparar el cuerpo crudo de dos respuestas 404 (la instancia lleva un identificador distinto).
