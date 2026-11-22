# Catálogo de oficios, oficios pedidos y su administración desde el panel (KNOCK-134)

## Qué cambió
Mercado (`Knock.Market.Api`, módulo Catalog) tiene el detalle público de un oficio (`GET /api/v1/oficios/{clave}`, de la misma caché de 24 horas de oficios activos, con `frasesSugeridas`), la administración del catálogo para el panel (`GET`, `POST` y `PUT /api/v1/admin/oficios`, con el permiso `catalog:manage`, edición parcial y clave inmutable) y «Otro oficio» (`POST /api/v1/oficios-pedidos`, para cuentas con perfil de técnico, con `upsert` por persona y texto normalizado). Las altas y cambios dejan una entrada en la auditoría del panel antes de escribir (`IPanelAuditTrail`) e invalidan la caché de oficios activos. El gateway publica las rutas de administración hacia Mercado y `POST /api/v1/oficios-pedidos`. Las rutas del panel de oficios pedidos y lista de espera (ya existentes) también quedan enrutadas.

## Por qué
La spec 045 pide ampliar la cobertura con datos reales: el panel abre oficios sin reprogramar y la app muestra el detalle y recoge los oficios que faltan. El detalle sale de la caché de la lista para que la invalidación de las escrituras ya probada (generaciones en Redis) cubra también la edición.

## A evitar
Reaplicar `oficios.json` con el cargador del catálogo reemplaza los oficios por su contenido y borra lo editado desde el panel: el cargador no corre al arrancar, pero no debe usarse en un entorno con oficios editados. Cambiar la política de límite de «Otro oficio» sin tocar `RateLimitingExtensions` de otra manera: hoy comparte `tecnicos-registro` para no mover ese archivo mientras la spec 055 lo reubica. Ofrecer `ambos` como modalidad en las rutas de solicitudes sin revisarlas: la spec 020 solo conoce `domicilio` y `taller`.

## Decidido
- OFP-01 es para cuentas con perfil de técnico (registro ya hecho); cualquier otra recibe 404 `not_found` sin más datos.
- El detalle público solo trae clave, nombre, modalidad y frases sugeridas; los parámetros de precio los ve solo el panel.
- `PUT` es parcial (solo cambian los campos enviados, al menos uno).
- Las dos rutas de escritura del panel responden 503 `panel_audit_unavailable` sin escribir mientras la auditoría real del panel (spec 033) no exista; la puerta de permisos sigue negando todo.
- La moderación del texto de «Otro oficio» es de patrones (correo, dirección web y más de seis dígitos); la moderación de contenido con Azure es de la spec 031.

## PARA VALIDACIÓN DE JOSÉ
Las cinco decisiones de arriba. Alternativa descartada para el detalle: devolver también los parámetros de precio (se descartó por mínima exposición en una ruta pública). Alternativa descartada para la auditoría: escribirla después del cambio (podría dejar un cambio sin constancia).- 2026-11-22: la auditoria no disponible ya no se lanza como excepcion propia: TradeAdminService devuelve null y el controlador responde 503 panel_audit_unavailable. Se quito el pragma de S3871, que la gobernanza rechaza sin excepcion registrada. A evitar: suprimir reglas con pragma sin EXC-NNN.
