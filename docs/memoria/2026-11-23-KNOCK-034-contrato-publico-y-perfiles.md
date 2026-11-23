# 2026-11-23 · Spec 034: contrato público, rutas del contrato y perfiles de réplicas

## Qué cambió
- `scripts/export_openapi.py --publico` genera `docs/openapi.publico.v1.json` uniendo los tres contratos de servicio con las rutas y verbos que expone el gateway (sin `servers`, orden estable, solo esquemas alcanzables). Falla nombrando la ruta o el esquema si dos servicios chocan. `--check` compara sin escribir y `--comprobar-publico` rechaza `servers`, hosts internos y ejemplos con celular +52, CURP o correo (R-19).
- `ci-pr.yml` regenera los tres contratos y el público, falla nombrando los archivos que cambiaron y `oasdiff` también compara el público (etiqueta `contrato-incompatible` sigue permitiéndolo).
- `tests/ContractTest`: fábricas por servicio, lector de operaciones, validador de esquemas y prueba de rutas en ambos sentidos contra el host real (`InternalsVisibleTo` de Identity y Market para ese proyecto).
- `deploy/apps/tests/replicas.tftest.hcl` prueba los perfiles `evaluacion` (2) y `dev` (máximo 1) y Redis (1); el módulo `service` expone `min_replicas` y `max_replicas`.

## Por qué
La spec 034 pide un contrato vivo que no se desfase del código ni revele la topología interna, y que los perfiles de réplicas no cambien sin una prueba.

## A evitar
- Editar `docs/openapi.publico.v1.json` a mano: se regenera con el script.
- Suponer que dev tiene 1 réplica: por la decisión de #277 está en 0-1 y la spec dice 1; la prueba acepta 0 o 1 con máximo 1 hasta que se concilie.
