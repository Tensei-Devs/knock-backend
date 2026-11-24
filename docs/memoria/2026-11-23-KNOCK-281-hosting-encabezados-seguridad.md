# 2026-11-23 · KNOCK-281 · Encabezados de seguridad y HSTS pasan a Knock.Hosting (spec 055, T-17 parcial)

## Qué cambió
`SecurityHeadersMiddleware` (con `SecurityHeadersOptions` y `UseKnockSecurityHeaders`) y `HstsSettings`/`AddKnockHsts` viven en `Knock.Hosting.Security`; se borraron las cuatro copias del middleware y el `HstsExtensions` del gateway. Las diferencias reales entre copias son opciones: el gateway pasa el valor de `Strict-Transport-Security` (se fija aquí porque detrás del ingreso la petición llega como HTTP y `UseHsts` no respondería, BUG-002), Mercado e Identidad piden relajar la CSP en Development solo para `/scalar` y `/openapi`, y Mensajería no pide nada. En Development nunca se envía HSTS. Cambio de momento: el gateway lee `Security:Hsts` al construir la tubería y no al primer request, así que un valor inválido falla al arrancar, igual que ya exigía `ValidateOnStart`. Se ajusta EXC-021 (ya no nombra `SecurityHeadersMiddleware.cs`).

## Por qué
Etapa 3 de la spec 055 (R-08). Quedan para otros PR `ProblemResponses`, CORS y `RequestBodyLimitMiddleware`.

## A evitar
- Volver a copiar el middleware en un servicio.
- Enviar HSTS en Development.
