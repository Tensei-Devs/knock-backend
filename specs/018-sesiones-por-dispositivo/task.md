# Tareas 018 — Sesiones por dispositivo

> **Estado:** validación integrada en `test/KNOCK-213-on-KNOCK-211`; CA-09 se verificó contra el endpoint de renovación de KNOCK-211.

| Tarea | Qué se hace | Cubre | Estado |
|---|---|---|---|
| T-01 | Agregar al repositorio las consultas de sesiones abiertas y de una sesión con su dueña | R-01, R-05 · CA-01, CA-02 | hecho |
| T-02 | Crear el servicio del listado sin huellas ni tokens | R-01, R-03 · CA-01, CA-03 | hecho |
| T-03 | Crear el servicio de cierre reutilizando la revocación y la cancelación en Redis | R-04, R-07 · CA-04, CA-09 | hecho; CA-09 verificado en integración |
| T-04 | Crear el controller con las validaciones del identificador y los códigos de respuesta | R-05, R-06, R-08 · CA-05 a CA-08 | hecho |
| T-05 | Escribir las pruebas unitarias, de integración y de seguridad negativa | R-01 a R-08 · CA-01 a CA-09 | hecho; CA-09 tiene prueba de integración |
| T-06 | Publicar el OpenAPI actualizado y registrar la decisión en `Memoria.md` | R-02 | hecho |

## Definición de terminado
- Los criterios de aceptación de `spec.md` están cubiertos por pruebas en verde.
- El CI está en verde, un par revisó el cambio y `Memoria.md` está al día.
