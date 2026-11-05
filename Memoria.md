# Memoria — knock-backend

> Registro del proyecto: estado, decisiones con su fecha y su porqué, y errores a evitar. Los agentes la leen al empezar y agregan una entrada al terminar cada cambio. Sin datos sensibles.

## Estado actual
- Constitución `1.0.0` en borrador, `AGENTS.md`, subagentes y skills creados.
- 14 specs de la base (arquitectura, estructura del repositorio, infraestructura, rendimiento, observabilidad, versionamiento y seguridad) redactadas en **Borrador**; sus tareas **no se han ejecutado**.
- Sin código de producto todavía.

## 2026-10-27 — Aclaración de «Uso de IA»
- **Decisión:** la sección «Uso de IA» es del **pull request** (plantilla de PR), no de cada spec.
- **Porqué:** `@constitution-reviewer` marcó FAIL a la spec 001 por leer «todo cambio» de forma literal; la redacción era ambigua.
- **A evitar:** copiar «Uso de IA» dentro de las specs para que pase una revisión.

## 2026-10-27 — Definición de la base
- **Decisión:** el repositorio se construye con SDD; la primera etapa solo define la base y no incluye funcionalidad.
- **Porqué:** así la arquitectura y la infraestructura quedan decididas y revisadas antes de escribir la primera línea de negocio.
- **A evitar:** empezar a codificar funciones antes de que la base esté aprobada.

## 2026-10-27 — Arquitectura
- **Decisión:** **Microservicios con una puerta de entrada.** Tres servicios pequeños —Identidad y confianza, Mensajería y notificaciones y Mercado— y un gateway que recibe todo el tráfico, valida el token, limita las peticiones y reparte la petición al servicio correcto. Los servicios internos no tienen dirección pública.
- **Porqué:** ver `specs/001-*/spec.md` (sección Decisiones) y `docs/descripcion-del-proyecto.md`.

## 2026-10-27 — Infraestructura y escalabilidad
- **Decisión:** **Nube:** Azure con la suscripción gratuita de estudiante: Container Apps (ejecuta los servicios y el gateway y reparte las peticiones), DocumentDB (MongoDB), Blob Storage, Key Vault y Application Insights. El backend corre en una sola región de las cinco que permite la suscripción.
- **Porqué:** ver las specs de infraestructura y escalabilidad y `docs/descripcion-del-proyecto.md`.

## 2026-11-02 — Primeras specs de endpoints
- **Decisión:** specs 015 a 018 para los endpoints de identidad y sesión del Excel (`SES-01` a `SES-05`, `USR-01`, `USR-02`), en el orden que pide la evaluación del 3 y 4 de noviembre: registro y login, logout, y cuatro endpoints más. Nombre `NNN-tema` como las anteriores; el identificador del Excel va en el encabezado de cada spec.
- **Porqué:** el Excel de endpoints se revisó y corrigió el mismo día (SES-01 recibe nombre y consentimientos, `GET /me` con caché, exportar y eliminar cuenta pasan a `POST`).
- **Pendiente de decidir:** SMS de Firebase en la evaluación, validación de la colonia mientras Mercado no esté desplegado y cambio de número con Firebase.

## 2026-11-03 — Alineación del login con el repositorio histórico
- **Decisión:** las specs 015 a 018 y el Excel de endpoints se alinearon con HU-001 y TT-API-12 del repositorio del equipo: `sub` UUID v8 derivado del `uid` de Firebase, celular cifrado más hash, `aceptaAviso` y `deviceId` obligatorios, 409 por conflicto de cuenta y respuesta `{ accessToken, refreshToken, esNuevo, perfilCompleto }`. El nombre se completa después con `PUT /me`.
- **Porqué:** esas decisiones ya estaban aprobadas por el líder y parte del código está en el PR #75; las specs nuevas se habían redactado desde el Excel sin ver HU-001.
- **A evitar:** redactar specs de un endpoint sin buscar antes si ya existe una spec aprobada del mismo tema en el repositorio histórico.
- **Pendiente:** decidir si `sesiones` y `refresh_tokens` de HU-001 son la misma colección.

