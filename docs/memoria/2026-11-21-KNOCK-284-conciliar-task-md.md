# Conciliación de task.md de la spec 039 con el código (KNOCK-284)

## Qué cambió
Se actualizó el estado de cada fila de `specs/039-cuenta-y-datos-personales/task.md` con la evidencia del código y de las pruebas `CA_039_01` a `CA_039_43` ya fusionadas en `sprint-05`. T-01 a T-04, T-06, T-07, T-10, T-12 y T-13 quedan hechas; T-00, T-05, T-08, T-09 y T-11 quedan parciales por dependencias de la spec 054 y de las rutas internas de Mercado.

## Por qué
La tabla decía 15 de 15 pendientes aunque los endpoints USR-03 a USR-11 ya existen. Un estado desactualizado oculta lo que de verdad falta: el ejecutor de tareas, el ZIP de exportación, el borrado al vencer la gracia y el cliente interno de Mercado.

## A evitar
Marcar como terminadas las partes que hoy responden con sustitutos (`UnavailableMarketAccountClient`, `PendingAccountOutbox`); reimplementar endpoints que ya existen.