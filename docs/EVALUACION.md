# Evaluación del backend: respuestas verificadas

Fecha: 2026-11-26. Base: rama `sprint-05` en el commit `75d5ad0e`, que va por delante de `main`. Las cifras de pruebas y cobertura son de la última corrida completa en verde de esa rama al escribir esto (45883514523, commit `853a3c38`). Cada respuesta indica dónde se comprueba y dice también lo que **no** hay.

El ambiente dev de Azure ejecuta el commit `cce78652` (ver [`VERSIONES_DESPLEGADAS.md`](VERSIONES_DESPLEGADAS.md)); `sprint-05` ya tiene cambios posteriores que todavía no están desplegados. Después del despliegue pasaron las pruebas de humo; los flujos con sesión no se probaron contra Azure.

## Infraestructura y despliegue

| Pregunta | Respuesta | Dónde se comprueba |
|---|---|---|
| Nube | Microsoft Azure: Container Apps, DocumentDB (API de MongoDB), Redis, Blob Storage, Key Vault, Container Registry y Application Insights. | `deploy/` |
| ¿Está desplegado? | Sí, en el ambiente dev: gateway, identity, messaging y market. El 26-nov `/health`, `/health/ready` y `/api/v1/oficios` respondieron 200 por el gateway. | `deploy/apps/main.tf` |
| Servicios externos fuera de Azure | Firebase Authentication de Google (acceso por celular). Firebase Cloud Messaging está en el código, con los avisos push apagados. GitHub para el repositorio y el CI, y Jira para la gestión. | `src/`, `deploy/apps/main.tf` |
| Arquitectura | Microservicios: tres servicios (Identity, Messaging y Market) detrás de un API Gateway con YARP; cada servicio tiene su propia base de datos. | `src/`, `specs/001` |
| Instancias | Terraform pone por omisión 1 réplica mínima y máxima por servicio, y Redis fija en 1. En la última comprobación registrada (23-nov) dev tenía mínimo 0 y máximo 1 por aplicación. Para la evaluación está previsto subir a 2 por servicio, pero **no consta que se haya aplicado**. El número en vivo hoy no se comprobó. | `deploy/apps/variables.tf`, `deploy/apps/main.tf` |
| Load balancer | El ingreso integrado de Container Apps reparte entre réplicas; no hay un balanceador aparte y con una sola réplica no hay reparto. | `deploy/modules/service/` |
| IaC | Terraform (`deploy/infra`, `deploy/database`, `deploy/apps`, `deploy/edge` y `deploy/modules`). El pipeline de despliegue no ejecuta `terraform apply`. | `deploy/`, `.github/workflows/ci-deploy.yml` |
| SSL/TLS | HTTPS termina en el ingreso de Container Apps y HTTP responde 301 a HTTPS. El código envía HSTS fuera de `Development`; dev corre como `Development` y el 26-nov el gateway **no envió** `Strict-Transport-Security`. | `src/Knock.Hosting/Security/`, `deploy/apps/variables.tf` |
| CDN de imágenes | **No hay CDN desplegado.** El módulo de Front Door existe, pero la suscripción lo rechazó. | `docs/memoria/2026-11-21-KNOCK-296-cdn-no-desplegado.md` |
| Observabilidad | Application Insights, Log Analytics, una alerta métrica de peticiones fallidas, OpenTelemetry y registros con Serilog con redacción de datos sensibles. **Faltan** las métricas y la alerta de la bandeja de salida (spec 054, T-07). | `deploy/modules/shared/`, `src/Knock.Hosting/` |
| IA en el producto | Un asistente sobre Azure OpenAI, **apagado**: responde con plantillas. No es un agente autónomo y no hay ML (ningún proyecto referencia ML.NET). | `deploy/apps/main.tf`, `src/Knock.Market.Api/` |
| Moderación | Content Safety **no está encendido**. En Market queda la lista local y lo que necesita Azure falla cerrado (en dev no se confirman imágenes). En Mensajería, la configuración de `Development` permite el modo sin revisión (`PermitirSinModeracion`), así que **el chat de dev no modera**, según la configuración del repositorio (no se probó en vivo). | `src/Knock.Market.Api/Modules/Moderation/`, `src/Knock.Messaging.Api/appsettings.Development.json` |

## Datos y caché

