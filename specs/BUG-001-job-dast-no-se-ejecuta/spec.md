# BUG-001 — El job `dast` del CI nunca se ejecuta por archivos faltantes

> **Jira:** KNOCK-238 · **Área:** CI · **Servicio:** Transversal (CI del repositorio) · **Responsable:** Alexis Blas · **Sprint:** S4 · **Puntos:** 1 (corrección) · **Prioridad:** Alta · **Capas:** CI
> **Repositorio:** knock-backend · **Otra parte en:** —
> **Encontrado en:** TT-SEC-03 (KNOCK-78), `docs/modelo-de-amenazas.md` §3, brecha **CI-6 / H-06** (2026-11-06).
> **Relacionadas:** spec 007 (CI y despliegue; deja ZAP fuera de su alcance hasta tener un servicio desplegable), KNOCK-233 (comprobaciones obligatorias de los rulesets, spec 007 T-05), KNOCK-239 y KNOCK-240 (encabezados del gateway, de otra persona).
> **Fuentes:** `.github/workflows/ci-pr.yml` (jobs `detect`, `dast` y `gate`) · `docs/QA_DEVSECOPS.md` (filas DAST y Smoke) · `docs/modelo-de-amenazas.md` · `deploy/docker-compose.local.yml` · `src/*/Dockerfile` · `security/exceptions.yaml`
> **Skills:** story-workflow, spec-first-writing, foundation-backend, security-core, testing-strategy-backend, reuse-before-create
> **Estado:** Borrador

## 1. Objetivo
Que el escaneo dinámico (ZAP baseline) y la prueba de humo que `docs/QA_DEVSECOPS.md` promete en cada PR **se ejecuten de verdad**. La pila de servicios corre dentro del runner, sin costo y sin Azure. Si el escaneo deja de poder ejecutarse, el `gate` debe fallar en vez de darlo por bueno.

**Qué pasa hoy.** El job `detect` calcula `dast=true` solo si existen `deploy/docker-compose.ci.yml` y `tests/smoke/smoke.sh`. Ninguno de los dos existe, y el job `dast` también lee `security/zap-rules.tsv`, que tampoco existe. Por eso `dast` sale como `skipped` en todas las ejecuciones (por ejemplo, la ejecución 42664316144 de `sprint-04`). El `gate` acepta `skipped` como bueno, así que la brecha no se ve en ningún PR. Además, el job todavía dice «PostgreSQL», aunque la base es MongoDB (`documentdb-local`) desde TT-BD-06.

## 2. Defecto y casos de prueba
**BUG-001** — Como equipo, queremos que el escaneo dinámico y la prueba de humo corran en cada PR que cambia un servicio, para detectar configuraciones inseguras antes de fusionar.

