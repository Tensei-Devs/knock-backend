# Spec 062 — Flujo privado de fotos de solicitud

> **Estado:** Aprobada · **Repositorio:** knock-backend · **Rector:** `constitution.md`
> **Jira:** KNOCK-271 (Seguimiento de KNOCK-208 · Spec 013, OI-3: enlaces firmados, endpoints de archivos, CDN y moderación real; relacionadas KNOCK-59 y KNOCK-185) · **Responsable:** Alexis Blas
> **Fuentes:** spec 020 (R-04: las fotos de una solicitud son de 1 a 5 `archivoId` de 32 hexadecimales con `uso = solicitud`, en estado `listo`, del `sub` del token y en el contenedor **privado**), spec 032 (R-04 y CA-02: `POST /api/v1/imagenes/subidas` solo admite `perfil` y `trabajo`; `solicitud` responde 422), spec 058 (R-09, R-11 y §7: las rutas del flujo privado «todavía no están definidas en ninguna spec aprobada»), spec 013 (archivos y moderación, flujo privado), spec 031 (moderación), decisión de José del 2026-11-23 (noche): «las fotos de solicitud son obligatorias (1 a 5), el backend no cambia; el flujo de fotos PRIVADAS de solicitud es prioridad máxima de la Fase 4».
> **Servicios:** Mercado (`Knock.Market.Api`, módulo `Media`; gateway) · **Colecciones:** `archivos` (spec 058, ya existe) · **Almacenamiento:** cuenta privada (`cuarentena/{blobKey}` y `archivos/{blobKey}/…`)
> **Skills:** `security-core`, `vertical-slice-api`, `api-contract-openapi`

## 1. Objetivo
Que una persona pueda **subir las fotos de su solicitud** (obligatorias, de 1 a 5) sin pasar por el servidor y sin que ninguna quede pública. Hoy `POST /api/v1/solicitudes` exige `archivoId` de estado `listo` con `uso = solicitud`, pero **ninguna ruta HTTP crea esos archivos**: `POST /api/v1/imagenes/subidas` solo acepta `perfil` y `trabajo` (flujo público) y los servicios del flujo privado (`MediaService.CreateUploadAsync`, spec 013 y 058) no tienen controlador. Sin esta spec nadie puede crear una solicitud válida.

## 2. Historia y alcance
**2.1 Historia.** Como cliente, quiero subir de 1 a 5 fotos del problema desde la app con un enlace temporal, para adjuntarlas a mi solicitud y que solo yo y los técnicos con permiso las vean.

**2.2 Dentro**
- Aceptar `proposito = "solicitud"` en `POST /api/v1/imagenes/subidas` y atenderlo con el flujo **privado** (`MediaService`, colección `archivos`), con la misma forma de respuesta que hoy (`id`, `urlSubida`, `metodo`, `encabezados`, `venceEn`, `confirmarAntesDe`).
- Confirmar con `POST /api/v1/imagenes/{id}/confirmar`: para una foto privada responde `{ id, ancho, alto, bytes }` **sin dirección pública**.
- El tope de 10 subidas pendientes vigentes por persona (spec 058 R-09 y R-11) como 429 `limit_exceeded`.
- Actualizar spec 032 (R-04, CA-02) y spec 058 (§7, riesgo de rutas) para que digan lo mismo.

**2.3 Fuera**
- Cambiar `POST /api/v1/solicitudes` o su validación de fotos (spec 020: **no cambia**).
- Fotos de trabajos terminados y de perfil (siguen en el flujo público, spec 032).
- Enlaces de lectura de 5 minutos: ya los emite `IMediaApi.CreateReadLinksAsync` (spec 020b).

