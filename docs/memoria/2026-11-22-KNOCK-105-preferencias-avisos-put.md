# KNOCK-105 - cambiar las preferencias de avisos (PRE-02, spec 044)

## Qué cambió
- `PUT /api/v1/me/preferencias-avisos` en Messaging (`MePreferenciasAvisosController.Update`): cuerpo `{ "tipos": [{ "tipo", "activo" }] }` con solo los tipos que cambian; responde 200 con las preferencias vigentes, igual que el GET.
- `UpdateNoticePreferencesRequestValidator`: lista de 1 a un renglón por tipo, tipos conocidos sin repetir, `activo` obligatorio y 422 si se intenta apagar `seguridad`; los motivos no repiten el valor.
- `NoticesService.UpdatePreferencesAsync` nunca guarda `seguridad` (siempre activa) y `NoticesStore.SetPreferencesAsync` hace un `$set` por tipo con upsert sobre `preferencias_avisos` (índice único por `usuarioId`; si dos peticiones lo crean a la vez, la perdedora repite).
- Política `avisos-preferencias` (20 por minuto por persona) y tope de cuerpo de 2 KiB (413).
- Gateway: la ruta `mensajeria-preferencias-avisos` acepta `GET` y `PUT`; antes el PUT caía en `/me/{**resto}` y llegaba a Identidad.
- Pruebas: unitarias (servicio, validador, `sub` del token), seguridad HTTP (401, 400, 422 sin eco, 413, 429) y enrutamiento del gateway.

## Por qué
La app necesita guardar «Qué avisos recibir» y el endpoint no existía en ninguna rama. La seguridad no se puede apagar (D-06), así que se rechaza en el borde y tampoco se persiste.

## A evitar
Aceptar un identificador de persona en el cuerpo (siempre el `sub` del token); guardar `seguridad: false`; devolver el valor inválido en el error; reemplazar todo el documento de preferencias en vez de `$set` por tipo (pisaría cambios hechos desde otro dispositivo); dejar el `PUT` sin método en el gateway.
