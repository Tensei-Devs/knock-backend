# Mapa de código

> Generado por `scripts/codemap.py` el 2026-11-04T20:21:20Z. No editar a mano.
> **Antes de crear una clase, método o archivo, busca aquí o corre `python scripts/codemap.py --query "<qué necesitas>"`.**

## Resumen

| Módulo | Capas | Archivos | Tipos | Métodos públicos |
|---|---|---|---|---|
| `Catalog` | -, controllers, data, domain, dtos, services, validators | 21 | 23 | 30 |
| `Knock.Contracts` | - | 2 | 3 | 6 |
| `Knock.Gateway` | - | 1 | 1 | 0 |
| `Knock.Identity.Api` | -, common, hosting | 25 | 51 | 65 |
| `Knock.Market.Api` | -, common, hosting | 25 | 51 | 65 |
| `Knock.Messaging.Api` | - | 1 | 0 | 0 |
| `Sesiones` | -, controllers, data, domain, dtos, services, validators | 23 | 43 | 42 |

## ⚠️ Código duplicado (misma estructura)

- `src/Knock.Identity.Api/Common/Errors/DomainException.cs:57 DomainErrors.ToStatusCode` · `src/Knock.Market.Api/Common/Errors/DomainException.cs:46 DomainErrors.ToStatusCode`
- `src/Knock.Identity.Api/Common/Security/JwtSettingsFactory.cs:15 JwtSettingsFactory.Create` · `src/Knock.Market.Api/Common/Security/JwtSettingsFactory.cs:14 JwtSettingsFactory.Create`
- `src/Knock.Identity.Api/Common/Security/JwtSettingsFactory.cs:30 JwtSettingsFactory.ResolveKey` · `src/Knock.Market.Api/Common/Security/JwtSettingsFactory.cs:29 JwtSettingsFactory.ResolveKey`
- `src/Knock.Identity.Api/Hosting/ApiVersioningExtensions.cs:8 ApiVersioningExtensions.AddKnockApiVersioning` · `src/Knock.Market.Api/Hosting/ApiVersioningExtensions.cs:7 ApiVersioningExtensions.AddKnockApiVersioning`
- `src/Knock.Identity.Api/Hosting/AuthenticationExtensions.cs:15 AuthenticationExtensions.AddKnockAuthentication` · `src/Knock.Market.Api/Hosting/AuthenticationExtensions.cs:14 AuthenticationExtensions.AddKnockAuthentication`
- `src/Knock.Identity.Api/Hosting/AuthenticationExtensions.cs:56 AuthenticationExtensions.AddInternalControllers` · `src/Knock.Market.Api/Hosting/AuthenticationExtensions.cs:55 AuthenticationExtensions.AddInternalControllers`
- `src/Knock.Identity.Api/Hosting/AuthenticationExtensions.cs:80 InternalControllerFeatureProvider.IsInternalController` · `src/Knock.Market.Api/Hosting/AuthenticationExtensions.cs:79 InternalControllerFeatureProvider.IsInternalController`
- `src/Knock.Identity.Api/Hosting/AuthenticationExtensions.cs:91 AuthenticationStartupCheck.StartAsync` · `src/Knock.Market.Api/Hosting/AuthenticationExtensions.cs:90 AuthenticationStartupCheck.StartAsync`
- `src/Knock.Identity.Api/Hosting/CorrelationIdMiddleware.cs:31 CorrelationIdMiddleware` · `src/Knock.Market.Api/Hosting/CorrelationIdMiddleware.cs:30 CorrelationIdMiddleware`
- `src/Knock.Identity.Api/Hosting/CorrelationIdMiddleware.cs:33 CorrelationIdMiddleware.InvokeAsync` · `src/Knock.Market.Api/Hosting/CorrelationIdMiddleware.cs:32 CorrelationIdMiddleware.InvokeAsync`
- `src/Knock.Identity.Api/Hosting/CorsMiddleware.cs:14 CorsMiddleware` · `src/Knock.Market.Api/Hosting/CorsMiddleware.cs:13 CorsMiddleware`
- `src/Knock.Identity.Api/Hosting/CorsMiddleware.cs:25 CorsMiddleware.InvokeAsync` · `src/Knock.Market.Api/Hosting/CorsMiddleware.cs:24 CorsMiddleware.InvokeAsync`
- `src/Knock.Identity.Api/Hosting/CorsMiddleware.cs:51 CorsMiddleware.AnswerPreflightAsync` · `src/Knock.Market.Api/Hosting/CorsMiddleware.cs:50 CorsMiddleware.AnswerPreflightAsync`
- `src/Knock.Identity.Api/Hosting/CorsMiddleware.cs:69 CorsMiddleware.IsGranted` · `src/Knock.Market.Api/Hosting/CorsMiddleware.cs:68 CorsMiddleware.IsGranted`
- `src/Knock.Identity.Api/Hosting/CorsMiddleware.cs:96 CorsMiddleware.AddVaryOrigin` · `src/Knock.Market.Api/Hosting/CorsMiddleware.cs:95 CorsMiddleware.AddVaryOrigin`
- `src/Knock.Identity.Api/Hosting/ForwardedHeadersExtensions.cs:16 ForwardedHeadersExtensions.AddKnockForwardedHeaders` · `src/Knock.Market.Api/Hosting/ForwardedHeadersExtensions.cs:15 ForwardedHeadersExtensions.AddKnockForwardedHeaders`
- `src/Knock.Identity.Api/Hosting/HealthExtensions.cs:20 HealthExtensions.MapKnockHealth` · `src/Knock.Market.Api/Hosting/HealthExtensions.cs:19 HealthExtensions.MapKnockHealth`
- `src/Knock.Identity.Api/Hosting/KnockApplicationExtensions.cs:9 KnockApplicationExtensions.AddKnockApi` · `src/Knock.Market.Api/Hosting/KnockApplicationExtensions.cs:8 KnockApplicationExtensions.AddKnockApi`
- `src/Knock.Identity.Api/Hosting/KnockApplicationExtensions.cs:31 KnockApplicationExtensions.UseKnockApi` · `src/Knock.Market.Api/Hosting/KnockApplicationExtensions.cs:30 KnockApplicationExtensions.UseKnockApi`
- `src/Knock.Identity.Api/Hosting/LoggingExtensions.cs:20 LoggingExtensions.AddKnockLogging` · `src/Knock.Market.Api/Hosting/LoggingExtensions.cs:19 LoggingExtensions.AddKnockLogging`
- `src/Knock.Identity.Api/Hosting/LoggingExtensions.cs:35 LoggingExtensions.ConfigureRequestLogging` · `src/Knock.Market.Api/Hosting/LoggingExtensions.cs:34 LoggingExtensions.ConfigureRequestLogging`
- `src/Knock.Identity.Api/Hosting/LoggingExtensions.cs:53 LoggingExtensions.RoutePath` · `src/Knock.Market.Api/Hosting/LoggingExtensions.cs:52 LoggingExtensions.RoutePath`
- `src/Knock.Identity.Api/Hosting/LoggingExtensions.cs:85 LoggingExtensions.OriginalRoute` · `src/Knock.Market.Api/Hosting/LoggingExtensions.cs:84 LoggingExtensions.OriginalRoute`
- `src/Knock.Identity.Api/Hosting/LoggingExtensions.cs:96 LoggingExtensions.IsVersion` · `src/Knock.Market.Api/Hosting/LoggingExtensions.cs:95 LoggingExtensions.IsVersion`
- `src/Knock.Identity.Api/Hosting/LoggingExtensions.cs:118 ScopeRequestPathEnricher.Enrich` · `src/Knock.Market.Api/Hosting/LoggingExtensions.cs:117 ScopeRequestPathEnricher.Enrich`
- `src/Knock.Identity.Api/Hosting/MongoExtensions.cs:68 KnockMongoOptionsValidator.Validate` · `src/Knock.Market.Api/Hosting/MongoExtensions.cs:67 KnockMongoOptionsValidator.Validate`
- `src/Knock.Identity.Api/Hosting/MongoExtensions.cs:136 MongoExtensions.AddKnockMongo` · `src/Knock.Market.Api/Hosting/MongoExtensions.cs:135 MongoExtensions.AddKnockMongo`
- `src/Knock.Identity.Api/Hosting/MongoExtensions.cs:158 MongoExtensions.AddKnockMongoDatabase` · `src/Knock.Market.Api/Hosting/MongoExtensions.cs:157 MongoExtensions.AddKnockMongoDatabase`
- `src/Knock.Identity.Api/Hosting/MongoExtensions.cs:172 MongoExtensions.CreateSettings` · `src/Knock.Market.Api/Hosting/MongoExtensions.cs:171 MongoExtensions.CreateSettings`
- `src/Knock.Identity.Api/Hosting/MongoExtensions.cs:183 MongoExtensions.Bind` · `src/Knock.Market.Api/Hosting/MongoExtensions.cs:182 MongoExtensions.Bind`
- `src/Knock.Identity.Api/Hosting/MongoExtensions.cs:215 MongoReadinessHealthCheck.CheckHealthAsync` · `src/Knock.Market.Api/Hosting/MongoExtensions.cs:214 MongoReadinessHealthCheck.CheckHealthAsync`
- `src/Knock.Identity.Api/Hosting/ObservabilityExtensions.cs:68 ObservabilityExtensions.AddKnockObservability` · `src/Knock.Market.Api/Hosting/ObservabilityExtensions.cs:67 ObservabilityExtensions.AddKnockObservability`
- `src/Knock.Identity.Api/Hosting/ObservabilityExtensions.cs:151 ObservabilityExtensions.ConfigureServerSpans` · `src/Knock.Market.Api/Hosting/ObservabilityExtensions.cs:150 ObservabilityExtensions.ConfigureServerSpans`
- `src/Knock.Identity.Api/Hosting/ObservabilityExtensions.cs:174 TelemetryExceptionRecorder.TryHandleAsync` · `src/Knock.Market.Api/Hosting/ObservabilityExtensions.cs:173 TelemetryExceptionRecorder.TryHandleAsync`
- `src/Knock.Identity.Api/Hosting/ObservabilityExtensions.cs:221 OpenTelemetryLogSink.Emit` · `src/Knock.Market.Api/Hosting/ObservabilityExtensions.cs:220 OpenTelemetryLogSink.Emit`
- `src/Knock.Identity.Api/Hosting/ObservabilityExtensions.cs:240 OpenTelemetryLogSink.Level` · `src/Knock.Market.Api/Hosting/ObservabilityExtensions.cs:239 OpenTelemetryLogSink.Level`
- `src/Knock.Identity.Api/Hosting/ProblemDetailsExtensions.cs:9 ProblemDetailsExtensions.AddKnockProblemDetails` · `src/Knock.Market.Api/Hosting/ProblemDetailsExtensions.cs:8 ProblemDetailsExtensions.AddKnockProblemDetails`
- `src/Knock.Identity.Api/Hosting/RateLimitingExtensions.cs:44 RateLimitingExtensions.AddKnockRateLimiting` · `src/Knock.Market.Api/Hosting/RateLimitingExtensions.cs:43 RateLimitingExtensions.AddKnockRateLimiting`
- `src/Knock.Identity.Api/Hosting/RateLimitingExtensions.cs:79 RateLimitingExtensions.GlobalPartition` · `src/Knock.Market.Api/Hosting/RateLimitingExtensions.cs:78 RateLimitingExtensions.GlobalPartition`
- `src/Knock.Identity.Api/Hosting/RateLimitingExtensions.cs:119 RateLimitingExtensions.IsAuthPath` · `src/Knock.Market.Api/Hosting/RateLimitingExtensions.cs:118 RateLimitingExtensions.IsAuthPath`
- `src/Knock.Identity.Api/Hosting/RateLimitingExtensions.cs:132 RateLimitingExtensions.ClientKey` · `src/Knock.Market.Api/Hosting/RateLimitingExtensions.cs:131 RateLimitingExtensions.ClientKey`
- `src/Knock.Identity.Api/Hosting/RequestBodyLimitMiddleware.cs:37 RequestBodyLimitMiddleware` · `src/Knock.Market.Api/Hosting/RequestBodyLimitMiddleware.cs:30 RequestBodyLimitMiddleware`
- `src/Knock.Identity.Api/Hosting/RequestBodyLimitMiddleware.cs:39 RequestBodyLimitMiddleware.InvokeAsync` · `src/Knock.Market.Api/Hosting/RequestBodyLimitMiddleware.cs:32 RequestBodyLimitMiddleware.InvokeAsync`
- `src/Knock.Identity.Api/Hosting/SecurityExtensions.cs:13 SecurityExtensions.AddKnockSecurity` · `src/Knock.Market.Api/Hosting/SecurityExtensions.cs:12 SecurityExtensions.AddKnockSecurity`
- `src/Knock.Identity.Api/Hosting/SecurityHeadersMiddleware.cs:12 SecurityHeadersMiddleware` · `src/Knock.Market.Api/Hosting/SecurityHeadersMiddleware.cs:11 SecurityHeadersMiddleware`
- `src/Knock.Identity.Api/Hosting/SecurityHeadersMiddleware.cs:25 SecurityHeadersMiddleware.Apply` · `src/Knock.Market.Api/Hosting/SecurityHeadersMiddleware.cs:24 SecurityHeadersMiddleware.Apply`
- `src/Knock.Identity.Api/Hosting/SecuritySettings.cs:50 SecuritySettingsValidator.Errors` · `src/Knock.Market.Api/Hosting/SecuritySettings.cs:49 SecuritySettingsValidator.Errors`
- `src/Knock.Identity.Api/Hosting/SecuritySettings.cs:84 SecuritySettingsValidator.IsValidOrigin` · `src/Knock.Market.Api/Hosting/SecuritySettings.cs:83 SecuritySettingsValidator.IsValidOrigin`
- `src/Knock.Identity.Api/Hosting/TelemetryRedactor.cs:40 TelemetryRedactor.Redact` · `src/Knock.Market.Api/Hosting/TelemetryRedactor.cs:39 TelemetryRedactor.Redact`
- `src/Knock.Identity.Api/Hosting/TelemetryRedactor.cs:70 TelemetryRedactor.RedactAttribute` · `src/Knock.Market.Api/Hosting/TelemetryRedactor.cs:69 TelemetryRedactor.RedactAttribute`
- `src/Knock.Identity.Api/Hosting/TelemetryRedactor.cs:94 TelemetryRedactor.RedactUrl` · `src/Knock.Market.Api/Hosting/TelemetryRedactor.cs:93 TelemetryRedactor.RedactUrl`
- `src/Knock.Identity.Api/Hosting/TelemetryRedactor.cs:116 TelemetryRedactor.ExceptionEvent` · `src/Knock.Market.Api/Hosting/TelemetryRedactor.cs:115 TelemetryRedactor.ExceptionEvent`
- `src/Knock.Identity.Api/Hosting/TelemetryRedactor.cs:162 CoordinatePairs.Redact` · `src/Knock.Market.Api/Hosting/TelemetryRedactor.cs:161 CoordinatePairs.Redact`
- `src/Knock.Identity.Api/Hosting/TelemetryRedactor.cs:187 CoordinatePairs.TryFirstNumber` · `src/Knock.Market.Api/Hosting/TelemetryRedactor.cs:186 CoordinatePairs.TryFirstNumber`
- `src/Knock.Identity.Api/Hosting/TelemetryRedactor.cs:212 CoordinatePairs.TrySecondNumber` · `src/Knock.Market.Api/Hosting/TelemetryRedactor.cs:211 CoordinatePairs.TrySecondNumber`
- `src/Knock.Identity.Api/Hosting/TelemetryRedactor.cs:247 CoordinatePairs.DecimalsEnd` · `src/Knock.Market.Api/Hosting/TelemetryRedactor.cs:246 CoordinatePairs.DecimalsEnd`
- `src/Knock.Identity.Api/Hosting/TelemetryRedactor.cs:280 TelemetryRedactionProcessor.OnEnd` · `src/Knock.Market.Api/Hosting/TelemetryRedactor.cs:279 TelemetryRedactionProcessor.OnEnd`
- `src/Knock.Identity.Api/Hosting/TelemetryRedactor.cs:313 LogRedactionProcessor.OnEnd` · `src/Knock.Market.Api/Hosting/TelemetryRedactor.cs:312 LogRedactionProcessor.OnEnd`
- `src/Knock.Identity.Api/Hosting/ValidationExtensions.cs:28 ValidationFilter.OnActionExecutionAsync` · `src/Knock.Market.Api/Hosting/ValidationExtensions.cs:27 ValidationFilter.OnActionExecutionAsync`
- `src/Knock.Identity.Api/Hosting/ValidationExtensions.cs:64 ValidationFilter.ToCamelCasePath` · `src/Knock.Market.Api/Hosting/ValidationExtensions.cs:63 ValidationFilter.ToCamelCasePath`
- `src/Knock.Identity.Api/Hosting/ValidationExtensions.cs:68 ValidationFilter.Collect` · `src/Knock.Market.Api/Hosting/ValidationExtensions.cs:67 ValidationFilter.Collect`

