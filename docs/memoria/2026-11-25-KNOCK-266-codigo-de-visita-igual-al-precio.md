# CA_022_02 intermitente: el código de visita «1000» coincidía con el precio sembrado

## Qué cambió
- `Spec020Host.CarriesVisitCode` busca el código sin contar `precio` y `tope` cuando valen justo el monto sembrado (`Spec021Host.SeededPrice`); lo usan las cuatro comprobaciones del código (022 y la agenda de 050). Solo pruebas.

## Por qué
- El código de visita son 4 dígitos al azar y la cotización sembrada vale 1000: con el código «1000», la vista del técnico traía ese número como precio y la prueba lo tomaba por una fuga. Falló en la promoción (run 45759420431) con el mismo árbol que había pasado completo. No hay fuga en el servicio.
- La comprobación no se debilita: en esos dos campos un número igual al precio no se distingue de él, y cualquier otro valor sigue contando (cubierto por una prueba determinista).

## A evitar
- Sembrar montos que puedan ser un valor aleatorio que la prueba busca en la respuesta.