| Pregunta | Respuesta | Dónde se comprueba |
|---|---|---|
| Bases de datos | Azure DocumentDB (API de MongoDB) y Redis; ambas NoSQL. DocumentDB tiene acceso de red público, registrado como excepción. | `db/`, `deploy/database/`, `security/exceptions.yaml` |
| Redis y JWT | No se guarda el JWT. Redis guarda las sesiones canceladas (`sid`) para que se rechacen. | `src/Knock.Gateway/Hosting/RevokedSessions.cs` |
| Logout | `POST /api/v1/auth/logout` cancela la sesión. | `src/Knock.Identity.Api/Modules/Sesiones/` |
| Caché | Memoria del servicio, Redis y encabezados de caché en las respuestas que lo permiten; las privadas usan `no-store`. | `src/`, `docs/endpoints.md` |
| Rate limit | Límites de peticiones en el gateway y en los servicios, con contador en Redis. | `docs/rate-limit-matrix.md`, `src/Knock.Hosting/RateLimiting/` |
| Paginación | Por cursor (`cursor` y `nextCursor`); algunas listas acotadas no se paginan y lo justifica su spec. | `docs/openapi.publico.v1.json`, `docs/endpoints.md` |
| Índices y optimización | Simples, compuestos, únicos, parciales, TTL y geoespaciales (`2dsphere`). No se midieron planes de consulta de forma general. | `db/` |
| Serializador JSON | `System.Text.Json`. | `src/` |
| Compresión | Compresión JSON en Identity y Market; excluye `/api/v1/auth` y, en Identity, el panel. **Gateway y Messaging no comprimen.** | `src/Knock.Hosting/Http/JsonCompressionMiddleware.cs` |
| Middleware | Correlación, encabezados de seguridad, CORS, límite de cuerpo, redacción de registros, límites de peticiones y validación del token. | `src/Knock.Hosting/`, `src/*/Hosting/` |
| Versionamiento | La API se versiona por ruta (`/api/v1`). El versionamiento por puntos está especificado y el CI exige la etiqueta de puntos en cada PR, pero **todavía no hay etiquetas de Git, releases ni versiones `X.Y.Z` asignadas**: las imágenes se etiquetan con el SHA. | `specs/012`, `docs/VERSIONES_DESPLEGADAS.md` |
| Tiempo real | Chat con SignalR. | `src/Knock.Messaging.Api/Modules/Chat/` |

## Seguridad y flujo de trabajo