## Tipos con el mismo nombre en varios lugares

- `ApiVersioningExtensions`: `src/Knock.Identity.Api/Hosting/ApiVersioningExtensions.cs:6`, `src/Knock.Market.Api/Hosting/ApiVersioningExtensions.cs:5`
- `AuthenticationExtensions`: `src/Knock.Identity.Api/Hosting/AuthenticationExtensions.cs:10`, `src/Knock.Market.Api/Hosting/AuthenticationExtensions.cs:9`
- `AuthenticationStartupCheck`: `src/Knock.Identity.Api/Hosting/AuthenticationExtensions.cs:89`, `src/Knock.Market.Api/Hosting/AuthenticationExtensions.cs:88`
- `AzureMonitorExportStatus`: `src/Knock.Identity.Api/Hosting/ObservabilityExtensions.cs:39`, `src/Knock.Market.Api/Hosting/ObservabilityExtensions.cs:38`
- `AzureMonitorSignalSettings`: `src/Knock.Identity.Api/Hosting/ObservabilityExtensions.cs:36`, `src/Knock.Market.Api/Hosting/ObservabilityExtensions.cs:35`
- `CoordinatePairs`: `src/Knock.Identity.Api/Hosting/TelemetryRedactor.cs:157`, `src/Knock.Market.Api/Hosting/TelemetryRedactor.cs:156`
- `CorrelationId`: `src/Knock.Contracts/Correlation/CorrelationId.cs:9`, `src/Knock.Identity.Api/Hosting/CorrelationIdMiddleware.cs:8`, `src/Knock.Market.Api/Hosting/CorrelationIdMiddleware.cs:7`
- `CorrelationIdMiddleware`: `src/Knock.Contracts/Correlation/CorrelationIdMiddleware.cs:11`, `src/Knock.Identity.Api/Hosting/CorrelationIdMiddleware.cs:31`, `src/Knock.Market.Api/Hosting/CorrelationIdMiddleware.cs:30`
- `CorsMiddleware`: `src/Knock.Identity.Api/Hosting/CorsMiddleware.cs:14`, `src/Knock.Market.Api/Hosting/CorsMiddleware.cs:13`
- `CorsSettings`: `src/Knock.Identity.Api/Hosting/SecuritySettings.cs:21`, `src/Knock.Market.Api/Hosting/SecuritySettings.cs:20`
- `DomainErrorKind`: `src/Knock.Identity.Api/Common/Errors/DomainException.cs:5`, `src/Knock.Market.Api/Common/Errors/DomainException.cs:4`
- `DomainErrors`: `src/Knock.Identity.Api/Common/Errors/DomainException.cs:55`, `src/Knock.Market.Api/Common/Errors/DomainException.cs:44`
- `DomainException`: `src/Knock.Identity.Api/Common/Errors/DomainException.cs:18`, `src/Knock.Market.Api/Common/Errors/DomainException.cs:17`
- `DomainExceptionHandler`: `src/Knock.Identity.Api/Hosting/DomainExceptionHandler.cs:13`, `src/Knock.Market.Api/Hosting/DomainExceptionHandler.cs:12`
- `ForwardedHeadersExtensions`: `src/Knock.Identity.Api/Hosting/ForwardedHeadersExtensions.cs:7`, `src/Knock.Market.Api/Hosting/ForwardedHeadersExtensions.cs:6`
- `HealthExtensions`: `src/Knock.Identity.Api/Hosting/HealthExtensions.cs:6`, `src/Knock.Market.Api/Hosting/HealthExtensions.cs:5`
- `HstsSettings`: `src/Knock.Identity.Api/Hosting/SecuritySettings.cs:7`, `src/Knock.Market.Api/Hosting/SecuritySettings.cs:6`
- `HttpsSettings`: `src/Knock.Identity.Api/Hosting/SecuritySettings.cs:15`, `src/Knock.Market.Api/Hosting/SecuritySettings.cs:14`
- `IgnoreIncomingTraceContextPropagator`: `src/Knock.Identity.Api/Hosting/ObservabilityExtensions.cs:191`, `src/Knock.Market.Api/Hosting/ObservabilityExtensions.cs:190`
- `InternalControllerFeatureProvider`: `src/Knock.Identity.Api/Hosting/AuthenticationExtensions.cs:72`, `src/Knock.Market.Api/Hosting/AuthenticationExtensions.cs:71`
- `JwtSettings`: `src/Knock.Identity.Api/Common/Security/JwtSettings.cs:7`, `src/Knock.Market.Api/Common/Security/JwtSettings.cs:6`
- `JwtSettingsFactory`: `src/Knock.Identity.Api/Common/Security/JwtSettingsFactory.cs:11`, `src/Knock.Market.Api/Common/Security/JwtSettingsFactory.cs:10`
- `KnockApplicationExtensions`: `src/Knock.Identity.Api/Hosting/KnockApplicationExtensions.cs:7`, `src/Knock.Market.Api/Hosting/KnockApplicationExtensions.cs:6`
- `KnockMongoOptions`: `src/Knock.Identity.Api/Hosting/MongoExtensions.cs:15`, `src/Knock.Market.Api/Hosting/MongoExtensions.cs:14`
- `KnockMongoOptionsValidator`: `src/Knock.Identity.Api/Hosting/MongoExtensions.cs:63`, `src/Knock.Market.Api/Hosting/MongoExtensions.cs:62`
- `LogRedactionProcessor`: `src/Knock.Identity.Api/Hosting/TelemetryRedactor.cs:311`, `src/Knock.Market.Api/Hosting/TelemetryRedactor.cs:310`
- `LoggingExtensions`: `src/Knock.Identity.Api/Hosting/LoggingExtensions.cs:13`, `src/Knock.Market.Api/Hosting/LoggingExtensions.cs:12`
- `ModulesExtensions`: `src/Knock.Identity.Api/Hosting/ModulesExtensions.cs:6`, `src/Knock.Market.Api/Hosting/ModulesExtensions.cs:5`
- `MongoExtensions`: `src/Knock.Identity.Api/Hosting/MongoExtensions.cs:125`, `src/Knock.Market.Api/Hosting/MongoExtensions.cs:124`
- `MongoReadinessHealthCheck`: `src/Knock.Identity.Api/Hosting/MongoExtensions.cs:213`, `src/Knock.Market.Api/Hosting/MongoExtensions.cs:212`
- `ObservabilityExtensions`: `src/Knock.Identity.Api/Hosting/ObservabilityExtensions.cs:51`, `src/Knock.Market.Api/Hosting/ObservabilityExtensions.cs:50`
- `ObservabilityOptions`: `src/Knock.Identity.Api/Hosting/ObservabilityExtensions.cs:22`, `src/Knock.Market.Api/Hosting/ObservabilityExtensions.cs:21`
- `OpenApiExtensions`: `src/Knock.Identity.Api/Hosting/OpenApiExtensions.cs:6`, `src/Knock.Market.Api/Hosting/OpenApiExtensions.cs:5`
- `OpenTelemetryLogSink`: `src/Knock.Identity.Api/Hosting/ObservabilityExtensions.cs:212`, `src/Knock.Market.Api/Hosting/ObservabilityExtensions.cs:211`
- `ProblemDetailsExtensions`: `src/Knock.Identity.Api/Hosting/ProblemDetailsExtensions.cs:6`, `src/Knock.Market.Api/Hosting/ProblemDetailsExtensions.cs:5`
- `ProblemResponses`: `src/Knock.Identity.Api/Hosting/RequestBodyLimitMiddleware.cs:8`, `src/Knock.Market.Api/Hosting/RequestBodyLimitMiddleware.cs:7`
- `RateLimitPolicySettings`: `src/Knock.Identity.Api/Hosting/RateLimitingExtensions.cs:12`, `src/Knock.Market.Api/Hosting/RateLimitingExtensions.cs:11`
- `RateLimitSettings`: `src/Knock.Identity.Api/Hosting/RateLimitingExtensions.cs:22`, `src/Knock.Market.Api/Hosting/RateLimitingExtensions.cs:21`
- `RateLimitingExtensions`: `src/Knock.Identity.Api/Hosting/RateLimitingExtensions.cs:34`, `src/Knock.Market.Api/Hosting/RateLimitingExtensions.cs:33`
- `RequestBodyLimitMiddleware`: `src/Knock.Identity.Api/Hosting/RequestBodyLimitMiddleware.cs:37`, `src/Knock.Market.Api/Hosting/RequestBodyLimitMiddleware.cs:30`
- `ScopeRequestPathEnricher`: `src/Knock.Identity.Api/Hosting/LoggingExtensions.cs:114`, `src/Knock.Market.Api/Hosting/LoggingExtensions.cs:113`
- `SecurityExtensions`: `src/Knock.Identity.Api/Hosting/SecurityExtensions.cs:7`, `src/Knock.Market.Api/Hosting/SecurityExtensions.cs:6`
- `SecurityHeadersMiddleware`: `src/Knock.Identity.Api/Hosting/SecurityHeadersMiddleware.cs:12`, `src/Knock.Market.Api/Hosting/SecurityHeadersMiddleware.cs:11`
- `SecuritySettings`: `src/Knock.Identity.Api/Hosting/SecuritySettings.cs:29`, `src/Knock.Market.Api/Hosting/SecuritySettings.cs:28`
- `SecuritySettingsValidator`: `src/Knock.Identity.Api/Hosting/SecuritySettings.cs:41`, `src/Knock.Market.Api/Hosting/SecuritySettings.cs:40`
- `SerilogLogState`: `src/Knock.Identity.Api/Hosting/ObservabilityExtensions.cs:252`, `src/Knock.Market.Api/Hosting/ObservabilityExtensions.cs:251`
- `TelemetryExceptionRecorder`: `src/Knock.Identity.Api/Hosting/ObservabilityExtensions.cs:172`, `src/Knock.Market.Api/Hosting/ObservabilityExtensions.cs:171`
- `TelemetryRedactionProcessor`: `src/Knock.Identity.Api/Hosting/TelemetryRedactor.cs:275`, `src/Knock.Market.Api/Hosting/TelemetryRedactor.cs:274`
- `TelemetryRedactor`: `src/Knock.Identity.Api/Hosting/TelemetryRedactor.cs:21`, `src/Knock.Market.Api/Hosting/TelemetryRedactor.cs:20`
- `ValidationExtensions`: `src/Knock.Identity.Api/Hosting/ValidationExtensions.cs:9`, `src/Knock.Market.Api/Hosting/ValidationExtensions.cs:8`
- `ValidationFilter`: `src/Knock.Identity.Api/Hosting/ValidationExtensions.cs:24`, `src/Knock.Market.Api/Hosting/ValidationExtensions.cs:23`

