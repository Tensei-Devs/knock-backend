# Spec 049: pruebas de integración de la cotización final y los ajustes de precio

## Qué cambió
- Se agregan las pruebas de integración `CA_049_01` a `CA_049_34` (cotización final, ajuste de precio, reglas y transversales) en `tests/IntegrationTest/Requests/Spec049*.cs`, con un apoyo común `Spec049Host`.
- No cambia código de producción ni contratos; `task.md` de la 049 refleja lo cubierto.

## Por qué
- El trazador marcaba los 34 casos de la spec sin prueba aunque las rutas y las unitarias ya existían.

## A evitar
- La moderación de notas real falla cerrado sin el servicio: el host de la 049 sustituye `INoteModerator` por uno que aprueba.
- Los vencimientos se prueban corriendo el manejador `VencerCotizacionFinal` o `VencerAjuste` con la fecha ya pasada, no esperando el reloj.
- No buscar textos cortos en el cuerpo crudo de una respuesta: se usa `CarriesValue` sobre el JSON.
- La spec cita la tabla criterio→prueba con nombres `Spec049Tests.CriterioNN_CA_049_NN` que no existen; el trazador solo exige el sufijo `CA_049_NN`.
