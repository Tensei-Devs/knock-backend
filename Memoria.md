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

## 2026-11-04 — Dockerfiles de los servicios (KNOCK-232)
- **Decisión:** un Dockerfile por servicio (`Knock.Gateway`, `Knock.Identity.Api`, `Knock.Messaging.Api` y el de `Knock.Market.Api` actualizado) con imágenes `sdk:11.0` y `aspnet:11.0`, restauración bloqueada, usuario sin privilegios (`APP_UID`, 1654) y puerto 8080; todos copian `Knock.Contracts`. El job `container` del CI ahora construye, escanea (Trivy) y revisa (Dockle) cada imagen con una matriz. EXC-002 (sin HEALTHCHECK, las sondas las define Container Apps) amplía su alcance a los cuatro Dockerfiles.
- **Porqué:** la spec 001 (R-06) pide construir y desplegar cada servicio por separado; el Dockerfile de Market apuntaba a .NET 10 y no copiaba `Knock.Contracts`, así que habría fallado al construirse.
- **Verificado:** las cuatro imágenes construyen con Docker y el gateway responde `/health` con `X-Correlation-Id` en un contenedor.
- **A evitar:** poner credenciales en la imagen o ejecutar como root.

## 2026-11-04 — El trazado de specs numeradas exige el número de spec (KNOCK-196)
- **Decisión:** `sdd_lint --trace` y `KNOCK_SPEC` aceptan una spec numerada solo con tres dígitos exactos (`015`), y una prueba la cubre solo si lleva el número en el nombre (`CA_015_01`); el `CA_01` suelto ya no vale.
- **Porqué:** la revisión del PR mostró que con `CA_01` suelto `--trace 006`, `007`, `008` y `010` daban «todos los casos tienen prueba» sin tener ninguna, porque el texto aparece en las pruebas de otra spec; y `--trace 01` resolvía la spec 010 sin avisar.
- **Pendiente:** las pruebas de la spec 001 se llaman `..._CA_02`, así que `--trace 001` marcará FALTA hasta renombrarlas a `..._CA_001_02`; la cabecera `> **Jira:** KNOCK-N` en las specs.
- **A evitar:** nombrar una prueba solo con el número del criterio.

## 2026-11-04 — El Terraform se alinea con lo que los servicios exigen al arrancar (KNOCK-200)
- **Decisión:** Identity recibe `Identity__PhoneProtection__EncryptionKeys__1`, `__HmacKeys__1` y las dos versiones (antes `Pii__*`, que ningún código lee); gateway, Identity y Market reciben `ForwardedHeaders__KnownNetworks__0` con la subred del entorno; los servicios llevan sondas de arranque y de disponibilidad (`/health/ready`) y se crean después de Redis; Market deja de recibir la cadena de Redis, que no usa.
- **Porqué:** la revisión de par de Guillermo y una comprobación contra el código mostraron que, tal como estaba, el primer canje de sesión habría fallado y los límites por IP habrían sido globales para todos los clientes.
- **Pendiente:** comprobar en el primer despliegue la IP real que ve el gateway, y que las llamadas internas por `http://` no se redirijan a HTTPS; acotar los permisos de `bootstrap.sh` (rol de administración de roles con condición) cuando se amplíe el equipo.
- **A evitar:** declarar variables de entorno sin comprobar el nombre exacto que lee el código (doble guion bajo por cada `:`).


## 2026-11-05 — Spec 019: MCP del proyecto (KNOCK-214)
- **Estado:** `docs/mcp.md` con el único servidor aprobado (el MCP oficial de Atlassian, solo lectura), la configuración de OpenCode (`opencode.json`), Codex (`.codex/config.toml`), Claude Code (`.mcp.json`) y VS Code (`.vscode/mcp.json`), y la regla de uso de MCP en `AGENTS.md`. Las 11 herramientas de lectura y las 14 de escritura son las mismas del frontend (spec 013).
- **Decisiones:** OpenCode usa `atlassian_*: ask` antes de las reglas por herramienta, así una herramienta de escritura nueva del servidor nunca queda permitida por omisión (la lista del frontend solo niega las 14 conocidas); GitHub se sigue usando con `gh`; no hay MCP de MongoDB.
- **Pendiente:** la restricción de escritura en **Claude Code** (`.claude/settings.json`) y **Gemini CLI** (`.gemini/settings.json`) necesita cambiar `scripts/sync_agents.py`, que es zona protegida; requiere la autorización del líder (CA-03 queda parcial). La prueba con y sin sesión (T-04, OAuth interactivo) y confirmar la dirección `.../v1/mcp/authv2` con la documentación oficial son manuales. VS Code no filtra herramientas por archivo: el control es manual.
- **A evitar:** guardar tokens en la configuración, usar el MCP de Atlassian con Claude Code o Gemini para escribir mientras falte la restricción, y agregar un servidor que no esté en `docs/mcp.md`.


