# Spec 032 — CDN de imágenes: entrega con caché y enlaces firmados

> **Estado:** Borrador · **Repositorio:** knock-backend · **Rector:** `constitution.md`
> **Clave de trabajo:** KNOCK-172 (TT-API-10) · **Relacionadas:** KNOCK-182 (TT-GES-10, ADR-007, en Listo) y spec 013 (archivos y moderación)
> **Servicio:** Mercado (módulo nuevo `Media`) e infraestructura compartida · **Colecciones:** `subidas_imagen` (nueva) · **Llaves de Redis:** `img:subidas:{sub}`, `img:confirmaciones:{sub}`
> **Sprint:** propuesto Sprint 5 (insumo §3; Jira la etiqueta `sprint-4`) · **Responsable sugerido:** José Julián (asignado en Jira)

## 1. Objetivo
Que las imágenes públicas de Knock (foto de perfil y fotos de un trabajo terminado; las fotos de una solicitud son privadas y no entran aquí, ver spec 020 R-04) se suban sin pasar por el servidor, se limpien, se moderen y se entreguen desde el almacenamiento de la nube con caché de larga duración e inmutable, y que las imágenes privadas solo se entreguen con enlaces firmados de 5 minutos. La entrega se hace como decidió el ADR-007 (opción D): **sin un recurso de CDN de pago**, con la dirección base configurable para poner una CDN delante sin cambiar código ni datos.

## 2. Historia y alcance
**2.1 Historia**
- **TT-API-10 (KNOCK-172)** — Como equipo, necesitamos que las imágenes públicas se sirvan con caché desde la nube y que las fotos privadas nunca pasen por la entrega pública, para que la app cargue rápido, el costo sea cero y lo privado siga privado.

**2.2 Dentro**
- Dos cuentas de almacenamiento: la **pública** (solo imágenes ya procesadas, lectura anónima por blob, sin listado) y la **privada** existente (cuarentena de subidas, verificación y respaldos).
- El contrato de subida de imágenes públicas: pedir un enlace de subida y confirmar la subida.
- El procesamiento al confirmar: validación por contenido real, límites contra imágenes bomba, orientación, eliminación de metadatos, reducción de tamaño, recodificación a JPEG, moderación previa (spec 013) y publicación con nombre por huella del contenido.
- Encabezados de caché de las imágenes públicas y de las respuestas de la API.
- El formato y la validación de una dirección pública de imagen.
- El componente compartido que emite enlaces firmados de **lectura** de 5 minutos para imágenes privadas, con su registro de acceso.
- Límites de peticiones de subida y confirmación.

**2.3 Fuera**
- Crear un perfil de Azure Front Door, Azure CDN o cualquier CDN de otro proveedor (ADR-007: Front Door cuesta 35 USD al mes; costo cero).
- Las fotos de verificación de identidad (INE y selfie): su subida, cifrado y lectura son de la spec 025; aquí solo se ofrece el emisor de enlaces de lectura que esa spec y la revisión del panel (HU-067) pueden usar.
- El detalle del cliente de moderación con Azure AI Content Safety y sus plantillas de respaldo (spec 031); aquí se usa la interfaz de la spec 013.
- Borrar imágenes publicadas, recolectar imágenes huérfanas y la retención de imágenes de cuentas eliminadas (queda como duda abierta).
- Video, GIF animado, SVG y documentos (spec 013, R-07).
- Las pantallas que toman o eligen fotos (knock-frontend).
- Cambiar cómo cada spec consumidora guarda o valida sus direcciones (017, 020, 024): siguen sus propias reglas y su lista de dominios permitidos, que el despliegue llena con el mismo valor de esta spec.

