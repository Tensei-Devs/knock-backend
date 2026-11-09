# Spec 030 — Chatear antes de elegir y mis conversaciones

> **Estado:** Borrador · **Repositorio:** knock-backend · **Rector:** `constitution.md`
> **Claves de trabajo:** KNOCK-72 (HU-029) y KNOCK-73 (HU-030) · **Sprint:** 5 (la spec se deja lista en el Sprint 4)
> **Servicio:** Mensajería y notificaciones (módulo `Conversaciones`, nuevo) y Mercado (módulo Requests, una ruta de permiso) · **Colecciones:** `conversaciones` y `mensajes` (nuevas, base `messaging`); llaves de Redis `chat:limite:{sub}` (compartida con la spec 026) y `conv:permiso:{conversacionId}`
> **Se apoya en:** la spec 026 (hub `/hubs/chat`, puente de Redis, autenticación del canal y salud) sin repetirla.
> **Otra parte en:** knock-frontend, spec 020 «Mi actividad y mis conversaciones» (lista, globo y leído)

## 1. Objetivo
Que el cliente y un técnico que recibió su solicitud conversen dentro de Knock antes de elegir, sin dar su número: los datos de contacto que se escriban antes de elegir se ocultan, la conversación muestra siempre el aviso de no pagar por adelantado, los mensajes se guardan moderados y solo los ven las dos partes. Y que cada persona vea todas sus conversaciones con un contador de no leídos que se actualiza en vivo.

## 2. Historia y alcance
**2.1 Historia**
- **HU-029 (KNOCK-72)** — Como cliente o técnico, quiero conversar dentro de Knock antes de elegir, para aclarar el trabajo sin dar mi número.
- **HU-030 (KNOCK-73)** — Como cliente o técnico, quiero ver todas mis conversaciones en un globo flotante, para no perder ningún mensaje.

**2.2 Dentro**
- La conversación por par (solicitud, técnico): crearla, leerla, enviar y listar mensajes guardados.
- La ruta de permiso en Mercado que dice quién participa y si la conversación está abierta.
- El ocultamiento de teléfonos, correos y enlaces de contacto antes de elegir, y el aviso fijo de seguridad.
- La moderación de cada mensaje antes de guardarlo (spec 031), que falla cerrada.
- La lista de conversaciones, el contador de no leídos y «marcar como leído».
- Los eventos en vivo por el hub de la spec 026 (mensaje nuevo y contador) y el evento del buzón para avisar a quien no está conectado.
- El límite de 30 mensajes por minuto por persona.

**2.3 Fuera**
- El canal, su autenticación, el puente de Redis y la salud: son de la spec 026 y no cambian, salvo los dos métodos y eventos que se agregan al hub (§5).
- Fotos y audios en el chat (ver §14, decisión 6).
- Respuestas rápidas: son textos de la app; el servidor las trata como cualquier mensaje.
- Ver perfil, bloquear y reportar desde el menú del chat (HU-013 y HU-067).
- Entregar el aviso push (HU-050 y la spec de notificaciones): aquí solo se escribe el evento.
- El resumen del chat con IA (HU-058).
- Abrir WhatsApp (HU-033, spec 021).
- Las pantallas del chat y del globo (frontend 020 y la spec de la pantalla de chat).

