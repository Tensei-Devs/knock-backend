# Migración desde el repositorio del equipo (JulianH2/knock-backend)

Fecha: 2026-11-03. Origen: rama `sprint-02`, respaldada en el bundle `_respaldos/knock-backend-origen-2026-11-03.bundle` y en la etiqueta `legacy-2026-11-03` del espejo. No se conservó el historial: la autoría va en el autor y los `Co-authored-by` de cada commit de la rama `migracion`, y en las secciones «Responsable» y «Aprobación» de cada spec.

## Qué se migró y dónde quedó
| Origen | Destino |
|---|---|
| `specs/` (TT y HU) | `specs/_legacy/` tal cual, con su estado y firmas |
| `docs/` (backlog, decisiones, arquitectura, referencias, guías) | `docs/` y `docs/migracion/` |
| Constitución, `AGENTS.md` y `Memoria.md` del origen | `docs/migracion/*_origen.md` (no sustituyen a los de Demo Base) |
| `.agents`, `.claude`, `.codex`, `.cursor`, `.gemini`, `.opencode`, `.specify`, skills | Misma ruta; los adaptadores se regeneraron con `sync_agents.py` |
| Workflows, `scripts/`, `security/`, configuraciones de calidad | Misma ruta, adaptados (ver abajo) |
| `src/Knock.Api` (módulos Catalog + Hosting + Common) | `src/Knock.Market.Api`, en net11.0 |
| Pruebas unitarias y de base de datos | `tests/UnitTest` (257) y `tests/DatabaseTest` (35), en verde |
| `src/Knock.Api` y `tests` completos | `legacy/` como referencia, fuera de la solución |

## Cambios hechos al migrar
- Los workflows usan `global.json` para la versión de .NET (net11.0); se desactivó la verificación de nombre de rama `KNOCK-n`.
- `scripts/sdd_lint.py` valida solo `specs/_legacy` (formato TT/HU); las specs `NNN-tema` aún no tienen validador.
- `scripts/codemap.py` y `.jscpd.json` ignoran `legacy/`.
- Las skills `api-client-contract` y `flutter-clean-architecture` (frontend) se sustituyeron por las del equipo; la versión anterior está en el historial de la rama.
- El `spec-writer` de `.opencode` se sustituyó por el generado; el anterior está en `docs/migracion/spec-writer_destino.md`.

