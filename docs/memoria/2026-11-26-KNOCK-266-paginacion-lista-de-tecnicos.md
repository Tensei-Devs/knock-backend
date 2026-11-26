# La lista de técnicos repetía y omitía resultados al paginar

## Qué cambió
- `GET /api/v1/tecnicos`: la página siguiente sigue el orden pedido (`TechnicianListPaging`). Por calificación: menor calificación o, en empate, `_id` menor; sin calificación, al final. Por distancia: más lejos o, en empate, `_id` mayor. El cursor y el contrato no cambian.
- Con orden por calificación, la posición filtra con `$geoWithin` (con radio) o por tener ubicación (sin radio), en lugar de `$nearSphere`.

## Por qué
- El orden era calificación descendente (o distancia) y el cursor pedía `_id` mayor que el último. Reproducido con MongoDB real: las páginas repetían técnicos (5 de 5 casos fallaban antes de la corrección).
- `$nearSphere` no se puede combinar con la condición «o» del cursor (respondía 500).

## A evitar
- Un cursor que no use la misma clave que el orden, y probar la paginación sin recorrer todas las páginas con identificadores en orden distinto al esperado.
