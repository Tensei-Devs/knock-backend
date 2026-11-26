# Las opiniones no influían en ningún orden de técnicos

## Qué cambió
- Al recalcular la reputación (publicar, ocultar o restaurar una opinión), `ReviewStore` escribe en la misma transacción `calificacion` y `totalResenas`; sin opiniones visibles se quita `calificacion`.
- El alta del técnico guarda `coloniaBaseNombre`, que la búsqueda por texto muestra como colonia base.
- `Spec058PendingLimitTests.CA_058_19` deja de ser intermitente: escuchaba por nombre y recibía medidas de otras pruebas en paralelo en una lista que no es segura entre hilos; ahora escucha solo sus instrumentos.

## Por qué
- La reputación se guardaba en `reputacion` (spec 051), pero la búsqueda, las oleadas, la lista, Explorar y el mapa leen `calificacion`, que nada escribía (specs 027, 028 y 046). Autorizado por José el 2026-11-25.

## A evitar
- Guardar un dato en un campo y leerlo en otro, con pruebas que siembran a mano el campo leído.
- Pendiente: los técnicos ya guardados necesitan un relleno en `db/` (nombre, palabras, calificación, total y colonia). `trabajosTerminados` (spec 028) sigue sin escribirse: falta la regla de qué cuenta como trabajo terminado.
