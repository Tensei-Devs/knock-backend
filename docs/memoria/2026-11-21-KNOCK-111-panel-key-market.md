# Llave del panel para Mercado (KNOCK-111)

## Qué cambió
`deploy/apps/main.tf`: el servicio `market` recibe `PanelJwt__SigningKey` desde el secreto `panel-jwt-signing-key`, el mismo que ya leen el gateway e Identity. El módulo de servicio crea el rol de lectura de ese secreto para la identidad de Mercado.

## Por qué
Las rutas `/admin/oficios-pedidos` y `/admin/lista-espera` de Mercado (KNOCK-111, spec 045) validan el token del panel con esa llave; sin ella no podían validarlo.

## A evitar
No crear un secreto nuevo ni usar `jwt-signing-key` (es otra llave). No aplicar hasta revisar el plan: debe mostrar solo la Container App de `market` y un rol nuevo, sin destruir nada.