| Pregunta | Respuesta | Dónde se comprueba |
|---|---|---|
| Herramientas en CI | Gitleaks, Semgrep, Checkov, Trivy, Dockle, ZAP baseline, OSV-Scanner, auditoría de paquetes NuGet, revisión de dependencias, oasdiff, jscpd, actionlint y Stryker (informativo, no bloquea). El único check requerido en `main` es `gate`. | `.github/workflows/` |
| Herramientas en CD | Al pasar el CI de `main`: acceso OIDC sin secretos guardados, Trivy, Dockle, imágenes etiquetadas con el SHA, actualización de Container Apps y pruebas de humo. No hay aprobación manual en ese camino. El despliegue actual de dev se hizo a mano. | `.github/workflows/ci-deploy.yml` |
| CodeQL | **No se usa.** La configuración por omisión de GitHub no está activada y los análisis de code scanning revisados (los 100 más recientes) son todos de Semgrep. Fue una decisión del equipo (KNOCK-64). `codeql-action` aparece en el CI solo para subir el resultado de Semgrep. | `docs/QA_DEVSECOPS.md` §3.1, `.github/workflows/ci-pr.yml` |
| SonarCloud | **No corre.** El flujo está en `sprint-05` pero no en `main`, y solo se dispara desde ahí. | `.github/workflows/sonarcloud.yml` |
| Ajustes de GitHub | Secret scanning, push protection y actualizaciones de seguridad de Dependabot **desactivados**. Alertas de Dependabot activas, sin ninguna abierta. La pausa de los PR de actualización de versión está escrita en `sprint-05`, pero no surte efecto hasta llegar a `main`. | Ajustes del repositorio, `.github/dependabot.yml` |
| Porcentaje de seguridad | No existe una norma que dé un porcentaje. **Estimación propia:** 15 de 21 controles (cerca del 70 %). | Fila siguiente |
| Controles de la estimación | **Presentes (15):** SAST, DAST, escaneo de secretos en CI, Key Vault, análisis de dependencias, escaneo de contenedores, escaneo de IaC, validación de entradas, JWT con revocación, autorización por recurso, límite de peticiones, TLS, encabezados, redacción de registros y `main` protegida. **Faltan (6):** secret scanning de GitHub, push protection, actualizaciones de seguridad de Dependabot, SBOM o firma de imágenes, DAST con sesión y WAF activo. | `.github/`, `src/`, `deploy/` |
| DAST | ZAP baseline pasivo contra la pila local del CI: **solo 3 URL**, sin sesión, así que no recorre los endpoints protegidos (67 reglas pasan, 0 fallan y 1 se ignora con una excepción que vence el 2026-12-25). | `security/exceptions.yaml`, `security/zap-rules.tsv` |
| OWASP | Hay modelo de amenazas y una tabla de OWASP API Security Top 10 con control y prueba. No hay una matriz que demuestre cobertura de todos los endpoints. | `docs/modelo-de-amenazas.md`, `docs/QA_DEVSECOPS.md` §4 |
| ¿Nivel bancario o fintech? | **No.** No hay pentest, WAF activo, DAST autenticado ni firma de imágenes. | — |
| ¿Vulnerabilidades críticas? | No se conoce ninguna. Hay 1 alerta de code scanning abierta (Semgrep, severidad `error`, en `scripts/hooks/knock_guard.py` de `main`), registrada como falso positivo (EXC-001). Esto no equivale a un pentest. | CI, `security/exceptions.yaml` |
| MCP | Atlassian (Jira). El producto no expone MCP. | `.mcp.json`, `docs/mcp.md` |
| Agentes de IA (desarrollo) | 7 definiciones en `.agents/agents` (replicadas en `.claude` y `.github`) y 11 en `.opencode/agents`. | `.agents/`, `.opencode/` |
| Skills | 17 en `.agents/skills` y 25 en `.opencode/skills`. | `.agents/`, `.opencode/` |
| Memoria del proyecto | `Memoria.md` y una entrada por tarea en `docs/memoria/`. | `Memoria.md`, `docs/memoria/` |
| Specs | 95 archivos `spec.md`: 64 numeradas, 5 de defectos (`BUG-*`) y 26 históricas en `_legacy`. | `specs/` |

## Endpoints

Contados en los tres contratos OpenAPI por servicio.

| Dato | `75d5ad0e` (`sprint-05`) | `cce78652` (desplegado) |
|---|---|---|
| Operaciones en los contratos | 161 | 160 |
| De negocio | 142 (Market 78, Identity 49, Messaging 15) | 142 |
| Internas entre servicios | 17 (14 rutas distintas; algunas existen en más de un servicio) | 16 |
| `/health` | 2 | 2 |

Fuera de esa cuenta: `/health` del gateway, `/health/ready` y el hub `/hubs/chat`. El contrato público que consume la app (`docs/openapi.publico.v1.json`) tenía 130 operaciones en `75d5ad0e`; el PR #400 le agrega las 11 rutas raíz del gateway que faltaban (141). Que una ruta exista no significa que su función esté habilitada.

**¿Endpoints sin spec?** Las 142 rutas de negocio aparecen citadas en algún `spec.md`. La comprobación es una búsqueda por texto: no demuestra que cada spec sea anterior a su endpoint.

### Excel de endpoints (130 filas) frente a los contratos

18 filas no coinciden literalmente con una ruta de los contratos:

