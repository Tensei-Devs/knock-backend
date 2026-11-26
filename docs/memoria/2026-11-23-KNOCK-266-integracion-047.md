# Spec 047: pruebas de validación y recurso inexistente de los servicios del técnico

## Qué cambió
- Se agregan las pruebas de integración `CA_047_09`, `10`, `13`, `16`, `18` y `21` en `Spec047ValidacionTests` (valor inválido, cuerpo ilegible, cuenta sin perfil, servicio inexistente y borrar sin sesión).
- No cambia código de producción ni contratos; `task.md` de la 047 refleja lo cubierto.

## Por qué
- El trazador marcaba esos seis casos sin prueba aunque las rutas ya existían.

## A evitar
- Una cuenta sin perfil de técnico recibe 404 `not_a_technician`, no 403 como dice CA-13; la prueba sigue al código y la diferencia queda anotada para decidir si cambia la spec o el código.