## Índice por módulo

### Catalog

**-**

- `src/Knock.Market.Api/Modules/Catalog/CatalogModuleExtensions.cs` — class `CatalogModuleExtensions`
  - `AddCatalogModule()`:13
- `src/Knock.Market.Api/Modules/Catalog/ICatalogApi.cs` — interface `ICatalogApi`
- `src/Knock.Market.Api/Modules/Catalog/ModelBinders/SingleValueQueryModelBinder.cs` — class `SingleValueQueryModelBinder`, class `SingleValueFromQueryAttribute`
  - `BindModelAsync()`:16

**controllers**

- `src/Knock.Market.Api/Modules/Catalog/Controllers/V1/ColoniasController.cs` — class `ColoniasController`
  - `GetAtPoint()`:29
- `src/Knock.Market.Api/Modules/Catalog/Controllers/V1/OficiosController.cs` — class `OficiosController`
  - `List()`:22

**data**

- `src/Knock.Market.Api/Modules/Catalog/Data/CatalogSeeder.cs` — class `CatalogSeeder`
  - `CatalogSeeder()`:13, `SeedAsync()`:19, `SeedAsync()`:29
- `src/Knock.Market.Api/Modules/Catalog/Data/CatalogStore.cs` — class `CatalogStore`
  - `EnsureIndexesAsync()`:66, `FindActiveOficiosAsync()`:89, `FindIntersectingAsync()`:99, `FindNearestCenterAsync()`:103, `FindNamesByPostalCodeAsync()`:106, `ColoniaExistsAsync()`:112, `ActiveOficiosQuery()`:116, `IntersectingQuery()`:122, `NearestCenterQuery()`:129, `NamesByPostalCodeQuery()`:135, `ColoniaExistsQuery()`:141

