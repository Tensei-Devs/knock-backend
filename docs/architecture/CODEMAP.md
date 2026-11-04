# Mapa de código

> Generado por `scripts/codemap.py` el 2026-11-04T20:46:07Z. No editar a mano.
> **Antes de crear una clase, método o archivo, busca aquí o corre `python scripts/codemap.py --query "<qué necesitas>"`.**

## Resumen

| Módulo | Capas | Archivos | Tipos | Métodos públicos |
|---|---|---|---|---|
| `Catalog` | -, controllers, data, domain, dtos, services, validators | 21 | 23 | 30 |
| `Knock.Contracts` | - | 2 | 3 | 6 |
| `Knock.Gateway` | -, hosting | 10 | 22 | 25 |
| `Knock.Identity.Api` | - | 1 | 0 | 0 |
| `Knock.Market.Api` | -, common, hosting | 25 | 51 | 65 |
| `Knock.Messaging.Api` | - | 1 | 0 | 0 |

## ⚠️ Código duplicado (misma estructura)

- `src/Knock.Gateway/Hosting/CorsExtensions.cs:36 CorsExtensions.AddKnockGatewayForwardedHeaders` · `src/Knock.Market.Api/Hosting/ForwardedHeadersExtensions.cs:15 ForwardedHeadersExtensions.AddKnockForwardedHeaders`
- `src/Knock.Gateway/Hosting/ProblemResponses.cs:9 ProblemResponses.WriteAsync` · `src/Knock.Market.Api/Hosting/RequestBodyLimitMiddleware.cs:9 ProblemResponses.WriteAsync`

## Tipos con el mismo nombre en varios lugares

- `AuthenticationExtensions`: `src/Knock.Gateway/Hosting/AuthenticationExtensions.cs:42`, `src/Knock.Market.Api/Hosting/AuthenticationExtensions.cs:9`
- `CorrelationId`: `src/Knock.Contracts/Correlation/CorrelationId.cs:9`, `src/Knock.Market.Api/Hosting/CorrelationIdMiddleware.cs:7`
- `CorrelationIdMiddleware`: `src/Knock.Contracts/Correlation/CorrelationIdMiddleware.cs:11`, `src/Knock.Market.Api/Hosting/CorrelationIdMiddleware.cs:30`
- `HealthExtensions`: `src/Knock.Gateway/Hosting/HealthExtensions.cs:10`, `src/Knock.Market.Api/Hosting/HealthExtensions.cs:5`
- `ProblemResponses`: `src/Knock.Gateway/Hosting/ProblemResponses.cs:7`, `src/Knock.Market.Api/Hosting/RequestBodyLimitMiddleware.cs:7`
- `RateLimitSettings`: `src/Knock.Gateway/Hosting/RateLimitingExtensions.cs:20`, `src/Knock.Market.Api/Hosting/RateLimitingExtensions.cs:21`
- `RateLimitingExtensions`: `src/Knock.Gateway/Hosting/RateLimitingExtensions.cs:63`, `src/Knock.Market.Api/Hosting/RateLimitingExtensions.cs:33`
- `SecurityHeadersMiddleware`: `src/Knock.Gateway/Hosting/SecurityHeadersMiddleware.cs:10`, `src/Knock.Market.Api/Hosting/SecurityHeadersMiddleware.cs:11`

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

**hosting**

- `src/Knock.Gateway/Hosting/AuthenticationExtensions.cs` — record `GatewayJwtSettings`, class `AuthenticationExtensions`
  - `GatewayJwtSettings()`:9, `Create()`:13, `AddKnockGatewayAuthentication()`:47
- `src/Knock.Gateway/Hosting/CorsExtensions.cs` — class `CorsExtensions`
  - `AddKnockGatewayCors()`:12, `AddKnockGatewayForwardedHeaders()`:36
- `src/Knock.Gateway/Hosting/DataExtensions.cs` — class `DataExtensions`
  - `AddKnockData()`:9
- `src/Knock.Gateway/Hosting/HealthExtensions.cs` — class `HealthExtensions`
  - `MapKnockReadiness()`:16
- `src/Knock.Gateway/Hosting/ProblemResponses.cs` — class `ProblemResponses`
  - `WriteAsync()`:9, `AddKnockGatewayProblemDetails()`:25
- `src/Knock.Gateway/Hosting/RateLimitingExtensions.cs` — class `RateLimitPolicy`, class `RateLimitSettings`, interface `IRequestCounter`, class `RedisRequestCounter`, class `RateLimitingExtensions`, class `IpRateLimitMiddleware`, class `UserRateLimitMiddleware`, class `RateLimitCheck`
  - `HitAsync()`:50, `AddKnockGatewayRateLimiting()`:65, `ClientKey()`:78, `IpRateLimitMiddleware()`:106, `InvokeAsync()`:110, `IsAuthPath()`:133, `UserRateLimitMiddleware()`:144, `InvokeAsync()`:146, `RejectedAsync()`:162
- `src/Knock.Gateway/Hosting/RedisExtensions.cs` — class `KnockRedisOptions`, class `KnockRedisOptionsValidator`, class `RedisExtensions`, class `RedisReadinessHealthCheck`
  - `Validate()`:41, `AddKnockRedis()`:105, `CreateConfiguration()`:123, `CheckHealthAsync()`:162
- `src/Knock.Gateway/Hosting/RevokedSessions.cs` — interface `IRevokedSessions`, class `RedisRevokedSessions`
  - `IsRevokedAsync()`:19
- `src/Knock.Gateway/Hosting/SecurityHeadersMiddleware.cs` — class `SecurityHeadersMiddleware`
  - `SecurityHeadersMiddleware()`:10, `InvokeAsync()`:16

### Knock.Identity.Api

**-**

- `src/Knock.Identity.Api/Program.cs` — —

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