## 2026-11-03 — Plataforma a .NET 11
- **Decisión:** el backend pasa de .NET 10 a **.NET 11** (hoy 11.0.100-rc.3, fase «go-live»); `global.json` fija el SDK y los cinco proyectos apuntan a `net11.0`. La constitución del backend en `demo-base` sube a `1.1.0` (sigue en borrador, como toda la base).
- **Porqué:** el profesor pidió la versión más reciente de cada tecnología; los proyectos estaban vacíos, así que el cambio no cuesta nada ahora. Compila en Release con 0 advertencias.
- **A evitar:** desplegar con el RC: al primer despliegue pasar a la versión final y volver a compilar. `Microsoft.AspNetCore.OpenApi` está en `11.0.0-rc.3` hasta entonces.

## 2026-11-03 — Alcance de bases de datos, ML e IA
- **Decisión:** no se agrega una base de grafos: el requisito de NoSQL queda cumplido con MongoDB y Redis. El aprendizaje automático (ML) no es prioridad. La IA generativa será una sola función sencilla, solo si Azure ya da acceso al servicio; no se invierte más en ella.
- **Porqué:** costo cero, tiempo hasta diciembre y la cuota de Azure OpenAI de la suscripción.
- **A evitar:** escribir en los documentos que el ML «se quita»: es no prioritario, no eliminado.

## 2026-11-03 — Proyectos de prueba y rulesets básicos
- **Decisión:** la solución tiene siete proyectos de prueba vacíos (`UnitTest`, `IntegrationTest`, `SecurityTest`, `ContractTest`, `DatabaseTest`, `MutationTest` y `PerformanceTest`), como en la solución de ejemplo del profesor (`WebAPIDevSecOps`). Los de mutación y rendimiento son bibliotecas sin pruebas: se activan después (spec 008). Se definieron rulesets básicos de GitHub en `.github/rulesets/`.
- **Porqué:** el profesor mostró esa estructura y dijo que compartiría los rulesets; la imagen no llegó, así que se dejaron los básicos (rama `main` y `develop` protegidas, etiquetas de versión protegidas).
- **Dependencias nuevas (plantilla de xUnit):** `xunit` 2.9.3, `xunit.runner.visualstudio` 3.1.5, `Microsoft.NET.Test.Sdk` 17.14.1 y `coverlet.collector` 6.0.4, solo en proyectos de prueba. Evaluación de licencia y mantenimiento pendiente.
- **A evitar:** afirmar que los rulesets se aplican: en una organización gratuita no se hacen cumplir en repositorios privados.

## 2026-11-03 — Ruleset de `main` alineado con las capturas del profesor
- **Decisión:** `main.json` copia las reglas de su ruleset «Main Proteccion»: no borrar, sin force push, pull request con 1 aprobación, métodos Merge/Squash/Rebase, regla de comprobaciones obligatorias (lista vacía hasta que exista el spec de CI/CD) y revisión automática de Copilot. Se conservan dos reglas más estrictas (aprobaciones descartadas con commits nuevos e hilos resueltos) y el rol de administrador como excepción, que en el ruleset del profesor no existe.
- **Porqué:** el profesor pidió cumplir a la letra; la imagen ya llegó y reemplaza los básicos provisionales de la entrada anterior.
- **Pendiente:** «Additional approval for unattributed Copilot pull requests» no está en el JSON (parámetro sin confirmar); `copilot_code_review` y la lista vacía de comprobaciones se escribieron sin validar la importación en GitHub.
- **A evitar:** afirmar que se aplican: en una organización gratuita no se hacen cumplir en repositorios privados.

