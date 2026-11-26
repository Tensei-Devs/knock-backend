# Evaluación del backend: respuestas verificadas

Fecha: 2026-11-26. Base: rama `sprint-05`, que va por delante de `main`. Las cifras de pruebas y cobertura son de una corrida completa del CI en verde sobre esa rama (45854521476). Cada respuesta indica dónde se comprueba y dice también lo que **no** hay.

El ambiente dev de Azure ejecuta el commit `cce78652`; `sprint-05` ya tiene cambios posteriores que todavía no están desplegados. Después del despliegue pasaron las pruebas de humo; los flujos con sesión no se probaron contra Azure.

## Infraestructura y despliegue

| Pregunta | Respuesta | Dónde se comprueba |
|---|---|---|
| Nube | Microsoft Azure: Container Apps, DocumentDB (API de MongoDB), Redis, Blob Storage, Key Vault, Container Registry y Application Insights. | `deploy/` |
| ¿Está desplegado? | Sí, en el ambiente dev: gateway, identity, messaging y market. | `deploy/apps/main.tf` |
| Servicios externos fuera de Azure | Firebase Authentication de Google (acceso por celular). Firebase Cloud Messaging está en el código, con los avisos push apagados. GitHub para el repositorio y el CI. | `src/` |
| Arquitectura | Tres servicios (Identity, Messaging y Market) detrás de un API Gateway con YARP; cada servicio tiene su propia base de datos. | `src/`, `specs/001` |
| Instancias | En dev, máximo una réplica por aplicación y escalan a cero cuando no hay uso. `evaluacion.tfvars` define dos réplicas, pero **no está aplicado**. | `deploy/apps/envs/` |
| Load balancer | El ingreso de Container Apps reparte entre réplicas; con una sola réplica no hay reparto. | `deploy/apps/` |
| IaC | Terraform (`deploy/infra`, `deploy/database`, `deploy/apps` y `deploy/modules`). | `deploy/` |
| SSL/TLS | HTTPS termina en el ingreso de Container Apps y HTTP redirige a HTTPS. El código envía HSTS fuera de `Development`; dev corre como `Development`, así que ahí no se envía. | `src/Knock.Hosting/Security/` |
| CDN de imágenes | **No hay CDN desplegado.** | — |
| Observabilidad | Application Insights, Log Analytics, una alerta métrica, OpenTelemetry y registros con Serilog con redacción de datos sensibles. | `deploy/`, `src/` |
| IA y moderación en Azure | El código integra Azure OpenAI (asistente) y Content Safety (moderación), pero **esos recursos no están desplegados ni encendidos**. El asistente responde con una plantilla; la moderación usa la lista local y lo que necesita Azure falla cerrado, así que en dev no se pueden confirmar imágenes. | `deploy/apps/main.tf`, `src/Knock.Market.Api/Modules/Moderation/` |

## Datos y caché

| Pregunta | Respuesta | Dónde se comprueba |
|---|---|---|
| Bases de datos | Azure DocumentDB (API de MongoDB) y Redis; ambas NoSQL. | `db/` |
| Redis y JWT | No se guarda el JWT. Redis guarda las sesiones canceladas (`sid`) para que se rechacen. | `src/Knock.Gateway/Hosting/` |
| Logout | `POST /api/v1/auth/logout` cancela la sesión. | `src/Knock.Identity.Api/Modules/Sesiones/` |
| Caché | Memoria del servicio, Redis y encabezados de caché en las respuestas que lo permiten; las privadas usan `no-store`. | `src/` |
| Rate limit | Límites de peticiones en el gateway y en los servicios. | `docs/rate-limit-matrix.md`, `src/Knock.Hosting/RateLimiting/` |
| Paginación | Por cursor (`cursor` y `nextCursor`). | `docs/openapi.publico.v1.json` |
| Índices | Simples, compuestos, únicos, parciales, TTL y geoespaciales (`2dsphere`). | `db/` |
| Serializador JSON | `System.Text.Json`. | `src/` |
| Compresión | Compresión JSON compartida en Identity y Market; excluye las rutas sensibles y, en Identity, las del panel. | `src/Knock.Hosting/Http/JsonCompressionMiddleware.cs` |
| Middleware | Encabezados de seguridad, correlación, redacción de registros, límite de peticiones y validación del token. | `src/Knock.Hosting/`, `src/*/Hosting/` |
| Versionamiento | La API se versiona por ruta (`/api/v1`). El versionamiento por etiquetas está especificado; el repositorio **todavía no tiene etiquetas ni releases**. | `specs/012` |
| Tiempo real | Chat con SignalR. | `src/Knock.Messaging.Api/Modules/Chat/` |