## 3. Requisitos
- **R-01:** Una conversación se identifica por el par `(solicitudId, tecnicoId)` y es única (índice único). Participan solo el cliente dueño de la solicitud y el técnico de ese par. No hay conversaciones de grupo ni entre dos clientes o dos técnicos.
- **R-02:** **Permiso en Mercado.** `GET /api/v1/solicitudes/{id}/conversacion?tecnicoId=` exige JWT (el de quien pide, reenviado por Mensajería). Si quien pide es técnico, `tecnicoId` se omite y se resuelve por su `sub` en `tecnicos`; si lo manda, se ignora (un técnico nunca habla en nombre de otro); si es el cliente, `tecnicoId` es obligatorio (lo conoce por la cotización, spec 021). Responde 200 solo si quien pide es el cliente dueño o el técnico del par y el técnico tiene con esa solicitud un envío (spec 028) o una cotización (spec 020). Cualquier otro caso responde 404 uniforme. La respuesta es `{ solicitudId, tecnicoId, clienteId, tecnicoUsuarioId, rol, abierta, motivoCierre?, elegido, visitaId?, servicio, coloniaNombre, tecnicoPublico: { nombre, fotoUrl } }`. Esta ruta **no** se publica en el gateway: solo la llama Mensajería por la red interna, como `GET /colonias/existe` (spec 017).
- **R-03:** La conversación está **abierta** si la solicitud está en `enviada` o `cotizando` y el envío del técnico no está `descartado` ni `cerrado`; o si la solicitud está en `cita_confirmada` y el técnico es el elegido. En cualquier otro caso está **cerrada** con `motivoCierre`: `solicitud_cancelada`, `solicitud_vencida`, `se_eligio_otra`, `descartada` o `servicio_terminado`. Una conversación cerrada se puede leer pero no recibe mensajes.
- **R-04:** `POST /api/v1/conversaciones` exige JWT y cuerpo `{ solicitudId, tecnicoId? }` (`tecnicoId` obligatorio para el cliente y omitido por el técnico, como en R-02). Mensajería consulta el permiso (R-02); con 404 responde 404; si la conversación ya existe responde 200 con ella; si no, la crea con los datos del permiso y responde 201. Es idempotente por el par (no necesita `Idempotency-Key`). Una conversación cerrada no se crea: 409 `conversacion_cerrada`.
- **R-05:** `GET /api/v1/conversaciones/{id}` exige JWT y responde solo a sus dos participantes (comparando el `sub` con `clienteId` o `tecnicoUsuarioId` guardados); a cualquier otra persona, 404. Devuelve `{ conversacionId, solicitudId, visitaId?, rol, contraparte: { nombre, fotoUrl }, servicio, estado, motivoCierre?, contactoVisible, avisoSeguridad, noLeidos }`.
- **R-06:** `contraparte` nunca lleva teléfono, correo, `sub` ni dirección. Para el cliente es el nombre público y la foto del técnico; para el técnico es `{ nombre: "Cliente de {colonia}", fotoUrl: null }`: el técnico no ve el nombre del cliente en el chat.
- **R-07:** `avisoSeguridad` es el texto fijo: «Por tu seguridad, no hagas pagos por adelantado ni compartas tu número antes de elegir. Paga solo cuando el trabajo esté hecho.» Vive en la configuración del módulo, no en cada documento.
- **R-08:** `POST /api/v1/conversaciones/{id}/mensajes` exige JWT de un participante, cuerpo `{ texto }` y `Idempotency-Key`. Los pasos, en orden: permiso vigente (R-09); conversación abierta, si no 409 `conversacion_cerrada`; texto válido (R-10); límite (R-11); ocultamiento de contacto (R-12); moderación (R-13); guardado (R-14). Responde 201 `{ mensajeId, autorRol, texto, contactoOculto, enviadoEn }`. La misma clave repetida por el mismo autor en la misma conversación responde 200 con el mismo mensaje y `Idempotent-Replay: true`, sin duplicar ni volver a moderar.
- **R-09:** El permiso se consulta a Mercado reenviando la sesión, con la dirección y el tiempo límite de la spec 026 (`Services:Market:BaseUrl`, 3 segundos), y se guarda en Redis 60 segundos (`conv:permiso:{conversacionId}`). Si Mercado no responde y no hay permiso en caché, responde 503 `mercado_no_disponible`; nunca se asume que la conversación está abierta. Al guardar el permiso se actualizan `estado`, `motivoCierre`, `elegido` y `visitaId` de la conversación.
- **R-10:** `texto` se recorta y debe medir de 1 a 1 000 caracteres, sin caracteres de control salvo el salto de línea; el cuerpo no puede pasar de 4 KB. Si no cumple, 400 con `errors.texto`.
- **R-11:** Límite de 30 mensajes por minuto por persona, con ventana fija de 60 segundos y contador en Redis `chat:limite:{sub}` (la misma llave que el chat de visita, spec 026 R-14) incrementado y con vencimiento en una sola operación atómica (el mismo patrón de la spec 026). Al pasarse responde 429 `limite_de_mensajes` con `Retry-After`. Crear conversaciones tiene un límite de 20 por hora por persona.
- **R-12:** Mientras `elegido` sea falso, antes de moderar y de guardar se reemplazan en el texto los teléfonos (10 dígitos con o sin lada, espacios, guiones o puntos, o con `+52`), los correos y los enlaces de WhatsApp o Telegram por «[dato de contacto oculto]», con los patrones del filtro de datos personales de la spec 024. Si se reemplazó algo, el mensaje guarda `contactoOculto = true` y la respuesta lo indica para que la app muestre el aviso «El número se comparte al elegir una cotización; te sugerimos no compartirlo antes». El texto original nunca se guarda ni se registra. Con `elegido` verdadero no se oculta nada.
- **R-13:** El texto ya ocultado pasa por la moderación de la spec 031 antes de guardarse. Si se rechaza, 422 `contenido_rechazado` y no se guarda nada; si la moderación no responde, 503 `moderacion_no_disponible` y no se guarda nada (constitución §2.13); si la persona agotó su tope diario de moderación, 429 `limite_moderacion` (traducción de la spec 031, R-13).
- **R-14:** Guardar el mensaje es una sola transacción en `messaging`: se inserta en `mensajes` (con `mensajeId` UUID v7, `autorRol`, `texto`, `contactoOculto`, `creadoEn` y `envioKey`), se actualizan en la conversación `ultimoMensaje` (texto recortado a 80 caracteres, autor y fecha), `ultimoMensajeEn` y el contador de no leídos de la otra parte (`$inc`), y se escribe `MensajeNuevo { conversacionId, destinatarioUsuarioId }` en el buzón (sin texto). Después de confirmar, se publican por el hub de la spec 026 el evento `mensajeConversacion` al grupo `conversacion:{id}` y el evento `noLeidos` al grupo `usuario:{sub}` de la otra parte.
- **R-15:** Al aceptar una conexión, el hub de la spec 026 agrega la conexión al grupo `usuario:{sub}` para recibir `noLeidos`. Se agrega el método `UnirseAConversacion(conversacionId)`, que solo admite a un participante (comprobado contra la conversación guardada, sin llamar a Mercado) y responde `{ ok: true }` o `{ ok: false, codigo: "no_autorizado" }`, y `SalirDeConversacion(conversacionId)`. Enviar sigue siendo por la ruta REST de R-08: el hub solo entrega.
- **R-16:** `GET /api/v1/conversaciones/{id}/mensajes` exige JWT de un participante y devuelve los mensajes del más reciente al más antiguo, con paginación por cursor (`limit` de 1 a 50, 30 por omisión): `{ items: [{ mensajeId, autorRol, texto, contactoOculto, enviadoEn }], nextCursor }`. Los mensajes no llevan `sub`, nombre ni teléfono del autor.
- **R-17:** `GET /api/v1/conversaciones` exige JWT y devuelve las conversaciones donde la persona participa (como cliente o como técnico), ordenadas por `ultimoMensajeEn` de la más reciente a la más antigua y luego por `_id`, con paginación por cursor (`limit` de 1 a 20, 20 por omisión): `{ items: [{ conversacionId, solicitudId, visitaId?, rol, contraparte, servicio, estado, ultimoMensaje: { texto, enviadoEn, autorRol } | null, noLeidos }], totalNoLeidos, nextCursor }`. Sin conversaciones responde `items: []` y `totalNoLeidos: 0`.
- **R-18:** `GET /api/v1/conversaciones/no-leidas` exige JWT y devuelve `{ total }`: la suma de los no leídos de la persona en todas sus conversaciones, calculada con los contadores guardados (no contando mensajes uno por uno).
- **R-19:** `POST /api/v1/conversaciones/{id}/leido` exige JWT de un participante, cuerpo `{ hasta }` (fecha y hora UTC del último mensaje que la app mostró) e `Idempotency-Key`. Mueve `leidoHasta` de quien pide al máximo entre el valor guardado y `min(hasta, ultimoMensajeEn)`, recalcula su contador de no leídos con los mensajes de la otra parte posteriores a esa marca, publica `noLeidos` a sus otros dispositivos y responde 204. Repetirlo con el mismo valor o con uno anterior responde 204 sin cambios.
- **R-20:** Todas las lecturas de esta spec responden `Cache-Control: private, no-store` y no se cachean en el servidor (spec 010, R-05: cambian al crear). Solo el permiso de R-09 se guarda en Redis, y no contiene texto de mensajes.
- **R-21:** Los mensajes se conservan 180 días desde su creación (índice TTL); la conversación se conserva mientras tenga mensajes y su `ultimoMensaje` se vacía cuando el último vence. Cuando una cuenta se elimine (spec de baja de cuenta), sus conversaciones y mensajes se borran con ella (ver §15).
- **R-22:** Los errores usan ProblemDetails con `correlationId` y `traceId`. Los registros llevan solo conteos, códigos y latencias; nunca texto de mensajes, `sub`, identificadores de conversación en texto libre, el texto ocultado ni el token.

