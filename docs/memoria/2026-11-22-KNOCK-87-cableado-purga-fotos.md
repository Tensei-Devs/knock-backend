# Cableado de la purga de fotos de verificación (KNOCK-87)

## Qué cambió
- `VerificationService.SubmitAsync` programa `AvisarAlVerificador` (24 h) y `BorrarFotosDeVerificacion` (7 días) en la misma transacción que pasa la verificación a `en_revision`; si el envío no procede no queda ninguna tarea.
- Entran de la pila de la spec 025 el almacén de fotos privado, la confirmación de fotos y el envío con HMAC de la CURP, con la política `verificacion-confirmar` (10 por hora por persona) en `IdentityRateLimits`.
- `TrustVerificationBlobPurger` y `TrustVerificationPhotoLinks` reemplazan a `NoVerificationBlobs` y `UnavailableVerificationPhotoLinks` del panel; la baja de cuenta ya purga por `PurgeAccountAsync`.
- Sin cuenta de almacenamiento privado configurada, borrar blobs no hace nada; emitir enlaces o guardar sigue respondiendo 503.

## Por qué
La spec 057 exige que las fotos se borren aunque nadie decida y que la baja de cuenta no deje fotos. Un borrado que fallara sin almacenamiento configurado bloquearía la purga y la baja de cuenta sin que existiera ningún blob.

## A evitar
- No programar las tareas fuera de la transacción del envío: se perdería la purga o quedaría una tarea sin verificación en revisión.
- No hacer que el borrado de blobs falle cuando el almacenamiento no está configurado.
- No registrar ni guardar enlaces, rutas, CURP ni llaves.
