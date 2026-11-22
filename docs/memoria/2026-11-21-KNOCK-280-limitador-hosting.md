# Mover el limitador de peticiones a Knock.Hosting (KNOCK-280)

## Qué cambió
`SharedRateLimiter`, `RedisRequestCounter` e `IRequestCounter`, y la parte común de `RateLimitingExtensions` (opciones, 429, rutas y clave de cliente) pasan a `src/Knock.Hosting/RateLimiting`, con StackExchange.Redis 2.13.23 (misma versión). Identity, Market y Messaging solo declaran sus políticas por nombre (`IdentityRateLimits`, `MarketRateLimits`, `MessagingRateLimits`); el gateway conserva sus middlewares y usa el contador compartido. El aviso 247 se registra también cuando Redis no está registrado o no conectado, y un servicio de arranque abre la conexión a Redis al iniciar.

## Por qué
Spec 055, etapa 2 (R-08, CA-11): una sola implementación del limitador para retirar EXC-025 y la parte de rate limiting de EXC-021.

## A evitar
Copiar de nuevo el limitador en un servicio: `Servicios_Sin_Copias_De_La_Etapa_2_CA_055_11` falla. Cambiar los valores de límite aquí: esta tarea solo mueve el código.

## PARA VALIDACIÓN DE JOSÉ
Mensajería conserva el límite general y de `/health/ready` por IP en memoria por copia (como antes), con la opción `SharedGlobalCounters = false`; pasarlo a Redis cambia comportamiento y queda para otra spec. Alternativa descartada: unificarlo ahora (cambio de comportamiento fuera del alcance de R-08).