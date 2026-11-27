# KNOCK-266 · El reparto cuenta los trabajos terminados desde las visitas

## Qué cambió

- Al leer los candidatos, el reparto cuenta con una sola consulta agrupada las visitas `terminada` de cada técnico (spec 028, R-18 y R-19). Ya no lee `tecnicos.trabajosTerminados`.

## Por qué

- Nada escribía ese campo: todos los técnicos contaban 0, así que el desempate no servía y todos eran «nuevos». No hace falta contador ni relleno.

## A evitar

- Volver a guardar el número en `tecnicos`: se desfasa de las visitas.
- Sembrar `trabajosTerminados` en pruebas del reparto: ya no tiene efecto; se siembran visitas.