## 3. Requisitos
- **R-01:** Existen dos cuentas de almacenamiento separadas (ADR-007, decisión 4). La **pública** tiene la lectura anónima habilitada a nivel de cuenta y un solo contenedor, `imagenes-publicas`, con acceso anónimo de nivel **blob** (se lee un archivo si se conoce su nombre; no se puede listar). La **privada** mantiene la lectura anónima deshabilitada a nivel de cuenta (`allow_nested_items_to_be_public = false`) y suma el contenedor `imagenes-cuarentena`. Ninguna imagen sin procesar llega jamás a la cuenta pública.
- **R-02:** Las dos cuentas tienen deshabilitado el acceso con llave compartida (`shared_access_key_enabled = false`), TLS mínimo 1.2, solo HTTPS y ninguna regla de CORS. Los enlaces firmados son **SAS de delegación de usuario** firmados con la identidad administrada del servicio de Mercado; nunca SAS de cuenta ni de llave.
- **R-03:** No se crea ningún recurso de CDN. La dirección base pública sale de `Imagenes:BaseUrlPublica` (esquema `https`, host y la ruta `/imagenes-publicas`, sin consulta, fragmento, puerto ni credenciales); si falta o está mal formada, Mercado no arranca. El despliegue llena con el mismo valor la lista de hosts permitidos de las specs consumidoras (`Perfil:HostsFoto` de la 017 y la de la 020 y la 024).
- **R-04:** `POST /api/v1/imagenes/subidas` exige JWT de persona y el cuerpo `{ proposito, tipoContenido, tamanoBytes }`: `proposito` ∈ `perfil`, `trabajo` (*decisión automática, revisar*: las fotos de una solicitud son privadas y se suben por el flujo de archivos de la spec 013 con `uso: solicitud`, nunca al contenedor público); `tipoContenido` ∈ `image/jpeg`, `image/png`, `image/webp`; `tamanoBytes` entero de 1 a 5 242 880 (5 MB). Un cuerpo inválido responde 422 con `errors` por campo sin repetir el valor y sin emitir enlaces; un cuerpo ilegible, 400. El cuerpo no puede pasar de 1 KB.
- **R-05:** La respuesta es 201 con `{ id, urlSubida, metodo: "PUT", encabezados, venceEn, confirmarAntesDe }`. `urlSubida` es una SAS de delegación de usuario con permisos solo de **crear y escribir** (`cw`) sobre **un solo blob** de `imagenes-cuarentena` cuyo nombre es el `id` (128 bits aleatorios de un generador criptográfico, en hexadecimal), solo HTTPS, inicio un minuto antes de la emisión (tolerancia de reloj) y vencimiento a los 5 minutos de la emisión. `encabezados` trae `x-ms-blob-type: BlockBlob` y el `Content-Type` declarado. `confirmarAntesDe` es la emisión más 30 minutos.
- **R-06:** `POST /api/v1/imagenes/subidas/{id}/confirmar` exige JWT del dueño de la subida y no lleva cuerpo. Una subida ajena o inexistente responde el mismo 404 `upload_not_found`. Pasado `confirmarAntesDe` responde 409 `upload_expired`; si el blob no existe responde 409 `file_not_uploaded`.
- **R-07:** Antes de descargar, la confirmación lee las propiedades del blob: si mide más de 5 242 880 bytes responde 422 `image_too_large` y borra el blob. Después valida el formato por sus bytes iniciales (JPEG, PNG o WebP; el declarado no basta) y responde 422 `format_not_supported` si no coincide con ninguno.
- **R-08:** La decodificación tiene límites contra imágenes bomba: como máximo 8 000 píxeles por lado y 40 megapíxeles, leídos de la cabecera antes de reservar memoria, y un tiempo máximo de 5 segundos. Una imagen que no se puede decodificar o que excede los límites responde 422 `image_invalid` y su blob se borra.
- **R-09:** La imagen se orienta según su metadato de orientación y después se descartan **todos** los metadatos (EXIF con coordenadas, XMP, IPTC, comentarios y perfiles de color; los colores se convierten a sRGB). Se reduce para que su lado mayor mida como máximo 1 600 píxeles (512 para `perfil`), sin ampliar nunca una imagen menor, y se recodifica a JPEG progresivo de calidad 82. Solo los bytes recodificados pueden publicarse.
- **R-10:** La imagen recodificada se envía al cliente de moderación de la spec 013 **antes** de publicarse. Si se marca como inapropiada, responde 422 `image_rejected`, se borra el blob de cuarentena y la subida queda `rechazada`. Si la moderación no responde o falla, responde 503 `moderation_unavailable` con `Retry-After`; si la persona agotó su cuota diaria de moderación, responde 429 `moderation_limit` con `Retry-After`. En los dos casos no publica nada y la subida sigue confirmable hasta `confirmarAntesDe` (falla cerrada, constitución regla 13). La traducción de resultados a HTTP es la de la spec 031 (`ModerationProblemMapper`).
- **R-11:** La publicación escribe en `imagenes-publicas/{proposito}/{sha256}.jpg`, donde `sha256` es la huella en hexadecimal de los bytes recodificados, con `Content-Type: image/jpeg`, `Cache-Control: public, max-age=31536000, immutable` y `Content-Disposition: inline`. Si ya existe un blob con ese nombre no se vuelve a escribir (mismo contenido). Después se borra el blob de cuarentena y la subida queda `publicada` con su `url`.
- **R-12:** La confirmación responde 200 con `{ url, ancho, alto, bytes }`. Confirmar otra vez una subida `publicada` responde 200 con el mismo cuerpo sin volver a procesar ni moderar (idempotente); confirmar una `rechazada` responde 422 `image_rejected`.
- **R-13:** Una dirección pública válida cumple, sin excepción: esquema `https`; host y prefijo iguales a `Imagenes:BaseUrlPublica`; ruta `/{proposito}/{64 hexadecimales en minúsculas}.jpg` con un `proposito` de R-04; sin consulta, fragmento, puerto ni credenciales. El validador `ImagenPublicaUrl` de Mercado aplica esta regla y lo usa la spec 024 (imágenes del asistente); la spec 020 no lo usa, porque sus fotos son privadas.
- **R-14:** Límites por persona, con contadores de ventana fija de una hora en Redis compartidos entre réplicas: 20 enlaces de subida (`img:subidas:{sub}`) y 30 confirmaciones (`img:confirmaciones:{sub}`). Además, como máximo 10 subidas `pendiente` vigentes por persona. Al pasarse responde 429 con `Retry-After`. Aplican también los límites del gateway (spec 009).
- **R-15:** Las respuestas de los dos endpoints llevan `Cache-Control: no-store` y no se comprimen, porque `urlSubida` lleva una firma (spec 010, R-09). Las imágenes públicas no pasan por la API: el cliente las pide directo al almacenamiento.
- **R-16:** El componente `IEnlacesPrivados` emite enlaces de **lectura** para blobs de la cuenta privada: SAS de delegación de usuario con permiso `r` sobre un solo blob, solo HTTPS, 5 minutos, y con `Cache-Control: no-store` y `Content-Disposition: inline` forzados en la respuesta del almacenamiento. Solo lo llama código que ya autorizó al solicitante; recibe un `motivo` de una lista cerrada (`verificacion_revision`, `verificacion_dueno`) y deja un registro de acceso con el motivo, la fecha, el identificador interno del blob y la huella HMAC del `sub` de quien pidió. Nunca emite enlaces para la cuenta pública ni para `imagenes-cuarentena`.
- **R-17:** Si el almacenamiento no responde, los dos endpoints responden 503 `storage_unavailable` con `Retry-After`; `/health/ready` de Mercado **no** depende del almacenamiento, para que una falla de Blob no saque de rotación al resto de Mercado.
- **R-18:** Los registros contienen solo códigos, tamaños, dimensiones, propósito y duraciones. Nunca contienen una SAS, una consulta de dirección, el `sub`, el nombre de un blob de cuarentena ni los bytes o metadatos de una imagen. Los errores usan ProblemDetails con `code`, `correlationId` y `traceId`.
- **R-19:** El blob de cuarentena se borra al terminar cada confirmación (publicada, rechazada o inválida) y, como respaldo, una regla de ciclo de vida del contenedor `imagenes-cuarentena` borra todo blob con más de un día. Los documentos de `subidas_imagen` en estado `pendiente`, `rechazada` o `invalida` se eliminan solos a los 7 días de creados (índice TTL sobre `expiraEn`); los de una subida `publicada` no vencen, porque permiten confirmar otra vez con el mismo resultado (R-12).

