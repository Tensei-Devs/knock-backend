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

## 2026-11-04 — Las pruebas de tiempo corren sin cobertura (KNOCK-203)
- **Decisión:** las pruebas con `[Trait("Category", "Performance")]` se excluyen del paso de unitarias con cobertura y corren en un paso aparte, sin cobertura, con el mismo límite.
- **Porqué:** la instrumentación de cobertura hace más lento el código y `TC_TT_OBS_01_12` (1 ms por 4 KB) daba entre 1,01 y 1,08 ms; subir el límite le quitaría fuerza donde sí mide.
- **A evitar:** subir el límite de una prueba de tiempo para que pase con cobertura; marca la prueba con la categoría `Performance`.