## 2026-11-04 — OpenAPI por servicio: contrato de Identity (KNOCK-197, spec 002)
- **Decisión:** `scripts/export_openapi.py` acepta `--service market|identity` (por defecto `market`, sin cambios). Identity se exporta a `docs/openapi.identity.v1.json`; Market conserva `docs/openapi.v1.json`. Identity arranca con configuración sintética generada al azar en cada ejecución (Redis, `Jwt:SigningKey`, llaves de `Identity:PhoneProtection`, Firebase de demostración, Mongo inalcanzable). El paso del CI regenera y compara los dos archivos, y el job `oasdiff` revisa también el de Identity. Los saltos CRLF de los comentarios XML se unifican a LF para que el contrato salga igual en Windows y en Linux. Pruebas en `scripts/test_export_openapi.py`.
- **Porqué:** el frontend (F-006) genera su cliente Dart desde el contrato y Identity solo tenía el endpoint de inicio de sesión sin contrato publicado. Se dejó el nombre de Market para no romper a quien ya lo consume.
- **Pendiente:** publicar el contrato de Identity al frontend (cuando exista el flujo de publicación, hoy no hay `contract-publish.yml` en el repositorio); regenerarlo si cambia la API de Identity antes de fusionar el PR #12; cambiar la base de este PR a `sprint-03` al fusionar el #12; que `ProblemDetails` declare `correlationId`/`traceId` y que las respuestas declaren el encabezado `X-Correlation-Id` (hoy no salen en el contrato).
- **A evitar:** editar a mano cualquier `docs/openapi*.json`; poner claves o contraseñas literales en el script (se generan en tiempo de ejecución); renombrar `docs/openapi.v1.json` sin coordinarlo con el frontend.

