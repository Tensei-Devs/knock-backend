# Spec 058: el migrador reporta cantidad y motivo de los fallidos, sin nombres

## Qué cambió
- `ArchivosMigrator.ReadLegacyAsync` entrega solo el contenido de cada JSON antiguo; se retiró `LegacyBlob` y con él el nombre del blob del reporte.
- `MigrateAsync` avisa por `onFailed` el motivo de cada fallido (`unreadable_json` o `unknown_state_or_usage`) y `ArchivosMigrationRunner` lo registra como advertencia sin nombres.
- `Spec058MigracionTests` (CA-15) captura los registros y comprueba que no hay nombres del JSON antiguo, ni de blob, ni ids.

## Por qué
- Enmienda firmada el 2026-11-24 de CA-15 y R-12 (T-10): ni siquiera el nombre del JSON antiguo se registra.

## A evitar
- No volver a pasar el nombre del blob a los registros ni a las métricas del migrador; para depurar un fallido se revisa el contenedor de origen en Azure, no el log.
