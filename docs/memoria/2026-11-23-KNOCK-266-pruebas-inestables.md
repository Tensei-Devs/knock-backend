# Pruebas inestables: filtro de la spec 024 y métricas de la spec 058

## Qué cambió
- `Spec024FiltroTests.Filtra_4_KB_En_Menos_De_5_Ms_CA_024_01` ahora toma el mínimo de 400 llamadas sueltas en lugar del mejor promedio de 7 lotes. El umbral de 5 ms y el criterio no cambian.
- `Spec058ArchivosSeguridadTests.Registros_Y_Metricas_Sin_Datos_CA_058_19` habilita solo los instrumentos de su propia instancia de `MediaMetrics` (comparación por referencia) en lugar de todo meter llamado `Knock.Media`.

## Por qué
- El promedio de un lote incluye los descansos que el planificador da al hilo cuando otras pruebas corren en paralelo; el costo real del filtro es el de una llamada sin interrupciones. El mínimo no oculta una regresión: si el filtro costara más de 5 ms, ninguna llamada bajaría de ese valor.
- Cada `MediaMetrics` crea su propio `Meter` con el mismo nombre, y el `MeterListener` suscrito por nombre recibía medidas de otras pruebas ejecutadas en paralelo, con etiquetas distintas.

## A evitar
- Suscribir un `MeterListener` por nombre de meter en pruebas que corren en paralelo: filtra por instancia del instrumento.
- Medir tiempo con promedios o con una sola medida bajo carga: usa el mínimo de muchos intentos o mide trabajo, no reloj.
