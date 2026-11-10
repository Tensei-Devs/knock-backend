# plan.md — BUG-004 — Mensajería sin JWT, encabezados de seguridad ni límite de peticiones propios

> Borrador. Falta la aprobación de la spec (KNOCK-241); no hay nada implementado.

## 1. Mapeo de archivos
- **Reutilizar:** `Hosting/AuthenticationExtensions`, `SecurityExtensions`, `RateLimitingExtensions` y `RevokedSessions` de Identidad/Mercado/gateway; `SecurityMarketFactory` y `SecurityHostConfiguration` como molde de pruebas. Antes de crear: `python scripts/codemap.py --query "jwt authentication rate limiting security headers"`.
- **Crear:** `src/Knock.Messaging.Api/Hosting/AuthenticationExtensions.cs`, `SecurityExtensions.cs`, `RateLimitingExtensions.cs`, `RevokedSessions.cs`; `tests/SecurityTest/Messaging/*` y su fábrica (extern alias `MessagingApi`); `tests/UnitTest/Messaging/AuthenticationStartupTests.cs`.
- **Modificar:** `src/Knock.Messaging.Api/Program.cs`, `Knock.Messaging.Api.csproj`, `appsettings.json`, `docs/rate-limit-matrix.md`, `docs/security-headers.md`, `docs/modelo-de-amenazas.md` (H-03), `Memoria.md`.
- **No tocar:** `.github/`, `scripts/`, `.agents/`, configuración de analizadores, `constitution.md`, `docs/openapi.v1.json` a mano, specs ajenas.

## 2. Riesgos y efectos secundarios
Copiar tres extensiones suma duplicados (cubierto por EXC-007, pero hay que registrar el nuevo origen). Una configuración nueva obligatoria hace que Mensajería no arranque si falta la llave en el entorno: hay que avisar al líder antes del despliegue.
