# Spec 050: pruebas de integración de agenda, cambios y cierre de la visita

## Qué cambió
- Se agregan las pruebas de integración `CA_050_*` que faltaban (agenda, reprogramar y responder, cancelar, cliente ausente, terminar, no terminado y permiso de fotos) en `tests/IntegrationTest/Requests/Spec050*.cs`, con un apoyo común `Spec050Host`.
- No cambia código de producción ni contratos; `task.md` de la 050 refleja lo cubierto (T-11 ya estaba cubierta por `Spec050TransversalesTests`).

## Por qué
- El trazador marcaba 38 casos de la spec sin prueba aunque las rutas ya existían.

## A evitar
- Una persona ajena a la visita recibe 404 (igual que una visita inexistente), no 403 como dice la tabla de casos de la spec; las pruebas siguen al código y la diferencia queda anotada en la spec para decidir.
- El id mal formado en las rutas de visita responde 400 y un `permiso` con tipo equivocado también (la spec pide 422 en CA-27 y CA-44).
- Las horas de la cita se fijan respecto de ahora (por ejemplo una hora atrás) y no con la hora del día, para que la ventana del cliente ausente no dependa de cuándo corre la prueba.
- El módulo de archivos real no entra: `IVisitPhotos` se sustituye por un doble que acepta las fotos.
