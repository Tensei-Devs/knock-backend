# Conciliacion de task.md de las specs 029 a 058 con el codigo (KNOCK-266)

## Qué cambió
Se actualizo el estado de las tareas de las specs 049, 050 y 054 (y filas puntuales de 030, 037, 044, 047) con la evidencia de archivos y pruebas presentes en `sprint-05`, y se reemplazo el encabezado «pendientes, no se ejecutan» por «en curso» en 13 specs.

## Por qué
Las tablas decian pendiente aunque el codigo ya existe; un estado desactualizado oculta lo que de verdad falta (pruebas CA de integracion, filas de endpoints.md, consumidores entre servicios).

## A evitar
Marcar hecha una tarea cuyas pruebas CA no existen: el trazador `sdd_lint.py --trace` es la referencia; no tocar las specs 060 a 064.