## 4. Criterios de aceptación
- **CA-01:** Dado un cuerpo válido con `proposito = trabajo`, cuando se pide un enlace, entonces responde 201 con una SAS de un solo blob de `imagenes-cuarentena`, permisos `cw`, solo HTTPS, que vence a los 5 minutos, y `Cache-Control: no-store`.
- **CA-02:** Dado un `tamanoBytes` de 5 242 881, un `tipoContenido` `image/svg+xml` o un `proposito` desconocido (como `solicitud`, que es privado), cuando se pide un enlace, entonces responde 422 con `errors` y no se emite ninguna SAS.
- **CA-03:** Dado un enlace de subida emitido hace más de 5 minutos, cuando se intenta escribir con él, entonces el almacenamiento lo rechaza.
- **CA-04:** Dada una SAS de subida, cuando se intenta leer, listar, borrar o escribir otro blob con ella, entonces el almacenamiento lo rechaza.
- **CA-05:** Dada una foto JPEG con coordenadas GPS y orientación girada, cuando se confirma, entonces responde 200, la imagen publicada no tiene ningún metadato, conserva la orientación visual y su lado mayor mide como máximo 1 600 píxeles.
- **CA-06:** Dada una imagen publicada, cuando se pide su dirección pública sin credenciales, entonces responde 200 con `Content-Type: image/jpeg` y `Cache-Control: public, max-age=31536000, immutable`.
- **CA-07:** Dado el contenedor público, cuando se intenta listarlo de forma anónima, entonces el almacenamiento lo rechaza.
- **CA-08:** Dado un blob de la cuenta privada (cuarentena o verificación), cuando se pide de forma anónima o con la dirección base pública, entonces el almacenamiento lo rechaza.
- **CA-09:** Dado un archivo HTML o SVG renombrado como `.jpg` y declarado `image/jpeg`, cuando se confirma, entonces responde 422 `format_not_supported`, no se publica nada y el blob de cuarentena se borra.
- **CA-10:** Dada una imagen que declara 20 000 × 20 000 píxeles en su cabecera, cuando se confirma, entonces responde 422 `image_invalid` sin reservar la memoria de la imagen completa y el blob se borra.
- **CA-11:** Dado un blob subido de 6 MB, cuando se confirma, entonces responde 422 `image_too_large` sin descargarlo y el blob se borra.
- **CA-12:** Dada una imagen que la moderación marca como inapropiada, cuando se confirma, entonces responde 422 `image_rejected`, nada se publica y la subida queda `rechazada`.
- **CA-13:** Dada la moderación sin responder, cuando se confirma, entonces responde 503 `moderation_unavailable` con `Retry-After`, nada se publica y la misma subida se puede confirmar después con éxito.
- **CA-14:** Dada una subida ya publicada, cuando se confirma otra vez, entonces responde 200 con la misma `url` y no se llama de nuevo a la moderación.
- **CA-15:** Dada la subida de otra persona o un `id` inexistente, cuando se confirma, entonces responde el mismo 404 `upload_not_found`.
- **CA-16:** Dada una subida sin blob, cuando se confirma, entonces responde 409 `file_not_uploaded`; dada una subida con `confirmarAntesDe` vencido, responde 409 `upload_expired`.
- **CA-17:** Dadas dos subidas con exactamente la misma imagen, cuando se confirman, entonces las dos responden la misma `url` y existe un solo blob público.
- **CA-18:** Dadas 21 peticiones de enlace de la misma persona en una hora, repartidas entre dos réplicas, cuando llega la 21, entonces responde 429 con `Retry-After`.
- **CA-19:** Dadas direcciones con otro host, con `http`, con consulta, con fragmento, con un nombre que no es una huella de 64 hexadecimales o con un propósito desconocido, cuando se validan con `ImagenPublicaUrl`, entonces todas se rechazan; la dirección devuelta por CA-05 se acepta.
- **CA-20:** Dado un módulo que pide un enlace privado con un motivo de la lista, cuando se emite, entonces es de solo lectura, de un solo blob, vence a los 5 minutos, fuerza `Cache-Control: no-store` y queda un registro de acceso con el motivo y la huella del `sub`; con un motivo fuera de la lista o un blob de la cuenta pública, falla.
- **CA-21:** Dado el almacenamiento sin responder, cuando se pide un enlace o se confirma, entonces responde 503 `storage_unavailable` y `/health/ready` de Mercado sigue en 200.
- **CA-22:** Dados los registros de una subida completa y de una rechazada, cuando se inspeccionan, entonces no contienen la SAS, `sig=`, el `sub`, el nombre del blob de cuarentena ni metadatos de la imagen.
- **CA-23:** Dada la configuración de Terraform, cuando se ejecuta `terraform plan` y el análisis de infraestructura, entonces hay dos cuentas, la privada con lectura anónima deshabilitada, las dos sin llave compartida, sin CORS, con TLS 1.2 y la regla de ciclo de vida de un día en `imagenes-cuarentena`, y no existe ningún recurso `azurerm_cdn_*` ni `azurerm_cdn_frontdoor_*`.
- **CA-24:** Dada `Imagenes:BaseUrlPublica` vacía, con `http`, con consulta o con puerto, cuando arranca Mercado, entonces el arranque falla con un mensaje que nombra la llave, sin su valor.