## 4. Criterios de aceptación
- **CA-01:** Dado un técnico con envío activo de una solicitud, cuando crea la conversación y escribe, entonces responde 201, el mensaje queda guardado y el cliente conectado recibe `mensajeConversacion` y `noLeidos` en vivo; y si el cliente no está conectado, hay un `MensajeNuevo` en el buzón con su `destinatarioUsuarioId` y sin texto.
- **CA-02:** Dado un texto de 1 y de 1 000 caracteres, cuando se envía, entonces la otra parte lo ve en `GET …/mensajes` y queda guardado; con 0, con 1 001 o con caracteres de control, responde 400 y no se guarda.
- **CA-03:** Dada una persona que no es parte del par, cuando intenta crear, leer, listar mensajes, marcar leído, enviar o unirse por el hub a esa conversación, entonces recibe 404 (o `no_autorizado` en el hub) y nada revela que existe.
- **CA-04:** Dado un mensaje antes de elegir con «llámame al 771 123 4567» o «escríbeme a correo@ejemplo.com», cuando se envía, entonces se guarda y se entrega con «[dato de contacto oculto]», `contactoOculto = true` y la respuesta lo indica; y con el técnico ya elegido, el mismo texto no se oculta.
- **CA-05:** Dados 30 mensajes de la misma persona en un minuto, cuando envía el 31, entonces responde 429 `limite_de_mensajes` con `Retry-After` y no se guarda; con dos réplicas el total sigue siendo 30.
- **CA-06:** Dada una solicitud cancelada, vencida, elegida con otro técnico o descartada por el técnico, cuando se intenta enviar, entonces responde 409 `conversacion_cerrada` con su `motivoCierre`, y la conversación se sigue pudiendo leer.
- **CA-07:** Dado un texto que la moderación rechaza, cuando se envía, entonces responde 422 `contenido_rechazado` y no se guarda; y con la moderación caída responde 503 `moderacion_no_disponible` y no se guarda.
- **CA-08:** Dada la misma `Idempotency-Key` enviada dos veces, cuando llega la segunda, entonces responde 200 con el mismo `mensajeId`, `Idempotent-Replay: true`, un solo mensaje guardado y una sola llamada a la moderación.
- **CA-09:** Dado Mercado caído sin permiso en caché, cuando se envía, entonces responde 503 `mercado_no_disponible` y no se guarda; con permiso en caché de menos de 60 segundos, se envía.
- **CA-10:** Dada una persona con mensajes sin leer en dos conversaciones, cuando pide `GET /conversaciones/no-leidas` o la lista, entonces `total` y `totalNoLeidos` son la suma de ambos contadores.
- **CA-11:** Dadas tres conversaciones con actividad en distinto momento, cuando se pide la lista, entonces vienen de la más reciente a la más antigua; con 25 conversaciones, la primera página trae 20 y un `nextCursor`, y la segunda las 5 restantes sin repetir.
- **CA-12:** Dada una conversación con 4 mensajes sin leer, cuando su participante marca leído hasta el último, entonces su contador queda en 0, el total baja en 4, sus otros dispositivos reciben `noLeidos` y repetir la llamada responde 204 sin cambios; con un `hasta` anterior, no sube el contador.
- **CA-13:** Dada una persona sin conversaciones, cuando pide la lista, entonces responde 200 con `items: []` y `totalNoLeidos: 0`.
- **CA-14:** Dada la conversación vista por el técnico, cuando se inspecciona, entonces `contraparte` es «Cliente de {colonia}» sin foto y `avisoSeguridad` trae el texto fijo de R-07; y en ninguna respuesta, evento ni mensaje aparecen teléfono, correo, dirección ni `sub` de la otra parte.
- **CA-15:** Dado un técnico que manda el `tecnicoId` de otro técnico, cuando crea la conversación, entonces se ignora y se usa el suyo (o 404 si no tiene envío); y dada la creación repetida de la misma conversación, cuando llega la segunda, entonces responde 200 con la misma `conversacionId` y no hay dos documentos; con dos creaciones simultáneas, hay un solo documento.
- **CA-16:** Dada una conversación con 70 mensajes, cuando se piden sus mensajes, entonces llegan 30 del más reciente al más antiguo con `nextCursor`, y las respuestas llevan `Cache-Control: private, no-store`.
- **CA-17:** Dada la ruta de permiso, cuando se llama a través del gateway, entonces no existe (404 del gateway); y llamada por la red interna con la sesión de alguien ajeno, responde 404.
- **CA-18:** Dados los registros y eventos de crear, enviar, ocultar y marcar leído, cuando se inspeccionan, entonces no contienen texto de mensajes, el texto original ocultado, `sub`, identificadores en texto libre ni tokens.
- **CA-19:** Dado un mensaje con `creadoEn` de hace más de 180 días, cuando pasa el TTL, entonces ya no existe; y dado que la colección `mensajes` tiene el índice TTL, cuando se inspecciona, entonces su vencimiento es de 180 días.
- **CA-20:** Dada una petición sin token a cualquier ruta de esta spec, cuando se llama, entonces responde 401; y dadas 21 creaciones de conversación en una hora, cuando llega la 21, entonces responde 429.

