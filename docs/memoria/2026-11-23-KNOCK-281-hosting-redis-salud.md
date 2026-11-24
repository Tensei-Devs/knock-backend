# 2026-11-23 · KNOCK-281 · Redis y salud pasan a Knock.Hosting (spec 055, T-16 parcial)

## Qué cambió
`RedisExtensions` (opciones, validador, conexión única y comprobación de readiness) y `HealthExtensions` (`AddKnockHealth`, `MapKnockHealth`, `MapKnockReadiness`) viven en `src/Knock.Hosting` (`Redis/` y `Health/`); se borraron las cuatro copias de Redis y las cuatro de salud. `AddKnockRedis(clientName)` recibe el nombre del cliente (`knock-api` en Mercado vía `MarketRedis.ClientName`, `knock-identity`, `knock-messaging` y `knock-gateway`), y `MapKnockReadiness(allowAnonymous)` conserva el `AllowAnonymous` que ya tenían Mensajería y el gateway. `KnockRedisOptions` y `RedisExtensions` son públicos para que los vea la prueba de base de datos. En `security/exceptions.yaml` EXC-008 y EXC-021 ya no nombran estos archivos; siguen vigentes por MongoDB y por las demás piezas.

## Por qué
Etapa 3 de la spec 055 (R-08): una sola implementación por pieza. La parte de MongoDB va aparte, a `Knock.Hosting.Data` (R-07a), para que el gateway no cargue el controlador.

## A evitar
- Registrar Redis sin nombre de cliente: cada servicio debe poder distinguirse en `CLIENT LIST`.
- Dejar MongoDB en `Knock.Hosting`: el gateway lo cargaría (CA-13).
