# 2026-11-23 · KNOCK-266 · ci-pr omite el job API en PR solo de documentos

- **Qué cambió:** `detect` calcula `docsonly` (todos los archivos cambiados en `docs/`, `specs/`, `*.md`, `db/README.md`, `Memoria.md`, y ninguno `docs/openapi*.json`); `build-test` se salta cuando es `true`. Criterio y tabla de pruebas en `docs/ci/omitir-api-solo-documentos.md`.
- **Por qué:** un PR de solo documentos esperaba ~32 min el job API sin tocar código.
- **Decisiones (con porqué):** (1) Se salta solo `build-test`; `gate` ya acepta `skipped`, y Gitleaks, SAST, gobierno y oasdiff siguen. (2) Falla cerrado: ante duda corre el API. (3) `docs/openapi*.json` cuenta como código porque es contrato.
- **A evitar:** usar `paths-ignore` a nivel workflow (el `gate` requerido nunca se reportaría y bloquearía el PR); añadir aquí tipos de archivo que afecten al build.
- **Pendiente:** ninguno.