## Seguridad y flujo de trabajo

| Pregunta | Respuesta | Dónde se comprueba |
|---|---|---|
| Herramientas en CI | Gitleaks, Semgrep, Checkov, Trivy, Dockle, ZAP baseline, OSV-Scanner, auditoría de paquetes NuGet, revisión de dependencias, oasdiff, jscpd, actionlint y Stryker (informativo; puede cortarse por tiempo). SonarCloud solo en `main`. | `.github/workflows/` |
| Herramientas en CD | Al pasar el CI de `main`: acceso OIDC sin secretos guardados, Trivy, Dockle, imágenes etiquetadas con el SHA, actualización de Container Apps y pruebas de humo. No hay aprobación manual. El despliegue actual de dev se hizo a mano. | `.github/workflows/ci-deploy.yml` |
| Porcentaje de seguridad | No existe una norma que dé un porcentaje. **Estimación propia:** 15 de 21 controles (cerca del 70 %). | Fila siguiente |
| Controles de la estimación | **Presentes (15):** SAST, DAST, escaneo de secretos en CI, Key Vault, análisis de dependencias, escaneo de contenedores, escaneo de IaC, validación de entradas, JWT con revocación, autorización por recurso, límite de peticiones, TLS, encabezados, redacción de registros y `main` protegida. **Faltan (6):** secret scanning de GitHub, push protection, actualizaciones de seguridad de Dependabot, SBOM o firma de imágenes, DAST con sesión y WAF activo. | `.github/`, `src/`, `deploy/` |
| Límites del DAST | ZAP corre sin sesión, así que no recorre los endpoints protegidos. Hay una regla ignorada con una excepción que vence el 2026-12-25. | `security/exceptions.yaml`, `security/zap-rules.tsv` |
| ¿Nivel bancario o fintech? | **No.** No hay pentest, WAF activo, DAST autenticado ni firma de imágenes. | — |
| ¿Vulnerabilidades críticas? | No se conoce ninguna. Esto no equivale a un pentest. | CI |
| MCP | Atlassian (Jira). | `.mcp.json`, `docs/mcp.md` |
| Agentes de IA | 7 definiciones en `.agents/agents` (en `.opencode/agents` hay 11). | `.agents/`, `.opencode/` |
| Skills | 17 en `.agents/skills` (en `.opencode/skills` hay 25). | `.agents/`, `.opencode/` |
| Memoria del proyecto | `Memoria.md` y 153 entradas en `docs/memoria/`. | `Memoria.md` |
| Specs | 95 archivos `spec.md`. | `specs/` |

## Endpoints

| Dato | Cifra |
|---|---|
| Operaciones en los contratos de los servicios | 160 |
| De negocio | 142 (Market 78, Identity 49, Messaging 15) |
| Internas entre servicios | 16 |
| De salud | 2 |

El contrato público que consume la app está en `docs/openapi.publico.v1.json`.

**¿Endpoints sin spec?** Las 142 rutas de negocio aparecen citadas en algún `spec.md`. La comprobación es una búsqueda por texto.

## Pruebas

| Tipo | Pruebas | Proyecto |
|---|---|---|
| Unitarias (incluye 46 del proyecto de rendimiento) | 3209 | `tests/UnitTest`, `tests/PerformanceTest` |
| Integración | 1312 | `tests/IntegrationTest` |
| Seguridad | 581 | `tests/SecurityTest` |
| Base de datos | 282 | `tests/DatabaseTest` |
| Arquitectura | 65 | `tests/ArchitectureTest` |
| Contrato | 44 | `tests/ContractTest` |
| **Total** | **5493** | |

Cobertura combinada: **82.8 % de líneas y 72.7 % de ramas** (mínimos del CI: 60 % y 50 %). Además hay guiones k6 en `tests/perf/`, mutación con Stryker y pruebas de humo en `tests/smoke/`.

### Endpoints sin cada tipo de prueba

Medido al buscar la ruta literal en el código de las pruebas; una ruta armada por partes puede contar como faltante. Las unitarias no se miden por endpoint.

- Sin ruta literal en las pruebas de integración: 19 de 142.
- Sin ruta literal en las pruebas de seguridad: 100 de 142.
- Sin ruta literal en ninguna de las dos: 7.
  - `POST /visitas/{id}/reprogramar/respuesta`
  - `POST /visitas/{id}/terminar`
  - `POST /me/verificacion/{id}/enviar`
  - `POST /me/verificacion/{id}/fotos/{tipo}/confirmar`
  - `POST /admin/verificaciones/{id}/decision`
  - `GET /me/exportaciones/ultima`
  - `POST /avisos/leidos`
