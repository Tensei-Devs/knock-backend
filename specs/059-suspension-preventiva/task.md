# Tareas 059 — Suspensión preventiva en casos críticos

> **Estado:** pendientes. En esta etapa **no se ejecutan**: se activan cuando la spec esté Aprobada y una persona lo indique. Jira: KNOCK-97 (subtareas por capa [BD] [Back] [Pruebas]).
> «Depende de» = tareas que deben estar fusionadas antes; «Paralelo con» = tareas que se pueden trabajar a la vez.

| Tarea | Qué se hace | Cubre | Depende de | Paralelo con | Responsable sugerido | Estado |
|---|---|---|---|---|---|---|
| T-00 | Prerrequisitos y dudas que hoy bloquean: spec 053 (REP-05) y 033 sin implementar; enmiendas a las specs 037 (tipo de aviso) y 054 (catálogo de `outbox`); respuestas sí/no a las decisiones 1 a 6 de §15 | §8 · §15 | — | T-01 | por asignar | parcial (KNOCK-97): enmiendas a las specs 037 (`visita_afectada`) y 054 (`CuentaSuspendida`, `CuentaReactivada`) hechas; 053 REP-05 implementada en KNOCK-86; faltan las respuestas sí/no a las decisiones 1 a 6 de §15 |
| T-01 | [BD] Script de `db/` que documenta `usuarios.suspension` (sin índices) y prueba de base de datos | §6 · R-04 | — | T-02 | José Guillermo Rangel Hernández | pendiente |
| T-02 | [Back] Dominio: `Suspender`, `Reactivar` y regla gradual con configuración `Suspension:*` validada al arrancar; pruebas unitarias | R-03, R-05, R-07, R-15 a R-17 · CA-04, CA-06, CA-11, CA-13, CA-15 | T-01 | T-01 | José Guillermo Rangel Hernández | pendiente |
| T-03 | [Back] Servicio `AccountSuspension`: transacción de estado, sesiones de la app y del panel, `outbox` y auditoría; cancelación en Redis con reintento | R-04, R-08, R-09 · CA-01, CA-02, CA-05, CA-07, CA-08, CA-18 | T-02 | T-04 | José Guillermo Rangel Hernández | pendiente |
| T-04 | [Back] Mercado: consumidor de `CuentaSuspendida` y `CuentaReactivada`, proyección del técnico y avisos a clientes con cita próxima | R-10 a R-12, R-19 · CA-02, CA-03, CA-16 | T-00 | T-03 | José Guillermo Rangel Hernández | hecho (KNOCK-97): `POST /api/v1/interno/eventos` en Mercado (permiso `eventos`), proyección `estado` del técnico idempotente por `eventoId`, un aviso `visita_afectada` por cita `confirmada`; pruebas `Spec059MarketTests` (CA-02, CA-03, CA-14, CA-16) |
| T-05 | [Back] Conexión con REP-05 (spec 053) y con el registro de advertencias; reactivación | R-01, R-02, R-14 a R-16 · CA-09, CA-10, CA-12, CA-14, CA-19 | T-03, T-04, spec 053 | — | por asignar | parcial (KNOCK-97): `IReportDecisionEffects` real (KNOCK-86), regla gradual con el conteo de advertencias decididas del propio `ReportesStore` (`IWarningCounter`, dentro de la transacción de la decisión; no depende de la 043 T-08) y reactivación por apelación probada; faltan las decisiones 1 a 6 de §15 (de José) |
| T-06 | [Pruebas] Seguridad negativa y privacidad | R-02, R-07, R-13, R-18 · CA-09 a CA-11, CA-17 | T-03 a T-05 | T-07 | José Guillermo Rangel Hernández | hecha (KNOCK-97): integración de Identidad `Spec059SuspensionTests` (CA-01, 04, 06, 07 a 13, 14, 15, 17 a 19, por la ruta del panel `POST /admin/reportes/{id}/decision`), de Mercado `Spec059MarketTests` (CA-02, 03, 14, 16, ruta interna 401/403/422) y SecurityTest `Spec059SeguridadTests` y `Spec059PrivacidadTests` |
| T-07 | `docs/events.md`, `docs/endpoints.md`, mutación del módulo y OpenAPI si cambia | §5 · §10 | T-03, T-04 | T-06 | José Guillermo Rangel Hernández | pendiente |
| T-08 | Gates, `Memoria.md`, `security-reviewer`, `traceability-clerk` y revisión de par | §12 | todas | — | por asignar | pendiente |

## Definición de terminado
- Los criterios de aceptación de `spec.md` están cubiertos por pruebas en verde (`sdd_lint --trace 059`).
- El CI está en verde, un par revisó el cambio y `Memoria.md` está al día.
