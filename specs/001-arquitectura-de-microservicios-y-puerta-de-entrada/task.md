# Tareas 001 — Arquitectura de microservicios y puerta de entrada

> **Estado:** hechas (2026-11-04, KNOCK-196). Pendiente: revisión de pares y fusión.

| Tarea | Qué se hace | Cubre | Estado |
|---|---|---|---|
| T-01 | Crear la solución `Knock.slnx` y los cuatro proyectos más `Knock.Contracts` | R-01, R-06 · CA-01 | hecha |
| T-02 | Configurar el gateway con rutas hacia los tres servicios | R-02 · CA-02, CA-04 | hecha |
| T-03 | Implementar el middleware de correlación común | R-05 · CA-02 | hecha |
| T-04 | Escribir la prueba de arquitectura que prohíbe referencias entre servicios | R-03, R-04 · CA-03 | hecha |
| T-05 | Documentar los servicios y sus responsabilidades en `docs/arquitectura.md` | R-01, R-07 · CA-01 | hecha |

## Definición de terminado
- Los criterios de aceptación de `spec.md` están cubiertos por pruebas en verde.
- El CI está en verde, un par revisó el cambio y `Memoria.md` está al día.