| Caso | Criterio (Dado / Cuando / Entonces) | Verificación |
|---|---|---|
| TC-BUG-001-01 | Dado un PR contra `sprint-04` que toca `src/`, cuando corre `ci-pr`, entonces `detect` da `dast=true` y el job «Smoke + DAST (ZAP baseline)» termina en `success` o `failure`, nunca en `skipped`. | (comando) `gh run view <id> --json jobs`; la salida va en el PR |
| TC-BUG-001-02 | Dado `deploy/docker-compose.ci.yml` con credenciales efímeras del job, cuando se levanta en el runner, entonces el gateway, Identidad, Mercado, Mensajería, `documentdb-local`, Redis y Azurite arrancan, y `GET /health/ready` del gateway responde 200 en 120 s o menos. | (comando) paso «Levantar» del job; la salida va en el PR |
| TC-BUG-001-03 | Dada la pila levantada, cuando corre `tests/smoke/smoke.sh <url>`, entonces comprueba que `/health` responde 200, que una ruta protegida sin token responde 401 con `application/problem+json` y que están los encabezados de seguridad de la spec 009 que ya existen hoy; termina con 0 solo si todo se cumple. | (comando) paso «Smoke» del job |
| TC-BUG-001-04 | Dado un servidor de prueba que omite un encabezado de seguridad o responde 200 sin token, cuando corre `smoke.sh` contra él, entonces termina con un código distinto de 0 y nombra la comprobación que falló, sin imprimir cuerpos ni encabezados con valores sensibles. | `tests/smoke/smoke_test.sh` → `TC-BUG-001-04` |
| TC-BUG-001-05 | Dado `security/zap-rules.tsv`, cuando ZAP baseline encuentra una alerta de riesgo alto (o media sin regla), entonces el job falla; cada regla en `IGNORE` o `WARN` cita su `EXC-NNN` de `security/exceptions.yaml` con motivo y vencimiento. | (comando) ejecución del job · revisión de `zap-rules.tsv` en el PR |
| TC-BUG-001-06 | Dado un PR donde `detect` da `docker=true` pero `dast=false` (falta un archivo de la pila), cuando corre `gate`, entonces `gate` falla con un mensaje que nombra el archivo que falta, en lugar de aceptar el `skipped`. | (comando) PR de prueba que borra `tests/smoke/smoke.sh`; la salida va en el PR |
| TC-BUG-001-07 | Dados el archivo de compose, `smoke.sh`, `zap-rules.tsv` y los registros del job, cuando se revisan, entonces no contienen contraseñas, llaves ni cadenas de conexión con credenciales (solo `${CI_*}` del entorno del job, enmascaradas), y Gitleaks no reporta hallazgos nuevos. | (comando) job «Secretos (Gitleaks)» en verde |
| TC-BUG-001-08 | Dada la ejecución del job, cuando se revisa, entonces no llama a Azure ni a servicios de pago, las imágenes de terceros del compose están fijadas por digest y el job termina dentro de su `timeout-minutes` (25). | (comando) revisión del compose y duración de la ejecución en el PR |

## 3. Alcance / No-alcance
- **In (este repositorio):**
  - Crear `deploy/docker-compose.ci.yml`: los cuatro servicios construidos desde sus `Dockerfile`, más `documentdb-local`, Redis y Azurite con las mismas imágenes fijadas por digest de `docker-compose.local.yml`. Sin puertos publicados salvo el 8080 del gateway, y con la configuración de cada servicio tomada de variables `CI_*`.
  - Crear `tests/smoke/smoke.sh` (bash, sin dependencias además de `curl`) y su autoprueba `tests/smoke/smoke_test.sh`.
  - Crear `security/zap-rules.tsv` (al principio vacío o con reglas justificadas por `EXC-NNN`).
  - Cambios en `.github/workflows/ci-pr.yml`, que **los aplica una persona**, porque `.github/` está protegido para el agente:
    1. El `gate` falla si `docker=true` y `dast=false` (TC-06).
    2. Los nombres de los pasos dicen MongoDB (`documentdb-local`) en lugar de PostgreSQL.
    3. Si hace falta, el paso de credenciales exporta las variables que pida el compose.
  - Actualizar `docs/QA_DEVSECOPS.md`, cerrar la brecha CI-6 / H-06 en `docs/modelo-de-amenazas.md` y registrar en `Memoria.md`.
- **Out:**
  - El escaneo de API o activo de ZAP con OpenAPI y un token de prueba (`docs/modelo-de-amenazas.md` §4; corre de noche y no bloquea el PR).
  - Infraestructura temporal en Azure.
  - Corregir los hallazgos de encabezados del gateway: KNOCK-239 (HSTS) y KNOCK-240 (`Server: Kestrel`) son de otra persona. Esta spec no los arregla; ver §8 sobre cómo los trata el smoke.
  - Seguridad propia de Mensajería (KNOCK-241).
  - Llenar las comprobaciones obligatorias de los rulesets (KNOCK-233, spec 007 T-05).

## 4. Contratos API y eventos
No agrega ni cambia endpoints. Usa los existentes: `GET /health` y `GET /health/ready` del gateway, y una ruta protegida del gateway para la comprobación de 401 (por ejemplo `GET /api/v1/me`). Interfaz que fija: `smoke.sh <url-base>` devuelve 0 si todas las comprobaciones pasan, y si no, un código distinto de 0 con una línea por comprobación fallida.

