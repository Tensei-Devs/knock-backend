# Calidad, pruebas y DevSecOps — knock-backend

> Matriz de controles realmente configurados en este repositorio. Si cambias una herramienta, actualiza esta tabla en el mismo PR.
> Nivel: **PR** = `ci-pr.yml` (rápido, bloquea vía `gate`) · **Nightly** = `nightly.yml` (exhaustivo, abre issue si falla) · **Deploy** = `deploy-staging.yml` / `deploy.yml` · **Local** = git hooks y guardián de agentes.

## 1. Comparación con los repositorios de referencia del profesor

| Práctica en los repos del profesor | Dónde la usan | En knock-backend |
|---|---|---|
| Unit, Integration (Testcontainers), Security, Database, Contract | WebAPIDevSecOpsScalling, API_SDD | ✅ mismas 5 suites (con PostGIS, Redis y Azurite) |
| Mutación con Stryker (PR incremental + nightly) | Scalling, ScallingSDD, API_SDD | ✅ PR informativo `--since` + nightly con umbral por módulo |
| Property-based (FsCheck) | Scalling | ✅ en la estrategia de unitarias |
| Pact / contrato | Scalling | ✅ captura del wire + OpenAPI + oasdiff (+ clientes generados en frontend) |
| Rendimiento (NBomber) | Scalling | ✅ k6 (lo fija la propuesta v2.0): humo, carga y estrés |
| Fuzzing (RESTler) | Scalling | ✅ RESTler nightly |
| Caos (caída de Redis/SQL, latencia) | Scalling | ✅ caída de Redis y PostgreSQL con recuperación |
| SAST: Semgrep, SonarAnalyzer, SonarCloud, CodeQL | todos | ✅ Semgrep con reglas de Knock (es la solución de análisis estático, ver §3.1), SonarAnalyzer + analizadores .NET como error, SonarCloud opcional; CodeQL no se usa (decisión del líder, KNOCK-64) |
| SCA: NuGetAudit, Dependabot, dependency-review, Snyk | todos | ✅ NuGetAudit, Dependabot, dependency-review con licencias, OSV-Scanner (en lugar de Snyk, sin cuenta) |
| Secretos: Gitleaks | WebDevSecOps | ✅ Gitleaks en PR + pre-commit + push protection de GitHub |
| Contenedores: Trivy, Dockle, Cosign, SBOM | Scalling, WebDevSecOps | ✅ Trivy, Dockle, SBOM y procedencia en la imagen, firma Cosign y verificación antes de desplegar |
| DAST: ZAP | Scalling, WebDevSecOps | ✅ ZAP baseline en PR + API scan nightly |
| Gates SDD (critic, endpoints, sdd-lint, cobertura) | API_SDD | ✅ sdd_lint + trazabilidad TC, mapa de código, excepciones, cobertura y actionlint |
| Agentes con skills y revisores sin edición | API_SDD (.opencode) | ✅ para 6 herramientas + hooks que bloquean |
| — | — | ➕ Nuevo: arquitectura (NetArchTest + codemap), duplicados, Checkov (IaC/Docker/Actions), sqlfluff, pinning a SHA, registro de excepciones, reglas de ramas/commits/PR |

## 2. Pruebas