## 2026-11-04 — Spec 009: seguridad del gateway (KNOCK-204)
- **Estado:** el gateway valida el JWT (HS256, 32 bytes o más, firmante, destinatario y vencimiento sin tolerancia), rechaza con 401 los JWT cuyo `sid` esté en `sesion-cancelada:{sid}` de Redis, aplica denegar por defecto, encabezados de seguridad, CORS por orígenes configurados y límite de peticiones con contadores en Redis (por IP antes de autenticar, estricto en `/auth` y por usuario después), con 429 y `Retry-After` en segundos. 22 pruebas de integración en verde con Redis real (las 12 nuevas del gateway más las 10 del enrutamiento).
- **Decisiones:** cada ruta de YARP declara su política (`anonymous` para `auth/firebase`, `auth/refresh` y `oficios`; `default` para el resto) y una política de respaldo exige sesión; si Redis no responde, el gateway responde 503 (no deja pasar sin poder revisar la sesión ni sin contar); los contadores usan un script de Redis (incremento y expiración juntos) para no dejar claves sin vencimiento; la política CORS se llama `knock` porque YARP reserva `default` y `disable`. Los valores de límite (100/min por IP, 10/min en `/auth`, 120/min por usuario) son un punto de partida en configuración.
- **Pendiente:** el filtro de registros sin datos personales del gateway (T-05) llega con el PR de observabilidad (#10); las llaves de Redis, `Jwt:SigningKey` y los proxies de confianza (`ForwardedHeaders`) los define una persona al desplegar; el costo de consultar Redis en cada petición se mide con las pruebas de carga.
- **A evitar:** dejar pasar el tráfico cuando Redis no responde, usar `default` o `disable` como nombre de política CORS con YARP, y registrar tokens, `sid` o IP en los registros.


## 2026-11-05 — Spec 014: entrega y reproducibilidad (KNOCK-209)
- **Estado:** `db/entrega.js` deja una instancia vacía con colecciones, índices y datos de ejemplo (3 oficios y 12 colonias) en un solo paso, y es idempotente; `docs/lista-de-entrega.md` con la lista de verificación y su registro. La prueba `Spec014DeliveryScriptTests` corre el script de verdad con `mongosh` contra documentdb-local, dos veces. El helper `MongoToolsContainer` ahora copia al contenedor todos los scripts de `db/` y las semillas del catálogo.
- **Decisiones:** el script de entrega aplica con `load()` los scripts numerados de `db/` en orden y luego carga las semillas con upsert por clave natural (`clave`; `municipio`, `cp`, `nombre`) conservando los `_id` fijos. No usa `new Function` porque `mongosh` solo vuelve síncronas las llamadas del shell en el código que evalúa él mismo (con `new Function`, `new Mongo()` devolvía una promesa). Por usar `load()`, los scripts numerados comparten ámbito: sus `const` de nivel superior no deben repetirse.
- **Pendiente:** el ensayo de crear, desplegar y destruir (T-05) y las dos copias de la ventana de evaluación (T-06) necesitan Azure y el Terraform (KNOCK-200); el `.rar` (T-03) y su escaneo (T-04) no se han generado, solo documentado (`git archive` más conversión y `gitleaks`). El script `db/002` de Identity (PR #12) usa `db.getSiblingDB` y no funciona con `--nodb`; hay que reescribirlo con el patrón de `001_indices.js` (`KNOCK_DB_URI` y `new Mongo`) para que `entrega.js` lo aplique.
- **A evitar:** dar la entrega por probada sin correr el ensayo en Azure, empaquetar desde el disco en vez de `git archive`, y poner la URI de la base en la línea de comandos.

## 2026-11-04 — Spec 012: versionamiento por puntos (KNOCK-207)
- **Decisión:** el cálculo vive en `scripts/ci/version.py` (sin dependencias; la ruta es la del plan 012) con `bump`, `points` y `check-labels`, y sus pruebas en `scripts/ci/test_version.py` (`..._CA_012_01` a `_05`). La versión se trata como número en base 10 (`X*100+Y*10+Z+puntos`), así los acarreos Z→Y e Y→X salen de la misma cuenta; `Y` y `Z` de un dígito; un cambio incompatible da `X+1.0.0` sin importar los puntos; cero puntos no cambia nada. El job `pr-points` de `ci-pr.yml` lee las etiquetas del evento (`toJSON(github.event.pull_request.labels.*.name)` por variable de entorno, sin token ni API) y falla sin una etiqueta `puntos-<componente>-N` válida (componentes de `docs/VERSIONAMIENTO.md` §2), con una mal formada o con dos del mismo componente; las ramas `release-please--*` y `dependabot/*` quedan exentas. El `pull_request` ahora también reacciona a `labeled` y `unlabeled`, y `pr-points` entra en `needs` de `gate`. `_governance.yml` corre `test_version.py`. `CONTRIBUTING.md` §9 lleva la tabla de puntos y `docs/VERSIONES_DESPLEGADAS.md` las columnas versión, puntos y cambios. Se tocaron `scripts/**` y `.github/**` con autorización de José solo para esta tarea.
- **Porqué:** R-04 y CA-04 piden que un PR sin puntos falle y R-05 a R-07 piden un cálculo verificable antes de integrarlo al despliegue.
- **Pendiente:** T-03 (integrar cálculo, etiqueta de Git `<componente>-vX.Y.Z` e imagen en el despliegue y llenar el registro) depende del workflow de despliegue (KNOCK-202); por eso CA-05 queda parcial (la regla de cero puntos está probada en el script, no en un despliegue). Las etiquetas `puntos-…` se crean en GitHub al usarlas. Contradicción para José: `docs/VERSIONAMIENTO.md` fija `0.1.0` como primera versión y SemVer con release-please, y la spec 012 (Aprobada) fija `1.0.0` y puntos; hay que reconciliar el documento. `sdd_lint.py --trace 012` encuentra las pruebas de Python solo con el linter del PR #7; con el de `sprint-03` solo mira `tests/**/*.cs`.
- **A evitar:** interpolar el texto o las etiquetas del PR dentro de `run:` (van por `env`); subir de versión con cero puntos; aceptar `Y` o `Z` de dos dígitos; usar `latest` como etiqueta de imagen.

## 2026-11-05 — Pruebas por criterio de la spec 002 y paginación común (KNOCK-197)
- **Estado:** los cuatro criterios de la spec 002 ya tienen prueba (`Spec002ApiStyleTests`: 404 en ProblemDetails, `/health/ready` 503 con MongoDB caído y contrato publicado igual al servicio en marcha; `Spec002ServiceStructureTests`: controllers sin acceso a MongoDB, con un controller de muestra que prueba que la regla puede fallar). `Knock.Contracts` trae `PageLimit` (20 por defecto, 50 como máximo) y `CursorPage<T>` (R-05).
- **Decisión:** Identity y Market usan `UseStatusCodePages()` tras el manejador de excepciones: un 404 o 405 sin cuerpo sale como ProblemDetails igual que una excepción. La prueba lo detectó: antes respondía 404 vacío.
- **Pendiente:** Market no tiene host de integración, así que su `UseStatusCodePages()` (idéntico) no tiene prueba propia; el ProblemDetails trae `correlationId` y el `traceId` que pide R-03 lo decide quien abra el PR de correlación; la paginación aún no la usa ningún listado (llega con la spec 010).
- **A evitar:** pruebas de arquitectura que pasan en vacío (comprobar siempre que el conjunto evaluado no esté vacío); la palabra «todo» en un comentario (el analizador S1135 la toma por TODO).

## 2026-11-05 — Las imágenes actualizan apt por el CVE de la imagen base (KNOCK-232)
- **Decisión:** en la etapa final de los cuatro Dockerfiles, antes de `USER`, una sola capa `apt-get install --only-upgrade "apt"` (el nombre va entre comillas: sin ellas Checkov marca `CKV_DOCKER_9`, que busca ` apt ` suelto, y con `apt=versión` Dockle marca `CIS-DI-0010` al tomarlo por una variable) y borra las listas de paquetes; el usuario sigue siendo no-root y el puerto 8080 no cambia.
- **Porqué:** Trivy empezó a marcar `CVE-2026-109518` (2 HIGH, con corrección) en `apt 3.2.0` de la imagen base `aspnet:11.0`, y eso hacía fallar el job de contenedores de todos los PR del backend. Microsoft aún no publica una imagen reconstruida (el digest vigente es el mismo), así que no hay otra imagen a la que subir.
- **Pendiente:** quitar ese bloque en cuanto Microsoft publique una imagen `aspnet:11.0` con `apt >= 3.2.1ubuntu0.1`; comprobarlo cada vez que se reconstruyan las imágenes.
- **A evitar:** dejar esta capa de forma permanente (hace menos reproducible la construcción) o resolver el fallo con una excepción de Trivy si existe una corrección disponible.

### 2026-11-05 · Pruebas de seguridad y huecos unitarios de los 7 endpoints de negocio
- **Decisión:** `tests/SecurityTest` deja de estar vacío: 264 pruebas (261 verdes, 3 omitidas como evidencia de hallazgos) contra los hosts reales de Identidad y Mercado en `Production`, con MongoDB (documentdb-local) y Redis de Testcontainers; los únicos dobles son el verificador de Firebase (`FakeFirebaseVerifier`) y el localizador y catálogo de colonias. Se enlazan `DocumentDbFixture`, `RedisTestFixture` y `ConsoleCapture` de las otras suites en vez de copiarlos. En `tests/UnitTest` se agregan 61 pruebas (configuración y validación del JWT, guardia de arranque, límite de cuerpo, clave del limitador, controllers de logout, refresh y colonias, reglas hostiles del perfil). Nombres: `SEC_<spec>_<NN>` (`SEC_TT_PRU_01_NN` y `SEC_TT_API_07_NN` para Mercado); no tocan `CA_NNN_KK` y `sdd_lint` sigue igual.
- **Por qué:** había integración por criterio pero ninguna prueba negativa por endpoint (OWASP API1, API2, API3, API4, API8); correr en Production evita que el `stackTrace` de Development oculte fugas, y sin emulador de Firebase las pruebas de seguridad no dependen de él.
- **Pendiente:** corregir H-1 (cancelar en Redis solo si la sesión es del `sub`), H-2 (400 automático con nombres de tipos .NET) y H-3 (`fotoUrl` con CR/LF) y quitar el `Skip` de `SEC_016_06`, `SEC_017_16` y `SEC_017_06`; Mercado no tiene integración de `GET /oficios` ni de `GET /colonias/en-punto`; `--trace 009` sigue con CA-05 sin prueba (ya ocurría antes); la validación real del ID token de Firebase solo la cubre la integración con emulador.
- **A evitar:** forjar un JWT sin `exp` con la librería (agrega uno por defecto: hay que armarlo a mano); usar `IdentityApiFactory` para medir fugas (corre en Development); compartir el `X-Test-Remote-Ip` entre pruebas del limitador; commitear `packages.lock.json` con CRLF.

## Mutacion (Stryker.NET) configurada
- .config/dotnet-tools.json: dotnet-stryker 5.0.0 fijo.
- stryker-config.json en la raiz (solucion Knock.slnx, pruebas solo tests/UnitTest con test-case-filter, mutate acotado a Sesiones, Usuarios, Catalog y Gateway).
- docs/MUTACION.md: guia en espanol.
- Verificacion local acotada: 47.17 por ciento. El job mutation-pr ya corre de verdad.