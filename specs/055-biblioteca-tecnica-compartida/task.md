# Tareas 055 — Biblioteca técnica compartida (`Knock.Hosting`) (KNOCK-251, KNOCK-280, KNOCK-281)

> **Estado:** etapas 1 y 2 en `sprint-05`; etapa 3 pendiente. Conciliado contra el código de `sprint-05` el 2026-11-23: cada «hecha» cita su evidencia; lo que no pude comprobar queda «parcial» o «pendiente». Ninguna tarea nueva se ejecuta sin que una persona lo indique.

## Etapa 1 — KNOCK-251

| Tarea | Qué se hace | Cubre | Estado |
|---|---|---|---|
| T-01 | Enmienda a la constitución (versión 1.2.0) y spec Aprobada antes del primer commit de código | §12 | hecha (`constitution.md` §4 y estructura del repositorio nombran `Knock.Hosting` y `Knock.Hosting.Data`; aprobaciones de la spec) |
| T-02 | Crear `src/Knock.Hosting` (`net11.0`, `FrameworkReference` a ASP.NET Core), incluirlo en `Knock.slnx`, sin referencias a servicios ni a `Knock.Contracts` | R-01, R-02 · CA-01 | hecha (`src/Knock.Hosting/Knock.Hosting.csproj`; `HostingLibraryBoundaryTests.No_Referencia_Servicios_Ni_Contratos_CA_055_01`) |
| T-03 | Mover `AddKnockForwardedHeaders` y la lectura del gateway; borrar las copias | R-10, R-08 · CA-03, CA-04 | hecha (`src/Knock.Hosting/ForwardedHeaders`; `Gateway_Sin_Copia_Propia_De_Los_Encabezados_Reenviados_CA_055_03`) |
| T-04 | Mover el redactor (`TelemetryRedactor`, `RedactingJsonFormatter`, procesadores) y borrar las cuatro copias | R-11, R-08 · CA-03, CA-05 | hecha (`src/Knock.Hosting/Logging/TelemetryRedactor.cs`; ningún servicio define `TelemetryRedactor`) |
| T-05 | Pruebas de arquitectura de las fronteras de la biblioteca, con un caso negativo | R-03, R-06 · CA-02 | hecha (`tests/ArchitectureTest/HostingLibraryBoundaryTests.cs`: `Sin_Controladores_Ni_Modulos_CA_055_02` y `La_Regla_Puede_Fallar_CA_055_02`) |
| T-06 | Pruebas por proyecto de que cada uno usa la implementación compartida (`Spec055ForwardedHeadersTests`, `Spec055RedaccionTests`) y regresión de las specs 006, 011, 035 y BUG-005 | R-12 · CA-04, CA-05, CA-06 | parcial (conciliado 2026-11-23): no existen `Spec055ForwardedHeadersTests` ni `Spec055RedaccionTests`; CA-04 a CA-06 no tienen prueba con su ID. Evidencia parcial: `Spec006ForwardedHeadersTests`, `Spec035RedaccionTests` y `MarketConsoleRedactionTests` (regresión) y `HostingLibraryBoundaryTests` (CA_055_03: gateway y servicios sin copias) |
| T-07 | `Dockerfile` de los cuatro componentes copian `Knock.Hosting` y su `packages.lock.json` | R-14 · CA-09 | hecha (los 4 `Dockerfile`; Gateway, Identity y Market verificados línea a línea, Messaging por la misma plantilla) |
| T-08 | `SHARED` de `ci-pr.yml` y `ci-deploy.yml` incluye `^src/Knock\.Hosting/` y `CODEOWNERS` agrega `/src/Knock.Hosting/` | R-15 · CA-10 | pendiente de verificar (archivos protegidos; José autorizó tocar `.github/`) |
| T-09 | `**/Knock.Hosting/**/*.cs` en `mutate` de `stryker-config.json` | R-18 · CA-14 | hecha (`stryker-config.json`) |
| T-10 | Retirar EXC-020 y acotar EXC-007 (sin `TelemetryRedactor.cs`); regenerar `clones-baseline.json` | R-13 · CA-07, CA-08 | hecha en este cambio (`security/exceptions.yaml`: EXC-020 retirada, EXC-007 acotada); la base de clones se regenera con `python scripts/codemap.py --update-baseline` si el CI lo pide |
| T-11 | `Memoria.md` con la decisión y `docs/arquitectura.md` con la biblioteca | §12 | parcial (la constitución la nombra; revisar `docs/arquitectura.md`) |

