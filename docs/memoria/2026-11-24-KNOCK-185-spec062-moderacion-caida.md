# Spec 062: con la moderación caída la confirmación privada responde moderation_unavailable

## Qué cambió
- `MediaOutcome<T>.Unavailable` acepta un detalle; `MediaService.ConfirmAsync` marca con `MediaRejection.ModerationUnavailable` los tres caminos en que la moderación no responde (resultado `Unavailable`, tiempo agotado y cancelación ajena a la petición).
- `ImagenesController.ProblemaPrivado` traduce ese detalle a 503 `moderation_unavailable`; sin detalle (almacenamiento caído al emitir el enlace, módulo no construido) sigue siendo 503 `storage_unavailable`.
- Pruebas: `CA_062_07` del flujo con Azurite ahora espera `moderation_unavailable` y que el archivo siga `pendiente`; una prueba HTTP sin contenedores cubre el almacenamiento caído (`storage_unavailable`); la unitaria de `MediaService` comprueba la causa.

## Por qué
- La spec 062 firmada (CA-07, R-06) pide 503 `moderation_unavailable` con la moderación caída; el código respondía `storage_unavailable` por omisión y la prueba aceptaba esa contradicción. El contrato OpenAPI ya declara el 503 sin enumerar códigos, así que no cambia.

## A evitar
- No volver a usar un resultado `Unavailable` sin causa cuando falla la moderación: el cliente decide si reintentar según el código.
- No repetir el detalle interno en la respuesta; el código sale del mapa del controlador.
