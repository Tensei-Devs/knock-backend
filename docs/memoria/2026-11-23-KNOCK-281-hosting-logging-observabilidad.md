# 2026-11-23 · KNOCK-281 · Registro y observabilidad pasan a Knock.Hosting (spec 055, T-15)

## Qué cambió
`LoggingExtensions` y `ObservabilityExtensions` (con `OpenTelemetryLogSink`, `SerilogLogState`, el propagador que ignora el `traceparent` entrante y el registrador de excepciones) viven ahora en `src/Knock.Hosting` (`Logging/` y `Observability/`); se borraron las cuatro copias del gateway, Identity, Market y Messaging. Cada proyecto llama a `AddKnockLogging(configuration, assembly)` y `AddKnockObservability(configuration, environment, assembly, nombreDeServicio, medidoresPropios)`: la categoría de respaldo del registro y la versión del servicio salen del ensamblado del servicio, el nombre por defecto (`knock-market` o `knock-api`) es un parámetro y Messaging pasa su medidor de chat. El comportamiento no cambia. `Knock.Hosting` suma las referencias a `Azure.Monitor.OpenTelemetry.Exporter` y a las instrumentaciones de ASP.NET Core y HTTP de OpenTelemetry con las mismas versiones que ya usaban los servicios, y los `Using` implícitos del SDK Web. Se retira EXC-007 porque ya no queda ninguna de sus copias. Las pruebas de Spec011, Spec035 y MarketConsoleRedaction apuntan a la implementación compartida (la de Spec035 queda en un solo caso por regla en lugar de cuatro idénticos).

## Por qué
Etapa 3 de la spec 055 (R-08): una sola implementación por pieza del catálogo y sin marcadores `jscpd:ignore`.

## A evitar
- Volver a copiar estas piezas en un servicio: la regla de piezas prohibidas de CA-12 las vigila al cerrar la etapa.
- Leer la versión del servicio desde el ensamblado de la biblioteca: se pasa el del servicio.