**4.1 Criterio → prueba prevista**

| Criterio | Prueba prevista |
|---|---|
| CA-01 | `Spec030EnvioTests.Entrega_En_Vivo_O_Por_Buzon_CA_030_01` |
| CA-02 | `Spec030EnvioTests.Valida_Longitud_Del_Texto_CA_030_02` |
| CA-03 | `Spec030AutorizacionTests.Ajeno_Recibe_404_CA_030_03` |
| CA-04 | `Spec030PrivacidadTests.Oculta_Contacto_Antes_De_Elegir_CA_030_04` |
| CA-05 | `Spec030LimiteTests.Corta_En_El_Mensaje_31_CA_030_05` |
| CA-06 | `Spec030EstadoTests.Conversacion_Cerrada_Solo_Lectura_CA_030_06` |
| CA-07 | `Spec030ModeracionTests.Falla_Cerrado_CA_030_07` |
| CA-08 | `Spec030EnvioTests.Idempotencia_Del_Envio_CA_030_08` |
| CA-09 | `Spec030PermisoTests.Mercado_No_Responde_CA_030_09` |
| CA-10 | `Spec030NoLeidosTests.Suma_De_Contadores_CA_030_10` |
| CA-11 | `Spec030ListaTests.Orden_Y_Paginacion_CA_030_11` |
| CA-12 | `Spec030NoLeidosTests.Marcar_Leido_Idempotente_CA_030_12` |
| CA-13 | `Spec030ListaTests.Lista_Vacia_CA_030_13` |
| CA-14 | `Spec030PrivacidadTests.Contraparte_Sin_Datos_Personales_CA_030_14` |
| CA-15 | `Spec030CreacionTests.Creacion_Idempotente_Y_Tecnico_Propio_CA_030_15` |
| CA-16 | `Spec030MensajesTests.Paginacion_De_Mensajes_CA_030_16` |
| CA-17 | `Spec030PermisoTests.Ruta_Interna_No_Publicada_CA_030_17` |
| CA-18 | `Spec030RegistrosTests.Sin_Datos_Sensibles_CA_030_18` |
| CA-19 | `Spec030RetencionTests.Ttl_De_180_Dias_CA_030_19` |
| CA-20 | `Spec030SeguridadTests.Exige_Sesion_Y_Limites_CA_030_20` |

## 5. Contrato
**Mensajería (publicadas en el gateway, política de sesión)**

