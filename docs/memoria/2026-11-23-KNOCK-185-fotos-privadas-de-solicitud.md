# Flujo privado de fotos de solicitud (spec 062)

## Qué cambió
- `POST /api/v1/imagenes/subidas` acepta `proposito = solicitud` y lo atiende `MediaService` (colección `archivos`, `uso = solicitud`): crea el archivo pendiente y emite una SAS de escritura de un solo blob de la cuarentena privada. El id es el `archivoId` de 32 hexadecimales que exige la spec 020.
- `POST /api/v1/imagenes/subidas/{id}/confirmar` prueba primero el flujo privado (dueño y archivo propio) y, si no existe o es ajeno, el público; para una foto de solicitud responde `{ id, ancho, alto, bytes }` sin `url`.
- El módulo `Media` usa un solo cliente de la cuenta privada (`CrearClientePrivado`: `Imagenes:CuentaPrivadaUrl` con identidad administrada, o `ConnectionStrings:KnockBlob` en local); antes `AddMediaModule` exigía la cadena y fallaba en Azure.
- `AlmacenBlob` firma subidas también para el contenedor `cuarentena` (`IEnlaceSubidaFirmado`); `AzureMediaBlobStore` ya no intenta crear contenedores sin permiso (tolera 403).
- Terraform crea los contenedores `cuarentena` y `archivos` con una regla de ciclo de vida de un día para la cuarentena, y da a Mercado `Storage Blob Data Contributor` sobre ambos.

## Por qué
La spec 020 exige 1 a 5 `archivoId` de estado `listo` con `uso = solicitud`, pero ninguna ruta los creaba: sin este flujo nadie podía crear una solicitud válida.

## A evitar
- Publicar una foto de solicitud en la cuenta pública o devolver una `url`: son privadas y se leen solo con enlaces de 5 minutos.
- Firmar subidas para contenedores que no son de cuarentena (el almacén lo rechaza).
- Crear contenedores desde la identidad de Mercado: los crea Terraform.