**domain**

- `src/Knock.Market.Api/Modules/Catalog/Domain/CatalogCoordinates.cs` — class `CatalogCoordinates`
  - `ToPosition()`:7
- `src/Knock.Market.Api/Modules/Catalog/Domain/Colonia.cs` — record `Colonia`
  - `Colonia()`:7, `IsPostalCode()`:14
- `src/Knock.Market.Api/Modules/Catalog/Domain/GeoPosition.cs` — record struct `GeoPosition`
- `src/Knock.Market.Api/Modules/Catalog/Domain/Oficio.cs` — class `Oficio`
  - `IsValidModalidad()`:10
- `src/Knock.Market.Api/Modules/Catalog/Domain/OficioListing.cs` — record `OficioListing`
- `src/Knock.Market.Api/Modules/Catalog/Domain/SearchNameNormalizer.cs` — class `SearchNameNormalizer`
  - `Normalize()`:12

**dtos**

- `src/Knock.Market.Api/Modules/Catalog/Dtos/NeighborhoodAtPointQuery.cs` — class `NeighborhoodAtPointQuery`
- `src/Knock.Market.Api/Modules/Catalog/Dtos/NeighborhoodAtPointResponse.cs` — record `NeighborhoodAtPointResponse`, record `NeighborhoodAtPointColoniaResponse`
- `src/Knock.Market.Api/Modules/Catalog/Dtos/OficioResponse.cs` — record `OficioResponse`

