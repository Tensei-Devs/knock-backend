# Mapa de código

> Generado por `scripts/codemap.py` el 2026-11-04T21:39:39Z. No editar a mano.
> **Antes de crear una clase, método o archivo, busca aquí o corre `python scripts/codemap.py --query "<qué necesitas>"`.**

## Resumen

| Módulo | Capas | Archivos | Tipos | Métodos públicos |
|---|---|---|---|---|
| `Catalog` | -, controllers, data, domain, dtos, services, validators | 21 | 23 | 30 |
| `Knock.Contracts` | - | 2 | 3 | 6 |
| `Knock.Gateway` | -, hosting | 7 | 22 | 27 |
| `Knock.Identity.Api` | -, hosting | 8 | 25 | 33 |
| `Knock.Market.Api` | -, common, hosting | 26 | 55 | 69 |
| `Knock.Messaging.Api` | -, hosting | 7 | 21 | 29 |

## ⚠️ Código duplicado (misma estructura)

- `src/Knock.Gateway/Hosting/LoggingExtensions.cs:20 LoggingExtensions.AddKnockLogging` · `src/Knock.Identity.Api/Hosting/LoggingExtensions.cs:20 LoggingExtensions.AddKnockLogging` · `src/Knock.Messaging.Api/Hosting/LoggingExtensions.cs:20 LoggingExtensions.AddKnockLogging`
- `src/Knock.Gateway/Hosting/LoggingExtensions.cs:35 LoggingExtensions.ConfigureRequestLogging` · `src/Knock.Identity.Api/Hosting/LoggingExtensions.cs:35 LoggingExtensions.ConfigureRequestLogging` · `src/Knock.Market.Api/Hosting/LoggingExtensions.cs:34 LoggingExtensions.ConfigureRequestLogging` · `src/Knock.Messaging.Api/Hosting/LoggingExtensions.cs:35 LoggingExtensions.ConfigureRequestLogging`
- `src/Knock.Gateway/Hosting/LoggingExtensions.cs:53 LoggingExtensions.RoutePath` · `src/Knock.Identity.Api/Hosting/LoggingExtensions.cs:53 LoggingExtensions.RoutePath` · `src/Knock.Market.Api/Hosting/LoggingExtensions.cs:52 LoggingExtensions.RoutePath` · `src/Knock.Messaging.Api/Hosting/LoggingExtensions.cs:53 LoggingExtensions.RoutePath`
- `src/Knock.Gateway/Hosting/LoggingExtensions.cs:85 LoggingExtensions.OriginalRoute` · `src/Knock.Identity.Api/Hosting/LoggingExtensions.cs:85 LoggingExtensions.OriginalRoute` · `src/Knock.Market.Api/Hosting/LoggingExtensions.cs:84 LoggingExtensions.OriginalRoute` · `src/Knock.Messaging.Api/Hosting/LoggingExtensions.cs:85 LoggingExtensions.OriginalRoute`
- `src/Knock.Gateway/Hosting/LoggingExtensions.cs:96 LoggingExtensions.IsVersion` · `src/Knock.Identity.Api/Hosting/LoggingExtensions.cs:96 LoggingExtensions.IsVersion` · `src/Knock.Market.Api/Hosting/LoggingExtensions.cs:95 LoggingExtensions.IsVersion` · `src/Knock.Messaging.Api/Hosting/LoggingExtensions.cs:96 LoggingExtensions.IsVersion`
- `src/Knock.Gateway/Hosting/LoggingExtensions.cs:118 ScopeRequestPathEnricher.Enrich` · `src/Knock.Identity.Api/Hosting/LoggingExtensions.cs:118 ScopeRequestPathEnricher.Enrich` · `src/Knock.Market.Api/Hosting/LoggingExtensions.cs:117 ScopeRequestPathEnricher.Enrich` · `src/Knock.Messaging.Api/Hosting/LoggingExtensions.cs:118 ScopeRequestPathEnricher.Enrich`
- `src/Knock.Gateway/Hosting/ObservabilityExtensions.cs:71 ObservabilityExtensions.AddKnockObservability` · `src/Knock.Identity.Api/Hosting/ObservabilityExtensions.cs:71 ObservabilityExtensions.AddKnockObservability` · `src/Knock.Market.Api/Hosting/ObservabilityExtensions.cs:67 ObservabilityExtensions.AddKnockObservability` · `src/Knock.Messaging.Api/Hosting/ObservabilityExtensions.cs:71 ObservabilityExtensions.AddKnockObservability`
- `src/Knock.Gateway/Hosting/ObservabilityExtensions.cs:154 ObservabilityExtensions.ConfigureServerSpans` · `src/Knock.Identity.Api/Hosting/ObservabilityExtensions.cs:154 ObservabilityExtensions.ConfigureServerSpans` · `src/Knock.Messaging.Api/Hosting/ObservabilityExtensions.cs:154 ObservabilityExtensions.ConfigureServerSpans`
- `src/Knock.Gateway/Hosting/ObservabilityExtensions.cs:177 TelemetryExceptionRecorder.TryHandleAsync` · `src/Knock.Identity.Api/Hosting/ObservabilityExtensions.cs:177 TelemetryExceptionRecorder.TryHandleAsync` · `src/Knock.Market.Api/Hosting/ObservabilityExtensions.cs:173 TelemetryExceptionRecorder.TryHandleAsync` · `src/Knock.Messaging.Api/Hosting/ObservabilityExtensions.cs:177 TelemetryExceptionRecorder.TryHandleAsync`
- `src/Knock.Gateway/Hosting/ObservabilityExtensions.cs:224 OpenTelemetryLogSink.Emit` · `src/Knock.Identity.Api/Hosting/ObservabilityExtensions.cs:224 OpenTelemetryLogSink.Emit` · `src/Knock.Market.Api/Hosting/ObservabilityExtensions.cs:220 OpenTelemetryLogSink.Emit` · `src/Knock.Messaging.Api/Hosting/ObservabilityExtensions.cs:224 OpenTelemetryLogSink.Emit`
- `src/Knock.Gateway/Hosting/ObservabilityExtensions.cs:243 OpenTelemetryLogSink.Level` · `src/Knock.Identity.Api/Hosting/ObservabilityExtensions.cs:243 OpenTelemetryLogSink.Level` · `src/Knock.Market.Api/Hosting/ObservabilityExtensions.cs:239 OpenTelemetryLogSink.Level` · `src/Knock.Messaging.Api/Hosting/ObservabilityExtensions.cs:243 OpenTelemetryLogSink.Level`
- `src/Knock.Gateway/Hosting/RedisExtensions.cs:41 KnockRedisOptionsValidator.Validate` · `src/Knock.Identity.Api/Hosting/RedisExtensions.cs:41 KnockRedisOptionsValidator.Validate` · `src/Knock.Market.Api/Hosting/RedisExtensions.cs:39 KnockRedisOptionsValidator.Validate`
- `src/Knock.Gateway/Hosting/RedisExtensions.cs:105 RedisExtensions.AddKnockRedis` · `src/Knock.Identity.Api/Hosting/RedisExtensions.cs:105 RedisExtensions.AddKnockRedis` · `src/Knock.Market.Api/Hosting/RedisExtensions.cs:103 RedisExtensions.AddKnockRedis`
- `src/Knock.Gateway/Hosting/RedisExtensions.cs:123 RedisExtensions.CreateConfiguration` · `src/Knock.Identity.Api/Hosting/RedisExtensions.cs:123 RedisExtensions.CreateConfiguration` · `src/Knock.Market.Api/Hosting/RedisExtensions.cs:121 RedisExtensions.CreateConfiguration`
- `src/Knock.Gateway/Hosting/RedisExtensions.cs:139 RedisExtensions.Bind` · `src/Knock.Identity.Api/Hosting/RedisExtensions.cs:139 RedisExtensions.Bind` · `src/Knock.Market.Api/Hosting/RedisExtensions.cs:137 RedisExtensions.Bind`
- `src/Knock.Gateway/Hosting/RedisExtensions.cs:162 RedisReadinessHealthCheck.CheckHealthAsync` · `src/Knock.Identity.Api/Hosting/RedisExtensions.cs:162 RedisReadinessHealthCheck.CheckHealthAsync` · `src/Knock.Market.Api/Hosting/RedisExtensions.cs:160 RedisReadinessHealthCheck.CheckHealthAsync`
- `src/Knock.Gateway/Hosting/TelemetryRedactor.cs:44 TelemetryRedactor.Redact` · `src/Knock.Identity.Api/Hosting/TelemetryRedactor.cs:44 TelemetryRedactor.Redact` · `src/Knock.Market.Api/Hosting/TelemetryRedactor.cs:39 TelemetryRedactor.Redact` · `src/Knock.Messaging.Api/Hosting/TelemetryRedactor.cs:44 TelemetryRedactor.Redact`
- `src/Knock.Gateway/Hosting/TelemetryRedactor.cs:74 TelemetryRedactor.RedactAttribute` · `src/Knock.Identity.Api/Hosting/TelemetryRedactor.cs:74 TelemetryRedactor.RedactAttribute` · `src/Knock.Market.Api/Hosting/TelemetryRedactor.cs:69 TelemetryRedactor.RedactAttribute` · `src/Knock.Messaging.Api/Hosting/TelemetryRedactor.cs:74 TelemetryRedactor.RedactAttribute`
- `src/Knock.Gateway/Hosting/TelemetryRedactor.cs:98 TelemetryRedactor.RedactUrl` · `src/Knock.Identity.Api/Hosting/TelemetryRedactor.cs:98 TelemetryRedactor.RedactUrl` · `src/Knock.Market.Api/Hosting/TelemetryRedactor.cs:93 TelemetryRedactor.RedactUrl` · `src/Knock.Messaging.Api/Hosting/TelemetryRedactor.cs:98 TelemetryRedactor.RedactUrl`
- `src/Knock.Gateway/Hosting/TelemetryRedactor.cs:120 TelemetryRedactor.ExceptionEvent` · `src/Knock.Identity.Api/Hosting/TelemetryRedactor.cs:120 TelemetryRedactor.ExceptionEvent` · `src/Knock.Market.Api/Hosting/TelemetryRedactor.cs:115 TelemetryRedactor.ExceptionEvent` · `src/Knock.Messaging.Api/Hosting/TelemetryRedactor.cs:120 TelemetryRedactor.ExceptionEvent`
- `src/Knock.Gateway/Hosting/TelemetryRedactor.cs:166 CoordinatePairs.Redact` · `src/Knock.Identity.Api/Hosting/TelemetryRedactor.cs:166 CoordinatePairs.Redact` · `src/Knock.Market.Api/Hosting/TelemetryRedactor.cs:161 CoordinatePairs.Redact` · `src/Knock.Messaging.Api/Hosting/TelemetryRedactor.cs:166 CoordinatePairs.Redact`
- `src/Knock.Gateway/Hosting/TelemetryRedactor.cs:191 CoordinatePairs.TryFirstNumber` · `src/Knock.Identity.Api/Hosting/TelemetryRedactor.cs:191 CoordinatePairs.TryFirstNumber` · `src/Knock.Market.Api/Hosting/TelemetryRedactor.cs:186 CoordinatePairs.TryFirstNumber` · `src/Knock.Messaging.Api/Hosting/TelemetryRedactor.cs:191 CoordinatePairs.TryFirstNumber`
- `src/Knock.Gateway/Hosting/TelemetryRedactor.cs:216 CoordinatePairs.TrySecondNumber` · `src/Knock.Identity.Api/Hosting/TelemetryRedactor.cs:216 CoordinatePairs.TrySecondNumber` · `src/Knock.Market.Api/Hosting/TelemetryRedactor.cs:211 CoordinatePairs.TrySecondNumber` · `src/Knock.Messaging.Api/Hosting/TelemetryRedactor.cs:216 CoordinatePairs.TrySecondNumber`
- `src/Knock.Gateway/Hosting/TelemetryRedactor.cs:251 CoordinatePairs.DecimalsEnd` · `src/Knock.Identity.Api/Hosting/TelemetryRedactor.cs:251 CoordinatePairs.DecimalsEnd` · `src/Knock.Market.Api/Hosting/TelemetryRedactor.cs:246 CoordinatePairs.DecimalsEnd` · `src/Knock.Messaging.Api/Hosting/TelemetryRedactor.cs:251 CoordinatePairs.DecimalsEnd`
- `src/Knock.Gateway/Hosting/TelemetryRedactor.cs:284 TelemetryRedactionProcessor.OnEnd` · `src/Knock.Identity.Api/Hosting/TelemetryRedactor.cs:284 TelemetryRedactionProcessor.OnEnd` · `src/Knock.Market.Api/Hosting/TelemetryRedactor.cs:279 TelemetryRedactionProcessor.OnEnd` · `src/Knock.Messaging.Api/Hosting/TelemetryRedactor.cs:284 TelemetryRedactionProcessor.OnEnd`
- `src/Knock.Gateway/Hosting/TelemetryRedactor.cs:317 LogRedactionProcessor.OnEnd` · `src/Knock.Identity.Api/Hosting/TelemetryRedactor.cs:317 LogRedactionProcessor.OnEnd` · `src/Knock.Market.Api/Hosting/TelemetryRedactor.cs:312 LogRedactionProcessor.OnEnd` · `src/Knock.Messaging.Api/Hosting/TelemetryRedactor.cs:317 LogRedactionProcessor.OnEnd`
- `src/Knock.Gateway/Hosting/TelemetryRedactor.cs:348 RedactingJsonFormatter.Format` · `src/Knock.Identity.Api/Hosting/TelemetryRedactor.cs:348 RedactingJsonFormatter.Format` · `src/Knock.Messaging.Api/Hosting/TelemetryRedactor.cs:348 RedactingJsonFormatter.Format`
- `src/Knock.Gateway/Hosting/TelemetryRedactor.cs:368 RedactingJsonFormatter.RedactNode` · `src/Knock.Identity.Api/Hosting/TelemetryRedactor.cs:368 RedactingJsonFormatter.RedactNode` · `src/Knock.Messaging.Api/Hosting/TelemetryRedactor.cs:368 RedactingJsonFormatter.RedactNode`
- `src/Knock.Identity.Api/Hosting/MongoExtensions.cs:76 KnockMongoOptionsValidator.Validate` · `src/Knock.Market.Api/Hosting/MongoExtensions.cs:74 KnockMongoOptionsValidator.Validate` · `src/Knock.Messaging.Api/Hosting/MongoExtensions.cs:76 KnockMongoOptionsValidator.Validate`
- `src/Knock.Identity.Api/Hosting/MongoExtensions.cs:149 MongoExtensions.AddKnockMongo` · `src/Knock.Market.Api/Hosting/MongoExtensions.cs:147 MongoExtensions.AddKnockMongo` · `src/Knock.Messaging.Api/Hosting/MongoExtensions.cs:149 MongoExtensions.AddKnockMongo`
- `src/Knock.Identity.Api/Hosting/MongoExtensions.cs:171 MongoExtensions.AddKnockMongoDatabase` · `src/Knock.Market.Api/Hosting/MongoExtensions.cs:169 MongoExtensions.AddKnockMongoDatabase` · `src/Knock.Messaging.Api/Hosting/MongoExtensions.cs:171 MongoExtensions.AddKnockMongoDatabase`
- `src/Knock.Identity.Api/Hosting/MongoExtensions.cs:185 MongoExtensions.CreateSettings` · `src/Knock.Market.Api/Hosting/MongoExtensions.cs:183 MongoExtensions.CreateSettings` · `src/Knock.Messaging.Api/Hosting/MongoExtensions.cs:185 MongoExtensions.CreateSettings`
- `src/Knock.Identity.Api/Hosting/MongoExtensions.cs:196 MongoExtensions.Bind` · `src/Knock.Market.Api/Hosting/MongoExtensions.cs:194 MongoExtensions.Bind` · `src/Knock.Messaging.Api/Hosting/MongoExtensions.cs:196 MongoExtensions.Bind`
- `src/Knock.Identity.Api/Hosting/MongoExtensions.cs:229 MongoReadinessHealthCheck.CheckHealthAsync` · `src/Knock.Market.Api/Hosting/MongoExtensions.cs:227 MongoReadinessHealthCheck.CheckHealthAsync` · `src/Knock.Messaging.Api/Hosting/MongoExtensions.cs:229 MongoReadinessHealthCheck.CheckHealthAsync`