| Método y ruta | Quién | Cuerpo / consulta | Respuestas |
|---|---|---|---|
| `POST /api/v1/conversaciones` | cliente dueño o técnico del par | `{ solicitudId, tecnicoId? }` | 201 / 200 `Conversacion`, 400, 401, 404, 409, 429, 503 |
| `GET /api/v1/conversaciones` | persona con sesión | `cursor?`, `limit?` (1–20) | 200 `{ items[], totalNoLeidos, nextCursor }`, 400, 401, 429 |
| `GET /api/v1/conversaciones/no-leidas` | persona con sesión | — | 200 `{ total }`, 401, 429 |
| `GET /api/v1/conversaciones/{id}` | participante | — | 200 `Conversacion`, 400, 401, 404, 429 |
| `GET /api/v1/conversaciones/{id}/mensajes` | participante | `cursor?`, `limit?` (1–50) | 200 `{ items[], nextCursor }`, 400, 401, 404, 429 |
| `POST /api/v1/conversaciones/{id}/mensajes` | participante | `{ texto }` + `Idempotency-Key` | 201 / 200 `Mensaje`, 400, 401, 404, 409, 422, 429, 503 |
| `POST /api/v1/conversaciones/{id}/leido` | participante | `{ hasta }` + `Idempotency-Key` | 204, 400, 401, 404, 429 |

**Mercado (no publicada en el gateway; solo red interna)**

| Método y ruta | Quién | Respuestas |
|---|---|---|
| `GET /api/v1/solicitudes/{id}/conversacion?tecnicoId=` | cliente dueño (con `tecnicoId`) o técnico del par (sin él); sesión reenviada | 200 `PermisoConversacion`, 400, 401, 404 |

**Hub `/hubs/chat` (spec 026; solo lo que se agrega)**

| Elemento | Entrada | Resultado |
|---|---|---|
| `UnirseAConversacion(conversacionId)` | `string` | `{ ok: true }` o `{ ok: false, codigo: "no_autorizado" }` |
| `SalirDeConversacion(conversacionId)` | `string` | `{ ok: true }` |
| Evento `mensajeConversacion` (grupo `conversacion:{id}`) | — | `{ conversacionId, mensajeId, autorRol, texto, contactoOculto, enviadoEn }` |
| Evento `noLeidos` (grupo `usuario:{sub}`) | — | `{ conversacionId, noLeidos, total }` |

**Cuerpos**
- `Conversacion`: `{ conversacionId, solicitudId, visitaId?, rol, contraparte: { nombre, fotoUrl }, servicio, estado, motivoCierre?, contactoVisible, avisoSeguridad, noLeidos }`.
- `Mensaje`: `{ mensajeId, autorRol, texto, contactoOculto, enviadoEn }`.
- `PermisoConversacion`: `{ solicitudId, tecnicoId, clienteId, tecnicoUsuarioId, rol, abierta, motivoCierre?, elegido, visitaId?, servicio, coloniaNombre, tecnicoPublico: { nombre, fotoUrl } }`.

**Códigos de error:** `conversacion_cerrada` (409), `contenido_rechazado` (422), `moderacion_no_disponible` (503), `mercado_no_disponible` (503), `limite_de_mensajes` (429), `limite_moderacion` (429, spec 031), `idempotency_key_requerida` (400). Errores de forma: 400 con `errors.texto`, `errors.hasta`, `errors.solicitudId`, `errors.tecnicoId`.

**Límites:** texto de 1 a 1 000 caracteres y 4 KB por cuerpo; 30 mensajes por minuto por persona; 20 conversaciones nuevas por hora por persona; 20 conversaciones y 50 mensajes por página; permiso en caché 60 segundos; 3 segundos de espera a Mercado; retención de 180 días; `Idempotency-Key` de 1 a 64 caracteres visibles.

**Encabezados:** `Cache-Control: private, no-store` en todas las lecturas; `Retry-After` en 429; `Idempotent-Replay: true` en repeticiones.

## 6. Modelo de datos
**`conversaciones`** (nueva, base `messaging`)
| Campo | Tipo | Nota |
|---|---|---|
| `_id` | ObjectId | `conversacionId` |
| `solicitudId`, `tecnicoId` | string | par único (identificadores de Mercado) |
| `clienteId`, `tecnicoUsuarioId` | string | `sub` de cada participante |
| `visitaId` | string? | cuando el técnico es el elegido |
| `servicio`, `coloniaNombre` | string | copia del permiso |
| `tecnicoPublico` | `{ nombre, fotoUrl }` | copia del permiso |
| `estado`, `motivoCierre` | string | `abierta` o `cerrada` y motivo de R-03 |
| `elegido` | bool | si es falso, se oculta el contacto |
| `ultimoMensaje` | `{ texto, enviadoEn, autorRol }`? | texto recortado a 80 |
| `ultimoMensajeEn` | date | orden de la lista; al crear, `creadoEn` |
| `noLeidosCliente`, `noLeidosTecnico` | int | contadores |
| `leidoHastaCliente`, `leidoHastaTecnico` | date? | marcas de lectura |
| `creadoEn`, `actualizadoEn` | date | UTC |

