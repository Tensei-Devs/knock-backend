# Tareas 015 — Registro e inicio de sesión

> **Estado:** pendientes. En esta etapa **no se ejecutan**: se activan cuando la spec esté Aprobada y una persona lo indique.

| Tarea | Qué se hace | Cubre | Estado |
|---|---|---|---|
| T-01 | Escribir el script de `db/` con `usuarios` y `sesiones` y sus índices (únicos en `sub` y `celularHash`, TTL en `venceEn`) | R-05, R-08 · CA-08 | pendiente |
| T-02 | Crear el validador del cuerpo (`firebaseIdToken`, `deviceId`, `aceptaAviso`, `dispositivo`) | R-03 · CA-03 | pendiente |
| T-03 | Implementar el verificador de Firebase detrás de su interfaz | R-02, R-13 · CA-04 | pendiente |
| T-04 | Derivar el `sub`, cifrar el celular, calcular su hash y emitir el JWT y el token de renovación con su huella | R-04, R-08, R-09 · CA-10 | pendiente |
| T-05 | Implementar el servicio de inicio de sesión: cuenta nueva o existente, conflicto de cuenta y estados de la cuenta | R-04, R-05, R-06, R-07 · CA-01, CA-02, CA-05, CA-06, CA-08, CA-09 | pendiente |
| T-06 | Aplicar el máximo de 10 sesiones por cuenta | R-10 · CA-07 | pendiente |
| T-07 | Crear el controller y la respuesta sin caché ni compresión | R-11, R-12 · CA-10, CA-12 | pendiente |
| T-08 | Marcar la ruta como pública en el gateway con el límite de autenticación | R-01 · CA-11 | pendiente |
| T-09 | Escribir las pruebas unitarias, de integración y de seguridad negativa | R-01 a R-13 · CA-01 a CA-12 | pendiente |
| T-10 | Registrar en `Memoria.md` la dependencia de Firebase y publicar el OpenAPI | R-13 | pendiente |

## Definición de terminado
- Los criterios de aceptación de `spec.md` están cubiertos por pruebas en verde.
- El CI está en verde, un par revisó el cambio y `Memoria.md` está al día.
