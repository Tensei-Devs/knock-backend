# Tareas 013 — Almacenamiento de archivos, entrega con caché y moderación

> **Estado:** pendientes. En esta etapa **no se ejecutan**: se activan cuando la spec esté Aprobada y una persona lo indique.

| Tarea | Qué se hace | Cubre | Estado |
|---|---|---|---|
| T-01 | Crear las dos cuentas de almacenamiento y sus contenedores en Terraform, sin CDN | R-01, R-04 | pendiente |
| T-02 | Implementar el servicio de enlaces firmados | R-02 · CA-02 | pendiente |
| T-03 | Implementar la validación (5 MB, JPEG/PNG/WebP), la limpieza de metadatos y la reducción de tamaño | R-03 · CA-01, CA-06 | pendiente |
| T-04 | Implementar el cliente de moderación compartido con falla cerrada y 503 reintentable | R-05, R-06, R-07 · CA-04, CA-05 | pendiente |
| T-05 | Escribir las pruebas de seguridad negativa (incluida la petición anónima a la cuenta privada) | R-04 · CA-03 | pendiente |
| T-06 | Crear el índice TTL de `subidas_imagen` (vence solo lo pendiente o rechazado) | R-08 · CA-07 | pendiente |

## Definición de terminado
- Los criterios de aceptación de `spec.md` están cubiertos por pruebas en verde.
- El CI está en verde, un par revisó el cambio y `Memoria.md` está al día.
