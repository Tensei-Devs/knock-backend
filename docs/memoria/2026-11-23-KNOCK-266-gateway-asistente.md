# 2026-11-23 · KNOCK-266 · Rutas del gateway para el asistente

## Qué cambió
`src/Knock.Gateway/appsettings.json` suma cinco rutas hacia Mercado, con la política `default` y el CORS `knock` de las demás rutas de Mercado: `POST /api/v1/asistente/conversaciones`, `POST /api/v1/asistente/resumen-chat`, `POST /api/v1/asistente/borrador-cotizacion`, `GET /api/v1/asistente/rango-precio` y `GET`/`PUT /api/v1/asistente/bitacora/{solicitudId}`. Solo se añadieron rutas. `GatewayRoutingTests.MarketRoutes` las incluye (reenvío con el token y 401 sin token).

## Por qué
Los seis endpoints del asistente existen en Mercado, pero el gateway no los enrutaba y la app no podía llamarlos.

## A evitar
- Un comodín `/api/v1/asistente/{**resto}`: se enruta cada ruta con su método, como el resto de Mercado.
- Publicar `GET /api/v1/solicitudes/{id}/conversacion`: es interna (CA_030_17) y sigue sin ruta.
