# KNOCK-266 · Pruebas negativas de acceso entre personas y de sesión requerida

## Qué cambió

- Pruebas nuevas en `tests/SecurityTest`, cada una con datos sembrados y la petición de control de quien sí tiene derecho: verificación de identidad (confirmar foto y enviar), bitácora del asistente, bloqueos, agenda, volver a mostrar una opinión desde el panel y rutas que exigen sesión (cierre de la sesión del panel, consulta de la apelación de una suspensión, alta y edición de servicios, confirmación de una subida de imagen).
- `MarketAccessHost` configura también la base del módulo Assistant. No cambia código de producción.

## Por qué

- Una prueba de dobles o con un identificador inexistente no falla si se quita el filtro por dueño: hace falta que el recurso exista, sea de otra persona y que el dueño sí pueda con la misma petición.
- La exigencia de sesión se comprueba ruta por ruta en el propio servicio, sin depender del gateway.

## A evitar

- Dar una ruta por probada o sin probar buscando su texto: muchas pruebas arman la ruta por partes, con `theory` o con apoyos.
- Confundir la prueba del contrato vivo con una prueba de acceso: su objetivo es que las respuestas cumplan el contrato.
- Usar un identificador al azar como «recurso ajeno».