**services**

- `src/Knock.Market.Api/Modules/Catalog/Services/CatalogApi.cs` — class `CatalogApi`
  - `FindNeighborhoodAtAsync()`:11, `FindNeighborhoodsByPostalCodeAsync()`:14, `NeighborhoodExistsAsync()`:25
- `src/Knock.Market.Api/Modules/Catalog/Services/INeighborhoodLocator.cs` — interface `INeighborhoodLocator`
- `src/Knock.Market.Api/Modules/Catalog/Services/NeighborhoodLocator.cs` — class `NeighborhoodLocator`
  - `FindAtAsync()`:19
- `src/Knock.Market.Api/Modules/Catalog/Services/OficioQueries.cs` — class `OficioQueries`
  - `OficioQueries()`:7, `ListActiveAsync()`:14

**validators**

- `src/Knock.Market.Api/Modules/Catalog/Validators/NeighborhoodAtPointQueryValidator.cs` — class `NeighborhoodAtPointQueryValidator`
  - `Parse()`:31

### Knock.Contracts

**-**

- `src/Knock.Contracts/Correlation/CorrelationId.cs` — class `CorrelationId`
  - `IsValid()`:24, `Create()`:44, `Current()`:47
- `src/Knock.Contracts/Correlation/CorrelationIdMiddleware.cs` — class `CorrelationIdMiddleware`, class `CorrelationIdApplicationBuilderExtensions`
  - `CorrelationIdMiddleware()`:11, `InvokeAsync()`:14, `UseKnockCorrelationId()`:43