## Tipos con el mismo nombre en varios lugares

- `AzureMonitorExportStatus`: `src/Knock.Gateway/Hosting/ObservabilityExtensions.cs:39`, `src/Knock.Identity.Api/Hosting/ObservabilityExtensions.cs:39`, `src/Knock.Market.Api/Hosting/ObservabilityExtensions.cs:38`, `src/Knock.Messaging.Api/Hosting/ObservabilityExtensions.cs:39`
- `AzureMonitorSignalSettings`: `src/Knock.Gateway/Hosting/ObservabilityExtensions.cs:36`, `src/Knock.Identity.Api/Hosting/ObservabilityExtensions.cs:36`, `src/Knock.Market.Api/Hosting/ObservabilityExtensions.cs:35`, `src/Knock.Messaging.Api/Hosting/ObservabilityExtensions.cs:36`
- `CoordinatePairs`: `src/Knock.Gateway/Hosting/TelemetryRedactor.cs:161`, `src/Knock.Identity.Api/Hosting/TelemetryRedactor.cs:161`, `src/Knock.Market.Api/Hosting/TelemetryRedactor.cs:156`, `src/Knock.Messaging.Api/Hosting/TelemetryRedactor.cs:161`
- `CorrelationId`: `src/Knock.Contracts/Correlation/CorrelationId.cs:9`, `src/Knock.Market.Api/Hosting/CorrelationIdMiddleware.cs:7`
- `CorrelationIdMiddleware`: `src/Knock.Contracts/Correlation/CorrelationIdMiddleware.cs:11`, `src/Knock.Market.Api/Hosting/CorrelationIdMiddleware.cs:30`
- `DataExtensions`: `src/Knock.Gateway/Hosting/DataExtensions.cs:7`, `src/Knock.Identity.Api/Hosting/DataExtensions.cs:7`, `src/Knock.Messaging.Api/Hosting/DataExtensions.cs:7`
- `HealthExtensions`: `src/Knock.Gateway/Hosting/HealthExtensions.cs:10`, `src/Knock.Identity.Api/Hosting/HealthExtensions.cs:10`, `src/Knock.Market.Api/Hosting/HealthExtensions.cs:5`, `src/Knock.Messaging.Api/Hosting/HealthExtensions.cs:10`
- `IgnoreIncomingTraceContextPropagator`: `src/Knock.Gateway/Hosting/ObservabilityExtensions.cs:194`, `src/Knock.Identity.Api/Hosting/ObservabilityExtensions.cs:194`, `src/Knock.Market.Api/Hosting/ObservabilityExtensions.cs:190`, `src/Knock.Messaging.Api/Hosting/ObservabilityExtensions.cs:194`
- `KnockMongoOptions`: `src/Knock.Identity.Api/Hosting/MongoExtensions.cs:16`, `src/Knock.Market.Api/Hosting/MongoExtensions.cs:14`, `src/Knock.Messaging.Api/Hosting/MongoExtensions.cs:16`
- `KnockMongoOptionsValidator`: `src/Knock.Identity.Api/Hosting/MongoExtensions.cs:71`, `src/Knock.Market.Api/Hosting/MongoExtensions.cs:69`, `src/Knock.Messaging.Api/Hosting/MongoExtensions.cs:71`
- `KnockRedisOptions`: `src/Knock.Gateway/Hosting/RedisExtensions.cs:15`, `src/Knock.Identity.Api/Hosting/RedisExtensions.cs:15`, `src/Knock.Market.Api/Hosting/RedisExtensions.cs:13`
- `KnockRedisOptionsValidator`: `src/Knock.Gateway/Hosting/RedisExtensions.cs:39`, `src/Knock.Identity.Api/Hosting/RedisExtensions.cs:39`, `src/Knock.Market.Api/Hosting/RedisExtensions.cs:37`
- `LogRedactionProcessor`: `src/Knock.Gateway/Hosting/TelemetryRedactor.cs:315`, `src/Knock.Identity.Api/Hosting/TelemetryRedactor.cs:315`, `src/Knock.Market.Api/Hosting/TelemetryRedactor.cs:310`, `src/Knock.Messaging.Api/Hosting/TelemetryRedactor.cs:315`
- `LoggingExtensions`: `src/Knock.Gateway/Hosting/LoggingExtensions.cs:13`, `src/Knock.Identity.Api/Hosting/LoggingExtensions.cs:13`, `src/Knock.Market.Api/Hosting/LoggingExtensions.cs:12`, `src/Knock.Messaging.Api/Hosting/LoggingExtensions.cs:13`
- `MongoExtensions`: `src/Knock.Identity.Api/Hosting/MongoExtensions.cs:138`, `src/Knock.Market.Api/Hosting/MongoExtensions.cs:136`, `src/Knock.Messaging.Api/Hosting/MongoExtensions.cs:138`
- `MongoReadinessHealthCheck`: `src/Knock.Identity.Api/Hosting/MongoExtensions.cs:227`, `src/Knock.Market.Api/Hosting/MongoExtensions.cs:225`, `src/Knock.Messaging.Api/Hosting/MongoExtensions.cs:227`
- `ObservabilityExtensions`: `src/Knock.Gateway/Hosting/ObservabilityExtensions.cs:51`, `src/Knock.Identity.Api/Hosting/ObservabilityExtensions.cs:51`, `src/Knock.Market.Api/Hosting/ObservabilityExtensions.cs:50`, `src/Knock.Messaging.Api/Hosting/ObservabilityExtensions.cs:51`
- `ObservabilityOptions`: `src/Knock.Gateway/Hosting/ObservabilityExtensions.cs:22`, `src/Knock.Identity.Api/Hosting/ObservabilityExtensions.cs:22`, `src/Knock.Market.Api/Hosting/ObservabilityExtensions.cs:21`, `src/Knock.Messaging.Api/Hosting/ObservabilityExtensions.cs:22`
- `OpenTelemetryLogSink`: `src/Knock.Gateway/Hosting/ObservabilityExtensions.cs:215`, `src/Knock.Identity.Api/Hosting/ObservabilityExtensions.cs:215`, `src/Knock.Market.Api/Hosting/ObservabilityExtensions.cs:211`, `src/Knock.Messaging.Api/Hosting/ObservabilityExtensions.cs:215`
- `RedactingJsonFormatter`: `src/Knock.Gateway/Hosting/TelemetryRedactor.cs:344`, `src/Knock.Identity.Api/Hosting/TelemetryRedactor.cs:344`, `src/Knock.Messaging.Api/Hosting/TelemetryRedactor.cs:344`
- `RedisExtensions`: `src/Knock.Gateway/Hosting/RedisExtensions.cs:93`, `src/Knock.Identity.Api/Hosting/RedisExtensions.cs:93`, `src/Knock.Market.Api/Hosting/RedisExtensions.cs:91`
- `RedisReadinessHealthCheck`: `src/Knock.Gateway/Hosting/RedisExtensions.cs:160`, `src/Knock.Identity.Api/Hosting/RedisExtensions.cs:160`, `src/Knock.Market.Api/Hosting/RedisExtensions.cs:158`
- `ScopeRequestPathEnricher`: `src/Knock.Gateway/Hosting/LoggingExtensions.cs:114`, `src/Knock.Identity.Api/Hosting/LoggingExtensions.cs:114`, `src/Knock.Market.Api/Hosting/LoggingExtensions.cs:113`, `src/Knock.Messaging.Api/Hosting/LoggingExtensions.cs:114`
- `SerilogLogState`: `src/Knock.Gateway/Hosting/ObservabilityExtensions.cs:255`, `src/Knock.Identity.Api/Hosting/ObservabilityExtensions.cs:255`, `src/Knock.Market.Api/Hosting/ObservabilityExtensions.cs:251`, `src/Knock.Messaging.Api/Hosting/ObservabilityExtensions.cs:255`
- `TelemetryExceptionRecorder`: `src/Knock.Gateway/Hosting/ObservabilityExtensions.cs:175`, `src/Knock.Identity.Api/Hosting/ObservabilityExtensions.cs:175`, `src/Knock.Market.Api/Hosting/ObservabilityExtensions.cs:171`, `src/Knock.Messaging.Api/Hosting/ObservabilityExtensions.cs:175`
- `TelemetryRedactionProcessor`: `src/Knock.Gateway/Hosting/TelemetryRedactor.cs:279`, `src/Knock.Identity.Api/Hosting/TelemetryRedactor.cs:279`, `src/Knock.Market.Api/Hosting/TelemetryRedactor.cs:274`, `src/Knock.Messaging.Api/Hosting/TelemetryRedactor.cs:279`
- `TelemetryRedactor`: `src/Knock.Gateway/Hosting/TelemetryRedactor.cs:25`, `src/Knock.Identity.Api/Hosting/TelemetryRedactor.cs:25`, `src/Knock.Market.Api/Hosting/TelemetryRedactor.cs:20`, `src/Knock.Messaging.Api/Hosting/TelemetryRedactor.cs:25`

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