| Tipo | Valida | Herramienta | Dónde | Nivel | Bloquea |
|---|---|---|---|---|---|
| Unitarias | Reglas de dominio, servicios, validadores | xUnit v2, AwesomeAssertions, FsCheck | `tests/UnitTest` | PR | Sí |
| Arquitectura | Fronteras de módulo, pipeline, controllers sin DbContext | NetArchTest.eNhancedEdition + `codemap.py --check` | `tests/UnitTest/Architecture` | PR + Local | Sí |
| Integración | Endpoints reales contra base y caché reales | WebApplicationFactory + Testcontainers | `tests/IntegrationTest` | PR | Sí |
| Seguridad negativa | 401/403/404 ajenos, roles, 429, entradas maliciosas, headers | xUnit + WAF | `tests/SecurityTest` | PR | Sí |
| Base de datos | Migraciones, semillas idempotentes, restricciones, PostGIS, respaldo | Testcontainers `postgis/postgis` | `tests/DatabaseTest` | PR | Sí |
| Contrato | JSON real, OpenAPI al día, sin cambios incompatibles | Captura + `export_openapi.py` + oasdiff | `tests/ContractTest`, `docs/openapi.v1.json` (Market), `docs/openapi.identity.v1.json` (Identity) | PR | Sí |
| Aceptación | Un caso `TC-<ID>-KK` por criterio | Suites de arriba + `sdd_lint --trace` | specs | PR | Sí |
| Smoke | Salud, auth exigida, headers | `tests/smoke/smoke.sh` | PR (pila CI) + Deploy | Sí |
| Mutación | Que las pruebas detecten errores | Stryker.NET | `stryker-config.json` | PR (informativo) · Nightly (Identity ≥ 80 %, negocio ≥ 70 %) | Nightly |
| Fuzzing | 500, fugas, acceso a recursos ajenos | RESTler | `tests/fuzz` | Nightly | Nightly |
| Rendimiento | p95, 2 instancias ≥ 1.6× 1, degradación bajo estrés | k6 | `tests/perf` | Nightly | Nightly |
| Caos | Degradación controlada y recuperación | Docker Compose | `tests/chaos` | Nightly | Nightly |

Cobertura mínima: **60 % líneas, 50 % ramas** (combinada, `check_coverage.py`).

## 3. Seguridad y cadena de suministro

| Control | Herramienta | Nivel | Bloquea |
|---|---|---|---|
| SAST | Semgrep (`.semgrep/semgrep.yaml` + p/csharp, p/secrets, p/owasp-top-ten, p/dockerfile); alcance y límites en §3.1 | PR y push a ramas fijas | Sí (ERROR) |
| Analizadores .NET + SonarAnalyzer como error | `Directory.Build.props`, `.editorconfig` | PR + Local | Sí |
| Dependencias vulnerables | NuGetAudit, `dotnet list --vulnerable`, OSV-Scanner, dependency-review | PR | Sí (altas/críticas) |
| Licencias | dependency-review (deny GPL/AGPL/SSPL) + OSV nightly | PR + Nightly | Sí |
| Dependencias obsoletas | `dotnet list --deprecated`, Dependabot | PR (informativo) + semanal | No |
| Secretos | Gitleaks (PR y pre-commit), push protection | PR + Local | Sí |
| Contenedor | Trivy (críticas/altas con fix), Dockle | PR + Deploy | Sí |
| IaC, Dockerfile, workflows | Checkov, actionlint, pinning a SHA | PR | Sí |
| DAST | ZAP baseline (PR), ZAP API scan (nightly) | PR + Nightly | Sí |
| SQL | sqlfluff | PR + Local | Sí |
| Firma y procedencia | SBOM + provenance (BuildKit), Cosign keyless, verificación antes de desplegar | Deploy | Sí |
| Excepciones | `check_exceptions.py` (vencidas, supresiones sin EXC) | PR + Local | Sí |

### 3.1 Análisis estático: Semgrep es la solución y CodeQL no se usa (KNOCK-64)
> **Decisión del líder (2026-11-06):** el análisis estático del repositorio es Semgrep. No se contrata GitHub Advanced Security (GitHub Code Security) para CodeQL. Lo que sigue describe lo que el workflow hace de verdad, leído en `.github/workflows/ci-pr.yml` y `.semgrep/semgrep.yaml`.

**Qué cubre.** El job `sast` («SAST (Semgrep con reglas de Knock)») corre en cada pull request y en cada push a `main`, `release`, `develop` y `sprint-*`; el workflow no tiene ejecución programada. Instala Semgrep con `pip install semgrep` y ejecuta `semgrep scan --config .semgrep/semgrep.yaml --config p/csharp --config p/secrets --config p/owasp-top-ten --config p/dockerfile --error --severity ERROR --sarif-output=semgrep.sarif`. Cualquier hallazgo de severidad `ERROR` hace fallar el job, y `sast` es uno de los jobs de los que depende el check `gate`.

