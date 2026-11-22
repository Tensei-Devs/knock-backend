# Spec 030 T-05 y T-07: crear y leer conversaciones, lista, no leídas, historial y leído (KNOCK-73)

## Qué cambió
- Módulo Conversaciones de Mensajería: `ConversacionesController` y `LecturaController`, `ConversacionService` y `LecturaService`, `ConversacionesStore` y `MensajesStore` (`EnsureIndexesAsync` con los nombres de `db/030-conversaciones.js`), cursor opaco `PageCursor`, validadores, aviso de seguridad en `Conversaciones:AvisoSeguridad` e `INotificadorEnVivo` (evento `noLeidos` al grupo `usuario:{sub}`).
- `market_unavailable` y el cliente del permiso vienen de T-04; esta tarea solo lo consume.
- `docs/endpoints.md` y `docs/rate-limit-matrix.md` con las rutas y el límite `conversaciones-crear` (20 por hora).

## Por qué
- La app necesita crear y ver conversaciones y el contador del globo antes del envío (T-06) y del hub (T-08).
- La lista mezcla en memoria dos consultas, una por índice de rol, porque un `$or` ordenado no usa ambos índices.
- `leido` recalcula el contador con los mensajes de la otra parte y solo lo aplica si `ultimoMensajeEn` no cambió (3 intentos), para no pisar un mensaje nuevo.
- El aviso `noLeidos` es de mejor esfuerzo y no registra la excepción (el grupo lleva el `sub`).
- Los errores del servicio no llevan `Cache-Control`: el manejador de excepciones limpia los encabezados.
- `scripts/export_openapi.py` no se tocó (zona protegida): el contrato de Mensajería queda propuesto en el PR.

## A evitar
- Usar el `tecnicoId` del cuerpo en vez del del permiso.
- Negar valores sobre el cuerpo completo de un error: usar `ProblemBody.Reasons`.
- Escribir «todo» o «TODO» en comentarios: S1135 lo marca como tarea pendiente.
