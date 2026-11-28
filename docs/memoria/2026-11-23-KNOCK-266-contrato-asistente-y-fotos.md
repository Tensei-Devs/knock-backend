# 2026-11-23 · KNOCK-266 · Contrato OpenAPI: asistente, 429 de «mis cotizaciones» y límites de fotos

## Qué cambió
- Las seis acciones de `AsistenteController` declaran ahora 400, 401, 403, 413, 422, 429 y 503 (según la ruta) como ProblemDetails; los códigos ya salían en código: `not_a_technician`, `forbidden`, `log_not_found`, `ai_daily_limit_reached` (429 con `Retry-After`), `price_reference_unavailable` y `ai_unavailable` (503 con `Retry-After`).
- `GET /tecnicos/me/cotizaciones` declara el 429.
- Los cuerpos de `POST /visitas/{id}/terminar` (1 a 5 fotos) y `POST /visitas/{id}/opinion` (0 a 3 fotos) documentan sus límites de fotos y el 422 `errors.fotos`; el de solicitud ya lo documentaba (1 a 5).
- Sin cambio de comportamiento.

## Por qué
El contrato omitía respuestas reales y los límites de fotos, y los clientes generados no sabían manejarlos.

## A evitar
- Declarar un estado que la ruta no puede devolver.
- Pendiente: los 409 y 503 de cada ruta de Mercado con su `code` (hay 48 operaciones sin 409 y 62 sin 503 declarados; solo aplican a las que realmente los devuelven y se revisan una por una).