| Fuente de reglas | Qué revisa |
|---|---|
| `.semgrep/semgrep.yaml` (11 reglas propias de Knock: 10 en `ERROR` y 1 en `WARNING`) | SQL interpolado en `FromSqlRaw`/`ExecuteSqlRaw`, `try/catch` y `DbContext` en controllers, validación de JWT debilitada, certificados TLS sin validar, `System.Random` para tokens, datos sensibles en logs, CORS abierto, deserialización insegura y `Task.Delay` como plazo de negocio (`Console.WriteLine` es solo advertencia) |
| `p/csharp` | Reglas públicas de Semgrep para C# |
| `p/secrets` | Secretos y llaves en el código |
| `p/owasp-top-ten` | Reglas alineadas con OWASP Top 10 |
| `p/dockerfile` | Malas prácticas en Dockerfile |

La última ejecución del job en el PR #27 cargó 216 reglas, según la API de code scanning de GitHub. El resultado se sube como SARIF a code scanning (categoría `semgrep`) solo si el repositorio es público o existe la variable `GHAS_ENABLED=true`; hoy el repositorio es público. Las supresiones (`// nosemgrep: <regla> -- EXC-NNN`) exigen una excepción vigente en `security/exceptions.yaml`.

**Qué no cubre frente a CodeQL.**
- **Análisis entre archivos.** El workflow instala la edición comunitaria de Semgrep, sin cuenta ni motor Pro. Según la documentación de Semgrep, esa edición solo analiza las interacciones dentro de una sola función; el análisis entre archivos es de un motor propietario que se instala aparte (`semgrep install-semgrep-pro`) y requiere una cuenta de su plataforma ([documentación de Semgrep](https://docs.semgrep.dev/semgrep-code/semgrep-pro-engine-intro)). CodeQL construye una base de datos que incluye el grafo de flujo de datos y el de control del código ([documentación de CodeQL](https://codeql.github.com/docs/codeql-overview/about-codeql/)). Una vulnerabilidad cuyo camino cruza archivos, por ejemplo un dato no confiable que va de un controller a un servicio en otro archivo, puede no verse con Semgrep.
- **Reglas propias basadas en patrones.** Las 11 reglas de Knock buscan formas concretas de código; no demuestran que un dato llegue a un punto peligroso.
- **Advertencias.** Los hallazgos `WARNING` e `INFO` se reportan pero no bloquean (`--severity ERROR`).
- **Sin análisis programado.** No hay ejecución semanal o nocturna: el código se analiza cuando hay un PR o un push a las ramas fijas.
- **Workflows de GitHub Actions.** CodeQL ofrece el lenguaje `actions`; con Semgrep esa revisión no existe. Los workflows los cubren Checkov (marco `github_actions`), actionlint y el pinning a SHA.

**Por qué se acepta.**
- Costo cero: en repositorios privados el code scanning de GitHub exige tener habilitado GitHub Code Security (GitHub Team o Enterprise); las cuentas Free y Pro solo pueden usarlo en repositorios públicos ([documentación de GitHub](https://docs.github.com/en/code-security/how-tos/scan-code-for-vulnerabilities/troubleshooting/troubleshooting-analysis-errors/cannot-enable-codeql-in-a-private-repository)).
- Lo que Semgrep no ve se cubre con otros controles que ya bloquean: analizadores .NET con `AnalysisMode=All`, `TreatWarningsAsErrors` y SonarAnalyzer (`Directory.Build.props`), pruebas de arquitectura, pruebas de seguridad negativa contra los hosts reales (`tests/SecurityTest`), OSV-Scanner y revisión de dependencias, Gitleaks, ZAP baseline y la revisión del `security-reviewer` antes del PR.
- Un escáner sin alertas no prueba que el sistema sea seguro (`SECURITY.md`). El riesgo residual que se acepta es el de una vulnerabilidad que cruza archivos y escapa tanto a Semgrep como a las pruebas negativas.

**Alternativa gratuita documentada (no activada).** Mientras el repositorio sea público, CodeQL se puede activar sin costo desde Settings → Code security → Code scanning → *Set up* → *Default*. La API de GitHub lista `csharp` y `actions` entre los lenguajes disponibles para este repositorio; la configuración por omisión aparece como `not-configured` y la organización está en el plan `free`. No requiere editar workflows. Si el repositorio pasa a privado deja de ser gratuita y debe apagarse; el análisis de Semgrep sigue funcionando en ambos casos. Activarla es una decisión del líder y este documento no la activa. Si se activa, hay que volver a poner CodeQL en esta sección y en `docs/BRANCH_PROTECTION.md`.

## 4. OWASP API Security Top 10 → control → prueba

| Riesgo | Control en Knock | Prueba |
|---|---|---|
| API1 Autorización a nivel de objeto | Servicio filtra por dueño; técnico solo ve colonia | SecurityTest: recurso ajeno → 404 |
| API2 Autenticación rota | Firebase validado en servidor, JWT 15 min, refresh rotativo con reuso, blacklist | IntegrationTest de sesión, token alterado/vencido → 401 |
| API3 Propiedades expuestas | DTOs explícitos; teléfono enmascarado | ContractTest (campos exactos) |
| API4 Consumo sin límites | Rate limits, paginación máxima, tamaño de cuerpo | SecurityTest → 429 / 413 |
| API5 Autorización por función | Políticas por permiso en el panel | SecurityTest: otro rol → 403 |
| API6 Flujos de negocio sensibles | Oleadas con tope, máx. 5 cotizaciones, idempotencia | IntegrationTest de casos TC |
| API7 SSRF | Sin URLs del usuario; lista permitida | Semgrep + revisión |
| API8 Mala configuración | Pipeline fijo, headers, CORS de un origen, ProblemDetails | Smoke + ZAP + PipelineOrder |
| API9 Inventario | `docs/endpoints.md` vs controllers, versionado `/api/v1` | `check_endpoints` en CI (TT-API-01) |
| API10 Consumo inseguro de APIs | Firebase/Azure con SDK oficial, timeouts, Polly | IntegrationTest con fallos simulados |

## 5. Matriz de riesgos

| Riesgo | Impacto | Prob. | Control | Verificación |
|---|---|---|---|---|
| Fuga de biométricos | Crítico | Media | Llave por verificación, borrado 7 días, permiso + 2FA, registro de accesos | Prueba del borrado programado, 403 a otros roles |
| Acceso a datos de otro usuario | Alto | Media | Autorización por recurso | SecurityTest por endpoint |
| Duplicar eventos o perderlos con 2 instancias | Alto | Media | Outbox + consumidores idempotentes | IntegrationTest con 2 consumidores, caos |
| Inyección SQL | Alto | Baja | EF parametrizado, Semgrep, CA2100 | Semgrep + RESTler |
| Secretos en el repo | Alto | Media | Key Vault, Gitleaks, push protection, guardián | Gitleaks |
| Dependencia vulnerable | Medio | Alta | NuGetAudit, OSV, Dependabot | CI |
| Agente rompe arquitectura o duplica código | Medio | Alta | Guardián, codemap, NetArchTest, revisión | `codemap --check` |

## 6. Checks que bloquean el merge
`gate` (pr-meta, governance, build-test, sast, sca, secrets, openapi-diff, sql-lint, iac, container, dast). Configuración: `docs/BRANCH_PROTECTION.md`.

## 7. Requiere configuración externa
Azure (OIDC, recursos, Key Vault) · Firebase (proyecto y números de prueba) · `FRONTEND_CONTRACT_TOKEN` · SonarCloud (opcional) · `ANTHROPIC_API_KEY` (revisión IA opcional) · protección de ramas y environments.

## 8. Limitaciones conocidas
- Hasta TT-API-01 no hay código: los jobs de API se saltan y la cobertura no se mide.
- La imagen de RESTler se usa como en los repos de referencia (`:latest`); fijarla por digest al validarla.
- k6, RESTler, ZAP API y caos corren de noche: un fallo no bloquea el PR, abre un issue.