**4.1 Criterio → prueba prevista**

| Criterio | Prueba prevista |
|---|---|
| CA-01 | `Spec032SubidasTests.Emite_Enlace_De_Un_Blob_CA_032_01` |
| CA-02 | `Spec032ValidacionTests.Rechaza_Cuerpo_Invalido_CA_032_02` |
| CA-03 | `Spec032SasTests.Enlace_Vencido_No_Escribe_CA_032_03` |
| CA-04 | `Spec032SasTests.Enlace_Solo_Escribe_Su_Blob_CA_032_04` |
| CA-05 | `Spec032ProcesamientoTests.Quita_Metadatos_Y_Reduce_CA_032_05` |
| CA-06 | `Spec032EntregaTests.Publica_Con_Cache_Inmutable_CA_032_06` |
| CA-07 | `Spec032EntregaTests.No_Se_Lista_El_Contenedor_CA_032_07` |
| CA-08 | `Spec032EntregaTests.Privado_No_Es_Anonimo_CA_032_08` |
| CA-09 | `Spec032ProcesamientoTests.Rechaza_Formato_Falso_CA_032_09` |
| CA-10 | `Spec032ProcesamientoTests.Rechaza_Imagen_Bomba_CA_032_10` |
| CA-11 | `Spec032ProcesamientoTests.Rechaza_Archivo_Grande_CA_032_11` |
| CA-12 | `Spec032ModeracionTests.Imagen_Rechazada_CA_032_12` |
| CA-13 | `Spec032ModeracionTests.Moderacion_Caida_Falla_Cerrado_CA_032_13` |
| CA-14 | `Spec032ConfirmacionTests.Confirmacion_Idempotente_CA_032_14` |
| CA-15 | `Spec032AutorizacionTests.Subida_Ajena_404_CA_032_15` |
| CA-16 | `Spec032ConfirmacionTests.Sin_Blob_O_Vencida_CA_032_16` |
| CA-17 | `Spec032ConfirmacionTests.Misma_Imagen_Un_Blob_CA_032_17` |
| CA-18 | `Spec032LimitesTests.Limite_Compartido_Entre_Replicas_CA_032_18` |
| CA-19 | `Spec032UrlTests.Valida_Direccion_Publica_CA_032_19` |
| CA-20 | `Spec032EnlacesPrivadosTests.Lectura_Firmada_Con_Registro_CA_032_20` |
| CA-21 | `Spec032SaludTests.Almacenamiento_Caido_CA_032_21` |
| CA-22 | `Spec032RegistrosTests.Sin_Datos_Sensibles_CA_032_22` |
| CA-23 | (comando) `terraform plan` del módulo compartido y `checkov -d deploy` |
| CA-24 | `Spec032OpcionesTests.Base_Url_Invalida_No_Arranca_CA_032_24` |

## 5. Contrato
| Método y ruta | Quién | Entrada | Respuestas |
|---|---|---|---|
| `POST /api/v1/imagenes/subidas` | persona con sesión | `{ proposito, tipoContenido, tamanoBytes }` (≤ 1 KB) | 201 `{ id, urlSubida, metodo, encabezados, venceEn, confirmarAntesDe }`; 400 (cuerpo ilegible), 401, 422 (`errors`), 429, 503 |
| `POST /api/v1/imagenes/subidas/{id}/confirmar` | dueño de la subida | sin cuerpo; `id` de 32 hexadecimales | 200 `{ url, ancho, alto, bytes }`; 400 (`id` mal formado), 401, 404, 409, 422, 429 (`limit_exceeded`, `moderation_limit`), 503 |
| `PUT {urlSubida}` | la app, directo al almacenamiento | bytes de la imagen con los `encabezados` dados | 201 del almacenamiento; 403 si la SAS venció o no permite la operación |
| `GET {url}` | cualquiera, directo al almacenamiento | — | 200 con la imagen y caché inmutable; 404 si no existe |

**Códigos (`code` en ProblemDetails):** `upload_not_found` (404), `upload_expired` (409), `file_not_uploaded` (409), `image_too_large` (422), `format_not_supported` (422), `image_invalid` (422), `image_rejected` (422), `moderation_unavailable` (503), `storage_unavailable` (503), `limit_exceeded` (429), `moderation_limit` (429, cuota diaria de moderación de la spec 031). El 400 de validación lleva `errors` por campo.

**Encabezados:** las dos respuestas de la API llevan `Cache-Control: no-store`, sin compresión y con `X-Correlation-Id`; 429 y 503 llevan `Retry-After` en segundos. Las imágenes públicas llevan `Cache-Control: public, max-age=31536000, immutable`, `Content-Type: image/jpeg` y `Content-Disposition: inline`; como el nombre es la huella del contenido, una imagen nunca cambia en la misma dirección y no hace falta `ETag` propio.

