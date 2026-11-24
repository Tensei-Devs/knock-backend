# Spec 058: migrador ejecutable, prueba con Azurite y MongoDB, retiro verificado y contrato

## Qué cambió
- El migrador de la colección `archivos` (`ArchivosMigrator`) ya se puede ejecutar: `ArchivosMigrationRunner` lee el contenedor antiguo de registros, descarta con su cuarentena los pendientes vencidos y reporta los fallidos por el nombre del blob (nunca su contenido). Si el contenedor no existe termina en cero y no lo crea. `SourceCanBeRemoved` aplica la regla R-16: cero fallidos y cada registro leído migrado, ya presente o descartado.
- `ArchivosMigrationService` corre el migrador una vez al arrancar solo con `Media:Migracion:Habilitada=true`; el nombre del contenedor de origen va en `Media:Migracion:ContenedorOrigen` y no tiene valor por omisión (el código ya no menciona el contenedor retirado). Apagado, no pide ningún cliente de Blob.
- Pruebas nuevas: `Spec058MigracionTests` (CA-12 a CA-15, con Azurite y MongoDB reales, solo CI), CA-11 con los nombres exactos de blob en `MediaConfirmationBlobTests`, CA-20 con `explain` en `Spec058ArchivosScriptTests`, `Spec058RetiroTests` (arquitectura, CA-16), `Spec058ContractTests` (CA-21 y CA-23) y `Spec058FotosFormatoTests` (el validador de `fotos` acepta 32 hexadecimales y rechaza 24).

## Por qué
- La spec pedía «comando o bandera de configuración» y que no hubiera arranque automático obligatorio: una bandera apagada y explícita lo cumple sin tarea programada.
- La regla R-16 (cuándo se puede borrar el contenedor) necesitaba código y prueba, no solo texto.
- El validador de `fotos` y el OpenAPI ya aceptaban 32 hexadecimales (R-18 estaba aplicado); faltaba la prueba con el caso CA-23.

## A evitar
- No ejecutar la migración sin la revisión manual del contenedor en Azure (R-17); con cero JSON solo se borra el contenedor.
- El nombre del blob de un registro fallido se registra como advertencia: es el identificador del archivo, no un dato personal, pero la spec 058 R-12 pide no registrar nombres de blob. Pendiente de decisión.
- Borrar el contenedor de origen y la regla de ciclo de vida de `cuarentena` (T-09) las hace una persona en Azure; este cambio no toca Azure.
