# Tareas 023 — Zona por ubicación o código postal, con lista de espera

> **Estado (2026-11-23):** T-01, T-02, T-04 y T-05 hechas; T-03 parcial; T-06 pendiente (gates y revisión de par).
> «Depende de» = tareas que deben estar fusionadas antes; «Paralelo con» = tareas que se pueden trabajar a la vez.

| Tarea | Qué se hace | Cubre | Depende de | Paralelo con | Responsable sugerido | Estado |
|---|---|---|---|---|---|---|
| T-01 | Script de `db/` con `listas_espera` e índices; prueba de base de datos sin campos de ubicación | §6 · R-08 | — | T-02, T-03 | Alexis | hecho |
| T-02 | Consulta por código postal: validador tipado, regla de cobertura, proyección, caché positiva y negativa, `ETag` y 304 | R-01 a R-04, R-10, R-15, R-16 · CA-01 a CA-04, CA-13 a CA-15 | — | T-01, T-03 | Alexis | hecho para la consulta; `%00` lo rechaza Kestrel con 400 antes de la aplicación: se pide al Tech Lead ajustar R-01 |
| T-03 | Lista de espera: validador de `cp` y `municipio`, `upsert`, `sub` del token, límite de 5 por hora y 413 | R-05 a R-09, R-11, R-17 · CA-05 a CA-11, CA-16 | T-01 | T-02 | Alexis (o Guillermo si queda libre; tarea independiente) | parcial: falta resolver `code` genérico de errores |
| T-04 | Pruebas de integración, seguridad negativa y registros sin coordenadas ni código postal ligado a la persona | R-12 a R-14 · CA-01 a CA-16 | T-02, T-03, `MarketApiFactory` (spec 020 T-02) | T-05 | Alexis | hecha: consulta probada, incluido rechazo de `%00` por Kestrel, y límite compartido entre réplicas en `Spec023LimiteCompartidoTests.Limite_De_Lista_De_Espera_Se_Comparte_Entre_Replicas_CA_023_11` (dos hosts, un Redis real), fusionada en el PR #311 y corrida solo en el CI (sin corrida verde registrada aún); `sdd_lint --trace 023` sin casos faltantes. La parte de `code` genérico depende de T-03 |
| T-05 | OpenAPI, `docs/endpoints.md` y `docs/rate-limit-matrix.md` | §5 | T-02, T-03 | T-04 | Alexis | hecho: consulta y lista de espera documentadas |
| T-06 | Gates, `Memoria.md` y revisión de par | §12 | T-04, T-05 | — | Alexis (revisa Guillermo) | pendiente |

## Definición de terminado
- Los criterios de aceptación de `spec.md` están cubiertos por pruebas en verde.
- El CI está en verde, un par revisó el cambio y `Memoria.md` está al día.