**Idempotencia:** la confirmación es idempotente por `id` (R-12). Pedir un enlace no lo es: cada llamada crea una subida nueva y la acotan los límites de R-14; la app no reintenta automáticamente una petición de enlace que ya respondió 201.

**Paginación:** no hay listados en esta spec.

**Límites:** 5 MB por imagen; 8 000 píxeles por lado y 40 megapíxeles; 5 segundos de decodificación; salida de 1 600 píxeles (512 en perfil); SAS de subida de 5 minutos y confirmación hasta 30 minutos después de emitida; 20 enlaces y 30 confirmaciones por hora por persona; 10 subidas pendientes por persona; enlaces privados de lectura de 5 minutos.

**Interfaces internas (no REST):** `IEnlacesPrivados.CrearLecturaAsync(BlobPrivado blob, MotivoAcceso motivo, string subSolicitante)` → `EnlaceFirmado { Url, VenceEn }`; `ImagenPublicaUrl.TryParse(string, out ImagenPublica)`. Se publican en `docs/endpoints.md` y los dos endpoints en el contrato OpenAPI de Mercado.

## 6. Modelo de datos
**Colección nueva `subidas_imagen` (base `market`)**

| Campo | Tipo | Regla |
|---|---|---|
| `_id` | string | 32 hexadecimales aleatorios (128 bits); también es el nombre del blob de cuarentena |
| `sub` | string | dueño; nunca se devuelve |
| `proposito` | string | `perfil`, `trabajo` |
| `tipoDeclarado` | string | el de R-04 |
| `tamanoDeclarado` | int | 1 a 5 242 880 |
| `estado` | string | `pendiente` → `publicada` o `rechazada` (o `invalida`) |
| `url` | string? | solo en `publicada` |
| `ancho`, `alto`, `bytes` | int? | solo en `publicada` |
| `creadaEn`, `confirmarAntesDe` | fecha UTC | `confirmarAntesDe = creadaEn + 30 min` |
| `expiraEn` | fecha UTC | `creadaEn + 7 días` en `pendiente`, `rechazada` e `invalida`; al publicar se fija a `9999-12-31T00:00:00Z` (no vence nunca) |

**Índices:** `{ sub: 1, estado: 1, confirmarAntesDe: 1 }` para contar pendientes vigentes; índice TTL sobre `expiraEn` con `expireAfterSeconds: 0`, el mismo mecanismo que ya usa `sesiones.venceEn`. Se usa una fecha lejana en lugar de quitar el campo porque la documentación de Azure DocumentDB lista el TTL como soportado pero no describe qué pasa con los documentos sin el campo. Script nuevo en `db/` con el siguiente número libre.

**Registro de acceso a privados:** colección `accesos_privados` (base `market`) con `{ motivo, blobId, subHmac, emitidoEn }`, solo inserción, TTL de 180 días. `subHmac` es HMAC-SHA256 del `sub` con la llave de búsqueda de Key Vault (spec 009, R-07). Si la spec 033 se aprueba, el registro del panel la complementa con la acción del panel; esta colección cubre a todo emisor.

**Llaves de Redis:** `img:subidas:{sub}` y `img:confirmaciones:{sub}`, contadores con vencimiento de una hora fijado en la misma operación atómica que el incremento (patrón del limitador del gateway).

**Almacenamiento:**
| Cuenta | Contenedor | Acceso | Contenido | Ciclo de vida |
|---|---|---|---|---|
| pública | `imagenes-publicas` | anónimo por blob, sin listado | `{proposito}/{sha256}.jpg` recodificados y moderados | sin vencimiento (ver dudas) |
| privada | `imagenes-cuarentena` | privado; SAS `cw` de un blob | bytes tal como los subió la app | borrado al confirmar y regla de 1 día |
| privada | `verificaciones-cuarentena`, `verificaciones`, `respaldos` | privado | de la spec 025 y del respaldo | sin cambio |

**Invariantes:** nada llega a la cuenta pública sin pasar por R-07 a R-10; una dirección pública nunca cambia de contenido; ningún blob de la cuenta privada se entrega sin SAS de lectura de 5 minutos.

## 7. Arquitectura y componentes
- Módulo nuevo `src/Knock.Market.Api/Modules/Media/` con `Controllers/ImagenesController`, `Dtos`, `Validators`, `Services` (`SubidaImagenService`, `ProcesadorImagen`, `PublicadorImagen`), `Domain` (`SubidaImagen`, `ImagenPublica`, `MotivoAcceso`), `Data` (`SubidasImagenStore`, `AccesosPrivadosStore`, `AlmacenBlob`) y `Options/ImagenesOptions` validado al arrancar.
- `AlmacenBlob` envuelve `Azure.Storage.Blobs` con dos clientes (público y privado) autenticados con `DefaultAzureCredential` (identidad administrada en Azure; Azurite en local y en pruebas). Obtiene la llave de delegación de usuario, la reutiliza mientras le quede más de una hora y emite las SAS.
- `ProcesadorImagen` es la única pieza que toca bytes de imagen: lee la cabecera, valida límites, orienta, quita metadatos, reduce y recodifica. Es puro (bytes de entrada → bytes de salida) para probarlo sin almacenamiento.
- `SubidaImagenService` orquesta: límites → documento `pendiente` → SAS; y en la confirmación: dueño → vigencia → propiedades → descarga → procesamiento → moderación (`IContentModerator` de la spec 013) → publicación → limpieza → estado.
- `IEnlacesPrivados` vive en `Modules/Media/Services` de Mercado y, para la spec 025 (servicio de Identidad), se reproduce en Identity con su propia identidad administrada y su marcador de duplicación registrado como excepción; no se comparte lógica por `Knock.Contracts`, que solo lleva tipos.
- El gateway enruta `/api/v1/imagenes/**` a Mercado con la política de sesión y el límite por usuario; no agrega caché.
- Terraform (`deploy/modules/shared`): agrega la cuenta pública con su contenedor y la regla de ciclo de vida de `imagenes-cuarentena` en la privada; asigna a la identidad de Mercado `Storage Blob Data Contributor` acotado a `imagenes-publicas` y a `imagenes-cuarentena`, y `Storage Blob Delegator` sobre la cuenta privada y la pública. Expone la dirección base como salida y el módulo de apps la pasa como `Imagenes__BaseUrlPublica`, `Perfil__HostsFoto__0` y la llave equivalente de la spec 020.
- Caché (constitución, principio 9): `subidas_imagen` no se cachea porque es un dato personal que cambia con cada confirmación (spec 010, R-05); la lectura por `id` va directo a MongoDB con proyección. Las imágenes públicas se cachean en el cliente por su encabezado inmutable.
- Observabilidad: contadores de subidas emitidas, confirmadas, rechazadas por código y duración del procesamiento.

