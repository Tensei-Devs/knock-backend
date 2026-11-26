# Relleno de una sola vez para los técnicos ya guardados (D5)

## Qué cambió
- `db/relleno/tecnicos-busqueda.js`: completa nombre público, palabras de búsqueda, calificación, total de opiniones y nombre de la colonia. Simula por omisión; `KNOCK_APLICAR=si` escribe. No se ha ejecutado contra datos reales.

## Por qué
- Las correcciones de las specs 027 y 051 solo cubren altas, ediciones y opiniones nuevas.
- Está en una subcarpeta y sin número para que el bucle de despliegue y `entrega.js` no lo apliquen solos.

## A evitar
- Comparar con `=== 1` los contadores de `mongosh`: llegan como `Long`, y el script escribía pero reportaba cero. La prueba lo detectó.
- Aplicarlo sin leer antes la simulación y sin un respaldo revisado.
