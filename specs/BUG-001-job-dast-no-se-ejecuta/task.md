# BUG-001 — tareas

> **Estado:** pendientes. No se ejecutan hasta que la spec esté Aprobada y una persona lo indique.

## T1 — Pila del CI [CI]
- **Modificar primero:** `deploy/docker-compose.local.yml`, agregar Azurite 3.37.0 por digest como en los PR de Media.
- **Crear después:** `deploy/docker-compose.ci.yml`, sin Postgres; esperar el readiness de los cuatro servicios.
- **Verificar:** TC-BUG-001-02 y TC-BUG-001-08 (`docker compose config --quiet`, arranque local, digests).
- **No hacer:** publicar puertos de las bases; escribir credenciales; llamar a Azure.
- **Agente:** db-builder o api-builder.

## T2 — Prueba de humo [Pruebas]
- **Crear:** `tests/smoke/smoke.sh` y `tests/smoke/smoke_test.sh`.
- **Verificar:** TC-BUG-001-03 y TC-BUG-001-04.
- **No hacer:** imprimir cuerpos ni encabezados con valores sensibles; exigir HSTS en HTTP local. Sí comprobar la ausencia de `Server: Kestrel` y los cuatro encabezados de TC-03.
- **Agente:** api-builder.

## T3 — Reglas de ZAP [Seguridad]
- **Crear:** `security/zap-rules.tsv` y, si hace falta, sus entradas `EXC-NNN` en `security/exceptions.yaml`.
- **Verificar:** TC-BUG-001-05.
- **No hacer:** ignorar una alerta sin `EXC-NNN` con vencimiento.
- **Agente:** security-reviewer (revisa); api-builder (escribe).

## T4 — `gate` y nombres de pasos [CI] (persona)
- **Modificar:** `.github/workflows/ci-pr.yml` (protegido). TC-01 y TC-06 dependen de `.github/`; Julián, Tech Lead, aplica esos cambios después de aprobar la spec.
- **Verificar:** TC-BUG-001-06 y TC-BUG-001-01.
- **No hacer:** cambiar otros jobs ni sus condiciones.
- **Agente:** ninguno; el agente puede preparar el diff en el PR.

## T5 — Evidencia, documentación y memoria [Docs]
- **Modificar:** `docs/QA_DEVSECOPS.md`, `docs/modelo-de-amenazas.md` (CI-6 / H-06), `Memoria.md`.
- **Verificar:** TC-BUG-001-07; la salida de cada caso «(comando)» va en el PR.
- **Agente:** docs-keeper.
