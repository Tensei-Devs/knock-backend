# Tareas 014 — Entrega del proyecto y reproducibilidad

> **Estado:** conciliado con el código el 2026-11-23: T-01 y T-02 hechas; T-03 a T-06 pendientes (dependen de Azure y de una persona autorizada).

| Tarea | Qué se hace | Cubre | Estado |
|---|---|---|---|
| T-01 | Consolidar los scripts de `db/` en el script de entrega y probarlo | R-02 · CA-01 | hecha: `db/entrega.js` y prueba `Spec014DeliveryScriptTests` (CA_014_01) |
| T-02 | Escribir la lista de verificación de la entrega | R-06 | hecha: `docs/lista-de-entrega.md`; sus filas 11 a 13 no se han ejecutado |
| T-03 | Preparar el paquete `.rar` con el proyecto y el script de la base | R-01 | pendiente: el paquete `.rar` no se ha generado (lo dice `docs/lista-de-entrega.md`) |
| T-04 | Escanear secretos en el paquete a entregar | R-03 · CA-02 | pendiente: el escaneo del paquete a entregar no se ha corrido (CA-02 sin prueba) |
| T-05 | Ensayar crear, desplegar y destruir | R-04 · CA-03 | pendiente: no hay ensayo completo de crear, desplegar y destruir; el despliegue manual del 2026-11-23 no incluyó destruir y volver a crear todo |
| T-06 | Preparar las dos copias para la ventana de evaluación | R-05 · CA-04 | pendiente: `deploy/apps/envs/evaluacion.tfvars` define las réplicas, pero no hay evidencia de las dos copias en la ventana (CA-04 sin prueba) |

## Definición de terminado
- Los criterios de aceptación de `spec.md` están cubiertos por pruebas en verde.
- El CI está en verde, un par revisó el cambio y `Memoria.md` está al día.