Índices: `{ solicitudId: 1, tecnicoId: 1 }` único; `{ clienteId: 1, ultimoMensajeEn: -1, _id: -1 }`; `{ tecnicoUsuarioId: 1, ultimoMensajeEn: -1, _id: -1 }`.

**`mensajes`** (nueva, base `messaging`)
| Campo | Tipo | Nota |
|---|---|---|
| `_id` | ObjectId | |
| `mensajeId` | string | UUID v7 |
| `conversacionId` | ObjectId | |
| `autorRol` | string | `cliente` o `tecnico` |
| `autorId` | string | `sub`; nunca se devuelve |
| `texto` | string | ya ocultado y moderado |
| `contactoOculto` | bool | |
| `envioKey` | string | `Idempotency-Key` |
| `creadoEn` | date | UTC, base del TTL |

Índices: `{ conversacionId: 1, creadoEn: -1, _id: -1 }`; `{ conversacionId: 1, autorId: 1, envioKey: 1 }` único; `{ creadoEn: 1 }` TTL de 180 días.

**Redis:** `chat:limite:{sub}` (contador de 60 s, compartido con la spec 026) y `chat:conv:crear:{sub}` (contador de 1 h); `conv:permiso:{conversacionId}` (60 s, solo `abierta`, `motivoCierre`, `elegido` y `visitaId`).

El script `db/` de esta spec crea las dos colecciones y sus índices en `messaging` de forma idempotente. Mercado no cambia su modelo: el permiso se calcula con `solicitudes`, `envios`, `cotizaciones`, `visitas` y `tecnicos`.

## 7. Arquitectura y componentes
- Módulo nuevo `src/Knock.Messaging.Api/Modules/Conversaciones/` con `Controllers`, `Dtos`, `Validators`, `Services`, `Domain`, `Data`.
- `ConversacionesController`, `MensajesController` y `LecturaController` separados (S6960); no tocan MongoDB, Redis ni HTTP.
- `ConversacionService` (crear, leer, listar), `EnvioDeMensajeService` (el orden de R-08) y `LecturaService` (R-19).
- `OcultadorDeContacto` (función pura, `Domain/`): aplica los patrones de teléfono, correo y enlaces de mensajería; reutiliza los patrones del `FiltroDatosPersonales` de la spec 024 copiándolos con su excepción de duplicación (Mercado y Mensajería no se referencian) o desde la biblioteca técnica de la spec 031 si se aprueba. Es el punto de mutación.
- `IPermisoDeConversacion` (en `Knock.Contracts`) con `MarketPermisoDeConversacion` en `Data/`: cliente HTTP tipado, `scoped`, mismo patrón que `MarketVisitaCatalog` de la spec 026; envuelve la caché de 60 s.
- `IContentModerator` (spec 013, implementado en la spec 031) para R-13.
- `ConversacionesStore` y `MensajesStore` son el único acceso a MongoDB; `LimiteDeConversacion` reutiliza `LimiteDeChat` de la spec 026 con otra llave y otro tope.
- `NotificadorEnVivo` usa `IHubContext<ChatHub>` de la spec 026 para publicar a los grupos; el `ChatHub` gana `UnirseAConversacion`, `SalirDeConversacion` y la unión automática a `usuario:{sub}` en `OnConnectedAsync`.
- En Mercado: `PermisoConversacionController` y `PermisoConversacionService` en el módulo Requests; la ruta no se agrega a `ReverseProxy` del gateway.
- Sin estado en el proceso: conversaciones y mensajes en MongoDB, contadores y permiso en Redis.

## 8. Dependencias
- Spec 026 (hub, puente, autenticación del canal, `Services:Market:BaseUrl`, Redis de Mensajería). Esta spec no cambia sus requisitos; solo agrega métodos y eventos.
- Spec 031 (moderación del texto antes de guardar) y 024 (patrones de datos personales).
- Specs 020, 021, 028 y 029 (estado de la solicitud, elección, envíos y cierres) para el permiso.
- Spec 003 (MongoDB de `messaging`, outbox), 009 (JWT y límites del gateway), 010 (cursor; sin caché personal), 011 (registros).
- Frontend 020 consume la lista, el contador y el leído; la pantalla del chat consume crear, leer, enviar, mensajes y el hub.
- La entrega del aviso push (HU-050) consume `MensajeNuevo`.

## 9. Amenazas y controles
| Amenaza | Control | Caso que lo prueba |
|---|---|---|
| Leer o escribir en una conversación ajena (IDOR) | Participantes guardados y comparados con el `sub`; 404 uniforme; el hub tampoco une a ajenos | CA-03 |
| Abrir conversación con una solicitud que no te llegó | Permiso en Mercado: solo cliente dueño y técnico con envío o cotización | CA-03, CA-17 |
| Sacar la conversación de Knock antes de elegir (fraude de anticipo) | Ocultamiento de teléfonos, correos y enlaces; aviso fijo de no pagar por adelantado | CA-04 |
| Revelar el teléfono o el nombre del cliente al técnico | Contraparte «Cliente de {colonia}»; sin datos de contacto en respuestas ni eventos | CA-14 |
| Contenido ofensivo guardado | Moderación previa que falla cerrada (spec 031) | CA-07 |
| Inundar la conversación | 30 por minuto con contador compartido; 20 conversaciones nuevas por hora | CA-05, CA-20 |
| Seguir escribiendo tras cancelar, vencer o elegir a otro | Estado de la conversación con el permiso (60 s); 409 `conversacion_cerrada` | CA-06 |
| Asumir permiso si Mercado falla | 503 sin permiso en caché | CA-09 |
| Ruta de permiso expuesta | No publicada en el gateway; sesión obligatoria | CA-17 |
| Mensajes duplicados por reintento | `Idempotency-Key` con índice único | CA-08 |
| Datos personales en registros o en el buzón | Solo conteos y códigos; el evento no lleva texto | CA-18, CA-01 |
| Retención indefinida de conversaciones | TTL de 180 días | CA-19 |
| Contadores manipulables desde la app | El contador se calcula en el servidor; `hasta` se acota a `ultimoMensajeEn` | CA-12 |

