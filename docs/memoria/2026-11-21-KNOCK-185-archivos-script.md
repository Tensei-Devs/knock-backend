# Colección archivos de Media: script de db (spec 058, T-01)

## Qué cambió
- `db/058-market-archivos.js` crea la colección `archivos` con los índices `archivoId_1` (único), `propietarioId_1_estado_1_confirmarAntesDe_1` y `expiraEn_1` (TTL de 0 segundos), de forma idempotente.
- `Spec058ArchivosScriptTests` en `DatabaseTest` comprueba índices, idempotencia y unicidad (solo CI).

## Por qué
La spec 058 (R-01, R-03, R-07) reemplaza el registro en JSON de `registros-temporales` por una colección con TTL; este script es el prerrequisito del almacén en MongoDB.

## A evitar
- Renombrado de 049 a 058 (F0.4): el número del script coincide con el de su spec; 049 queda reservado para cotización final.
- No quitar el campo `expiraEn` de un archivo listo: se fija a 9999-12-31.