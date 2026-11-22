# Spec 029 T-07 y T-08: concurrencia, registros sin datos y eventos documentados (KNOCK-71)

## Qué cambió
Se integró el vencimiento de solicitudes (`VencerSolicitud`, `BarrerVencidas`, `db/029-requests-vencimiento.js`) sobre la base de integración y se agregaron las pruebas de la T-07: cancelar contra elegir (gana exactamente una operación, la otra responde 409 con el código de R-06), tarea y barrido a la vez con un solo `SolicitudVencida`, y un registro sin descripción, dirección, nombre ni `sub`. `docs/events.md` documenta `SolicitudCancelada`, `SolicitudVencida` y las dos tareas.

## Por qué
La spec 029 pide que cancelar, elegir y vencer sean seguros ante la concurrencia (CA-05, CA-08) y que los eventos y registros no lleven datos personales (CA-15).

## A evitar
No buscar valores cortos (el motivo, un estado) en cuerpos crudos: las pruebas comparan las claves de `datos` y mensajes de registro con valores largos y fijos. No crear un segundo evento si la actualización condicionada no cambió nada. `docs/endpoints.md` y `docs/rate-limit-matrix.md` quedan para el cierre F3.
