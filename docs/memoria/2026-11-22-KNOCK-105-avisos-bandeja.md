# KNOCK-105 - bandeja de avisos dentro de la app y lectura de preferencias (spec 044)

## Qué cambió
- `Modules/Notices` en Messaging: `GET /api/v1/avisos` con cursor opaco, contador de no leídos con tope de 99, marcar uno o todos como leídos y `GET /api/v1/me/preferencias-avisos`.
- `NoticesStore` es el único punto con `IMongoCollection`; todo se filtra por el `sub` del token y un aviso ajeno responde el mismo 404 que uno inexistente.
- `db/044-avisos-dentro-de-la-app.js` crea los índices de `avisos` (con TTL de 30 días) y `preferencias_avisos`; rutas del gateway para avisos y preferencias (solo GET).
- Pruebas unitarias CA-01 a CA-24, de seguridad sobre HTTP (401, 422, 413, 429) y de índices en base de datos (corren solo en CI).

## Por qué
La persona debe ver su historial de avisos sin que el módulo pueda crearlos ni cambiar preferencias: la creación pertenece al despachador de la spec 037, y así la bandeja no puede suplantar avisos de otros.

## A evitar
Crear avisos desde las rutas de la bandeja; devolver 403 para un aviso ajeno (revela que existe); registrar identificadores o datos personales; guardar `leido` por defecto sin el índice `usuarioId`+`leido`+`creadoEn`.

## Pendiente
T-00 (el despachador que inserta en `avisos` desde eventos como `ReporteDecidido` es de la spec 037), `PUT /api/v1/me/preferencias-avisos` y su método en el gateway (037), T-09 (OpenAPI, endpoints y matriz de límites, cierre F3) y la revisión de par.
