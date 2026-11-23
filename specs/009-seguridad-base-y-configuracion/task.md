# Tareas 009 — Seguridad base y configuración

> **Estado:** conciliado con el código el 2026-11-23: T-01 a T-06, T-08 y T-09 hechas, T-07 parcial (falta la prueba `CA_009_05`).

| Tarea | Qué se hace | Cubre | Estado |
|---|---|---|---|
| T-01 | Agregar la configuración tipada y su validación | R-01 | hecha: configuración tipada validada al arrancar; prueba `SecuritySettingsValidationTests` |
| T-02 | Agregar el middleware de cabeceras de seguridad | R-02 · CA-02 | hecha: cabeceras de seguridad en gateway y servicios; pruebas `GatewaySecurityTests` (CA_009_02), `HstsTests`, `SecurityHeadersTests` de Mensajería |
| T-03 | Configurar CORS y el límite de peticiones | R-03, R-04 | hecha: CORS (`CorsExtensions` del gateway, `CorsMiddleware` de Identidad) y límite de peticiones (`Knock.Hosting/RateLimiting`); pruebas `GatewaySecurityTests` (CA_009_07) y `Spec006SharedRateLimiterTests` |
| T-04 | Configurar la autenticación JWT y la autorización por defecto | R-05 · CA-01, CA-04 | hecha: JWT y autorización por defecto; pruebas `GatewaySecurityTests` (CA_009_01, CA_009_04) y `BearerValidationTests` |
| T-05 | Agregar el filtro que oculta datos personales en los registros | R-06 · CA-03 | hecha: `TelemetryRedactor` y filtros de registros; pruebas `GatewaySecurityTests` (CA_009_03), `TelemetryRedactorTests`, `Spec035RegistrosTests` |
| T-06 | Escribir las pruebas de seguridad negativa | R-01 a R-08 | hecha: suites de seguridad negativa en `tests/SecurityTest` (autenticación, gateway, identidad, mercado, mensajería, registros) |
| T-07 | Cifrar y buscar campos sensibles con AES-256-GCM y HMAC-SHA256 (llaves en Key Vault) | R-07 · CA-05 | parcial: `FieldCipher` (AES-GCM) y `PhoneProtector` (HMAC-SHA256 con llaves separadas) con pruebas `PhoneProtectorTests` (TC_HU_001_13 y 14); falta una prueba con el nombre `CA_009_05` que lea el campo directo de la base (`sdd_lint --trace 009` lo marca sin prueba) |
| T-08 | Cancelar la sesión (`sid`) en Redis y rechazar sus JWT en el gateway | R-08 · CA-06 | hecha: prueba `GatewaySecurityTests` (CA_009_06: el JWT de una sesión cancelada devuelve 401 y, si Redis cae, el paso se cierra con 503) |
| T-09 | Agregar `Retry-After` a las respuestas 429 | R-09 · CA-07 | hecha: prueba `GatewaySecurityTests` (CA_009_07, 429 con `Retry-After` en segundos) |

## Definición de terminado
- Los criterios de aceptación de `spec.md` están cubiertos por pruebas en verde.
- El CI está en verde, un par revisó el cambio y `Memoria.md` está al día.