## 10. Plan de pruebas
| Tipo | ¿Aplica? | Motivo |
|---|---|---|
| Unitarias | Sí | `OcultadorDeContacto` (formatos de teléfono, `+52`, correos, enlaces), reglas de apertura, validadores, cálculo de no leídos y cursor |
| Integración | Sí | Mensajería con MongoDB y Redis reales (`Testcontainers`), Mercado simulado con un servidor HTTP de prueba, moderador falso de la spec 031 y cliente de SignalR para los eventos; Mercado real para la ruta de permiso |
| Seguridad negativa | Sí | Persona ajena en todas las rutas y en el hub, sin token, ruta de permiso por el gateway, límites, `hasta` en el futuro |
| Contrato | Sí | OpenAPI de Mensajería (nuevo, se exporta como los de Market e Identity) y de Mercado; el hub se documenta en `docs/endpoints.md` |
| Base de datos | Sí | Script idempotente de `conversaciones` y `mensajes` con el TTL |
| Arquitectura | Sí | Controllers sin MongoDB, Redis ni HTTP; el cliente de Mercado no se captura en un singleton; Mensajería no referencia a Mercado |
| Mutación | Sí | `OcultadorDeContacto` y la regla de apertura (meta 70 %) |
| Humo y extremo a extremo | Sí | Flujo en el entorno de evaluación: el técnico escribe antes de elegir, el cliente recibe `mensajeConversacion` y `noLeidos` en vivo, y la lista muestra el contador; la lista sin token responde 401 |
| Regresión | Sí | Las 18 pruebas de la spec 026 siguen en verde con los métodos nuevos del hub |
| Rendimiento | Sí (acotado) | Lista y contador de una persona con 200 conversaciones en menos de 200 ms (índices de §6) |
| CI/CD y análisis | Sí | Semgrep, Gitleaks, Trivy y dependencias sin hallazgos nuevos; sin paquetes nuevos |
| Aceptación | Sí | CA-01 a CA-20 con la tabla 4.1 |

**Gates:** los de `AGENTS.md`; `python scripts/sdd_lint.py --trace 030`; el contrato de Mensajería se exporta con `scripts/export_openapi.py` (requiere ampliar el script con `--service messaging`, que es zona protegida: lo autoriza el líder).

## 11. Escenarios
**Positivos**
- El técnico pregunta «¿me manda una foto del boiler?» antes de cotizar y el cliente responde.
- El cliente elige al técnico y la conversación sigue abierta con él; las demás quedan cerradas.
- El cliente abre la lista desde el globo, ve 3 no leídos y al abrir la conversación el contador baja a 0.

**Negativos**
- El técnico escribe su número antes de elegir: llega oculto.
- Una persona con el identificador de otra conversación intenta leerla.
- Un técnico que descartó la solicitud intenta seguir escribiendo.

**Errores esperados**
- 400 (`errors.*`, `idempotency_key_requerida`), 401, 404, 409 `conversacion_cerrada`, 422 `contenido_rechazado`, 429 `limite_de_mensajes`, 503 (`moderacion_no_disponible`, `mercado_no_disponible`).

**Casos límite**
- Texto de exactamente 1 000 caracteres; mensaje 30 y 31 de la ventana.
- Teléfono escrito con espacios, guiones, puntos, `+52` o pegado a letras; una cantidad de 10 dígitos que no es teléfono (por ejemplo, un precio con ceros) también se oculta: se prefiere ocultar de más.
- Cancelación de la solicitud mientras el permiso sigue en caché (hasta 60 s de margen).
- `hasta` igual, anterior o posterior al último mensaje.
- Dos dispositivos de la misma persona: el leído en uno actualiza el contador del otro.

## 12. Definición de terminado
- Los 20 criterios tienen prueba en verde y la tabla 4.1 coincide con los nombres reales.
- `db/` crea `conversaciones` y `mensajes` con sus índices y el TTL en `messaging`.
- `docs/endpoints.md` con las rutas nuevas y los métodos y eventos del hub; `docs/events.md` con `MensajeNuevo`; contrato de Mensajería exportado.
- `Memoria.md` con la entrada del cambio y la decisión de la copia de los patrones (o el uso de la biblioteca de la 031).
- Revisión de par y sin hallazgos de seguridad abiertos.