| ID del Excel | Ruta en el Excel | Cómo quedó | Estado |
|---|---|---|---|
| COL-02 | `GET /colonias/por-punto` | `GET /api/v1/colonias/en-punto` | Implementado con otra ruta |
| COL-03 | `GET /colonias/por-codigo-postal/{cp}` | `GET /api/v1/zonas/cp/{cp}` (spec 023) | Implementado con otra ruta |
| LIS-01 | `POST /lista-espera` | `POST /api/v1/zonas/lista-espera` (spec 023) | Implementado con otra ruta |
| VER-01, VER-02 | `POST /verificaciones`, `GET /verificaciones/me` | `POST` y `GET /api/v1/me/verificacion` (spec 025) | Implementado con otra ruta |
| ARC-01, ARC-02 | `POST /archivos/subida`, `.../{id}/confirmar` | `POST /api/v1/imagenes/subidas` y `.../{id}/confirmar` | Implementado con otra ruta |
| COT-04 | `POST /cotizaciones/{id}/elegir` | `POST /api/v1/solicitudes/{id}/elegir` (spec 021) | Implementado con otra ruta |
| VIS-08 | `POST /visitas/{id}/aviso-confianza` | `POST /api/v1/visitas/{id}/aviso` (spec 022) | Implementado con otra ruta |
| TEC-12 | `GET /tecnicos/me/resumen` | `GET /api/v1/tecnicos/me/trabajo` (spec 036) | Implementado con otra ruta |
| MSJ-03 | `GET /hubs/chat` | Hub SignalR `/hubs/chat` (spec 026) | Implementado como hub |
| SOL-08 | `GET /solicitudes/{id}/archivos` | El detalle de la solicitud entrega los enlaces (spec 048, R-11 y D-05) | Sin ruta propia, por decisión |
| CNV-05 | `POST /conversaciones/{id}/cerrar-por-eleccion` | Evento interno, sin ruta pública (spec 048, R-14 y D-07) | Sin ruta pública, por decisión |
| CON-01 a CON-03 | `/me/contactos-confianza` | Los contactos de confianza no se guardan en el servidor (spec 042, R-09 y D-04) | Retirados |
| INT-01 | La fila declara que no hay endpoint público | No aplica | No aplica |
| COL-01 | `GET /colonias` | Ver abajo | **No implementado** |

**COL-01 (`GET /colonias`).** El Excel lo pide como lista pública de colonias, con filtros `codigoPostal`, `q` (autocompletar) y `activa`, paginada por cursor. No hay controlador ni ruta en los contratos, ninguna spec lo define y no se encontró una llamada a esa ruta en la app (`sprint-05` del frontend). La consulta por código postal ya la cubre `GET /api/v1/zonas/cp/{cp}` (spec 023, con sesión); **no están cubiertos** el listado completo, la búsqueda por texto ni el filtro `activa`. No se implementa sin una spec aprobada: queda pendiente de que el equipo decida escribirla o retirar la fila del Excel.

## Pruebas

Corrida 45883514523 (`853a3c38`), sin fallos ni pruebas omitidas.

| Tipo | Pruebas | Proyecto |
|---|---|---|
| Unitarias | 3163 | `tests/UnitTest` |
| Rendimiento (categoría `Performance`, sin cobertura) | 46 | `tests/UnitTest` |
| Integración | 1319 | `tests/IntegrationTest` |
| Seguridad | 581 | `tests/SecurityTest` |
| Base de datos | 282 | `tests/DatabaseTest` |
| Arquitectura | 65 | `tests/ArchitectureTest` |
| Contrato | 44 | `tests/ContractTest` |
| **Total** | **5500** | |

Cobertura combinada: **82.9 % de líneas y 72.7 % de ramas** (mínimos del CI: 60 % y 50 %). Además hay pruebas de humo en `tests/smoke/` y guiones k6 en `tests/perf/`, que el CI no ejecuta.

**Mutación (Stryker):** informativa, no bloquea. Dos corridas del 26-nov sobre lo cambiado en su PR dieron 27.94 % (45837402343) y 0.00 % (45837615099). No es una puntuación del proyecto completo.

### Endpoints sin cada tipo de prueba

Medido sobre `75d5ad0e` al buscar la ruta literal en el código de las pruebas (sin distinguir el método HTTP ni medir ejecución); una ruta armada por partes cuenta como faltante y una prueba de enrutamiento del gateway cuenta como cita. Las unitarias prueban clases, no rutas, y no se miden por endpoint.

- Sin ruta literal en las pruebas de integración: 19 de 142.
- Sin ruta literal en las pruebas de seguridad: 99 de 142.
- Sin ruta literal en ninguna de las dos: 7.
  - `GET /me/exportaciones/ultima`
  - `POST /admin/verificaciones/{id}/decision`
  - `PUT /admin/oficios/{clave}`
  - `POST /avisos/{id}/leido`
  - `POST /avisos/leidos`
  - `POST /me/verificacion/{id}/enviar`
  - `POST /me/verificacion/{id}/fotos/{tipo}/confirmar`
