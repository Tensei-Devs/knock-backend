# Tareas 010 — Caché en tres niveles y paginación

> **Estado:** pendientes. En esta etapa **no se ejecutan**: se activan cuando la spec esté Aprobada y una persona lo indique.

| Tarea | Qué se hace | Cubre | Estado |
|---|---|---|---|
| T-01 | Definir la interfaz de caché y su implementación con Redis | R-01 | pendiente |
| T-02 | Envolver los repositorios con caché e invalidación al escribir | R-01, R-02, R-05 · CA-01, CA-02 | pendiente |
| T-03 | Implementar `ETag` y 304 | R-03, R-06 · CA-03 | pendiente |
| T-04 | Configurar las vigencias por tipo de dato | R-04 | pendiente |
| T-05 | Implementar la paginación por cursor y las proyecciones | R-07, R-08 · CA-05 | pendiente |
| T-06 | Configurar la compresión y los serializadores generados | R-09 · CA-04 | pendiente |
| T-07 | Escribir la prueba de carga con k6 para ajustar vigencias | R-04 | pendiente |

## Definición de terminado
- Los criterios de aceptación de `spec.md` están cubiertos por pruebas en verde.
- El CI está en verde, un par revisó el cambio y `Memoria.md` está al día.
