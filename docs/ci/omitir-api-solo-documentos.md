# Omitir el job API cuando el PR solo cambia documentos

El job `detect` de `ci-pr.yml` publica la salida `docsonly`. El job «API — build, formato y pruebas» (`build-test`) corre solo si `api == 'true'` y `docsonly != 'true'`. Los jobs que dependen de él (`mutation-pr`, `container`, `dast`) se saltan en cascada.

## Criterio
`docsonly=true` únicamente si hay archivos cambiados y TODOS cumplen: `docs/**`, `specs/**`, `**/*.md`, `db/README.md` o `Memoria.md`; y NINGUNO es `docs/openapi*.json`.

Falla cerrado: lista vacía, push a rama nueva (`__ALL__`) o cualquier archivo de otro tipo (`src/`, `tests/`, `db/*.js`, `.github/`, `deploy/`, `scripts/`) dejan `docsonly=false` y el API corre.

## Lo que NO cambia
`gate` (acepta `success` y `skipped`), Gitleaks, SAST, SCA, gobierno, `iac`, y `openapi-diff` (oasdiff: un cambio en `docs/openapi*.json` da `docsonly=false`, y de todos modos ese job solo mira `api`).

## Prueba de la lógica (simulada, sin Azure)
| Archivos cambiados | docsonly |
|---|---|
| `docs/a.md`, `specs/034/task.md`, `Memoria.md`, `db/README.md`, `docs/img/x.png` | true |
| `README.md`, `docs/memoria/x.md` | true |
| `docs/openapi.market.json`, `specs/a.md` | false |
| `docs/a.md`, `src/X/Y.cs` | false |
| `db/050.js` | false |
| `.github/workflows/ci-pr.yml`, `README.md` | false |
| vacío o `__ALL__` | false |

## Eventos sin base (corrección)
Antes, sin `pull_request.base.sha` y sin `before` (disparo manual) o con `before` de ceros, el script comparaba `HEAD~1 HEAD`: si el último commit solo tocaba documentos, `docsonly=true` y el job API salía SKIPPED aun en un run manual (run 45229917141). Ahora esos casos fuerzan `CHANGED="__ALL__"` y el API corre completo. `pull_request` y el gate no cambian.

| Evento (simulado con `git` falso) | CHANGED | docsonly |
|---|---|---|
| `workflow_dispatch` (último commit solo docs) | `__ALL__` | false |
| push con `before` válido, solo docs/specs | lista | true |
| push con `before` válido, incluye `src/` | lista | false |
| `pull_request`, solo docs | lista | true |
| push con `before` inalcanzable (diff falla) | `__ALL__` | false |
| push con `before` de ceros (rama nueva) | `__ALL__` | false |