## 8. Dependencias
- **Base:** specs 002 (estructura), 003 (MongoDB, Redis, TTL), 005 (Terraform, identidades administradas), 009 (JWT, límites, HMAC), 010 (sin caché ni compresión en respuestas con firma), 011 (registros) y **013** (contenedores separados, enlaces de 5 minutos, limpieza de metadatos y moderación con falla cerrada). Esta spec fija el contrato que la 013 dejó sin rutas.
- **ADR-007** (opción D) debe estar aprobado; mientras sea propuesta, esta spec hereda su estado.
- **Moderación:** interfaz de la spec 013; su implementación real con Azure es la spec 031 (borrador en paralelo, ya alineada: recibe los bytes recodificados, JPEG de hasta 4 MB, y traduce `image_rejected`, `moderation_limit` y `moderation_unavailable`). Mientras no exista, el moderador de pruebas responde según el caso y en un entorno desplegado sin moderador real la confirmación responde 503 (falla cerrada).
- **Consumidoras:** 017 (foto de perfil, `Perfil:HostsFoto`), 024 (imágenes para el asistente, que ya rechaza enlaces con consulta), 025 (usa `IEnlacesPrivados` para leer sus fotos, si así se decide al aprobarla) y HU-067 (revisión en el panel, spec 033 para permisos y auditoría).
- **Dependencias nuevas a evaluar y registrar en `Memoria.md`:** `Azure.Storage.Blobs` y `Azure.Identity` (MIT); `SkiaSharp` con `SkiaSharp.NativeAssets.Linux.NoDependencies` (MIT) para decodificar y recodificar. En pruebas, Azurite en contenedor (ya en `docker-compose`, MIT).
- **Frontend:** las pantallas que suben fotos consumen estos dos endpoints y suben con `PUT` directo; ninguna spec de frontend vigente los consume todavía (hallazgo para la validación cruzada).

## 9. Amenazas y controles
| Amenaza | Control | Caso que lo prueba |
|---|---|---|
| Fuga de la ubicación por los metadatos de la foto | Se descartan todos los metadatos y solo se publican bytes recodificados | CA-05 |
| Publicar HTML, SVG o un archivo malicioso como imagen (XSS desde el dominio de almacenamiento) | Validación por bytes, decodificación real, recodificación a JPEG y `Content-Type` fijo | CA-09 |
| Imagen bomba que agota la memoria | Límites de píxeles leídos de la cabecera antes de decodificar y tiempo máximo | CA-10 |
| Archivos enormes que inflan el costo | 5 MB revisados por propiedades antes de descargar y borrado inmediato | CA-11 |
| Contenido inapropiado publicado | Moderación previa con falla cerrada | CA-12, CA-13 |
| Usar la SAS para leer, listar o escribir otros blobs | SAS de delegación de usuario, permisos `cw`, un solo blob, 5 minutos, solo HTTPS | CA-03, CA-04 |
| Acceso anónimo a fotos privadas | Cuenta separada con lectura anónima deshabilitada a nivel de cuenta | CA-08 |
| Enumerar imágenes públicas | Contenedor sin listado y nombres por huella SHA-256 de 64 hexadecimales | CA-07 |
| Enumerar o confirmar subidas ajenas | `id` de 128 bits y 404 uniforme para ajena o inexistente | CA-15 |
| Llave de cuenta robada | Acceso con llave compartida deshabilitado; solo identidades administradas | CA-23 |
| Abuso de subidas (almacenamiento gratis para terceros) | 20 enlaces y 30 confirmaciones por hora, 10 pendientes y borrado de cuarentena en un día | CA-18 |
| Fuga de la SAS por registros o caché intermedia | `no-store`, sin compresión, `sig` redactado y sin registrar direcciones | CA-01, CA-22 |
| Enlace privado reenviado a un tercero | Vigencia de 5 minutos, `no-store` forzado y registro de acceso con motivo | CA-20 |
| Inyección de direcciones ajenas en las solicitudes (rastreo) | `ImagenPublicaUrl` exige host, prefijo y nombre por huella | CA-19 |
| Dejar de servir Mercado por una falla de Blob | `/health/ready` no depende del almacenamiento; 503 solo en estos endpoints | CA-21 |
| Una CDN de pago creada por error | El análisis de Terraform comprueba que no existe ningún recurso de CDN | CA-23 |