## 3. Requisitos
- **R-01:** `POST /api/v1/imagenes/subidas` acepta `proposito` ∈ `perfil`, `trabajo`, `solicitud`. Con `perfil` o `trabajo` no cambia nada (flujo público, spec 032). Con `solicitud` usa `MediaService.CreateUploadAsync` con `uso = solicitud` y devuelve `201` con `id` (32 hexadecimales en minúscula, el `archivoId` de la spec 020), el enlace de subida a `cuarentena/{blobKey}` de la cuenta privada y los mismos plazos que el flujo público.
- **R-02:** Las reglas de entrada de R-04 de la spec 032 (tipo de contenido `image/jpeg`, `image/png` o `image/webp`, `tamanoBytes` de 1 a 5 242 880; 422 con `errors` sin repetir el valor; cuerpo de 1 KB) valen igual para `solicitud`.
- **R-03:** **Tope.** Una persona tiene como máximo 10 archivos `pendiente` vigentes al mismo tiempo (spec 058 R-09, sumando los de cualquier `uso`); el siguiente responde 429 `limit_exceeded` con `Retry-After` (spec 058 R-11).
- **R-04:** `POST /api/v1/imagenes/{id}/confirmar` para un archivo `solicitud`: comprueba dueño (404 uniforme si es ajeno o no existe), vigencia, propiedades del blob, descarga, procesamiento (limpieza de metadatos y versión reducida), moderación con falla cerrada (spec 031) y mueve el blob a `archivos/{blobKey}/original.jpg` y `reducida.jpg`; deja el archivo en `listo`. Responde `200 { id, ancho, alto, bytes }` **sin `url`** (el contrato de la respuesta hace `url` opcional y nunca la envía para `solicitud`).
- **R-05:** Una foto `solicitud` nunca produce una dirección pública: no se publica en `imagenes-publicas`, `IMediaApi` no emite enlaces públicos para ella y el acceso se registra como cualquier enlace privado (spec 032 R-16).
- **R-06:** Los códigos de error son los de la spec 032 (§5: `upload_not_found`, `upload_expired`, `file_not_uploaded`, `image_too_large`, `format_not_supported`, `image_invalid`, `image_rejected`, `moderation_unavailable`, `storage_unavailable`) más `limit_exceeded`; todos en ProblemDetails con `code` en inglés y snake_case.
- **R-07:** El gateway ya enruta `/api/v1/imagenes/{**resto}` a Mercado: no hay ruta nueva.
- **R-08:** Los registros no contienen `sub`, nombres de blob, enlaces firmados ni identificadores de fotos (spec 011 y 032 R-18).
- **R-09:** Límites de peticiones: se conservan los de la política `imagenes-subidas` de la spec 032; el tope de R-03 es adicional.

## 4. Criterios de aceptación
- **CA-01:** Dado un cliente con sesión, cuando pide un enlace con `proposito = solicitud`, `image/jpeg` y 1 MB, entonces responde 201 con un `id` de 32 hexadecimales en minúscula y un enlace de subida de la cuenta privada.
- **CA-02:** Dado el archivo subido con ese enlace, cuando confirma, entonces responde 200 con `{ id, ancho, alto, bytes }` sin `url`, el archivo queda `listo` con `uso = solicitud`, y `POST /api/v1/solicitudes` con ese `id` responde 201.
- **CA-03:** Dado un archivo `solicitud` en `listo`, cuando se inspeccionan los blobs, entonces están en `archivos/{blobKey}/original.jpg` y `reducida.jpg` de la cuenta **privada** y no existe ninguno en `imagenes-publicas`.
- **CA-04:** Dado un cliente con 10 subidas pendientes vigentes, cuando pide el enlace número 11, entonces responde 429 `limit_exceeded` con `Retry-After` y no se emite ninguna SAS.
- **CA-05:** Dado un `tipoContenido` `image/svg+xml`, un `tamanoBytes` de 5 242 881 o un `proposito` desconocido, cuando pide el enlace, entonces 422 con `errors` y sin SAS.
- **CA-06:** Dada una foto de otra persona o inexistente, cuando se confirma, entonces 404 uniforme; dada una vencida, 410 o el código `upload_expired` según la spec 032.
- **CA-07:** Dado un contenido rechazado por moderación, cuando se confirma, entonces 422 `image_rejected`, el archivo no pasa a `listo` y la cuarentena se limpia; con la moderación caída, 503 `moderation_unavailable` (falla cerrada).
- **CA-08:** Dado un `id` de solicitud que no es del `sub`, de otro `uso` o no `listo`, cuando se crea la solicitud, entonces 422 `photo_invalid` sin decir cuál condición falló (spec 020 R-04, sin cambio).
- **CA-09:** Dado cualquier registro o error, cuando se inspeccionan, entonces no contienen `sub`, nombres de blob, enlaces firmados ni identificadores de fotos.
- **CA-10:** Dado el contrato publicado, cuando se lee `docs/openapi.v1.json`, entonces `proposito` incluye `solicitud`, la respuesta de confirmar tiene `url` opcional y `POST /imagenes/subidas` declara el 429. *(Enmienda 2026-11-23, aprobada por José Julián Hernández Hernández el 2026-11-24: el contrato publicado deja `url` requerida y `proposito` como cadena libre a propósito; ver la nota al final.)*