- `src/Knock.Gateway/Hosting/DataExtensions.cs` — class `DataExtensions`
  - `AddKnockData()`:9
- `src/Knock.Gateway/Hosting/HealthExtensions.cs` — class `HealthExtensions`
  - `MapKnockReadiness()`:16
- `src/Knock.Gateway/Hosting/LoggingExtensions.cs` — class `LoggingExtensions`, class `ScopeRequestPathEnricher`
  - `AddKnockLogging()`:20, `ConfigureRequestLogging()`:35, `RoutePath()`:53, `Enrich()`:118
- `src/Knock.Gateway/Hosting/ObservabilityExtensions.cs` — class `ObservabilityOptions`, record `AzureMonitorSignalSettings`, class `AzureMonitorExportStatus`, class `ObservabilityExtensions`, class `TelemetryExceptionRecorder`, class `IgnoreIncomingTraceContextPropagator`, class `OpenTelemetryLogSink`, class `SerilogLogState`
  - `Record()`:47, `AddKnockObservability()`:71, `TryHandleAsync()`:177, `Inject()`:200, `ExtractTraceIdAndState()`:202, `ExtractBaggage()`:208, `Emit()`:224, `GetEnumerator()`:285
- `src/Knock.Gateway/Hosting/RedisExtensions.cs` — class `KnockRedisOptions`, class `KnockRedisOptionsValidator`, class `RedisExtensions`, class `RedisReadinessHealthCheck`
  - `Validate()`:41, `AddKnockRedis()`:105, `CreateConfiguration()`:123, `CheckHealthAsync()`:162
