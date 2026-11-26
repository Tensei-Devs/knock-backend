# La búsqueda por nombre (spec 027) no encontraba a ningún técnico real

## Qué cambió
- El alta (`TechnicianProfileStore`) y la edición (`TechnicianSelfStore`) del perfil escriben `nombrePublico` y `palabrasBusqueda`; la baja de cuenta los borra, porque contienen el nombre de la persona.
- `SearchNameNormalizer` pasa a `Common/Text` para que quien guarda y quien consulta usen el mismo normalizador.

## Por qué
- `BusquedaStore` filtraba y mostraba esos campos, pero nada los escribía: las pruebas los insertaban a mano. La spec 027 §15 avisaba de que quien escribiera la ficha del técnico debía calcularlos.
- Comprobado: la prueba que da de alta por la API y busca por nombre falla sin la corrección (lista vacía) y pasa con ella.

## A evitar
- Probar una lectura sembrando a mano los campos que otra parte del sistema debe escribir.
- Pendiente: los técnicos ya guardados no tienen los campos (hace falta un relleno en `db/`, sin ejecutar). Tampoco se escriben `calificacion`, `totalResenas`, `coloniaBaseNombre` ni `trabajosTerminados`, que leen las búsquedas y el reparto; la reputación se guarda en `reputacion` (spec 051).
