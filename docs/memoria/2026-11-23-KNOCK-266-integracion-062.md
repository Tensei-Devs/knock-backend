# Spec 062: pruebas del flujo privado de fotos de solicitud

## Qué cambió
- Se agregan las pruebas de integración `CA_062_02`, `03`, `07`, `08` y `09` (`Spec062FotosPrivadasFlujoTests`, con Azurite y MongoDB reales y la API completa) y la de contrato `CA_062_10` (`Spec062ContractTests`).
- No cambia código de producción ni contratos; `task.md` de la 062 refleja lo cubierto.

## Por qué
- El trazador marcaba seis casos de la spec sin prueba aunque el flujo ya existía.

## A evitar
- Azurite no firma SAS de delegación: el enlace de subida firmado no se puede emitir en estas pruebas. El archivo pendiente se crea con `MediaService` y la subida del cliente se simula escribiendo en la cuarentena.
- La moderación caída responde 503 con `storage_unavailable` en la ruta privada, no `moderation_unavailable` como dice CA-07; la prueba solo fija el 503 y que nada pasa a «listo» hasta decidir cuál vale.
- El contrato publicado mantiene `url` como cadena requerida y `proposito` como cadena libre para no romper a los clientes; CA-10 pide otra cosa y queda por decidir.
- El host de la prueba usa una base propia para el módulo de archivos (`Mongo:Databases:Media`) para no escribir en la base compartida.