- `src/Knock.Gateway/Hosting/TelemetryRedactor.cs` — class `TelemetryRedactor`, class `CoordinatePairs`, class `TelemetryRedactionProcessor`, class `LogRedactionProcessor`, class `RedactingJsonFormatter`
  - `IsSensitiveKey()`:42, `Redact()`:44, `RedactAttribute()`:74, `RedactUrl()`:98, `ExceptionEvent()`:120, `Redact()`:166, `OnEnd()`:284, `OnEnd()`:317, `Format()`:348

### Knock.Identity.Api

**-**

- `src/Knock.Identity.Api/Program.cs` — —

**hosting**

- `src/Knock.Identity.Api/Hosting/DataExtensions.cs` — class `DataExtensions`
  - `AddKnockData()`:12
- `src/Knock.Identity.Api/Hosting/HealthExtensions.cs` — class `HealthExtensions`
  - `MapKnockReadiness()`:16
- `src/Knock.Identity.Api/Hosting/LoggingExtensions.cs` — class `LoggingExtensions`, class `ScopeRequestPathEnricher`
  - `AddKnockLogging()`:20, `ConfigureRequestLogging()`:35, `RoutePath()`:53, `Enrich()`:118
- `src/Knock.Identity.Api/Hosting/MongoExtensions.cs` — class `KnockMongoOptions`, class `KnockMongoOptionsValidator`, class `MongoExtensions`, class `MongoReadinessHealthCheck`
  - `Seconds()`:60, `Validate()`:76, `AddKnockMongo()`:149, `AddKnockMongoDatabase()`:171, `CreateSettings()`:185, `CheckHealthAsync()`:229
