# 053/050: el reporte de «no terminado» llega a la cola del panel por rutas internas (BB13)

## Qué cambió

- Mercado expone `GET /api/v1/interno/visitas/{visitaId}/reporte-no-terminado` (política `InternalNotFinishedReport`, permiso `reporte-no-terminado`, el token lleva el `visitaId` y debe coincidir con la ruta; 401/403/404; sin gateway, sin caché). Devuelve identificadores, texto y fecha.
- Identidad recibe el evento `ReporteNoTerminadoCreado` (solo identificadores, como lo escribe Mercado) en `POST /api/v1/interno/reportes-no-terminados`, protegida por `InternalServiceTokenAttribute` (clave `Security:InternalTokens:MarketKey`, `iss` `knock-market`, `aud` `knock-identity`, permiso `reporte-no-terminado-creado`, vida de 5 minutos como máximo).
- `NotFinishedReportConsumer` lee el texto con `MarketNotFinishedReportClient` (misma dirección y clave `Services:Market` que el historial) y abre el reporte con `ReportesStore.OpenFromVisitAsync`: `abierto`, motivo `mal_servicio`, referencia a la visita, sin gravedad (la asigna el panel). Es idempotente por visita con el índice único disperso `visitaNoTerminadaId_1` (también en `db/043-reportes.js`).
- `ServiceTokens` firma los tokens de servicio de Identidad; el historial y la entrega de la bandeja lo usan en vez de repetir la firma.

## Por qué

- Decisión de José (opción A): el evento no lleva el texto para que la bandeja no tenga datos personales (spec 050 R-32); el texto se lee por una ruta interna con permiso propio y se respeta el 503 si Mercado no responde, para que quien entrega reintente.

## A evitar

- Poner el texto o los identificadores de las dos personas en el evento, la bandeja o los registros.
- Reutilizar los permisos `historial` o `bloqueos` para estas rutas, o exponerlas por el gateway.
- Quitar el índice `visitaNoTerminadaId_1`: sin él dos entregas simultáneas pueden abrir dos reportes.
- Pendiente (A, F3): la entrega de la bandeja de Mercado hacia esta ruta, las claves `Security:InternalTokens:MarketKey` (Identidad) y la de Mercado hacia Identidad, y OpenAPI/endpoints.md.
