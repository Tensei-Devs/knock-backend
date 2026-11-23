# Matriz de límites de peticiones

> TT-API-03 (KNOCK-37). Los valores salen de `src/Knock.Api/appsettings.json` (sección `RateLimiting`), sin secretos, y se afinan con el k6 de TT-QA-02.
> Orden en el pipeline: `docs/MIDDLEWARES.md`. Implementación: `src/Knock.Api/Hosting/RateLimitingExtensions.cs`.

## Políticas por IP

| Política | Alcance | Límite (punto de partida) | Ventana | Clave de partición |
|---|---|---|---|---|
| Global | Cualquier ruta salvo `/health` y `/health/ready` | 100 peticiones | 1 minuto (fija) | IP del cliente |
| Propia `/health/ready` | Solo `/health/ready` (TT-API-06) | 60 peticiones | 1 minuto (fija) | IP del cliente |
| Estricta `/auth` | `/api/v{n}/auth/*` | 10 peticiones | 1 minuto (fija) | IP del cliente |
| Estricta del panel (spec 033) | `/api/v{n}/panel/sesion/*` y `/api/v{n}/panel/activacion` | 10 peticiones | 1 minuto (fija) | IP del cliente (contador propio `ratelimit:panel-sesion:{ip}` en el gateway) |

