# Spec 034 T-01, T-02 y T-04: sondas, apagado ordenado y k6 de despliegue

## Qué cambió
- Módulo Terraform `service`: periodo de gracia de 30 s y sonda de vida con intervalo de 10 s y 3 fallos; nueva prueba `sondas_exactas_y_gracia_CA_034_02` (terraform test: 7 pruebas pasan).
- `Knock.Hosting.Lifecycle.AddKnockGracefulShutdown`: `HostOptions.ShutdownTimeout` de 25 s y una comprobación `ready` que falla al empezar el apagado (readiness en 503). Cableado en gateway, Identidad, Mercado y Mensajería; pruebas unitarias `GracefulShutdownTests` (CA-034-03).
- `tests/perf/spec034-deploy-sin-corte.js` (20 usuarios, errores 5xx y de conexión < 0.5 %, p95 < 1 s) y la consulta de instancias por componente en `docs/OBSERVABILIDAD.md`.

## Por qué
Spec 034 R-02, R-03, R-05 y R-06: dos réplicas sin corte durante el despliegue.

## A evitar
No apuntar la sonda de vida a `/health/ready`. Pendiente (no hecho): prueba de perfiles de `deploy/apps`, ensayo de reversión (T-05, requiere Azure), fábricas de ContractTest (T-08) y el contrato público (T-07); nada se aplicó en Azure.
