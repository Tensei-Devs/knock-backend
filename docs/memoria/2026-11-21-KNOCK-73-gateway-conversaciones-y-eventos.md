# KNOCK-73 — rutas del gateway de Conversaciones y evento MensajeNuevo (spec 030 T-09 y T-10)

## Qué cambió
- `src/Knock.Gateway/appsettings.json`: rutas `mensajeria-conversaciones` (`/api/v1/conversaciones/{**resto}`) y `mensajeria-dispositivos` (`/api/v1/dispositivos/{**resto}`), ambas con política `default` (exigen sesión) y CORS `knock`. La ruta de permiso de Mercado no se publica: sigue sin ruta en el gateway y responde 404 (CA-17).
- `GatewayRoutingTests`: enrutado a `mensajeria` y 401 sin token sin llegar al servicio para las rutas nuevas.
- `docs/events.md`: evento `MensajeNuevo` del buzón de salida.

## Por qué
La app solo llega a Mensajería por el gateway; sin ruta, las conversaciones del chat no eran alcanzables.

## A evitar
Publicar la ruta interna de permiso de Mercado; documentar `MensajeNuevo` con texto del mensaje.

## Pendiente para el cierre (F3)
`docs/endpoints.md`, `docs/rate-limit-matrix.md` y OpenAPI de Mensajería (rutas de `/conversaciones` y `/dispositivos`; límites `conversaciones-crear` 20/h y `conversaciones-mensajes` 30/min).