**Mensajería** (`Knock.Messaging.Api`, BUG-004 / KNOCK-241) aplica el mismo mecanismo por IP con dos políticas, sin la estricta de `/auth` (no tiene rutas de sesión): **Global** 100 peticiones por minuto (cualquier ruta salvo `/health` y `/health/ready`) y **propia de `/health/ready`** 60 por minuto, configuradas en `RateLimiting:Global` y `RateLimiting:Ready` de su `appsettings.json`. Responde `429` con `Retry-After`. El chat de 30 mensajes por minuto por persona lo define la spec 026. **Límite conocido (aceptado):** el limitador vive en la memoria de cada réplica, así que con 2 réplicas el límite efectivo se duplica, igual que en Identidad y Mercado (se mueve a Redis en el Sprint 5). Además, la IP del cliente solo es la real cuando `ForwardedHeaders` confía en la red del ingreso (`ForwardedHeaders:KnownNetworks`, del #61, KNOCK-201, que Mensajería ya usa): si el despliegue no la fija, Mensajería ve la IP del proxy y el límite se comparte entre todos los clientes. La prueba de «otra IP no se ve afectada» (TC-BUG-004-04) fija la IP con un encabezado de prueba, no de punta a punta.

**Panel interno (spec 033, KNOCK-75).** Los cinco endpoints de sesión y activación del panel llevan la política estricta por IP de arriba, con la misma configuración (`RateLimiting:Auth`) y contador aparte del de `/auth`. El resto de `/api/v{n}/panel/**` y todo `/api/v{n}/admin/**` (spec 053) lleva la política por usuario del gateway (`RateLimiting:User`) con el `sub` del token del panel (contador propio `ratelimit:panel-user:{sub}`). Aparte de ellos, los fallos de TOTP se cuentan por IP en Redis (`panel:fallos-ip:{ip}`: 10 en 15 minutos dan 429 `limit_exceeded`) y por cuenta en MongoDB (5 en 15 minutos dan 429 `locked`); ver la spec 033, R-11.

- Las peticiones a `/auth/*` cuentan en las dos políticas (la estricta y la global).
- `/health` (sin comprobaciones) es la **única exención** del límite. `/health/ready` hace un ping a MongoDB por petición anónima, así que desde TT-API-06 (KNOCK-138) tiene su propia política por IP (60 por minuto) y no cuenta en la global.
- La prueba `PublicEndpointsRateLimitTests` (TC-TT-API-06-16) enumera los endpoints públicos de la API y exige 429 en todos salvo `/health`; un endpoint público nuevo hace fallar la prueba hasta que se documente. Hoy son `/health`, `/health/ready`, `POST /api/v1/auth/firebase`, `GET /api/v1/oficios` y, solo en Development, `/openapi/v1.json` y `/scalar`.
- Respuesta al superar el límite: `429` `application/problem+json` con `Retry-After` (segundos); el cuerpo es el mismo de los demás errores (`type`, `title`, `status`, `traceId`, `correlationId`) y **no** revela IP, clave de partición, límite ni contador.
- Los valores se configuran con `RateLimiting:Global:PermitLimit`, `RateLimiting:Global:Window`, `RateLimiting:Auth:PermitLimit`, `RateLimiting:Auth:Window`, `RateLimiting:Ready:PermitLimit` y `RateLimiting:Ready:Window` (`hh:mm:ss`). No hay valores por defecto en código: sin configuración válida la API no arranca.

## IP del cliente

- Sin proxies de confianza configurados se usa la IP de la conexión; `X-Forwarded-For` falso no mueve al cliente de partición. El middleware de ASP.NET Core procesa `X-Forwarded-For` y `X-Forwarded-Proto` (este último desde TT-API-06, solo de proxies de confianza; `X-Forwarded-Host` no); el encabezado `Forwarded` (RFC 7239) se **ignora**.
- Con `ForwardedHeaders:KnownProxies` o `ForwardedHeaders:KnownNetworks` configurados, solo las peticiones que llegan desde esas direcciones sustituyen la IP por la de `X-Forwarded-For`. El repositorio no trae valores de producción: una persona los define al desplegar. Mal configurado deja evadir el límite o hace que todos compartan la IP del proxy.
- IPv4 mapeada en IPv6 se normaliza a IPv4 y las direcciones IPv6 se agrupan por prefijo /64.

## Límites conocidos (aceptados)

- **Solo por IP, no por usuario.** Personas detrás de la misma IP (NAT) comparten el contador. El límite por usuario lo hará el gateway (TT-ARQ-02, KNOCK-154), que valida el JWT. El caso TC-TT-API-03-05 comprueba que hoy el límite no distingue por usuario.
- **Redis compartido con respaldo por proceso (KNOCK-247, spec 006 R-05).** Identity y Market cuentan las peticiones en Redis (claves `ratelimit:identity:*` y `ratelimit:market:*`, con el SHA-256 de la partición, sin IP ni `sub` en claro), así que el límite es el mismo con cualquier número de réplicas mientras Redis responde; el cuerpo y el `Retry-After` de la respuesta 429 no cambian. Si Redis no está conectado o falla, cada réplica usa de inmediato su propio límite en memoria y registra un aviso sin datos personales: durante esa degradación **el límite efectivo se multiplica por el número de réplicas** (hasta 200 por minuto por IP en el global y 20 en `/auth` con 2 réplicas) y R-05 se cumple solo en parte (decisión del Tech Lead). Un Market sin Redis configurado también usa solo memoria. Las secciones de más abajo que dicen «por proceso» describen ese respaldo; con Redis sano esas políticas por `sub` también se comparten entre réplicas.
- Memoria: cada IP distinta crea una partición que expira con su ventana; la clave es la IP validada, no datos libres del cliente.
- Fuera de alcance de esta matriz: bloqueo por intentos de código SMS y límites por número de teléfono (HU-001/HU-003, ADR-004) y chat 60/min por usuario (Messaging).

## Advertencias para el despliegue (TT-AZ)
- **Detrás de Container Apps la IP de conexión es la del proxy de entrada.** Sin `ForwardedHeaders:KnownProxies` o `ForwardedHeaders:KnownNetworks` configurados, todos los clientes compartirían la misma partición (100 por minuto global y 10 por minuto en `/auth`): una denegación de servicio autoinfligida. El despliegue (Terraform, ADR-003) debe fijar la red del ingress, por ejemplo `ForwardedHeaders__KnownNetworks__0=<CIDR del ingress>`.
- **No usar `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`:** haría que el host confiara en cualquier remitente y permitiría evadir el límite con `X-Forwarded-For`. El Dockerfile no la define y no debe definirse.
- Un valor de red demasiado amplio también permite evadir el límite: usar el CIDR mínimo del ingress.

- `/api/v1/colonias/en-punto` va bajo la política global por IP sin exención (TT-API-07, KNOCK-141).

## Crear solicitudes en Mercado (spec 020, KNOCK-59)

`POST /api/v1/solicitudes` tiene además una política por `sub` del JWT de **10 solicitudes por hora**, ventana fija, por proceso (`RateLimitingExtensions.CreateRequestPolicy`); la respuesta 429 incluye `Retry-After`. Como las demás políticas de Mercado vive en la memoria de cada réplica, así que con dos réplicas el límite efectivo se duplica. El cuerpo se limita a 8 KB (413 antes de llegar al servicio). Los límites de cotizar (3 por minuto por técnico) los define el controller de cotizaciones (KNOCK-74).

## Comparar y elegir cotización en Mercado (spec 021, KNOCK-82)

`GET /api/v1/solicitudes/{id}/cotizaciones` tiene una política por `sub` del JWT de **60 listados por minuto** y `POST /api/v1/solicitudes/{id}/elegir` de **10 elecciones por minuto**, ambas de ventana fija y por proceso (`RateLimitingExtensions.CompareQuotesPolicy` y `ChooseQuotePolicy`); la respuesta 429 incluye `Retry-After`. Como las demás políticas de Mercado viven en la memoria de cada réplica, con dos réplicas el límite efectivo se duplica. El cuerpo de la elección se limita a 8 KB (413). El control real contra una segunda cita no es el límite sino el índice único de `visitas` por `solicitudId` y la transacción.

## Visita segura en Mercado (spec 022, KNOCK-85)

`POST /api/v1/visitas/{id}/llegada` tiene una política por `sub` del JWT de **5 peticiones por minuto** por técnico, de ventana fija y por proceso (`RateLimitingExtensions.ArrivalPolicy`); la respuesta 429 incluye `Retry-After`. Como las demás políticas de Mercado vive en la memoria de cada réplica, con dos réplicas el límite efectivo se duplica. El control real contra la fuerza bruta del código de cuatro dígitos no es este límite sino los tres intentos por visita y el bloqueo, guardados en la visita. `GET /api/v1/visitas/{id}` y `POST /api/v1/visitas/{id}/aviso` no tienen política propia (decisión del 2026-11-12 para el aviso): rige solo el límite general. El cuerpo de la llegada se limita a 8 KB (413).

## Técnicos en Mercado (spec 046, KNOCK-48)

`POST /api/v1/tecnicos` tiene una política por `sub` del JWT de **5 peticiones por hora** (`RateLimitingExtensions.RegisterTechnicianPolicy`), ventana fija y por proceso; la respuesta 429 incluye `Retry-After`. Como las demás políticas de Mercado vive en la memoria de cada réplica, así que con dos réplicas el límite efectivo se duplica. El registro solo ocurre una vez por cuenta (índice único de `usuarioId`), así que el límite frena el abuso de validaciones y no los duplicados. El cuerpo se limita a 8 KB (413). Rigen además los límites globales del gateway y el límite por IP de Mercado.

`GET /api/v1/tecnicos/{id}` es pública y no tiene política propia: rigen los límites globales del gateway y el límite por IP de Mercado. La caché de 1 minuto del perfil reduce las lecturas a MongoDB por técnico.

`PUT /api/v1/tecnicos/me`, `PUT /api/v1/tecnicos/me/disponibilidad`, `GET`/`PUT /api/v1/tecnicos/me/horarios` y `GET /api/v1/tecnicos/me/trabajo` no tienen política propia: rigen los límites globales del gateway y el límite por IP de Mercado. Los cuerpos de las escrituras se limitan a 8 KB, 1 KB y 4 KB (413).

`GET /api/v1/tecnicos/autocompletar` es pública: la frena el límite global por IP del gateway y de Mercado (R-41) y la caché de 30 s por texto. `GET /api/v1/tecnicos/{id}/llega-a-mi-colonia` pide sesión y usa los límites globales; su caché de 1 minuto evita repetir la lectura. GET /api/v1/tecnicos es pública y no tiene política propia: rigen los límites globales del gateway y el límite por IP de Mercado; en el servidor, la respuesta se cachea 30 s por réplica con claves que no llevan datos de otra persona, y las escrituras del perfil invalidan la caché de lista (KNOCK-90).

## Lista de espera en Mercado (spec 023, KNOCK-47)

`GET /api/v1/zonas/cp/{cp}` requiere sesión y usa los límites globales del gateway por IP y usuario y el límite por IP de Mercado; no tiene política específica adicional.

`POST /api/v1/zonas/lista-espera` tiene además una política por `sub` del JWT de **5 peticiones por hora**, ventana fija, por proceso; la respuesta 429 incluye `Retry-After`. Con una réplica el límite es de 5; con dos puede alcanzar 10 por hora. El límite compartido en Redis queda para KNOCK-247. Se mantienen los límites globales del gateway y el límite por IP de Mercado.

## Cotizaciones en Mercado (spec 020, KNOCK-74)

`POST /api/v1/solicitudes/{id}/cotizaciones` añade una ventana fija de **3 peticiones por minuto por `sub`** y por proceso, además de los límites generales. La cuarta responde 429 con `Retry-After`. Con dos réplicas pueden pasar hasta 6 por minuto; el límite compartido queda para KNOCK-247.

## Descarte en el gateway (spec 028, KNOCK-68)

`POST /api/v1/solicitudes/{id}/descartar` suma una ventana fija de **30 peticiones por hora por `sub`**, compartida mediante Redis (`ratelimit:descarte:*`). La petición 31 responde 429 con `Retry-After`; si Redis falla responde 503. También aplican los límites generales del gateway. La bandeja no tiene límite específico adicional.

## Direcciones del cliente en Identidad (spec 040, KNOCK-285)

`GET` y `POST /api/v1/me/direcciones` y `PUT` y `DELETE /api/v1/me/direcciones/{id}` no tienen política propia: usan la política global por IP de Identidad y los límites del gateway por IP y usuario; la respuesta 429 incluye `Retry-After`. `POST` y `PUT` limitan el cuerpo a 4 KB (413). El tope de 10 direcciones por persona es una regla de negocio (409 `address_limit_reached`), no un límite de peticiones.

## Técnicos de confianza en Mercado (spec 042, KNOCK-91)

`GET`, `PUT` y `DELETE /api/v1/me/tecnicos-confianza` comparten la política `tecnicos-confianza`: **30 peticiones por minuto por `sub`**, por proceso (ventana fija del limitador de Mercado), además de los límites generales. La 31.ª responde 429 con `Retry-After`. `PUT` y `DELETE` no llevan cuerpo: más de 1 KiB responde 413. Con dos réplicas pueden pasar hasta 60 por minuto; el límite compartido queda para KNOCK-247.

Las rutas de servicios del técnico (`GET /api/v1/tecnicos/{id}/servicios` y `POST`, `PUT`, `DELETE /api/v1/tecnicos/me/servicios`) comparten la política `tecnicos-servicios`: **30 peticiones por minuto por `sub`**, por proceso, además de los límites generales. La 31.ª responde 429 con `Retry-After`. Cuerpos de más de 2 KiB (1 KiB en `DELETE`) responden 413 (spec 047, KNOCK-57).
`GET /api/v1/admin/oficios-pedidos` y `GET /api/v1/admin/lista-espera` comparten la política `admin-catalogo`: **60 peticiones por minuto por IP** (el token del panel no pasa por el esquema de la app, así que aún no hay `sub` en el limitador), por proceso, además de los límites generales. La 61.ª responde 429 con `Retry-After`. Decisión provisional (spec 045, KNOCK-111).

`POST /api/v1/admin/opiniones/{id}/ocultar` y `POST /api/v1/admin/opiniones/{id}/mostrar` (spec 061) comparten la política `admin-opiniones`: **30 peticiones por minuto por IP** (como las demás rutas de `/admin` de Mercado, el token del panel aún no deja el `sub` al limitador; la spec pide «por persona del panel» y queda así hasta que el limitador lo lea), por proceso, además de los límites generales. La 31.ª responde 429 con `Retry-After`. El cuerpo de `ocultar` se limita a 2 KiB.
## Chat de visita en Mensajería (spec 026, KNOCK-62)

`EnviarMensaje` del hub `/hubs/chat` limita a **30 mensajes por minuto por persona** (`Chat:MensajesPorMinuto`, de 1 a 600), con una ventana fija de 60 segundos y un contador compartido en Redis (`chat:limite:{sub}`) que se incrementa y vence en una sola operación atómica, así que dos copias del servicio comparten el total. Al pasarse, la invocación responde `{ ok: false, codigo: "message_limit", reintentarEnSegundos }` y el mensaje no se retransmite. El saludo del hub (`POST /hubs/chat/negotiate`) y sus reintentos son peticiones HTTP y siguen los límites generales del gateway y del servicio. Si Redis no responde, el saludo responde 503 y no se acepta la conexión.

## Gateway (spec 009, KNOCK-204)

El gateway limita con contadores de ventana fija en Redis (`ratelimit:ip:*`, `ratelimit:auth:*`, `ratelimit:user:*`, `ratelimit:busqueda:*`), compartidos entre réplicas, a diferencia del limitador en memoria de los servicios.

| Política | Alcance | Límite (punto de partida) | Ventana | Clave |
|---|---|---|---|---|
| IP | Toda ruta salvo `/health`, antes de autenticar | 100 peticiones | 1 minuto | IP del cliente (IPv6 por /64) |
| Estricta `/auth` | `/api/v{n}/auth/*`, además de la de IP | 10 peticiones | 1 minuto | IP del cliente |
| Usuario | Peticiones con sesión, después de autenticar | 120 peticiones | 1 minuto | `sub` del JWT |
| Búsqueda de expertos | `/api/v{n}/busqueda`, además de la de usuario (spec 027, R-14) | 30 peticiones | 1 minuto | `sub` del JWT |

- Configuración: `RateLimiting:Ip`, `RateLimiting:Auth`, `RateLimiting:User` y `RateLimiting:Search` (`PermitLimit` y `Window`), sin valores por defecto en código.
- 429 en `application/problem+json` con `Retry-After` en segundos (mínimo 1); el cuerpo no revela IP, clave ni contador.
- Si Redis no responde, el gateway responde 503 con `Retry-After: 5`: sin contadores no se puede limitar.

## Mi casa y mi auto en Identidad (spec 041, KNOCK-286)

Las cinco rutas de `/api/v1/me/casa-y-auto` (CAS-01 a CAS-05) no tienen política propia: rige el límite global por IP de Identidad (100 por minuto) y el del gateway por IP y usuario; la respuesta 429 incluye `Retry-After`. Los cuerpos de `POST` y `PUT` se limitan a 4 KB (413). El control contra el abuso por repetición es el tope de 20 elementos por persona (409 `item_limit_reached`). La ruta interna `GET /api/v1/interno/historial` de Mercado cae bajo el límite global por IP de Mercado y no está en el gateway.

## Cuenta y datos personales en Identidad (spec 039, KNOCK-284)

Políticas por `sub` del JWT (ventana fija, por proceso; con dos réplicas el límite efectivo se duplica), todas con 429 y `Retry-After`: `POST /me/cambio-numero` **3 por hora** (R-20), `POST /me/cambio-numero/confirmar` **5 por hora**, `POST /me/exportaciones` **3 por hora** y `POST /me/eliminacion` **5 por hora**; estos tres últimos son valores propuestos porque la spec no los fija. `POST /recuperacion-acceso` (pública) tiene **3 por hora por IP** y además **3 por hora por número** (nuevo y anterior, contados por su HMAC en `RecoveryAttemptLimiter`, en memoria del proceso). `POST` y `GET /suspension/apelacion` (pública, spec 060) llevan la política `suspension-apelacion`: **20 por hora por IP** (`IdentityRateLimits`) y **5 por hora por número** (HMAC, `SuspensionAppealLimiter`, en memoria del proceso); cuerpo de 2 KiB (413). `GET/PUT /me/consentimientos`, `GET /me/pendientes` y `POST /me/eliminacion/cancelar` usan solo el límite general. Cuerpos: 4 KiB en cambio de número y recuperación, 8 KiB en eliminación, 1 KiB en consentimientos, exportaciones y cancelar (413). Para que las políticas por persona lean el `sub`, Identidad ejecuta `UseAuthentication` antes de `UseRateLimiter`.

## Verificación de identidad en Identidad (spec 025, KNOCK-79)

`POST /api/v1/me/verificacion` tiene una política por `sub` de **3 creaciones por hora** (`RateLimitingExtensions.VerificationCreatePolicy`, con `SubjectPolicy` y partición compartida); la respuesta 429 incluye `Retry-After`. Frena el uso de la creación como oráculo de CURP (§9). `GET /api/v1/me/verificacion` usa los límites globales. El cuerpo se limita a 8 KB (413).
## Exploración sin cuenta (HU-005, KNOCK-53)

Las lecturas públicas de exploración son `GET /api/v1/oficios`, `GET /api/v1/tecnicos/autocompletar` y `GET /api/v1/tecnicos/{id}`; no tienen política propia: las frena el límite global por IP del gateway y de Mercado (100 por minuto), no uno de 60. El criterio CA4 de la HU-005 pide 429 tras 60 peticiones por minuto por IP; bajar el valor es una decisión pendiente del equipo, porque la matriz parte de TT-API-03 con 100 y afectaría a toda la API. `/api/v1/busqueda` y `/api/v1/zonas/cp/{cp}` piden sesión por decisión de las specs 027 y 023 (la apertura de zonas a la exploración sigue como pregunta abierta en la 023). TC-HU-005-04 comprueba en el gateway que las tres rutas públicas responden sin token y dan 429 con `Retry-After` al pasar el límite configurado.

`POST /api/v1/solicitudes/{id}/cancelar` tiene una política por `sub` del JWT de **10 cancelaciones por hora** (`RateLimitingExtensions.CancelRequestPolicy`, spec 029, R-14), ventana fija y por proceso; la respuesta 429 incluye `Retry-After`. El cuerpo se limita a 8 KB (413).

## Catálogo de oficios, oficios pedidos y lista de espera en Mercado (spec 045, KNOCK-134)

`GET /api/v1/oficios/{clave}` es pública y no tiene política propia: la frenan el límite global por IP del gateway y de Mercado (100 por minuto) y la caché de 24 horas de oficios activos. `GET`, `POST` y `PUT /api/v1/admin/oficios` comparten con `GET /api/v1/admin/oficios-pedidos` y `GET /api/v1/admin/lista-espera` la política `admin-catalogo`: **60 peticiones por minuto por IP**, por proceso (el token del panel no pasa por el esquema de la app, así que aún no hay `sub` en el limitador); la respuesta 429 incluye `Retry-After`. `POST /api/v1/oficios-pedidos` usa la política `tecnicos-registro` (**5 por hora por `sub`**, la misma de `POST /api/v1/tecnicos`, porque es parte del flujo de registro; así no se toca `RateLimitingExtensions`). Los cuerpos se limitan a 4 KiB en las dos rutas de escritura del panel y a 1 KiB en `POST /api/v1/oficios-pedidos`; más responde 413. Con dos réplicas el límite efectivo se duplica, como en las demás políticas de Mercado.

## Visitas, cotización final y agenda en Mercado (specs 049 y 050)

Políticas por `sub` del JWT (ventana fija; el contador vive en Redis compartido entre réplicas, con respaldo por copia, `Knock.Hosting`), además de los límites generales. La que se pasa responde 429 `rate_limited` con `Retry-After`. Los valores salen de `MarketRateLimits` (existen en sprint-05).

| Política | Rutas | Límite | Ventana | Clave |
|---|---|---|---|---|
| `agenda-tecnico` | `GET /api/v1/agenda` | 60 peticiones | 1 minuto | `sub` |
| `visitas-cambios` | `POST /api/v1/visitas/{id}/reprogramar`, `.../reprogramar/respuesta`, `.../cancelar`, `.../cliente-ausente`, `.../tecnico-ausente`, `.../terminar`, `.../no-terminado` y `PUT .../permiso-fotos` (una sola política de 8 rutas) | 20 peticiones | 1 minuto | `sub` |
| `visitas-cotizacion-final` | `POST /api/v1/visitas/{id}/cotizacion-final` | 10 peticiones | 1 minuto | `sub` |
| `visitas-cotizacion-final-decision` | `POST /api/v1/visitas/{id}/cotizacion-final/decision` | 20 peticiones | 1 minuto | `sub` |
| `cotizaciones-ajuste-precio` | `POST /api/v1/cotizaciones/{id}/ajuste-precio` | 10 peticiones | 1 minuto | `sub` |
| `ajustes-decision` | `POST /api/v1/ajustes/{id}/decision` | 20 peticiones | 1 minuto | `sub` |

`GET /api/v1/tecnicos/me/reputacion` y `GET /api/v1/tecnicos/{id}/fotos-trabajos` no tienen política propia: rigen los límites globales.

## Ampliar y referencia de mercado en Mercado (spec 048)

`POST /api/v1/solicitudes/{id}/ampliar` tiene la política `solicitudes-ampliar`: **10 peticiones por hora por `sub`**. `GET /api/v1/cotizaciones/referencia-mercado` tiene `cotizaciones-referencia`: **30 peticiones por minuto por `sub`**. Ambas en `MarketRateLimits`.

## Mapa y Explorar en Mercado (spec 046)

`GET /api/v1/mapa/tecnicos` (`tecnicos-mapa`) y `GET /api/v1/explorar` (`tecnicos-explorar`) son públicos: **60 peticiones por minuto por IP cada uno**, con contador propio por política (clave `{política}:{IP del cliente}`), además del límite global. Están en sprint-05.

## Opiniones en Mercado (spec 051)

Las tres rutas de opiniones (`GET /api/v1/tecnicos/{id}/opiniones`, `POST /api/v1/visitas/{id}/opinion` y `POST /api/v1/opiniones/{id}/respuesta`) comparten la política `opiniones`: **30 peticiones por minuto por `sub`**. Está en sprint-05.

## Asistente en Mercado (spec 052)

`POST /api/v1/asistente/conversaciones`, `.../resumen-chat` y `.../borrador-cotizacion` (las que llaman al modelo) tienen la política `asistente-ia`: **10 peticiones por minuto por `sub`**. `GET /api/v1/asistente/rango-precio` y `PUT`/`GET .../bitacora/{solicitudId}` tienen `asistente-consulta`: **60 peticiones por minuto por `sub`**. Aparte, el presupuesto de la IA se frena con `Asistente:Limites` (`TokensPorUsuarioPorDia` 20 000, `TokensGlobalesPorDia` 200 000, `TiempoMaximoSegundos` 10, `TokensSalidaMaximos` 800): al agotarlo responde 429 `ai_daily_limit_reached` con `Retry-After`. `Asistente:Habilitado` viene en `false`. Está en sprint-05.

## Conversaciones, dispositivos y avisos en Mensajería (specs 030, 037 y 044)

Políticas por `sub` del JWT de `MessagingRateLimits` (ventana fija; contador compartido en Redis), además del límite global por IP de Mensajería. La respuesta 429 lleva `Retry-After`.

| Política | Rutas | Límite | Ventana | Clave |
|---|---|---|---|---|
| `conversaciones-crear` | `POST /api/v1/conversaciones` | 20 peticiones | 1 hora | `sub` |
| `conversaciones-mensajes` | `POST /api/v1/conversaciones/{id}/mensajes` | 30 peticiones | 1 minuto | `sub` |
| `dispositivos` | `POST /api/v1/dispositivos` y `DELETE /api/v1/dispositivos/{id}` | 30 peticiones | 1 hora | `sub` |
| `avisos-leer` | `GET /api/v1/avisos`, `GET /api/v1/avisos/no-leidos/contador` y `GET /api/v1/me/preferencias-avisos` | 120 peticiones | 1 minuto | `sub` |
| `avisos-marcar` | `POST /api/v1/avisos/{id}/leido` y `POST /api/v1/avisos/leidos` | 60 peticiones | 1 minuto | `sub` |
| `avisos-preferencias` | `PUT /api/v1/me/preferencias-avisos` | 20 peticiones | 1 minuto | `sub` |

- El 429 de `conversaciones-mensajes` lleva `code` `message_limit` (spec 030, R-11); los demás, `rate_limited`.
- Existen en sprint-05 todas las políticas de esta tabla.
- `GET /api/v1/conversaciones`, `.../no-leidas`, `GET /api/v1/conversaciones/{id}`, `.../mensajes` y `POST .../leido` no tienen política propia: rigen los límites globales.
- El límite global por IP de Mensajería (100 por minuto, `/health/ready` 60) sigue siendo por copia (`SharedGlobalCounters = false`); las políticas por `sub` de arriba sí usan el contador compartido. La línea sobre Redis en el párrafo de Mensajería de arriba quedó atrás: las políticas por persona ya lo usan.

## Verificación de identidad: confirmar foto (spec 025, R-19)

`POST /api/v1/me/verificacion/{id}/fotos/{tipo}/confirmar` tiene la política `verificacion-confirmar` (`IdentityRateLimits.VerificationConfirmPolicy`): **10 peticiones por hora por `sub`**. Está en el PR #272 de integración de Identidad, sin fusionar. `POST /api/v1/me/verificacion/{id}/enviar` no tiene política propia.

## Rutas internas (specs 037, 039, 043, 053 y 054)

Ninguna ruta de `/api/v1/interno/*` tiene política de límite propia: no son políticas de `RateLimiting`, sino de **autorización** del token de servicio (`InternalHistory`, `InternalEvents`, `InternalBlocks`, `InternalAccount`, `InternalTechnicianHidden`, `InternalNotFinishedReport` en Mercado; `InternalNotices` en Mensajería). Las llama solo otro servicio de Knock, no pasan por el gateway y solo les aplica el límite global por IP de su servicio. Los nombres `InternalBlocks`, `InternalEvents`, `InternalNotices` y `AccountPolicy` de la lista de trabajo corresponden a esas políticas de autorización (`AccountPolicy` es la constante de `InternalAccount`); no hay un valor de peticiones por minuto que documentar.