## Estado de las specs (18 sin firma o en borrador)
| Spec | Procede de | Estado | Responsable | Revisión |
|---|---|---|---|---|
| HU-001-entrar-con-celular-y-codigo-sms | origen (equipo) | Aprobada | José Guillermo Rangel Hernández | José Julián Hernández Hernández (2026-10-26) |
| HU-009-zona-por-ubicacion-o-codigo-postal | origen (equipo) | Aprobada | — | José Julián Hernández Hernández (2026-10-26) |
| TT-API-01-proyecto-aspnet-core-10-base | origen (equipo) | Aprobada | José Guillermo Rangel Hernández | José Julián Hernández Hernández (2026-10-14) |
| TT-API-02-spike-token-firebase-sesion-jwt | origen (equipo) | Aprobada | José Guillermo Rangel Hernández | José Julián Hernández Hernández (2026-10-17) |
| TT-API-03-middlewares-errores-problemdetails-serilog | origen (equipo) | Firmada | José Guillermo Rangel Hernández | José Julián Hernández Hernández (2026-10-21) |
| TT-API-05-servicio-de-fotos-privadas-blob-sas | origen (equipo) | Aprobada | — | José Julián Hernández Hernández (2026-10-21) |
| TT-API-06-pipeline-de-seguridad-headers-hsts-cors | origen (equipo) | Aprobada | José Guillermo Rangel Hernández | José Julián Hernández Hernández (2026-10-29) |
| TT-API-07-endpoint-colonia-por-punto | origen (equipo) | Aprobada | Ángel Alexis Blas Castillo | José Julián Hernández Hernández (2026-10-27) |
| TT-API-12-sub-v8-con-sha-256 | origen (equipo) | Aprobada | José Guillermo Rangel Hernández | José Julián Hernández Hernández (2026-10-26) |
| TT-AZ-01-cuenta-de-azure-grupo-de-recursos-presup | origen (equipo) | Firmada | José Julián Hernández Hernández | José Julián Hernández Hernández (2026-10-12) |
| TT-AZ-02-infraestructura-como-codigo-terraform | origen (equipo) | Aprobada | José Julián Hernández Hernández | José Julián Hernández Hernández (2026-11-03) |
| TT-AZ-05-estado-remoto-de-terraform | origen (equipo) | Aprobada | José Julián Hernández Hernández | José Julián Hernández Hernández (2026-11-03) |
| TT-BD-01-postgresql-postgis-docker-compose | origen (equipo) | Reemplazada | Ángel Alexis Blas Castillo | José Julián Hernández Hernández (2026-10-13) |
| TT-BD-02-catalogo-base-oficios-colonias | origen (equipo) | Reemplazada | Ángel Alexis Blas Castillo | José Julián Hernández Hernández (2026-10-13) |
| TT-BD-03-semillas-ficticias-mongodb | origen (equipo) | Aprobada | Ángel Alexis Blas Castillo | José Julián Hernández Hernández (2026-11-03) |
| TT-BD-05-spike-documentdb-vs-atlas | origen (equipo) | Firmada | — | José Julián Hernández Hernández (2026-10-20) |
| TT-BD-06-migracion-catalogo-mongodb | origen (equipo) | Aprobada | Ángel Alexis Blas Castillo | José Julián Hernández Hernández (2026-10-27) |
| TT-CI-01-github-actions-en-cada-pr-job-branch-nam | origen (equipo) | Aprobada | José Julián Hernández Hernández | José Julián Hernández Hernández (2026-10-12) |
| TT-FB-01-proyecto-de-firebase-autenticacion-por-t | origen (equipo) | Aprobada | José Julián Hernández Hernández | José Julián Hernández Hernández (2026-10-12) |
| TT-OBS-01-observabilidad-application-insights-y-sentry | origen (equipo) | Aprobada | Alexis Blas | José Julián Hernández Hernández (2026-11-03) |
| TT-PRU-01-endpoint-get-api-v1-oficios | origen (equipo) | Aprobada | Erick Trejo Reséndiz | José Julián Hernández Hernández (2026-10-22) |
| TT-REPO-01-crear-repositorios-knock-backend-y-frontend | origen (equipo) | Aprobada | José Julián Hernández Hernández | José Julián Hernández Hernández (2026-10-12) |
| TT-REPO-02-ramas-main-release-develop-y-sprint-00-c | origen (equipo) | Aprobada | José Julián Hernández Hernández | José Julián Hernández Hernández (2026-10-12) |
| TT-REPO-03-conectar-github-con-jira-clave-knock-par | origen (equipo) | Aprobada | José Julián Hernández Hernández | José Julián Hernández Hernández (2026-10-12) |
| TT-SDD-01-constitucion-agents-md-y-skills-base-par | origen (equipo) | Aprobada | José Julián Hernández Hernández | José Julián Hernández Hernández (2026-10-12) |
| TT-STD-01-reglas-de-codificacion-editorconfig-anal | origen (equipo) | Aprobada | José Julián Hernández Hernández | José Julián Hernández Hernández (2026-10-12) |
| 001-arquitectura-de-microservicios-y-puerta-de-entrada | Demo Base | Borrador | — | sin firma |
| 002-estructura-interna-de-cada-servicio-y-estilo-de-la-api | Demo Base | Borrador | — | sin firma |
| 003-bases-de-datos-mongodb-y-redis | Demo Base | Borrador | — | sin firma |
| 004-estructura-del-repositorio-y-flujo-de-trabajo | Demo Base | Borrador | — | sin firma |
| 005-infraestructura-en-azure-con-terraform | Demo Base | Borrador | — | sin firma |
| 006-escalabilidad-balanceo-y-salud-de-los-servicios | Demo Base | Borrador | — | sin firma |
| 007-integracion-continua-y-despliegue | Demo Base | Borrador | — | sin firma |
| 008-calidad-pruebas-y-estandares | Demo Base | Borrador | — | sin firma |
| 009-seguridad-base-y-configuracion | Demo Base | Borrador | — | sin firma |
| 010-cache-en-tres-niveles-y-paginacion | Demo Base | Borrador | — | sin firma |
| 011-observabilidad-y-registro | Demo Base | Borrador | — | sin firma |
| 012-versionamiento-por-puntos | Demo Base | Borrador | — | sin firma |
| 013-almacenamiento-de-archivos-y-moderacion | Demo Base | Borrador | — | sin firma |
| 014-entrega-del-proyecto-y-reproducibilidad | Demo Base | Borrador | — | sin firma |
| 015-registro-e-inicio-de-sesion | Demo Base | Borrador | — | sin firma |
| 016-renovacion-y-cierre-de-sesion | Demo Base | Borrador | — | sin firma |
| 017-perfil-de-la-persona | Demo Base | Borrador | — | sin firma |
| 018-sesiones-por-dispositivo | Demo Base | Borrador | — | sin firma |