## 13. Decisiones
- **Enviar por REST, entregar por el hub:** el envío necesita moderación, idempotencia, transacción y respuesta con código HTTP; el hub de la spec 026 queda como canal de entrega y no duplica esas reglas.
- **Conversación por par (solicitud, técnico):** es la alternativa que la spec 026 dejó anotada (decisión 1) y la que pide la ficha («chat por solicitud»); un técnico que recibió la solicitud puede preguntar antes de cotizar.
- **Mercado decide la pertenencia y Mensajería la guarda:** Mensajería no lee la base de Mercado (constitución §4); pide el permiso con la sesión de quien escribe y guarda los participantes para no preguntar en cada lectura.
- **Ocultar el contacto en vez de solo advertir:** la regla del proyecto es que el teléfono y la dirección solo se revelan al elegir una cotización; un aviso que se puede ignorar no la cumple.
- **Contadores guardados en la conversación:** contar mensajes en cada lectura del globo sería costoso; el contador se mueve en la misma transacción que el mensaje.

## 14. Decisiones tomadas sin consulta
Se tomaron de noche, con el criterio de la constitución; quien apruebe puede cambiarlas antes de firmar.

1. **El límite es de 30 mensajes por minuto** (el de la ficha, CA5 de HU-029) **y es el mismo que el chat de visita de la spec 026**, con una sola llave `chat:limite:{sub}` (*decisión automática, revisar*). Alternativa: dos límites separados, que permiten esquivar el tope cambiando de chat.
2. **El contacto se oculta, no solo se advierte.** La ficha pide mostrar el aviso y sugerir no hacerlo; se oculta porque la regla de privacidad del proyecto lo exige y evita el fraude de anticipo. Alternativa: guardar el texto tal cual y mostrar el aviso.
3. **El técnico no ve el nombre del cliente en el chat** («Cliente de {colonia}»). Mensajería no tiene el nombre (vive en Identidad) y el técnico no lo necesita para cotizar. Alternativa: pedir el nombre a Identidad con otra interfaz entre servicios.
4. **La conversación con el técnico elegido sigue abierta durante la cita**; se cierra al terminar el servicio. Así la persona no cambia de chat al elegir. La sala efímera de la visita (spec 026) sigue existiendo; se recomienda que la app use esta conversación.
5. **El permiso se cachea 60 segundos.** Evita una llamada a Mercado por mensaje (la misma razón de la spec 026); el costo es que una conversación puede aceptar mensajes hasta 60 s después de cerrarse. Alternativa: consultar siempre, con 3 s más por mensaje.
6. **Sin fotos en el chat en esta spec.** Las fotos del chat muestran el interior de una casa y no deberían ir al contenedor público de la spec 013; necesitan contenedor privado, enlaces temporales y moderación de imagen (spec 031). Alternativa: aceptar fotos del contenedor público, que las haría accesibles para cualquiera con la dirección.
7. **Retención de 180 días por mensaje.** No hay una regla de retención para el chat; seis meses cubren las reclamaciones de un servicio. Alternativa: conservar indefinidamente, que contradice la minimización de datos.
8. **La ruta de permiso vive en Mercado y no se publica.** Responde con los `sub` de los dos participantes, que la app no necesita. Alternativa: una ruta pública que devuelva solo `abierta`, más una interfaz interna aparte.
9. **`POST /conversaciones/{id}/leido` recibe `hasta`**, como lo propone frontend 020, y se acota en el servidor al último mensaje.
10. **Se agrega `GET /conversaciones/no-leidas`** además de `totalNoLeidos` en la lista, para que el globo se actualice sin pedir la lista completa.

## 15. Riesgos y dudas abiertas
- **Conflicto con la spec 026 (borrador):** su §2.3 deja «guardar mensajes» para HU-029 (esta spec) y su decisión 1 deja el chat previo fuera; aquí se cubre. Hay que reflejar en la 026 los métodos `UnirseAConversacion` y `SalirDeConversacion`, la unión automática a `usuario:{sub}` y los eventos `mensajeConversacion` y `noLeidos`. Límites unificados en 30 por minuto con llave compartida (decisión 1).
- **La ruta de permiso cambia Mercado:** depende de las specs 020, 021, 028 y 029 implementadas; con `Oleadas:Habilitado` en falso (spec 028), el técnico del par debe tener una cotización.
- **Hueco: baja de cuenta.** No hay spec de eliminación de cuenta en el backend que diga cómo se borran conversaciones y mensajes de una persona; el TTL de 180 días no basta para el derecho de cancelación.
- **Hueco: entrega del aviso push** (HU-050) sin spec; quien no está conectado solo verá el contador al abrir la app.
- **Contrato de Mensajería:** `scripts/export_openapi.py` solo exporta Market e Identity; ampliarlo toca `scripts/` (zona protegida).
- **Falsos positivos del ocultamiento:** un número largo que no es teléfono también se oculta; se acepta porque ocultar de más es más seguro que de menos.
- **Moderación con cuota:** cada mensaje consume una llamada de la spec 031; con la cuota gratuita agotada, nadie puede escribir (falla cerrada).

## Aprobación
- [ ] Revisor 1 — nombre: ________ fecha: ________
- [ ] Sin conflicto con la constitución
- **Estados:** Borrador (agente) → Aprobada (persona; habilita el código) → Implementada (tras el merge)
