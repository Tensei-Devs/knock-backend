# Spec 025: cliente real de Blob para las fotos de verificación y casos de integración con fotos

## Qué cambió
- `AzureVerificationBlobStore` es el cliente real de `IVerificationBlobStore` (spec 025, T-04). Emite SAS de delegación de usuario firmados con la identidad administrada (nunca la llave de cuenta): de escritura de un solo blob de cuarentena y de lectura de un solo blob definitivo, solo HTTPS y de 5 minutos. Lee la cuarentena con un tope de 8 MB + 1 byte, escribe el definitivo sin sobrescribir (`If-None-Match`), borra de forma idempotente y convierte cualquier fallo de Azure en 503 sin repetir nombres, enlaces ni contenido.
- Se enciende con `Verificacion:Blob:Habilitado` (APAGADO por omisión; apagado queda `UnavailableVerificationBlobStore`, 503). Con el interruptor encendido el arranque exige `Verificacion:Blob:CuentaUrl` https y dos nombres de contenedor distintos (`verificaciones-cuarentena` y `verificaciones` por omisión). Mismo patrón que el envoltorio de Key Vault.
- Dependencia nueva en `Knock.Identity.Api`: `Azure.Storage.Blobs` 12.30.1 (MIT), la misma versión ya autorizada en Mercado; la spec 025 pedía evaluarla si era nueva en Identidad. Se regeneraron los `packages.lock.json`.
- El envío a revisión turna por clave de CURP dentro de la instancia (`KeyedGate`): dos envíos a la vez con la misma CURP se bloqueaban entre sí sobre el índice único parcial de `curpVigente` en documentdb-local; ahora el segundo relee y recibe 409 `curp_duplicate` (CA-20).
- Pruebas: `Spec025BlobStoreTests` (unitarias: SAS, interruptor, validación) y `Spec025VerificacionFotosTests` (integración con MongoDB y Azurite reales, solo CI: CA-07, 09, 10, 11, 12, 13, 16 y 20).

## Por qué
- Sin cliente real el flujo de fotos no podía probarse ni encenderse: toda la ruta de confirmación respondía 503.
- Azure no se toca desde este cambio: crear la cuenta separada, los contenedores, el rol de la identidad y encender el interruptor es de una persona con permiso (T-04 queda con ese resto).

## A evitar
- Azurite no emite SAS de delegación: las pruebas con Azurite usan enlaces de mentira y suben directo a la cuarentena; la emisión de SAS se prueba con una llave de delegación sintética (`Sign`).
- No encender `Verificacion:Blob:Habilitado` sin los contenedores y el rol de la identidad administrada: con cualquier fallo de Azure la persona recibe 503.
- Las pruebas del Key Vault llevan el sufijo `CA_025_07` aunque ese caso es la CURP duplicada: no confundir al leer la trazabilidad.
- Pendiente fuera de este cambio: T-09 (par revisor y `security-reviewer`: biometría y credenciales).
- Autorrevisión de seguridad (2026-11-24, asistente de código con IA; la revisión por par y la de security-reviewer se omitieron por decisión de José): `Sign` rechaza un SAS de más de 5 minutos o ya vencido (tope duro, no solo la constante de quien llama) y el fallo cerrado (503) cubre también errores de red y de E/S. Riesgo anotado, no corregido: si el borrado de cuarentena falla tras escribir el definitivo, el reintento recibe 409 `photo_already_confirmed` con la foto aún sin marcar; la captura vence a las 24 h y se reinicia (disponibilidad, no fuga).
