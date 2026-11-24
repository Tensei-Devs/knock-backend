# 2026-11-23 · KNOCK-97 · Rutas del gateway para las specs 063 y 064

## Qué cambió
`src/Knock.Gateway/appsettings.json` suma tres rutas hacia Mercado, con la política `default` y el CORS `knock` de las demás rutas de `visitas`: `GET /api/v1/visitas/{id}/cotizacion-final`, `GET /api/v1/visitas/{id}/ajustes-pendientes` (spec 063) y `POST /api/v1/visitas/{id}/tecnico-ausente` (spec 064; inofensiva hasta que su endpoint llegue a sprint-05). `GatewayRoutingTests` las incluye (reenvío con el token y 401 sin token) y prueba que otros métodos no se reenvían. Las specs 063 y 064 llevan una enmienda fechada que corrige el dato falso de que el gateway ya enrutaba `/api/v1/visitas/{**resto}`.

## Por qué
Los endpoints existían en Mercado, pero las rutas de `visitas` se declaran una por una y la app no podía alcanzarlos.

## A evitar
- Un comodín `/api/v1/visitas/{**resto}`: cada ruta se enruta con su método.
- Dar por hecho que el gateway enruta un endpoint nuevo: añadir su ruta en el mismo PR.