## 10. Plan de pruebas
| Tipo | ¿Aplica? | Motivo |
|---|---|---|
| Unitarias | Sí | Validadores del cuerpo, `ImagenPublicaUrl`, `ProcesadorImagen` con imágenes de muestra generadas en la prueba (con GPS, giradas, bomba, PNG con transparencia, WebP), estados de la subida y opciones |
| Integración | Sí | Mercado con MongoDB, Redis y Azurite reales (`Testcontainers`); moderador falso con respuestas `aprobada`, `rechazada` y caída; dos hosts para el límite compartido |
| Contrato | Sí | Los dos endpoints, sus códigos y encabezados en el OpenAPI de Mercado; comparación con `oasdiff` sin cambios incompatibles |
| Seguridad negativa | Sí | SAS vencida y fuera de su blob, subida ajena, formatos falsos, bomba, tamaño, lectura anónima de privados y listado del contenedor |
| Regresión | Sí | Suites de 017 y 020 con direcciones generadas por esta spec; pruebas de 013 y 025 sin cambios |
| Arquitectura | Sí | El controller no toca Blob ni MongoDB; solo `AlmacenBlob` referencia `Azure.Storage.Blobs`; `ProcesadorImagen` sin dependencias de E/S |
| Mutación | Sí | Condiciones de límites de píxeles y tamaño, validación de la dirección y dueño de la subida (meta de la spec 008) |
| Infraestructura | Sí | `terraform validate`, `terraform plan` y Checkov; la excepción por contenedor público se registra en `security/exceptions.yaml` (lo hace una persona) |
| CI/CD | Sí | Los gates de la spec 007; la imagen de Mercado incluye las bibliotecas nativas de SkiaSharp y Trivy la escanea |
| Análisis estático y vulnerabilidades | Sí | Semgrep, Gitleaks (sin llaves de cuenta en el repositorio), OSV-Scanner y auditoría de NuGet de las dependencias nuevas |
| Rendimiento | Sí (acotado) | Confirmar 20 imágenes de 5 MB seguidas: p95 menor a 5 segundos y memoria del contenedor estable |
| Humo en Azure | Sí | Una subida real en el entorno de evaluación y la lectura anónima de la imagen pública; la de un blob privado debe fallar |

## 11. Escenarios
**Positivos**
- Un técnico sube la foto de su perfil: pide un enlace, sube con `PUT`, confirma y la app muestra la dirección pública.
- Un técnico cambia su foto de perfil: la imagen se reduce a 512 píxeles y la dirección se guarda con `PUT /me`.
- La app vuelve a abrir la solicitud y las imágenes salen de la caché del teléfono sin pedirse de nuevo.
- La confirmación se repite porque se perdió la red: responde la misma dirección.

**Negativos**
- Un archivo SVG con código disfrazado de JPEG.
- Una imagen de 20 000 × 20 000 píxeles de 200 KB.
- Una persona intenta confirmar la subida de otra adivinando el `id`.
- Un bot pide 100 enlaces en una hora.
- Alguien intenta listar el contenedor público o leer una foto de verificación sin firma.

**Errores esperados**
- 400 por cuerpo ilegible o `id` mal formado; 401 sin sesión; 422 `errors` por campo inválido; 404 `upload_not_found`; 409 `upload_expired` y `file_not_uploaded`; 422 `image_too_large`, `format_not_supported`, `image_invalid` e `image_rejected`; 429 `limit_exceeded`; 503 `moderation_unavailable` y `storage_unavailable`.

**Casos límite**
- Imagen de exactamente 5 242 880 bytes y de 5 242 881; de 8 000 píxeles de lado y de 8 001; de 40 y de 40.1 megapíxeles.
- Imagen menor de 1 600 píxeles: no se amplía.
- PNG con transparencia: se compone sobre blanco al pasar a JPEG.
- La subida 20 y la 21 de la hora; la pendiente 10 y la 11.
- Confirmar justo en `confirmarAntesDe`.
- Escribir con la SAS en el segundo 299 y en el 301.
- Dos confirmaciones simultáneas de la misma subida: una procesa y la otra responde lo mismo al terminar (actualización condicionada por estado).
- Reloj del teléfono adelantado: la SAS empieza un minuto antes para tolerarlo.

## 12. Definición de terminado
- CA-01 a CA-24 con prueba o comando en verde y la tabla 4.1 igual a los nombres reales; `python scripts/sdd_lint.py --trace 032` sin faltantes.
- Terraform con dos cuentas, sin llave compartida, sin CORS y sin ningún recurso de CDN; la excepción por el contenedor público registrada por una persona.
- Los dos endpoints en el OpenAPI de Mercado regenerado con `scripts/export_openapi.py` y en `docs/endpoints.md`.
- `docs/CUMPLIMIENTO_LINEAMIENTOS.md` mantiene el punto 15c como parcial con la explicación del ADR-007.
- `Memoria.md` registra las dependencias nuevas, la decisión de SAS de delegación de usuario y la duplicación de `IEnlacesPrivados` si la spec 025 la usa.
- Humo en Azure hecho y documentado en el PR; revisión de par y sin hallazgos de seguridad abiertos; PR con «Uso de IA» y etiqueta de puntos.

