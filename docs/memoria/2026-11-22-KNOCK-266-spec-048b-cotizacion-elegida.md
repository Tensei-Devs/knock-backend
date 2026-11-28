# Cotización elegida cierra las demás conversaciones (spec 048, CA-21)

## Qué cambió
- Mercado escribe el evento `CotizacionElegida` (solo `solicitudId`, `cotizacionId` y `tecnicoUsuarioId`) en su bandeja, en la misma transacción que la elección de la cotización. El tipo ya estaba en el catálogo de la spec 054.
- Mensajería recibe `POST /api/v1/interno/eventos` (fuera del gateway, mismo token de servicio que `interno/avisos`, permiso `CotizacionElegida`). Cierra las conversaciones abiertas de la solicitud cuyo técnico no es el elegido con `motivoCierre: other_quote_chosen`, sin borrar mensajes. Responde 202; sin permiso 403, sin token 401 y con campos extra o tipo desconocido 422.
- Pruebas: `Spec048CotizacionElegidaTests` (cierre, mensajes intactos, repetición sin cambios, otra solicitud intacta, rechazos) y la elección de la spec 021 comprueba que el evento se escribe.

## Por qué
- El cierre es un `UpdateMany` filtrado por estado `abierta`, así que recibir el mismo evento otra vez no cambia nada (CA-21) y no hace falta guardar identificadores de eventos.
- El técnico elegido se excluye por su `sub` (`tecnicoUsuarioId`), el mismo dato que guarda la conversación.

## A evitar
- Dependencia: el transporte de la bandeja de Mercado a esta ruta (`EntregaInternaHttp`, rama de la spec 043) no está en esta base; hasta integrarlo el evento queda pendiente en la bandeja. No reimplementarlo aquí.
- El permiso de conversación se guarda 60 s en Redis: una conversación recién cerrada puede aceptar un mensaje hasta que expire, y Mercado ya responde que no está abierta al renovarlo.
- Pendiente en F3: `docs/endpoints.md` y `docs/rate-limit-matrix.md` (ruta interna nueva), claves del emisor Mercado en la configuración.
