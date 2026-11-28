# 2026-11-25 · KNOCK-137 · El contrato público incluye las rutas raíz

## Qué cambió
`route_regex` de `scripts/export_openapi.py` trata `{**resto}` como cero o más segmentos, igual que el enrutamiento de ASP.NET Core. `docs/openapi.publico.v1.json` se regeneró: entran 11 operaciones que el gateway ya atiende y faltaban (`GET` y `PUT /me`, `GET /oficios`, `GET /avisos`, `GET` y `POST /conversaciones`, `POST /dispositivos`, `POST /reportes`, `GET /me/tecnicos-confianza`, `GET` y `POST /admin/oficios`). La regeneración también sincroniza un desfase previo del archivo con los contratos de servicio: entra `GET /me/exportaciones/ultima` y sale la respuesta 503 de `PUT /admin/oficios/{clave}`, que el contrato de Mercado ya no declara.

## Por qué
La expresión anterior exigía una barra después del prefijo, así que la ruta raíz de cada comodín quedaba fuera del público (spec 034, R-13) y los clientes no podían generarse desde él.

## A evitar
Dar por completo el contrato público sin compararlo con las rutas del gateway. `GET /solicitudes/{id}/conversacion` sigue fuera porque el gateway no tiene ruta para ella: eso es un cambio de gateway, no del script.