- `src/Knock.Identity.Api/Hosting/ObservabilityExtensions.cs` — class `ObservabilityOptions`, record `AzureMonitorSignalSettings`, class `AzureMonitorExportStatus`, class `ObservabilityExtensions`, class `TelemetryExceptionRecorder`, class `IgnoreIncomingTraceContextPropagator`, class `OpenTelemetryLogSink`, class `SerilogLogState`
  - `Record()`:47, `AddKnockObservability()`:71, `TryHandleAsync()`:177, `Inject()`:200, `ExtractTraceIdAndState()`:202, `ExtractBaggage()`:208, `Emit()`:224, `GetEnumerator()`:285
- `src/Knock.Identity.Api/Hosting/RedisExtensions.cs` — class `KnockRedisOptions`, class `KnockRedisOptionsValidator`, class `RedisExtensions`, class `RedisReadinessHealthCheck`
  - `Validate()`:41, `AddKnockRedis()`:105, `CreateConfiguration()`:123, `CheckHealthAsync()`:162
- `src/Knock.Identity.Api/Hosting/TelemetryRedactor.cs` — class `TelemetryRedactor`, class `CoordinatePairs`, class `TelemetryRedactionProcessor`, class `LogRedactionProcessor`, class `RedactingJsonFormatter`
  - `IsSensitiveKey()`:42, `Redact()`:44, `RedactAttribute()`:74, `RedactUrl()`:98, `ExceptionEvent()`:120, `Redact()`:166, `OnEnd()`:284, `OnEnd()`:317, `Format()`:348

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
  - `Seconds()`:58, `Validate()`:74, `AddKnockMongo()`:147, `AddKnockMongoDatabase()`:169, `CreateSettings()`:183, `CheckHealthAsync()`:227