## 5. Modelo de datos
No aplica: la pila del CI arranca con bases vacías y se destruye con `down -v` al terminar.

## 6. Seguridad, privacidad y amenazas
| Amenaza | Control | Caso que lo prueba |
|---|---|---|
| Fuga de información: credenciales en el compose o en los registros del job | Credenciales generadas por ejecución con `openssl rand` y `::add-mask::`; el compose solo lee `${CI_*}`; Gitleaks sin hallazgos | TC-BUG-001-07 |
| Alteración: imagen de terceros cambiada (cadena de suministro) | Imágenes por digest, la acción de ZAP fijada por SHA (ya lo está) | TC-BUG-001-08 |
| Repudio o control silenciado: el escaneo deja de correr y nadie lo nota | `gate` falla si `docker=true` y `dast=false` | TC-BUG-001-06 |
| Control debilitado: ignorar alertas de ZAP para «pasar» | Toda regla `IGNORE`/`WARN` necesita su `EXC-NNN` con vencimiento, revisada en el PR | TC-BUG-001-05 |
| Falso verde: un smoke que nunca falla | Autoprueba negativa de `smoke.sh` | TC-BUG-001-04 |
| Denegación del CI: la pila no arranca y bloquea todos los PR | Espera acotada (120 s) con mensaje claro y `docker compose logs` de los servicios (sin variables de entorno) al fallar | TC-BUG-001-02 |

## 7. Pantallas
—

## 8. Pruebas, gates y límites
**Plan de pruebas:**

| Tipo | ¿Aplica? | Motivo / herramienta |
|---|---|---|
| Unitarias | No | No hay código de producción nuevo |
| Integración | Sí | La pila completa en el runner (TC-02) |
| Humo | Sí | `smoke.sh` (TC-03) |
| Seguridad negativa | Sí | Autoprueba de `smoke.sh` (TC-04) y `gate` con un archivo faltante (TC-06) |
| DAST | Sí | ZAP baseline (TC-05) |
| Contrato | No | No cambia rutas |
| Arquitectura | No | No cambia código |
| Mutación | No | No hay lógica de dominio |
| Regresión | Sí | El resto de los jobs de `ci-pr` sigue igual |
| CI y análisis | Sí | Checkov sobre el compose y los workflows, y Gitleaks |

**Gates:** ver `AGENTS.md` (Definición de terminado); además, una ejecución real del job en un PR de prueba con la salida en el PR.

**Límites y dudas abiertas:**
- **Archivos protegidos:** los cambios a `.github/workflows/ci-pr.yml` (TC-06 y los nombres de los pasos) los aplica una persona. Sin ellos, TC-06 no se puede cerrar.
- **Hallazgos esperados en el gateway:** si el smoke y ZAP comprueban HSTS o el encabezado `Server`, fallarán hasta que se fusionen KNOCK-239 y KNOCK-240. Propuesta: que el smoke compruebe solo los encabezados que ya existen hoy, y que esos dos se agreguen cuando se fusionen sus correcciones. La alternativa es registrarlos en ZAP como `WARN` con un `EXC-NNN` que venza al fusionarse. **Lo decide el revisor.**
- **Configuración mínima de cada servicio en el CI:** hay que confirmar con qué valores sintéticos arrancan Identidad (Firebase: emulador o verificación desactivada solo en el perfil `Ci`), Mercado (contenedores de Azurite) y Mensajería. No se agrega ningún modo inseguro que pueda activarse fuera del CI.
- **`/health/ready` del gateway:** hay que confirmar que la ruta existe y que refleja la salud de los servicios. Si no, la espera usa la salud de cada servicio.
- **Tiempo del runner:** construir cuatro imágenes puede acercarse a los 25 minutos. Si pasa, se reutilizan las imágenes del job `container` en lugar de volver a construirlas.

## Aprobación
- [ ] Revisor 1 — nombre: ________ fecha: ________
- [ ] Sin conflicto con la constitución
- **Estados:** Borrador (agente) → Aprobada (persona; habilita el código) → Firmada (tras el merge)
