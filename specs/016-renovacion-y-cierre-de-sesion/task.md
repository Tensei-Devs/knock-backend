# Tareas 016 — Renovación y cierre de sesión

> **Estado:** pendientes. En esta etapa **no se ejecutan**: se activan cuando la spec esté Aprobada y una persona lo indique.

| Tarea | Qué se hace | Cubre | Estado |
|---|---|---|---|
| T-01 | Agregar la rotación atómica y la revocación masiva al repositorio de sesiones | R-03, R-04, R-05 · CA-02, CA-03 | pendiente |
| T-02 | Implementar el servicio de renovación con detección de reuso | R-02, R-03, R-05, R-08 · CA-01, CA-04 | pendiente |
| T-03 | Implementar el servicio de logout y la cancelación en Redis | R-06 · CA-05, CA-08 | pendiente |
| T-04 | Validar en el gateway que el `sid` no esté cancelado | R-07 · CA-06 | pendiente |
| T-05 | Crear los endpoints `refresh` y `logout` y sus validaciones | R-01, R-09, R-10 · CA-07, CA-09, CA-10 | pendiente |
| T-06 | Escribir las pruebas unitarias, de integración y de seguridad negativa | R-01 a R-10 · CA-01 a CA-10 | pendiente |
| T-07 | Publicar el OpenAPI actualizado y registrar la decisión en `Memoria.md` | R-09 | pendiente |

## Definición de terminado
- Los criterios de aceptación de `spec.md` están cubiertos por pruebas en verde.
- El CI está en verde, un par revisó el cambio y `Memoria.md` está al día.
