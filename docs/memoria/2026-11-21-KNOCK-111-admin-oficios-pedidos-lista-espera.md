# Rutas del panel de oficios pedidos y lista de espera (KNOCK-111)

## Qué cambió
Mercado expone `GET /api/v1/admin/oficios-pedidos` y `GET /api/v1/admin/lista-espera` (spec 045, OFP-02 y LIS-02, HU-070): conteos agrupados por texto normalizado y por código postal, con cursor (20 por defecto, 50 máximo). Se agrega el esquema `PanelBearer` (llave `PanelJwt:SigningKey`, destinatario `knock-panel`) y la puerta `IPanelPermissionGate` que decide con los roles del servidor; la implementación por omisión niega todo.

## Por qué
HU-070 pide ver oficios pedidos y lista de espera sin datos personales. Las rutas `/admin` dependen del panel (spec 033), que vive en Identidad.

## A evitar
Aceptar el token de la app en rutas del panel, decidir con claims del token (no lleva permisos) o devolver celulares. Registrar la puerta real solo con autenticación entre servicios.

## PARA VALIDACIÓN DE JOSÉ
Autorización provisional: permiso `metrics:read` (no `catalog:manage`, que queda para OFI-03 a OFI-05), puerta que falla cerrado hasta que Identidad ofrezca una consulta interna de permisos, límite 60 por minuto por IP. Alternativa descartada: llamar a Identidad desde Mercado sin la spec 056.