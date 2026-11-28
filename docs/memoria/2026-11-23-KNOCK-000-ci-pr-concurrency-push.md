# 2026-11-23 · KNOCK-000 · ci-pr no cancela los runs de push

- **Qué cambió:** en `.github/workflows/ci-pr.yml`, `cancel-in-progress` pasó de `true` a `${{ github.event_name == 'pull_request' }}`. El `group` no cambió: usa `github.ref`, que es `refs/pull/N/merge` en PR y `refs/heads/X` en push, así que un push no espera detrás de un PR.
- **Por qué:** cada merge a `sprint-05` cancelaba el run completo (job API) del push anterior y no quedaba un resultado verde de la punta.
- **A evitar:** volver a poner `true` fijo; cambiar el `group` para que PR y push lo compartan.