## 13. Decisiones
- **Sin recurso de CDN (ADR-007, opción D):** la única CDN que Azure permite crear hoy cuesta 35 USD al mes; el proyecto es de costo cero. La dirección base configurable deja la puerta abierta.
- **Cuarentena en la cuenta privada:** los bytes sin revisar nunca tocan la cuenta con lectura anónima; un error de configuración de un contenedor no los expone.
- **Nombre por huella del contenido:** permite caché inmutable de un año, evita duplicados y hace inviable adivinar nombres.
- **Recodificar siempre a JPEG:** elimina metadatos y cargas ocultas de una sola vez y fija un tipo de contenido seguro.
- **SAS de delegación de usuario:** no existe ninguna llave de cuenta que filtrar ni rotar; la firma depende de la identidad administrada.
- **Moderar después de procesar:** se modera exactamente lo que se publicará y con menos bytes (menos cuota).

## 14. Decisiones tomadas sin consulta
Se tomaron de noche, con el criterio de la constitución; quien apruebe puede cambiarlas antes de firmar.

1. **El título dice «CDN», pero no se crea una CDN.** Se respeta el ADR-007 y el punto 15c queda parcial. Alternativa: Front Door Standard (35 USD al mes, rompe el costo cero) o Cloudflare Free (otro proveedor, no evaluado en el ADR).
2. **Esta spec fija las rutas de subida que la spec 013 no definió.** Sin ellas, 017 y 020 reciben direcciones que nadie produce. Alternativa: pedir una enmienda de la 013 (aprobada) para agregar el contrato; se prefirió no tocar una spec aprobada.
3. **El módulo vive en Mercado.** Solo el propósito `trabajo` es de Mercado; la foto de perfil solo necesita la dirección, y las fotos de solicitud son privadas y no usan este módulo. Alternativa: un módulo por servicio, que duplica el procesamiento de imágenes.
4. **Errores de validación de campos y rechazos de contenido con 422; cuerpo ilegible con 400.** Sigue la regla de la spec 002 (R-09 y R-10). El borrador usaba 400 para la validación del cuerpo; se cambió al fijarse la convención (KNOCK-253).
5. **SkiaSharp para procesar imágenes.** Licencia MIT y sin costo. Alternativa: ImageSharp, cuya licencia comercial depende de los ingresos y obliga a revisarla; o System.Drawing, que no se admite en Linux.
6. **Salida de 1 600 píxeles (512 en perfil) y JPEG de calidad 82.** Equilibra nitidez en teléfono y peso para los 100 GB de salida gratuitos. Alternativa: varias resoluciones por imagen, que multiplica el almacenamiento y la complejidad.
7. **Límites de 20 enlaces y 30 confirmaciones por hora y 10 pendientes.** Alcanzan para una solicitud con 5 fotos y reintentos. Alternativa: sin límite propio, solo el del gateway (120 por minuto por usuario), que permitiría usar el almacenamiento como disco gratuito.
8. **`/health/ready` de Mercado no depende de Blob.** Una falla del almacenamiento no debe apagar solicitudes ni cotizaciones. Alternativa: incluirlo, que sacaría de rotación a Mercado completo.
9. **Registro de acceso a privados con TTL de 180 días y huella HMAC del `sub`.** Permite responder quién vio qué sin guardar el identificador en claro. Alternativa: guardar el `sub` en claro (más simple, más sensible).
10. **La cuenta pública también usa `Storage Blob Delegator`.** No emite SAS hoy, pero deja el mismo patrón si una imagen pública debe borrarse con firma. Alternativa: no asignarlo (mínimo privilegio estricto); se puede quitar en la revisión.

## 15. Riesgos y dudas abiertas
- **Riesgo académico:** el punto 15c pide CDN y no la hay (ADR-007). Se explica con el costo.
- **Conflicto con la spec 013 (aprobada):** su R-04 decía que las imágenes públicas «se sirven por la CDN». Se resuelve con la enmienda de la 013 (sin CDN de pago, dos cuentas, 5 MB, WebP, 503 reintentable y vencimiento solo para lo pendiente o rechazado), pendiente de su nueva aprobación.
- **El ADR-007 sigue como propuesta:** si el líder lo rechaza, cambian R-01 a R-03 y CA-23.
- **Imágenes huérfanas:** una imagen publicada que nadie usa se queda para siempre; hace falta decidir una limpieza (por ejemplo, marcar las direcciones en uso y borrar las demás a los 30 días) y la retención al eliminar una cuenta (aviso de privacidad).
- **Imágenes compartidas por huella:** si dos personas suben la misma imagen, comparten la dirección; borrar la de una afectaría a la otra. Por eso el borrado queda fuera hasta decidir la limpieza.
- **Bibliotecas nativas de SkiaSharp:** aumentan el tamaño de la imagen del contenedor y su superficie de vulnerabilidades; Trivy debe vigilarlas.
- **Moderación:** sin la spec 031 implementada, en Azure todas las confirmaciones responderán 503; la demo depende de ella.
- **La SAS no limita el tamaño:** con una SAS de 5 minutos se puede escribir un blob mucho mayor que el declarado. La confirmación lo borra sin descargarlo y la regla de un día lo elimina si nadie confirma; el costo residual (almacenamiento por horas de lo que se suba en 5 minutos, con 20 enlaces por hora por persona) se acepta y se vigila con la alerta de presupuesto.
- **La regla de ciclo de vida de Blob se ejecuta una vez al día:** un blob abandonado puede vivir hasta casi 48 horas.
- **Ninguna pantalla de frontend consume aún estos endpoints:** las specs de frontend que suben fotos deben alinearse.

## Aprobación
- [ ] Revisor 1 — nombre: ________ fecha: ________
- [ ] Sin conflicto con la constitución
- **Estados:** Borrador (agente) → Aprobada (persona; habilita el código) → Implementada (tras el merge)
