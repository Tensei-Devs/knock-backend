# Tareas 060 — Apelación de una persona suspendida

> **Estado:** pendientes. En esta etapa **no se ejecutan**: se activan cuando la spec esté Aprobada (firma de José) y una persona lo indique.

| Tarea | Qué se hace | Cubre | Depende de | Estado |
|---|---|---|---|---|
| T-01 | Validador del cuerpo (`texto` de 8 a 500) y reglas puras de la apelación (una sola vez, idempotencia por texto) con unitarias | R-03, R-04 · CA-04, CA-05 | — | pendiente |
| T-02 | Extraer del inicio de sesión de Firebase el verificador de `idToken` con `auth_time` reciente para reutilizarlo (sin duplicar) | R-01 · CA-02 | — | pendiente |
| T-03 | `SuspensionAppealService`: huella del número, 404 uniforme, creación de la apelación con relectura y `KeyedGate`, asignación a otra persona del panel (o «misma persona») | R-02, R-04, R-05 · CA-01, CA-03, CA-05, CA-10 | T-01, T-02 | pendiente |
| T-03a | Vigencia: `suspension.venceEn` (7 días) en la suspensión gradual, tarea programada que reactiva al vencer y regla de que la crítica no vence; tope de una apelación abierta y ninguna tras un rechazo | R-04a, R-06a · CA-08a, CA-08b | T-03 | pendiente |
| T-04 | `POST` y `GET /api/v1/suspension/apelacion` con política de límites `suspension-apelacion` (5 por hora por número y 20 por hora por IP) y tope de cuerpo de 2 KiB | R-07, R-09 · CA-11, CA-13 | T-03 | pendiente |
| T-05 | `POST /api/v1/admin/apelaciones/{id}/resolucion` (permiso `reports:resolve` y doble factor): aceptar llama `Reactivar` (spec 059), rechazar cierra la apelación; auditoría y `ApelacionResuelta` en la bandeja | R-06, R-10 · CA-07, CA-08, CA-09 | T-03 | pendiente |
| T-06 | Rutas públicas en el gateway (sin autorización de Knock) y la de panel dentro de `/api/v1/admin/**`; prueba de enrutamiento | §5 | T-04, T-05 | pendiente |
| T-07 | Pruebas de seguridad negativa (tokens inválidos o viejos, enumeración, límites, permisos del panel) | CA-02, CA-03, CA-09, CA-11 | T-04, T-05 | pendiente |
| T-08 | Prueba de que registros, auditoría y errores no llevan `idToken`, número, texto de la apelación ni texto del reporte | R-08 · CA-12 | T-04, T-05 | pendiente |
| T-09 | OpenAPI de Identidad regenerado, `docs/endpoints.md`, `docs/rate-limit-matrix.md`, `docs/events.md` y `Memoria.md` | §12 | T-06 | pendiente |
| T-10 | Gates de `AGENTS.md §5`, par revisor, `security-reviewer` y `python scripts/sdd_lint.py --trace 060` | §12 | todas | pendiente |

## Definición de terminado
- Los casos CA de `spec.md` tienen prueba con su ID; `python scripts/sdd_lint.py --trace 060` en OK.
- Gates de `AGENTS.md §5` en verde, par revisor y `Memoria.md` al día.
