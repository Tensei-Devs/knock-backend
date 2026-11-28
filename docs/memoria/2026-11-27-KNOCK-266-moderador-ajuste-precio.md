# KNOCK-266 · Moderador real para el motivo del ajuste de precio

## Qué cambió

- `INoteModerator` del módulo Requests ya no se registra como `UnavailableNoteModerator` (siempre «no disponible»): ahora es `ContentNoteModerator`, que pasa el texto al `IContentModerator` del Market (el adaptador de la spec 031, el mismo de la descripción de la solicitud).
- Con eso `POST /api/v1/cotizaciones/{id}/ajuste-precio` (AJU-01) deja de responder siempre 503: aprobado crea el ajuste (201), rechazado responde 422 `content_rejected` y la moderación caída sigue en 503 `moderation_unavailable`, sin guardar nada.
- La nota de la cotización (`POST /solicitudes/{id}/cotizaciones`, spec 020 R-15) usa el mismo moderador, así que también deja de fallar siempre cuando la moderación está encendida.
- Pruebas nuevas: `Spec049MotivoModeradoTests` en UnitTest (traducción de cada resultado) y en IntegrationTest (201, 422 y 503 sin reemplazar el moderador de notas). Se quitó `DefaultModeratorIsUnavailable`, que probaba la clase retirada.

## Por qué

- La spec 049 (R-08 y §6) y la 020 (R-15) ya dicen «Moderado»; no cambia ninguna regla ni el contrato. El moderador de notas nunca se conectó al de la spec 031, y las pruebas lo tapaban con `ApprovingNoteModerator`.
- Sin Azure encendido (`Moderacion:Habilitado` en falso) el Market usa solo la lista local, que falla cerrado: la ruta sigue en 503 hasta que se encienda la moderación. Eso es lo que pide la constitución (§2.13).

## A evitar

- Volver a registrar un moderador que apruebe sin revisar: lo que no se pudo moderar no se publica.
- El tope diario por persona (`LimitReached`) se traduce a 503 y no a 429 `moderation_limit`, porque `NoteModerationResult` solo tiene tres valores. Distinguirlo cambia las respuestas de dos rutas: consultarlo con Julián antes.