### Knock.Gateway

**-**

- `src/Knock.Gateway/Program.cs` — class `Program`

### Knock.Identity.Api

**-**

- `src/Knock.Identity.Api/Program.cs` — —

**common**

- `src/Knock.Identity.Api/Common/Errors/DomainException.cs` — enum `DomainErrorKind`, class `DomainException`, class `DomainErrors`
  - `ToStatusCode()`:57
- `src/Knock.Identity.Api/Common/Security/JwtSettings.cs` — record `JwtSettings`
- `src/Knock.Identity.Api/Common/Security/JwtSettingsFactory.cs` — class `JwtSettingsFactory`
  - `Create()`:15

**hosting**

- `src/Knock.Identity.Api/Hosting/ApiVersioningExtensions.cs` — class `ApiVersioningExtensions`
  - `AddKnockApiVersioning()`:8
- `src/Knock.Identity.Api/Hosting/AuthenticationExtensions.cs` — class `AuthenticationExtensions`, class `InternalControllerFeatureProvider`, class `AuthenticationStartupCheck`
  - `AddKnockAuthentication()`:15, `AddInternalControllers()`:56, `IsController()`:74, `StartAsync()`:91, `StopAsync()`:109
- `src/Knock.Identity.Api/Hosting/CorrelationIdMiddleware.cs` — class `CorrelationId`, class `CorrelationIdMiddleware`
  - `IsValid()`:15, `Create()`:17, `Current()`:20, `CorrelationIdMiddleware()`:31, `InvokeAsync()`:33
- `src/Knock.Identity.Api/Hosting/CorsMiddleware.cs` — class `CorsMiddleware`
  - `CorsMiddleware()`:14, `InvokeAsync()`:25
- `src/Knock.Identity.Api/Hosting/DomainExceptionHandler.cs` — class `DomainExceptionHandler`
  - `TryHandleAsync()`:15
- `src/Knock.Identity.Api/Hosting/ForwardedHeadersExtensions.cs` — class `ForwardedHeadersExtensions`
  - `AddKnockForwardedHeaders()`:16
- `src/Knock.Identity.Api/Hosting/HealthExtensions.cs` — class `HealthExtensions`
  - `AddKnockHealth()`:14, `MapKnockHealth()`:20
- `src/Knock.Identity.Api/Hosting/KnockApplicationExtensions.cs` — class `KnockApplicationExtensions`
  - `AddKnockApi()`:9, `UseKnockApi()`:31
- `src/Knock.Identity.Api/Hosting/LoggingExtensions.cs` — class `LoggingExtensions`, class `ScopeRequestPathEnricher`
  - `AddKnockLogging()`:20, `ConfigureRequestLogging()`:35, `RoutePath()`:53, `Enrich()`:118
- `src/Knock.Identity.Api/Hosting/ModulesExtensions.cs` — class `ModulesExtensions`
  - `AddKnockModules()`:9
- `src/Knock.Identity.Api/Hosting/MongoExtensions.cs` — class `KnockMongoOptions`, class `KnockMongoOptionsValidator`, class `MongoExtensions`, class `MongoReadinessHealthCheck`
  - `Seconds()`:52, `Validate()`:68, `AddKnockMongo()`:136, `AddKnockMongoDatabase()`:158, `CreateSettings()`:172, `CheckHealthAsync()`:215
- `src/Knock.Identity.Api/Hosting/ObservabilityExtensions.cs` — class `ObservabilityOptions`, record `AzureMonitorSignalSettings`, class `AzureMonitorExportStatus`, class `ObservabilityExtensions`, class `TelemetryExceptionRecorder`, class `IgnoreIncomingTraceContextPropagator`, class `OpenTelemetryLogSink`, class `SerilogLogState`
  - `Record()`:47, `AddKnockObservability()`:68, `TryHandleAsync()`:174, `Inject()`:197, `ExtractTraceIdAndState()`:199, `ExtractBaggage()`:205, `Emit()`:221, `GetEnumerator()`:282
- `src/Knock.Identity.Api/Hosting/OpenApiExtensions.cs` — class `OpenApiExtensions`
  - `AddKnockOpenApi()`:8, `MapKnockOpenApi()`:15
- `src/Knock.Identity.Api/Hosting/ProblemDetailsExtensions.cs` — class `ProblemDetailsExtensions`
  - `AddKnockProblemDetails()`:9
- `src/Knock.Identity.Api/Hosting/RateLimitingExtensions.cs` — class `RateLimitPolicySettings`, class `RateLimitSettings`, class `RateLimitingExtensions`
  - `AddKnockRateLimiting()`:44, `ClientKey()`:132
- `src/Knock.Identity.Api/Hosting/RequestBodyLimitMiddleware.cs` — class `ProblemResponses`, class `RequestBodyLimitMiddleware`
  - `WriteAsync()`:10, `RequestBodyLimitMiddleware()`:37, `InvokeAsync()`:39
- `src/Knock.Identity.Api/Hosting/SecurityExtensions.cs` — class `SecurityExtensions`
  - `AddKnockSecurity()`:13