## 2026-11-03 — Specs 003, 007, 008, 009 y 013 alineadas con las reglas del equipo
- **Decisión:** se agregan a las specs de arquitectura reglas que el repositorio del equipo ya aplicaba y que no estaban: región West US (003), Semgrep, Gitleaks, Trivy y Dependabot en el CI (007), Testcontainers con `documentdb-local` y nombres de prueba con el criterio que cubren (008), cifrado de campo AES-256-GCM con HMAC-SHA256 y cancelación de tokens en Redis (009), y Azure AI Content Safety como API de moderación (013). Cada regla nueva tiene criterio y tarea; las specs siguen en Borrador.
- **Porqué:** la auditoría de la migración mostró que esas reglas solo vivían en la constitución del equipo y en el código, no en las specs de la base.
- **Pendiente:** cancelar por `jti` o por `sid` (duda abierta en la spec 009; decide el líder con Guillermo); INE y selfie necesitan su propia spec.
- **A evitar:** dar por firmadas estas specs: las cambió un agente y las firma una persona.

## 2026-11-04 — Spec 019: MCP del proyecto
- **Decisión:** se escribe, en Borrador, la spec de MCP que pidió el profesor al final de la secuencia (RP-72 y RP-16). Jira con el MCP oficial de Atlassian (OAuth por persona, sin tokens en el repositorio), GitHub con la CLI `gh`, y solo lectura por defecto.
- **Porqué:** hoy el repositorio no tiene MCP configurado y el uso en el equipo es personal; sin spec no hay reglas de permisos ni de secretos.
- **Pendiente:** confirmar la dirección oficial y las herramientas del servidor de Atlassian al implementar; decidir si se usa MongoDB, Figma o el MCP de Dart.
- **A evitar:** guardar tokens en la configuración o permitir escritura en Jira desde un agente sin aprobación.

## 2026-11-04 — Revisión de todas las specs antes de la firma
- **Decisión:** se revisaron las specs de arquitectura y de endpoints con comprobaciones automáticas (cada requisito y criterio tiene tarea; valores como 15 minutos, 30 días, 10 sesiones y 5 minutos coinciden entre specs) y se corrigieron las diferencias: la región West US ahora está en la spec 005, el frontend define `deviceId` y la aceptación del aviso (spec 009), y las specs de MCP cubren todos sus requisitos con tareas.
- **Porqué:** el frontend y el backend se contradecían en el contrato de inicio de sesión, y una spec afirmaba una región que la otra no fijaba.
- **Pendiente (decide una persona):** `Idempotency-Key` (el frontend lo consume y el backend no lo define), el comportamiento ante 429 y `Retry-After`, y cancelar por `jti` o `sid`. Las specs siguen en Borrador: las firma una persona.
- **A evitar:** darlas por aprobadas sin leer las dudas abiertas de la sección 9.

## 2026-11-04 — Dudas de las specs resueltas
- **Decisión:** se resolvieron las dudas abiertas para poder aprobar: cancelar sesiones por `sid` (no `jti`), una sola colección `sesiones` (no `refresh_tokens`), 429 con `Retry-After`, `Idempotency-Key` fuera de esta etapa, colonia validada siempre contra Mercado (503 si no responde), 409 en eliminación programada, 404 para sesión ajena, y MCP de MongoDB y de Dart sin activar.
- **Porqué:** las dudas bloqueaban la aprobación de las specs de sesión y de seguridad; cada decisión quedó en la sección 9 de su spec con el motivo.
- **Pendiente:** actualizar la hoja de endpoints del Excel (sesión ajena: 404) y la constitución al fusionarlas (`sid` en lugar de `jti`); avisar a Guillermo del cambio de HU-001.
- **A evitar:** dar estas decisiones por firmadas: las aprueba una persona al aprobar cada spec.

## 2026-11-04 — Una revisión de pares obligatoria
- **Decisión:** cada PR necesita una revisión de otra persona y la segunda es opcional; el Tech Lead puede aprobar sus propios PR, y su revisión es opcional si es urgente o no hay nadie disponible (anota el motivo). Los rulesets dejan de descartar aprobaciones por commits nuevos y de exigir hilos resueltos.
- **Porqué:** dos revisiones frenaban el flujo con cuatro personas; el profesor tampoco las exige en su ruleset.
- **A evitar:** que un agente apruebe o fusione: sigue prohibido.

