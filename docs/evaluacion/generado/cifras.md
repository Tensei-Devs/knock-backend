# Cifras de evidencia del backend

Generado el 2026-11-21 sobre el commit `7326cb02` (rama `docs/KNOCK-298-evaluacion-fase-0`). **No edites este archivo a mano:** corre `python docs/evaluacion/generar_evidencia.py`.

## Endpoints
_estático: operaciones de los openapi*.json (ruta × método)_

| Contrato | Operaciones |
|---|---|
| `openapi.identity.v1.json` | 39 |
| `openapi.v1.json` | 32 |
| **Total** | **71** |

## Pruebas (recuento estático)
_estático: atributos [Fact] y [Theory] en tests/<Tipo> (NO es una corrida; una Theory cuenta una vez)_

| Tipo | `[Fact]` | `[Theory]` |
|---|---|---|
| UnitTest | 598 | 260 |
| IntegrationTest | 433 | 61 |
| SecurityTest | 141 | 49 |
| DatabaseTest | 123 | 27 |
| ArchitectureTest | 11 | 5 |
| ContractTest | 2 | 6 |
| PerformanceTest | 0 | 0 |

Las cifras de una corrida real (casos ejecutados, cobertura) salen del artefacto `api-reports` y del log del job «API — build, formato y pruebas» de `ci-pr`; no se mezclan con este recuento.

## Agentes y skills
_estático: archivos por carpeta. La canónica es .agents/ (las demás son reflejos); hay que fijar y documentar cuál cuenta_

| Carpeta | Cantidad |
|---|---|
| agentes · `.agents/agents` | 7 |
| agentes · `.claude/agents` | 7 |
| agentes · `.github/agents` | 7 |
| agentes · `.opencode/agents` | 11 |
| skills · `.agents/skills` | 17 |
| skills · `.claude/skills` | 17 |
| skills · `.opencode/skills` | 25 |

## Memoria
_estático: Memoria.md (historial hasta 2026-11-20) y docs/memoria/ (un archivo por PR desde 2026-11-21)_

- `Memoria.md`: 1304 líneas y 227 secciones.
- `docs/memoria/`: 3 entradas por tarea.

## Specs
_estático: filas de tabla de specs/*/task.md por la palabra con que empieza el estado (task.md puede estar desactualizado)_

- Specs: 65.
- Tareas por estado: hecha 165, otro 19, parcial 24, pendiente 306

## Réplicas por entorno
_estático: bloque replicas de deploy/apps/envs/*.tfvars (NO es el estado desplegado; para eso, az containerapp replica list)_

- `dev`: gateway 1-1, identity 1-1, messaging 1-1, market 1-1
- `evaluacion`: gateway 2-2, identity 2-2, messaging 2-2, market 2-2

## SonarCloud
_consulta: https://sonarcloud.io/api/qualitygates/project_status (rama principal)_

- Proyecto: `Tensei-Devs_knock-backend`
- **Quality gate: NONE** (NONE = todavía no hay análisis)
- Captura manual (si la API no responde): _vacío_