- `src/Knock.Identity.Api/Hosting/SecurityHeadersMiddleware.cs` — class `SecurityHeadersMiddleware`
  - `SecurityHeadersMiddleware()`:12, `InvokeAsync()`:18
- `src/Knock.Identity.Api/Hosting/SecuritySettings.cs` — class `HstsSettings`, class `HttpsSettings`, class `CorsSettings`, class `SecuritySettings`, class `SecuritySettingsValidator`
  - `Validate()`:47, `Errors()`:50, `IsValidOrigin()`:84
- `src/Knock.Identity.Api/Hosting/TelemetryRedactor.cs` — class `TelemetryRedactor`, class `CoordinatePairs`, class `TelemetryRedactionProcessor`, class `LogRedactionProcessor`
  - `IsSensitiveKey()`:38, `Redact()`:40, `RedactAttribute()`:70, `RedactUrl()`:94, `ExceptionEvent()`:116, `Redact()`:162, `OnEnd()`:280, `OnEnd()`:313
- `src/Knock.Identity.Api/Hosting/ValidationExtensions.cs` — class `ValidationExtensions`, class `ValidationFilter`
  - `AddKnockValidation()`:12, `OnActionExecutionAsync()`:28, `ToCamelCasePath()`:64

### Knock.Market.Api

**-**

- `src/Knock.Market.Api/Program.cs` — —

**common**

- `src/Knock.Market.Api/Common/Errors/DomainException.cs` — enum `DomainErrorKind`, class `DomainException`, class `DomainErrors`
  - `ToStatusCode()`:46
- `src/Knock.Market.Api/Common/Security/JwtSettings.cs` — record `JwtSettings`
- `src/Knock.Market.Api/Common/Security/JwtSettingsFactory.cs` — class `JwtSettingsFactory`
  - `Create()`:14

**hosting**

- `src/Knock.Market.Api/Hosting/ApiVersioningExtensions.cs` — class `ApiVersioningExtensions`
  - `AddKnockApiVersioning()`:7
- `src/Knock.Market.Api/Hosting/AuthenticationExtensions.cs` — class `AuthenticationExtensions`, class `InternalControllerFeatureProvider`, class `AuthenticationStartupCheck`
  - `AddKnockAuthentication()`:14, `AddInternalControllers()`:55, `IsController()`:73, `StartAsync()`:90, `StopAsync()`:108
- `src/Knock.Market.Api/Hosting/CorrelationIdMiddleware.cs` — class `CorrelationId`, class `CorrelationIdMiddleware`
  - `IsValid()`:14, `Create()`:16, `Current()`:19, `CorrelationIdMiddleware()`:30, `InvokeAsync()`:32
- `src/Knock.Market.Api/Hosting/CorsMiddleware.cs` — class `CorsMiddleware`
  - `CorsMiddleware()`:13, `InvokeAsync()`:24
- `src/Knock.Market.Api/Hosting/DomainExceptionHandler.cs` — class `DomainExceptionHandler`
  - `TryHandleAsync()`:14
- `src/Knock.Market.Api/Hosting/ForwardedHeadersExtensions.cs` — class `ForwardedHeadersExtensions`
  - `AddKnockForwardedHeaders()`:15
- `src/Knock.Market.Api/Hosting/HealthExtensions.cs` — class `HealthExtensions`
  - `AddKnockHealth()`:13, `MapKnockHealth()`:19
- `src/Knock.Market.Api/Hosting/KnockApplicationExtensions.cs` — class `KnockApplicationExtensions`
  - `AddKnockApi()`:8, `UseKnockApi()`:30
- `src/Knock.Market.Api/Hosting/LoggingExtensions.cs` — class `LoggingExtensions`, class `ScopeRequestPathEnricher`
  - `AddKnockLogging()`:19, `ConfigureRequestLogging()`:34, `RoutePath()`:52, `Enrich()`:117
- `src/Knock.Market.Api/Hosting/ModulesExtensions.cs` — class `ModulesExtensions`
  - `AddKnockModules()`:7
- `src/Knock.Market.Api/Hosting/MongoExtensions.cs` — class `KnockMongoOptions`, class `KnockMongoOptionsValidator`, class `MongoExtensions`, class `MongoReadinessHealthCheck`
  - `Seconds()`:51, `Validate()`:67, `AddKnockMongo()`:135, `AddKnockMongoDatabase()`:157, `CreateSettings()`:171, `CheckHealthAsync()`:214
- `src/Knock.Market.Api/Hosting/ObservabilityExtensions.cs` — class `ObservabilityOptions`, record `AzureMonitorSignalSettings`, class `AzureMonitorExportStatus`, class `ObservabilityExtensions`, class `TelemetryExceptionRecorder`, class `IgnoreIncomingTraceContextPropagator`, class `OpenTelemetryLogSink`, class `SerilogLogState`
  - `Record()`:46, `AddKnockObservability()`:67, `TryHandleAsync()`:173, `Inject()`:196, `ExtractTraceIdAndState()`:198, `ExtractBaggage()`:204, `Emit()`:220, `GetEnumerator()`:281
- `src/Knock.Market.Api/Hosting/OpenApiExtensions.cs` — class `OpenApiExtensions`
  - `AddKnockOpenApi()`:7, `MapKnockOpenApi()`:14
- `src/Knock.Market.Api/Hosting/ProblemDetailsExtensions.cs` — class `ProblemDetailsExtensions`
  - `AddKnockProblemDetails()`:8
- `src/Knock.Market.Api/Hosting/RateLimitingExtensions.cs` — class `RateLimitPolicySettings`, class `RateLimitSettings`, class `RateLimitingExtensions`
  - `AddKnockRateLimiting()`:43, `ClientKey()`:131
- `src/Knock.Market.Api/Hosting/RequestBodyLimitMiddleware.cs` — class `ProblemResponses`, class `RequestBodyLimitMiddleware`
  - `WriteAsync()`:9, `RequestBodyLimitMiddleware()`:30, `InvokeAsync()`:32
- `src/Knock.Market.Api/Hosting/SecurityExtensions.cs` — class `SecurityExtensions`
  - `AddKnockSecurity()`:12
- `src/Knock.Market.Api/Hosting/SecurityHeadersMiddleware.cs` — class `SecurityHeadersMiddleware`
  - `SecurityHeadersMiddleware()`:11, `InvokeAsync()`:17