**4.1 Criterio → prueba prevista**

| Criterio | Prueba prevista |
|---|---|
| CA-01, CA-02, CA-03, CA-04, CA-06, CA-07 | `IntegrationTest/Media/Spec062FotosPrivadasTests` (con Azurite y MongoDB reales) |
| CA-05, CA-09 | `SecurityTest/Media/Spec062FotosPrivadasSeguridadTests` |
| CA-08 | `IntegrationTest/Requests/Spec020FotosPrivadasTests` |
| CA-10 | `ContractTest/Spec062ContractTests` |

## 5. Contrato
| Ruta | Quién | Entrada | Éxito | Errores |
|---|---|---|---|---|
| `POST /api/v1/imagenes/subidas` | persona con sesión | `{ proposito: "perfil" \| "trabajo" \| "solicitud", tipoContenido, tamanoBytes }` | `201 { id, urlSubida, metodo, encabezados, venceEn, confirmarAntesDe }` | `400`, `401`, `422`, `429 limit_exceeded`, `503` |
| `POST /api/v1/imagenes/{id}/confirmar` | dueño | — | `200 { url?, ancho, alto, bytes }` (sin `url` para `solicitud`) | `400`, `401`, `404`, `409`, `422`, `429`, `503` |

## 6. Modelo de datos
Sin colecciones ni campos nuevos: se usa `archivos` (spec 058) con `uso = solicitud`. Sin índices nuevos.

## 7. Arquitectura y componentes
`ImagenesController` despacha por `proposito`: `perfil` y `trabajo` a `SubidaImagenService` (sin cambio); `solicitud` a `MediaService` (`CreateUploadAsync` y la confirmación del flujo privado). Se extrae `PropositoImagen.Solicitud` y un traductor de `MediaOutcome` a los códigos de la spec 032 (sin duplicar el de `Respuesta<T>`). `MediaVisitPhotos` y `MediaRequestPhotos` (adaptadores sobre `IMediaApi`) no cambian. Sin dependencias nuevas.

## 8. Dependencias
Specs 013 (flujo privado y moderación), 020 y 020b (uso de archivos y enlaces de lectura), 031 (moderación), 032 (flujo público y códigos), 058 (colección `archivos` y tope). Código ya en `sprint-05`: `MediaService`, `MongoMediaFileStore`, `AlmacenBlob`, `IMediaApi`.

## 9. Amenazas y controles
| Amenaza | Control | Caso |
|---|---|---|
| Una foto privada de una solicitud queda pública | Nunca se publica en `imagenes-publicas`; sin `url` en la respuesta | CA-02, CA-03 |
| Subir sin parar para llenar el almacenamiento | Tope de 10 pendientes, caducidad y cuarentena | CA-04 |
| Contenido prohibido o con metadatos de ubicación | Limpieza de metadatos y moderación con falla cerrada | CA-07 |
| Usar la foto de otra persona en una solicitud | `archivoId` solo del `sub`, 404 uniforme y `photo_invalid` sin detalle | CA-06, CA-08 |
| Filtrar enlaces o nombres de blob en registros | Redacción y prueba de registros | CA-09 |

## 10. Plan de pruebas
Unitarias (traductor de resultados y validador del propósito), integración con MongoDB y Azurite reales (flujo completo y creación de la solicitud), seguridad negativa (dueño, tipo, tamaño, límite), contrato (OpenAPI de Mercado) y registros sin datos personales.

## 11. Escenarios
**Positivo:** el cliente pide el enlace, sube la foto, confirma y crea la solicitud con ese `id`. **Negativo:** intenta usar la foto de otra persona (422 `photo_invalid`). **Error esperado:** SVG (422). **Límite:** la undécima subida pendiente (429).