- `src/Knock.Market.Api/Hosting/ObservabilityExtensions.cs` — class `ObservabilityOptions`, record `AzureMonitorSignalSettings`, class `AzureMonitorExportStatus`, class `ObservabilityExtensions`, class `TelemetryExceptionRecorder`, class `IgnoreIncomingTraceContextPropagator`, class `OpenTelemetryLogSink`, class `SerilogLogState`
  - `Record()`:46, `AddKnockObservability()`:67, `TryHandleAsync()`:173, `Inject()`:196, `ExtractTraceIdAndState()`:198, `ExtractBaggage()`:204, `Emit()`:220, `GetEnumerator()`:281
- `src/Knock.Market.Api/Hosting/OpenApiExtensions.cs` — class `OpenApiExtensions`
  - `AddKnockOpenApi()`:7, `MapKnockOpenApi()`:14
- `src/Knock.Market.Api/Hosting/ProblemDetailsExtensions.cs` — class `ProblemDetailsExtensions`
  - `AddKnockProblemDetails()`:8
- `src/Knock.Market.Api/Hosting/RateLimitingExtensions.cs` — class `RateLimitPolicySettings`, class `RateLimitSettings`, class `RateLimitingExtensions`
  - `AddKnockRateLimiting()`:43, `ClientKey()`:131
