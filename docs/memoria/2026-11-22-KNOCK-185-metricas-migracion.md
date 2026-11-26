# Métricas de la migración de archivos de Media (spec 058, R-12)

## Qué cambió
- `MediaMetrics` suma los contadores `knock.media.migration.migrated`, `already_present`, `expired_discarded` y `failed`, y el histograma `knock.media.migration.duration` en segundos.
- `ArchivosMigrator` los publica al terminar cada corrida, sin etiquetas; el informe `ArchivosMigrationReport` no cambia.
- La prueba `Migrate_PublishesCountersAndDurationWithoutTags_CA_058_19` verifica conteos, duración y ausencia de etiquetas.

## Por qué
El R-12 y el CA-19 piden contar migrados, ya migrados, caducados descartados y fallidos, y la duración, sin datos de personas ni nombres de blob.

## A evitar
- Agregar etiquetas con `sub`, `blobKey` o nombres de blob a estas métricas.
- Publicar la duración con el reloj de pared: se usa `Stopwatch` para que no dependa de cambios de hora.