## 12. Definición de terminado
CA-01 a CA-10 con prueba; `python scripts/sdd_lint.py --trace 062` en OK; OpenAPI de Mercado regenerado con `scripts/export_openapi.py`; `docs/endpoints.md`, `docs/rate-limit-matrix.md`, specs 032 y 058 al día; gates de `AGENTS.md §5` en verde; `Memoria.md` con la entrada.

## 13. Decisiones
- **D-01:** el flujo privado se expone por la **misma ruta** `POST /api/v1/imagenes/subidas` con `proposito = solicitud` (la app no aprende una ruta nueva). **D-02 (DECIDIDO, José, 2026-11-23 noche):** las fotos de solicitud son obligatorias (1 a 5) y el backend de solicitudes no cambia; esta spec crea el flujo privado que falta.

## 14. Decisiones tomadas sin consulta
1. **Misma ruta con un propósito nuevo** en lugar de rutas nuevas bajo `/api/v1/archivos`. Alternativa: rutas separadas para el flujo privado.
2. **`url` opcional en la respuesta de confirmar** (nunca presente para `solicitud`). Alternativa: otra respuesta para el flujo privado.
3. **El tope de 10 suma todos los `uso`** (spec 058 R-09). Alternativa: tope aparte para `solicitud`.

## 15. Riesgos y dudas abiertas
- La decisión 1 de §14 (misma ruta con `proposito = solicitud`) cambia el contrato público de `POST /api/v1/imagenes/subidas` de forma **aditiva** (no rompe a quien ya usa `perfil` y `trabajo`); se revisa al firmar la spec.
- Las pruebas con Azurite corren en el CI; en local requieren Docker.

## Nota de enmienda (2026-11-23, decisión de José)
- **Estado de esta enmienda:** aprobada por José Julián Hernández Hernández el 2026-11-24. La aprobación de abajo es la original; la firma de esta enmienda es la línea «Enmienda» de la sección Aprobación y el texto anterior de cada criterio se conserva como historial.
- **CA-10 y la decisión 2 de §14: desviación a propósito del contrato publicado.** La spec pedía `url` opcional en la respuesta de confirmar y `proposito` con el valor `solicitud` declarado. El contrato publicado (`docs/openapi.v1.json`) deja `url` **requerida** (cadena no nula) y `proposito` como **cadena libre** (sin lista cerrada de valores): hacer `url` opcional o cerrar `proposito` con una lista cambia el contrato público de forma incompatible y `oasdiff` lo marca (`response-property-became-nullable`). La compatibilidad con quien ya usa `perfil` y `trabajo` pesa más que reflejar `solicitud` en el esquema.
- **Cómo se cumple igual:** en una foto de solicitud el servicio pasa `url = null` y la propiedad **se omite del JSON** (`JsonIgnore(Condition = WhenWritingNull)`); `id` la sustituye. El valor `solicitud` y los demás se validan en tiempo de ejecución (un `proposito` desconocido responde 422, CA-05), no en el esquema; `POST /imagenes/subidas` sí declara el 429. Evidencia: `ImagenConfirmadaResponse` en `ImagenesDtos.cs` (su comentario cita `oasdiff`) y el esquema `ImagenConfirmadaResponse` con `required: [id, url, ancho, alto, bytes]` en el contrato.
- **Lo que cambia en CA-10:** se prueba que el contrato declara el 429 y que `url` sigue requerida con la omisión en tiempo de ejecución, no «`url` opcional» ni «`proposito` incluye `solicitud`». Un contrato v2 podría reflejar ambas cosas (huecos de comportamiento de la auditoría, 3.C).
- **No se enmienda CA-07** (503 `storage_unavailable` frente a `moderation_unavailable` con la moderación caída): queda como pregunta abierta para José.

## Aprobación
- [x] Revisor 1 — nombre: José Julián Hernández Hernández fecha: 2026-11-23
- [x] Enmienda 2026-11-23 — nombre: José Julián Hernández Hernández fecha: 2026-11-24
- [x] Sin conflicto con la constitución
- **Estados:** Borrador (agente) → Aprobada (persona; habilita el código) → Implementada (tras el merge)
