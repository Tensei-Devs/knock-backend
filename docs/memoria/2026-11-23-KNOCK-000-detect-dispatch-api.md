# 2026-11-23 · KNOCK-000 · `detect` ya no omite el API en disparo manual

- **Qué cambió:** en el job `detect` de `ci-pr.yml`, `workflow_dispatch` y el push con `before` de ceros fijan `CHANGED="__ALL__"` (nueva variable `EVENT_NAME`); se quitó la caída a `git diff HEAD~1 HEAD`. Documentado con 6 casos simulados en `docs/ci/omitir-api-solo-documentos.md`.
- **Por qué:** en un run manual, si el último commit solo tocaba docs, `docsonly=true` y el job API salía SKIPPED (run 45229917141). La caída de `before` de ceros tenía el mismo defecto, contra lo que se creía.
- **A evitar:** deducir «solo documentos» de `HEAD~1` cuando el evento no trae base; `pull_request` y el gate no se tocan.
