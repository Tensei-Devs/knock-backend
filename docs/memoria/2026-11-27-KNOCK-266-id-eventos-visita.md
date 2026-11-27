# Una segunda propuesta o cancelar una visita bloqueada respondía 500

## Qué cambió
- `ReprogramacionPropuesta`, `VisitaReprogramada`, `ReprogramacionRechazada`, `ReprogramacionVencida` y `VisitaCerrada` llevan `_id` `{tipo}:{visitaId}:{version}` (spec 054, R-02).
- La visita cuenta sus propuestas en `propuestas`; la tarea `VencerPropuestaDeReprogramacion` usa `{visitaId}:{n}` y sigue aceptando las claves antiguas.

## Por qué
- El segundo evento repetía el `_id`, la transacción fallaba y la API respondía 500: también al cancelar una visita bloqueada y en la baja de cuenta.
- La segunda propuesta además no programaba su vencimiento, porque la tarea ya existía con la clave de la visita (spec 050, R-25).

## A evitar
- `OutboxMessage.Create` para un hecho que puede repetirse sobre el mismo agregado: usar `CreateVersion`.
- Decidir con la visita leída antes de la transacción si ya estaba bloqueada: se toma del documento anterior al cambio.

## Pendiente
- Los demás eventos del buzón de Mercado siguen con `{tipo}:{agregadoId}`: no se ha visto que se repitan.
- El PR #413 cambia las mismas líneas de cancelar y de cliente ausente en `VisitScheduleStore`: al unirlos van las dos cosas.