- `src/Knock.Market.Api/Hosting/RedisExtensions.cs` — class `KnockRedisOptions`, class `KnockRedisOptionsValidator`, class `RedisExtensions`, class `RedisReadinessHealthCheck`
  - `Validate()`:39, `AddKnockRedis()`:103, `CreateConfiguration()`:121, `CheckHealthAsync()`:160
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

**hosting**

- `src/Knock.Messaging.Api/Hosting/DataExtensions.cs` — class `DataExtensions`
  - `AddKnockData()`:12
- `src/Knock.Messaging.Api/Hosting/HealthExtensions.cs` — class `HealthExtensions`
  - `MapKnockReadiness()`:16
- `src/Knock.Messaging.Api/Hosting/LoggingExtensions.cs` — class `LoggingExtensions`, class `ScopeRequestPathEnricher`
  - `AddKnockLogging()`:20, `ConfigureRequestLogging()`:35, `RoutePath()`:53, `Enrich()`:118
- `src/Knock.Messaging.Api/Hosting/MongoExtensions.cs` — class `KnockMongoOptions`, class `KnockMongoOptionsValidator`, class `MongoExtensions`, class `MongoReadinessHealthCheck`
  - `Seconds()`:60, `Validate()`:76, `AddKnockMongo()`:149, `AddKnockMongoDatabase()`:171, `CreateSettings()`:185, `CheckHealthAsync()`:229
- `src/Knock.Messaging.Api/Hosting/ObservabilityExtensions.cs` — class `ObservabilityOptions`, record `AzureMonitorSignalSettings`, class `AzureMonitorExportStatus`, class `ObservabilityExtensions`, class `TelemetryExceptionRecorder`, class `IgnoreIncomingTraceContextPropagator`, class `OpenTelemetryLogSink`, class `SerilogLogState`
  - `Record()`:47, `AddKnockObservability()`:71, `TryHandleAsync()`:177, `Inject()`:200, `ExtractTraceIdAndState()`:202, `ExtractBaggage()`:208, `Emit()`:224, `GetEnumerator()`:285
- `src/Knock.Messaging.Api/Hosting/TelemetryRedactor.cs` — class `TelemetryRedactor`, class `CoordinatePairs`, class `TelemetryRedactionProcessor`, class `LogRedactionProcessor`, class `RedactingJsonFormatter`
  - `IsSensitiveKey()`:42, `Redact()`:44, `RedactAttribute()`:74, `RedactUrl()`:98, `ExceptionEvent()`:120, `Redact()`:166, `OnEnd()`:284, `OnEnd()`:317, `Format()`:348
