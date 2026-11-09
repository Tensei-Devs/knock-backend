# Tareas 022 — Visita segura con código de 4 dígitos

> **Estado:** pendientes. En esta etapa **no se ejecutan**: se activan cuando la spec esté Aprobada y una persona lo indique.
> «Depende de» = tareas que deben estar fusionadas antes; «Paralelo con» = tareas que se pueden trabajar a la vez.

| Tarea | Qué se hace | Cubre | Depende de | Paralelo con | Responsable sugerido | Estado |
|---|---|---|---|---|---|---|
| T-01 | Índices nuevos de `visitas` en `db/` y prueba de base de datos | §6 | spec 021 T-01 | T-02 | Alexis | pendiente |
| T-02 | Reglas puras: ventana de la cita, `motivoCodigo`, `puedeLlegar`, `puedeAvisar`, plantilla del aviso (unitarias y mutación) | R-03 a R-06, R-12, R-13 · CA-11, CA-16 | — | T-01 | Guillermo | pendiente |
| T-03 | `VisitaService` y `VisitasController`: proyección por rol, código solo para el cliente, contacto público y lugar; evento `VisitaCerrada` en el buzón al cerrar la visita (cancelada, terminada, no presentada o bloqueada) | R-01 a R-06, R-14, R-17 · CA-01 a CA-03, CA-09, CA-10, CA-13, CA-17, CA-19 | T-02, spec 021 fusionada | T-04, T-05 | Alexis (revisa Guillermo) | pendiente |
| T-04 | `LlegadaService` y `LlegadaController`: actualización condicionada, intentos, bloqueo, `llegadaKey`, eventos y límite de 5 por minuto | R-07 a R-11, R-15, R-16 · CA-04 a CA-08, CA-12, CA-15, CA-16, CA-18 | T-02, spec 021 fusionada | T-03, T-05 | Guillermo | pendiente |
| T-05 | `AvisoService` y `AvisoController` con límite de 10 por hora | R-12, R-13 · CA-11, CA-18 | T-02 | T-03, T-04 | Erick con revisión de Guillermo (tarea pequeña, ligada a su pantalla) | pendiente |
| T-06 | Pruebas de integración, seguridad negativa (rol, visita ajena) y registros sin código ni dirección | CA-01 a CA-19 | T-03, T-04, T-05 | T-07 | Guillermo | pendiente |
| T-07 | OpenAPI, `docs/endpoints.md` y `docs/rate-limit-matrix.md` | §5 | T-03, T-04, T-05 | T-06 | Alexis | pendiente |
| T-08 | Gates, `Memoria.md` y revisión de par | §12 | T-06, T-07 | — | Guillermo | pendiente |

## Definición de terminado
- Los criterios de aceptación de `spec.md` están cubiertos por pruebas en verde.
- El CI está en verde, un par revisó el cambio y `Memoria.md` está al día.
