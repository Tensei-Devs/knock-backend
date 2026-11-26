# Scripts de `db/`

`entrega.js` aplica en orden **alfabético por nombre** los scripts que empiezan con tres dígitos y un guion o guion bajo (`^\d{3}[_-].+\.js$`). No lleva un registro de scripts aplicados: cada script es idempotente y se vuelve a correr completo en cada entrega.

## Prefijos repetidos
El número es el de la spec que creó la colección, no un orden global. Hay prefijos repetidos a propósito (`024`, `037`, `041`, `043` y `044`) cuando una misma spec toca colecciones de dos servicios o agrega índices después. Dentro de un mismo prefijo el orden lo da el resto del nombre; por eso `037-z-dispositivos-id.js` lleva una `z` (corre después de `037-notificaciones.js`).

Reglas para un script nuevo:
- Usa el número de su spec. Si ese número ya existe, escoge un nombre que ordene después del existente (por ejemplo, con `z-`) cuando dependa de él.
- No renombres un script ya entregado: las pruebas de `tests/DatabaseTest/Scripts` y `Memoria.md` lo citan por nombre.
- Sus nombres de nivel superior (`const`) no deben repetirse entre scripts, porque corren en el mismo ámbito (`load()`).

## Rellenos de una sola vez (`db/relleno/`)

Son migraciones de datos que corre una persona. No están numerados y viven en una subcarpeta a propósito: ni `db/entrega.js` ni el bucle de despliegue los aplican. Por omisión solo simulan; escriben únicamente con `KNOCK_APLICAR=si`, y su salida no lleva nombres.

- `relleno/tecnicos-busqueda.js` (specs 027, 028, 046 y 051): completa en los técnicos ya guardados `nombrePublico`, `palabrasBusqueda`, `calificacion`, `totalResenas` y `coloniaBaseNombre`. No toca cuentas eliminadas. Antes de aplicarlo: respaldo revisado y la simulación leída. No escribe `trabajosTerminados` (falta la regla).
