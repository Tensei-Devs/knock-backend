# Rutas de Mercado en el gateway (F3, KNOCK-266)

## Qué cambió
El gateway enruta a Mercado las rutas públicas que ya existían en el código sin ruta YARP: servicios del técnico (047), agenda y cambios de visita (050), cotización final y ajuste de precio (049), cancelar solicitud (029), fotos de trabajos y reputación (046) y permiso de conversación. Las rutas `/admin/oficios-pedidos` y `/admin/lista-espera` las define el catálogo de oficios (KNOCK-134), no este cambio.

## Por qué
Sin ruta, el cliente real recibía 404 aunque el servicio respondiera. Las rutas llevan método explícito y política de autenticación como sus hermanas; el límite de tasa lo aplica cada servicio.

## A evitar
Añadir rutas `/interno/**` al gateway; usar un comodín `/admin/**` nuevo en Mercado; publicar una ruta sin prueba de enrutado y de 401 sin token.