- `src/Knock.Market.Api/Hosting/SecuritySettings.cs` — class `HstsSettings`, class `HttpsSettings`, class `CorsSettings`, class `SecuritySettings`, class `SecuritySettingsValidator`
  - `Validate()`:46, `Errors()`:49, `IsValidOrigin()`:83
- `src/Knock.Market.Api/Hosting/TelemetryRedactor.cs` — class `TelemetryRedactor`, class `CoordinatePairs`, class `TelemetryRedactionProcessor`, class `LogRedactionProcessor`
  - `IsSensitiveKey()`:37, `Redact()`:39, `RedactAttribute()`:69, `RedactUrl()`:93, `ExceptionEvent()`:115, `Redact()`:161, `OnEnd()`:279, `OnEnd()`:312
- `src/Knock.Market.Api/Hosting/ValidationExtensions.cs` — class `ValidationExtensions`, class `ValidationFilter`
  - `AddKnockValidation()`:11, `OnActionExecutionAsync()`:27, `ToCamelCasePath()`:63

### Knock.Messaging.Api

**-**

- `src/Knock.Messaging.Api/Program.cs` — —

### Sesiones

**-**

- `src/Knock.Identity.Api/Modules/Sesiones/SesionesModuleExtensions.cs` — class `SesionesModuleExtensions`
  - `AddSesionesModule()`:11

**controllers**

- `src/Knock.Identity.Api/Modules/Sesiones/Controllers/V1/AuthController.cs` — class `AuthController`
  - `ExchangeFirebaseToken()`:30
- `src/Knock.Identity.Api/Modules/Sesiones/Controllers/V1/LogoutController.cs` — class `LogoutController`
  - `Logout()`:20
- `src/Knock.Identity.Api/Modules/Sesiones/Controllers/V1/RefreshController.cs` — class `RefreshController`
  - `Refresh()`:27

**data**

- `src/Knock.Identity.Api/Modules/Sesiones/Data/ISesionesStore.cs` — interface `ISesionesStore`
- `src/Knock.Identity.Api/Modules/Sesiones/Data/SesionCancelada.cs` — interface `ISesionCancelada`, class `RedisSesionCancelada`
  - `Key()`:20, `CancelAsync()`:22
- `src/Knock.Identity.Api/Modules/Sesiones/Data/SesionesDataExtensions.cs` — class `SesionesDataExtensions`
  - `AddSesionesData()`:11
- `src/Knock.Identity.Api/Modules/Sesiones/Data/SesionesStore.cs` — class `SesionesStore`
  - `EnsureIndexesAsync()`:78, `FindBySubAsync()`:96, `FindByPhoneHashAsync()`:101, `CreateAccountAsync()`:109, `AddSessionAsync()`:150, `FindByIdAsync()`:203, `FindSessionByFingerprintAsync()`:214, `FindSessionByUsedFingerprintAsync()`:218, `RotateAsync()`:223, `RevokeAllAsync()`:247, `RevokeAsync()`:270, `IsDuplicateKey()`:284

**domain**

- `src/Knock.Identity.Api/Modules/Sesiones/Domain/PhoneNumberPolicy.cs` — class `PhoneNumberPolicy`
  - `IsValid()`:12
- `src/Knock.Identity.Api/Modules/Sesiones/Domain/SesionesDomain.cs` — class `AccountState`, class `ConsentDocuments`, record `UserAccount`, record `NewSession`, record `NewAccount`, enum `AccountCreation`, record `StoredSession`
  - `NewSession()`:34

**dtos**

- `src/Knock.Identity.Api/Modules/Sesiones/Dtos/SessionDtos.cs` — record `FirebaseExchangeRequest`, record `SessionResponse`, record `RefreshRequest`, record `RefreshResponse`

**services**

- `src/Knock.Identity.Api/Modules/Sesiones/Services/FirebaseIdTokenVerifier.cs` — class `FirebaseIdTokenVerifier`
  - `VerifyAsync()`:17, `Dispose()`:37
- `src/Knock.Identity.Api/Modules/Sesiones/Services/IFirebaseIdTokenVerifier.cs` — record `FirebaseIdentity`, interface `IFirebaseIdTokenVerifier`
  - `FirebaseIdentity()`:7, `ToString()`:9
- `src/Knock.Identity.Api/Modules/Sesiones/Services/LoginService.cs` — record `LoginRequest`, record `LoginResult`, class `ConsentVersionsOptions`, class `LoginService`
  - `LoginService()`:30, `LoginAsync()`:44
- `src/Knock.Identity.Api/Modules/Sesiones/Services/LogoutService.cs` — class `LogoutService`
  - `LogoutService()`:9, `LogoutAsync()`:20
- `src/Knock.Identity.Api/Modules/Sesiones/Services/PhoneProtectionOptions.cs` — class `PhoneProtectionOptions`, class `PhoneProtectionKeys`, class `PhoneProtectionOptionsValidator`
  - `TryCreate()`:56, `Validate()`:138
- `src/Knock.Identity.Api/Modules/Sesiones/Services/PhoneProtector.cs` — record `ProtectedPhone`, record `PhoneHash`, class `PhoneProtector`
  - `PhoneProtector()`:19, `Protect()`:29, `Hash()`:53, `AssociatedData()`:60
- `src/Knock.Identity.Api/Modules/Sesiones/Services/RefreshTokenFingerprint.cs` — class `RefreshTokenFingerprint`
  - `Compute()`:15, `TryCompute()`:36
- `src/Knock.Identity.Api/Modules/Sesiones/Services/SessionRefreshService.cs` — record `RefreshResult`, class `SessionRefreshService`
  - `SessionRefreshService()`:13, `RefreshAsync()`:20
- `src/Knock.Identity.Api/Modules/Sesiones/Services/SessionTokenIssuer.cs` — record `IssuedSession`, class `SessionTokenIssuer`
  - `SessionTokenIssuer()`:15, `Issue()`:21
- `src/Knock.Identity.Api/Modules/Sesiones/Services/UserIdDerivation.cs` — class `UserIdDerivation`
  - `Derive()`:18

**validators**

- `src/Knock.Identity.Api/Modules/Sesiones/Validators/FirebaseExchangeRequestValidator.cs` — class `FirebaseExchangeRequestValidator`
- `src/Knock.Identity.Api/Modules/Sesiones/Validators/RefreshRequestValidator.cs` — class `RefreshRequestValidator`