## Etapa 2 — KNOCK-280

| Tarea | Qué se hace | Cubre | Estado |
|---|---|---|---|
| T-12 | Mover `SharedRateLimiter`, `RedisRequestCounter` y `RateLimitingExtensions`; borrar las copias | R-08 · CA-11 | hecha (`src/Knock.Hosting/RateLimiting`; PR #257) |
| T-13 | Retirar EXC-025 y la parte de rate limiting de EXC-021 | R-13 · CA-11 | hecha para EXC-025 (no existe en `security/exceptions.yaml`); EXC-021 sigue por las demás piezas |
| T-14 | `HostingLibraryBoundaryTests.Servicios_Sin_Copias_De_La_Etapa_2_CA_055_11` | CA-11 | hecha: `HostingLibraryBoundaryTests.Servicios_Sin_Copias_De_La_Etapa_2_CA_055_11` en `tests/ArchitectureTest` |
| — | `KeyedGate` (turnos por clave con relectura) agregado a `Knock.Hosting.Concurrency` por las auditorías de concurrencia (#270) | R-04 | **hecho sin estar en el catálogo de §3.2:** requiere la enmienda de R-04 (cambiar la spec con dos aprobaciones y entrada en `Memoria.md`) |

## Etapa 3 — KNOCK-281

| Tarea | Qué se hace | Cubre | Estado |
|---|---|---|---|
| T-15 | `LoggingExtensions` y `ObservabilityExtensions` a `Knock.Hosting` | R-08 · CA-12 | pendiente |
| T-16 | `RedisExtensions` y `HealthExtensions` a `Knock.Hosting`; `MongoExtensions` y la parte técnica de `DataExtensions` a `src/Knock.Hosting.Data` (no existe) | R-07a · CA-12, CA-13 | pendiente |
| T-17 | `SecurityHeadersMiddleware`, `HstsExtensions`, `ProblemResponses`, `CorsExtensions`/`CorsMiddleware`, `RequestBodyLimitMiddleware`, `JwtSettings` y la validación del JWT | R-08 · CA-12 | parcial: `JwtSettings`, `JwtSettingsFactory` y los parámetros de validación del JWT ya están en `Knock.Hosting.Security` (KNOCK-281); faltan `SecurityHeadersMiddleware`, `HstsExtensions`, `ProblemResponses`, CORS y `RequestBodyLimitMiddleware`, que difieren entre copias (CSP en desarrollo, HSTS del gateway, código `payload_too_large`, encabezados CORS permitidos) y se parametrizan en PR aparte |
| T-18 | `JsonCompressionMiddleware` y la validación de opciones de caché | R-08 · CA-12 | pendiente (copiados en Identity y Market) |
| T-19 | El resto de `Common/` y `Hosting/` de Identity copiado de Market | R-08 · CA-12 | pendiente |
| T-20 | Retirar EXC-007, EXC-008, EXC-015, EXC-016, EXC-018 y EXC-021; `**/Knock.Hosting.Data/**/*.cs` en `mutate` de Stryker | R-13, R-18 · CA-12 | pendiente (vencen EXC-015, EXC-016 y EXC-018 el 2026-12-04: renovarlas o hacer T-17 a T-19 antes) |
| T-21 | `HostingLibraryBoundaryTests.Servicios_Sin_Copias_Del_Catalogo_CA_055_12` y `Sin_MongoDB_En_Hosting_Ni_Gateway_CA_055_13` | CA-12, CA-13 | pendiente |

## Definición de terminado
- Los casos CA de `spec.md` tienen prueba con su ID; `python scripts/sdd_lint.py --trace 055` en OK, salvo CA-11 y CA-12 hasta sus etapas y los marcados «(comando)».
- Gates de `AGENTS.md §5` en verde, par revisor y `Memoria.md` al día.