## 2026-11-04 — Spec 011: observabilidad y registro en Identity, Messaging y Gateway (KNOCK-206)
- **Decisión:** los tres servicios usan copias de `LoggingExtensions`, `ObservabilityExtensions` y `TelemetryRedactor` de Market (Serilog estructurado, OpenTelemetry con redacción, Azure Monitor solo con `APPLICATIONINSIGHTS_CONNECTION_STRING`), cableadas en línea en su `Program.cs`, con `Serilog:MinimumLevel` y `Observability:ServiceName` (`knock-identity`, `knock-messaging`, `knock-gateway`; nombres por confirmar) en `appsettings.json`. Duplicados aceptados por el líder con **EXC-007** (jscpd, 9 archivos, vence 2027-02-02; marcador `// jscpd:ignore-start -- EXC-007` en la primera línea de cada copia). Dependencias nuevas en los tres servicios, con las mismas versiones que Market: `Serilog.AspNetCore` 10.0.0; `OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Instrumentation.AspNetCore` y `OpenTelemetry.Instrumentation.Http` 1.20.1; `Azure.Monitor.OpenTelemetry.Exporter` 1.10.0. Pruebas `_CA_011_01` y `_CA_011_02` en `tests/UnitTest/Hosting/Spec011ObservabilityTests.cs`, con el mismo cableado y el `appsettings.json` real de cada servicio; CA-03 se verifica a mano en el PR.
- **Porqué:** R-01 a R-04 piden el mismo formato y la misma redacción en todos los servicios, y todavía no hay una biblioteca técnica compartida; un servicio no referencia a otro.
- **Decisión del líder (2026-11-04, PR #10):** la consola de Serilog pasa por la misma redacción que OpenTelemetry en Identity, Messaging y Gateway (Market queda fuera de este PR: el líder lo arregla aparte). `RedactingJsonFormatter` (en `TelemetryRedactor.cs`, copias idénticas bajo EXC-007) envuelve a `RenderedCompactJsonFormatter`: lee la línea JSON, redacta `@m`, `@mt`, `@x` y cada propiedad (una clave conocida se reemplaza entera, también dentro de objetos) y la vuelve a serializar, así que sigue siendo JSON válido aunque se tape un número (`"lat":19.4326`). Las pruebas de tiempo de `TelemetryRedactor` (límite de 1 ms sin cambios) llevan `[Trait("Category", "Performance")]`: en las copias de Identity, Messaging y Gateway de este PR; el de Market entra en el PR #11.
- **Pendiente:** `X-Correlation-Id` y el campo `correlationId` llegan con `Knock.Contracts` (PR #3). Terraform, R-05, R-06 y el humo en Application Insights van con KNOCK-200 (PR #6). El alta ya no registra la propiedad `{Phone}` en el mensaje (decisión del líder, PR #10); el celular nacional sin `+` en texto libre sigue siendo riesgo aceptado (ver `docs/OBSERVABILIDAD.md`).
- **A evitar:** `public partial class Program {}` en un servicio (choca con CA1515 y S1118): se usan `Aliases` en la referencia de UnitTest e `InternalsVisibleTo` en el csproj. Copiar `HealthExtensions` (choca con el de la spec 003) o `CorrelationIdMiddleware`. Un `#pragma warning disable` sin EXC: con `InternalsVisibleTo` no hace falta. Poner el marcador de jscpd después del `namespace`: el bloque de `using` sigue contando como clon. Redactar como texto la línea JSON entera de la consola (rompe el JSON al tapar un número). Una `[CollectionDefinition]` de xUnit en el proyecto de pruebas (CA1515 y CA1711): basta con un `[Collection("…")]` con nombre. Correr `dotnet test --no-build` después de revertir una mutación sin recompilar: prueba los binarios mutados.

## 2026-11-04 — Spec 003 (KNOCK-198): MongoDB y Redis
- **Estado:** `Mongo:Region` validada (`westus`, CA-05) en los servicios con MongoDB. Cliente único de MongoDB (`Hosting/MongoExtensions.cs`) en Market, Identity y Messaging; de Redis (`Hosting/RedisExtensions.cs`) en Market, Identity y Gateway, con configuración, contraseña obligatoria y readiness (`/health/ready` en los servicios nuevos). En Identity y Gateway se registra al arrancar (`AddKnockData`); en Market sigue a demanda del módulo que lo use, para que la API arranque sin Redis. `db/001_indices.js` (mongosh, idempotente) con `outbox {estado, creadoEn}` y `tareas_programadas {estado, programadaPara}` en `market`, `identity` y `messaging`, y los índices del Catalog iguales a los de `CatalogStore` (siguen también en el código porque la siembra depende de ellos; CA-01 compara ambos). `deploy/backup/mongo-backup.sh` (`dump`/`restore`, URI solo por variable). Redis y documentdb-local en `deploy/docker-compose.local.yml`. Pruebas `..._CA_003_01` a `..._CA_003_05` en clases `Spec003*` y en `MongoAccessTests`.
- **Decisiones del líder (2026-11-04):** pruebas con el número de spec (`_CA_003_KK`). Redis en Identity (escribe `sesion-cancelada:{sid}` al cerrar sesión, spec 016; se cancela por `sid`, no por `jti`), en el Gateway (lo lee y lleva el límite de peticiones, spec 009) y en Market (caché); Messaging no usa Redis por ahora. Este PR solo deja la infraestructura de acceso; la lógica es de las specs 016 y 009. Se aceptan por ahora las copias de `MongoExtensions`, `RedisExtensions` y `HealthExtensions` entre servicios con **EXC-008** (vence 2027-02-02; plan: unificarlas en una biblioteca técnica). CA-04 en contenedor local y CA-05 como configuración validada; la verificación en Azure espera a KNOCK-200 (PR #6). El guardián se usa con `KNOCK_SPEC=003` (arreglo en el PR #7).
- **Dependencias:** **StackExchange.Redis 2.13.23** (última estable 2.x, licencia MIT) en Market, Identity y Gateway; **MongoDB.Driver 3.12.0** (la de Market) en Identity y Messaging. Imagen **`redis:7.2-alpine`** fijada por digest en el compose y en `RedisFixture`: 7.2 es la última versión BSD-3; 7.4 y posteriores son RSALv2/SSPLv1 (y 8.x, además, AGPLv3). Imagen **`mongo:8.0`** fijada por digest, solo por mongosh, mongodump y mongorestore en `MongoToolsContainer` (CA-01 y CA-04).
- **Pendiente:** programar el respaldo y subirlo a Blob Storage (spec 005); pruebas en IntegrationTest, SecurityTest y ContractTest (hoy vacíos).
- **A evitar:** dos agentes en la misma carpeta de trabajo (otro agente cambió aquí la imagen a `redis:7-alpine` sin digest y metió `AddKnockRedis()` en el arranque general de Market). En UnitTest, referenciar otro servicio sin alias: su `Program` choca con el de los demás (`Aliases` en la referencia).

## 2026-11-04 — Las pruebas de tiempo corren sin cobertura (KNOCK-203)
- **Decisión:** las pruebas con `[Trait("Category", "Performance")]` se excluyen del paso de unitarias con cobertura y corren en un paso aparte, sin cobertura, con el mismo límite.
- **Porqué:** la instrumentación de cobertura hace más lento el código y `TC_TT_OBS_01_12` (1 ms por 4 KB) daba entre 1,01 y 1,08 ms; subir el límite le quitaría fuerza donde sí mide.
- **A evitar:** subir el límite de una prueba de tiempo para que pase con cobertura; marca la prueba con la categoría `Performance`.

## 2026-11-04 — Validador de ramas y commits al día con las specs numeradas (KNOCK-199)
- **Decisión:** `scripts/ci/branch_rules.py` acepta los ámbitos de commit que ya usa el equipo (gateway, contracts, deploy, docker, container, scripts, architecture, security, db) y un título de PR con ID opcional (`KNOCK-N · <título>`, o con HU-, TT-, BUG-, B-NNN o F-NNN); se agregaron pruebas en `scripts/ci/test_branch_rules.py`.
- **Porqué:** con `ENFORCE_BRANCH_RULES=true` los commits y PR de Sprint 3 habrían fallado (ámbitos y títulos del esquema antiguo); sin reglas aplicables en GitHub (plan gratuito), el CI es el único control.
- **Pendiente:** corregir en la spec 004 (R-02) que las ramas salen de `sprint-XX`, no de `develop` (lo edita José); poner `ENFORCE_BRANCH_RULES=true` en los dos repositorios tras fusionar este cambio.
- **A evitar:** activar `ENFORCE_BRANCH_RULES` antes de fusionar este cambio y los PR abiertos de Sprint 3: varios de sus commits tienen encabezados de más de 100 caracteres (regla vigente) y fallarían; hay que activarlo cuando ya estén fusionados, o reescribir esos commits con autorización.

## 2026-11-04 — Spec 001 (KNOCK-196): gateway, correlación y fronteras entre servicios
- **Decisión:** el gateway usa YARP con las rutas y las direcciones en configuración (`ReverseProxy`); el middleware de `X-Correlation-Id` y su regla viven en `Knock.Contracts.Correlation` (biblioteca común, sin lógica de negocio) y los tres servicios nuevos y el gateway lo usan; los cuatro proyectos responden `/health`. Se agrega `tests/ArchitectureTest` (19 pruebas: ningún servicio referencia a otro, `Knock.Contracts` no referencia servicios y la regla puede fallar) y 11 pruebas de integración del gateway con servicios simulados.
- **Porqué:** la spec 001 pide una sola entrada pública, correlación en toda petición y fronteras comprobadas por prueba; así lo siguen la 009 (token y límite de peticiones en el gateway, de Guillermo) y la 005 (ingreso interno).
- **Quitado:** `UseHttpsRedirection` del gateway, de Identity y de Messaging (Market lo conserva, condicionado a `Security:Https:RedirectPort`): el HTTPS lo termina el ingreso de Container Apps y la redirección interna rompería las llamadas del gateway por HTTP.
- **Pendiente:** validar el token y limitar peticiones en el gateway (spec 009); unificar el middleware de correlación de `Knock.Market.Api` con el de `Knock.Contracts`; Dockerfiles (KNOCK-232); CA-04 (servicio interno inaccesible desde fuera) se comprueba en el despliegue.
- **A evitar:** agregar lógica de negocio a `Knock.Contracts` o referencias directas entre servicios.

## 2026-11-04 — Spec 015: registro e inicio de sesión en Identity.Api (KNOCK-210, KNOCK-231)
- **Estado:** módulo `Sesiones` de `Knock.Identity.Api` con `POST /api/v1/auth/firebase`, colecciones `usuarios` y `sesiones`, script `db/002_identidad_usuarios_sesiones.js` y pruebas unitarias. Se migró desde `legacy/` lo reutilizable de HU-001: derivación del `sub`, cifrado y hash del celular, huella del token de renovación y verificador de Firebase.
- **Decisiones:** el JWT lleva `sub` y `sid` (sin `jti`; confirmado por el líder); `sesiones` reemplaza a `refresh_tokens` y el consentimiento va dentro de `usuarios`; cada canje crea una sesión nueva, y al pasar de 10 abiertas se revoca la más antigua (solo en MongoDB, sin Redis, porque la spec no lo pide). Las versiones del aviso y de los términos salen de configuración (`Consentimientos`, «1.0»). Dependencia nueva evaluada: `FirebaseAdmin` 3.7.0 (verifica el token con las llaves públicas de Google; sin credenciales de servicio).
- **Pendiente:** el contrato OpenAPI de Identity lo publica el líder en el PR #14; la ruta pública, la validación del JWT y el rechazo de sesiones canceladas del gateway van en el PR #13 (spec 009). Las pruebas de integración con MongoDB, Redis y el emulador de Firebase y la exención CA1515 de Identity.Api ya están en este PR. Riesgos que quedan: las transacciones de MongoDB son indispensables (si la instancia no las admite, todo canje falla; hay que comprobarlo contra el clúster real), la firma real de Google no se prueba (solo el emulador), así que «otra firma» de CA-04 queda sin prueba, y las llaves del celular se validan en el primer canje, no al arrancar. El `Common/` y el `Hosting/` de Identity.Api son copia de los de Market (EXC-015, vence el 2026-12-04).
- **A evitar:** guardar el celular en claro o el token de renovación, devolver detalle de la otra cuenta en el 409, y crear la cuenta fuera de la transacción de usuario y primera sesión.

## 2026-11-04 — Spec 016: renovación y cierre de sesión (KNOCK-211)
- **Estado:** `POST /api/v1/auth/refresh` y `POST /api/v1/auth/logout` en `Knock.Identity.Api` (módulo `Sesiones`), con 21 pruebas unitarias nuevas y 13 de integración con MongoDB, Redis y el emulador de Firebase reales.
- **Decisiones:** la rotación es un solo `updateOne` condicionado a la huella vigente, a que no esté revocada y a que no haya vencido; un token ya usado (entre las últimas 5 huellas) revoca todas las sesiones de la cuenta y cancela sus `sid` en Redis 15 min; el logout solo cierra la sesión si el `sub` del JWT es su dueño. Los rechazos devuelven un 401 uniforme. El tiempo de vida de la clave de cancelación es lo que le queda al JWT, entre 1 s y 15 min. Los controllers son tres (`AuthController`, `RefreshController`, `LogoutController`) porque S6960 pide no mezclar responsabilidades.
- **Pendiente:** la conexión a Redis de producción (`AddKnockRedis`) llega con el PR #8; hasta entonces solo las pruebas registran `IConnectionMultiplexer`. El rechazo de JWT cancelados en el gateway es de la spec 009 (KNOCK-204). Riesgo aceptado de la spec: una renovación cuya respuesta se pierde cuenta como reuso.
- **A evitar:** registrar tokens, huellas o `sid` en los registros; devolver el motivo del 401.

## 2026-11-05 — Spec 017: perfil de la persona (KNOCK-212)
- **Estado:** `GET` y `PUT /api/v1/me` en el módulo `Usuarios` de `Knock.Identity.Api`, con la interfaz `IColoniaCatalog` en `Knock.Contracts`. 82 pruebas unitarias nuevas y 15 de integración con MongoDB, Redis y el emulador de Firebase reales (60 en total en la suite).
- **Decisiones:** la caché usa la clave `perfil:{sub}` (el `sub` y no el `_id`, porque es lo único que se conoce antes de ir a MongoDB) con 60 s; leer y guardar la caché fallan en silencio, pero borrarla no (una escritura no responde dejando un perfil viejo). El ETag débil mezcla el `version` con un resumen del `sub` para que quien cambie de cuenta en el mismo teléfono nunca reciba un 304 con el perfil de otra persona. Los campos inválidos responden 400 como pide la spec 017 (CA-06) y no 422 como el filtro general: por eso las reglas son una clase propia y no un validador de FluentValidation. La escritura es un solo `findOneAndUpdate` condicionado a que la cuenta no esté en eliminación programada, y sube `version` y `actualizadoEn`. Nuevo tipo de error `ServiceUnavailable` (503).
- **Pendiente:** Mercado no expone la consulta «¿existe esta colonia?» que pide R-06 (solo `GET /colonias/en-punto` por coordenadas); mientras no exista, `IColoniaCatalog` está registrado como no disponible, así que un `PUT` con colonia responde 503 y uno sin colonia funciona. `Perfil:HostsFoto` queda vacío (ninguna foto se acepta) hasta que el despliegue ponga el dominio del almacenamiento o la CDN. El rechazo de JWT con `sid` cancelado de CA-04 es del gateway (PR #13).
- **A evitar:** loguear el nombre, el celular o el `sub`; aceptar una colonia sin validarla; usar el mismo ETag para dos personas.
