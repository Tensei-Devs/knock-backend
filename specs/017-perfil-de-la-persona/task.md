# Tareas 017 — Perfil de la persona

> **Estado:** pendientes. En esta etapa **no se ejecutan**: se activan cuando la spec esté Aprobada y una persona lo indique.

| Tarea | Qué se hace | Cubre | Estado |
|---|---|---|---|
| T-01 | Agregar al repositorio de `usuarios` la lectura con caché y la actualización con invalidación | R-02, R-07 · CA-02, CA-05 | pendiente |
| T-02 | Crear el validador del `PUT` con las reglas de nombre, código postal y foto | R-04, R-05, R-08 · CA-06, CA-08 | pendiente |
| T-03 | Definir la interfaz de colonias en `Knock.Contracts` y usarla en la validación | R-06 · CA-07 | pendiente |
| T-04 | Crear el servicio de perfil (búsqueda por `sub`, `perfilCompleto`) | R-01, R-09 · CA-01, CA-09, CA-11 | pendiente |
| T-05 | Crear el controller con `ETag`, `Cache-Control` y 304 | R-03 · CA-03, CA-04 | pendiente |
| T-06 | Filtrar el celular y el nombre en los registros | R-10 · CA-10 | pendiente |
| T-07 | Escribir las pruebas unitarias, de integración y de seguridad negativa | R-01 a R-10 · CA-01 a CA-11 | pendiente |
| T-08 | Publicar el OpenAPI actualizado y registrar la decisión en `Memoria.md` | R-03 | pendiente |

## Definición de terminado
- Los criterios de aceptación de `spec.md` están cubiertos por pruebas en verde.
- El CI está en verde, un par revisó el cambio y `Memoria.md` está al día.
