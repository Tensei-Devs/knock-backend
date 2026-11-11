# Spec 026 — Mensajería en tiempo real con SignalR y Redis

> **Estado:** Borrador · **Repositorio:** knock-backend · **Rector:** `constitution.md`
> **Claves de trabajo (verificadas en Jira el 2026-11-09):** KNOCK-62 «TT-RT-01 · SignalR con Redis como backplane»; relacionada: KNOCK-241 «BUG-004 · Mensajería sin JWT, encabezados de seguridad ni límite de peticiones propios» (la validación propia del token de R-03 la cubre en parte; el resto del BUG queda en su tarea)
> **Responsable sugerido:** Guillermo · Erick consume el hub (frontend 016) · **Ola:** C · **Sprint:** 4 (si no cabe, pasa al 5 sin romper la ruta crítica) · **Prioridad:** P3
> **Servicio:** Mensajería y notificaciones · **Módulo:** Chat · **Colecciones:** ninguna nueva; llaves de Redis `chat:limite:{sub}`

## 1. Objetivo
Que el cliente y el técnico elegido puedan intercambiar mensajes en tiempo real durante una visita confirmada, con la misma experiencia aunque los atienda cualquiera de las dos copias del servicio, sin que un desconocido pueda escuchar o escribir en esa conversación y sin que nadie pueda inundar el servicio de mensajes.

## 2. Historia y alcance
**2.1 Historia**
- **TT-RT-01 (KNOCK-62)** — Como equipo, necesitamos el chat en tiempo real del servicio de Mensajería con Redis como puente entre copias, autenticación del canal, reconexión y límites, para que la conversación de una visita funcione con dos instancias y sin fugas.

**2.2 Dentro**
- El canal de tiempo real (`/hubs/chat`) del servicio de Mensajería y su autenticación con el token de la sesión.
- La conversación de una visita: el cliente dueño y el técnico elegido entran a la misma sala y se ven entre sí.
- La comprobación de pertenencia contra la vista de la visita de Mercado (spec 022), reenviando la sesión de quien se conecta.
- El puente de Redis entre las copias del servicio, para que un mensaje enviado en una copia llegue a quien está conectado en la otra.
- El límite de mensajes por persona y su contador compartido en Redis.
- La reconexión del cliente, el vencimiento del token durante la conexión y el apagado controlado del canal.
- La salud del servicio cuando Redis no responde.

**2.3 Fuera**
- Guardar mensajes, historial, adjuntos, audios, fotos, marcas de leído, indicador de "escribiendo" y presencia: son de HU-029 y de una spec posterior.
- La moderación del texto del chat: solo aplica cuando el mensaje se guarda (spec 013); esta spec no guarda nada.
- Las notificaciones por evento al cliente o al técnico (cotización nueva, cita confirmada): las entrega Mensajería con los avisos, en su propia spec.
- El chat antes de elegir técnico: la app del técnico no existe y la conversación previa a la visita no tiene un dueño claro todavía.
- La pantalla del chat, la cámara y el almacenamiento local: son de knock-frontend.
- El cierre de la visita y su cancelación: son de HU-034 y HU-041; aquí solo se lee el estado de la visita.
- El registro de accesos y la denuncia de una conversación: son de HU-067 y del módulo de reportes de Identidad.

## 3. Requisitos
- **R-01:** El servicio de Mensajería expone el hub de SignalR en `/hubs/chat`. El contrato del hub se documenta en esta spec y en `docs/endpoints.md`; no aparece en OpenAPI porque no es un endpoint REST (excepción documentada de la spec 002).
- **R-02:** El canal exige un JWT válido. Como el cliente WebSocket no puede enviar el encabezado `Authorization` en el saludo, el token viaja en la consulta `access_token` y **solo** para esta ruta. El gateway valida el token con las reglas de la spec 009 (firma, firmante, destinatario, vencimiento sin tolerancia y `sid` no cancelado en `sesion-cancelada:{sid}`), lo reenvía al servicio en el encabezado `Authorization`, **quita** `access_token` de la URL reenviada y no lo registra.
- **R-03:** El servicio vuelve a validar el token por su cuenta al aceptar la conexión: si falta, está vencido, la firma no corresponde o el `sid` está cancelado en Redis, el saludo responde 401 y no se abre ningún canal. La doble validación es defensa en profundidad: el servicio también se puede llamar sin pasar por el gateway en desarrollo.
- **R-04:** La conexión se cierra sola cuando el token vence (`CloseOnAuthenticationExpiration`), a los 15 minutos como máximo. El cliente renueva con `POST /api/v1/auth/refresh` (spec 016) y reconecta; el servidor no emite tokens ni alarga sesiones.
- **R-05:** La conversación es la visita: el identificador de la sala es el `visitaId`. Al conectarse, la persona **no** entra a ninguna sala; entra con `UnirseAVisita(visitaId)` y sale con `SalirDeVisita(visitaId)`.
- **R-06:** `UnirseAVisita` exige que la visita exista, que quien pide sea el cliente dueño o el técnico elegido y que su estado sea `confirmada` o `en_curso`. La comprobación se hace contra `GET /api/v1/visitas/{id}` de Mercado (spec 022) reenviando la sesión de quien pide; un 200 con un estado admitido entra a la sala `visita:{visitaId}`. La respuesta se deserializa a un tipo que solo tiene `rol` y `estado` (los demás campos, incluidos el código y la dirección de la vista del cliente, se descartan sin materializarse en el modelo) y el cuerpo nunca se registra. Cualquier otro resultado responde `{ ok: false, codigo: "no_autorizado" }` sin distinguir "no existe" de "es ajena", y un estado `terminada`, `cancelada` o `no_presentado` responde `{ ok: false, codigo: "conversacion_no_disponible" }`.
- **R-07:** Si Mercado no responde, tarda más de 3 segundos o contesta algo que no se entiende, la unión falla con `{ ok: false, codigo: "no_disponible" }`. Nunca se asume pertenencia cuando no se pudo comprobar.
- **R-08:** La dirección de Mercado sale de `Services:Market:BaseUrl` y debe ser solo el host, sin ruta, consulta ni fragmento; si falta o está mal formada, el arranque falla. El límite de espera sale de `Services:Market:TiempoLimiteSegundos` (3 por omisión). Los valores no llevan secretos.
- **R-09:** El token de quien se conecta se conserva solo en la memoria de esa conexión, para reenviarlo a Mercado mientras dura; nunca se guarda en Mongo, en Redis ni en un registro, y se descarta al cerrar la conexión.
- **R-10:** `EnviarMensaje(visitaId, texto)` exige que la conexión ya esté en la sala de esa visita; si no, responde `{ ok: false, codigo: "no_unido" }`. La autorización del canal es la unión ya comprobada: no se llama a Mercado por cada mensaje. Para que una conexión abierta no siga autorizada hasta 15 minutos después de cancelar, terminar o bloquear una visita (*decisión automática, revisar*), Mensajería marca la llave `chat:visita-cerrada:{visitaId}` al recibir el evento `VisitaCerrada` de Mercado (visita cancelada, terminada, no presentada o bloqueada, spec 022 R-17, y `VisitaBloqueada`, spec 022 R-08); `EnviarMensaje` y `UnirseAVisita` consultan esa llave en Redis, sin llamar a Mercado, responden `conversacion_no_disponible` y sacan la conexión de la sala.
- **R-11:** `texto` se recorta y debe tener de 1 a 1 000 caracteres, sin caracteres de control salvo el salto de línea. El cuerpo de la invocación no puede pasar de 4 KB y el hub acepta mensajes de hasta 8 KB (`MaximumReceiveMessageSize`). Un texto inválido responde `{ ok: false, codigo: "mensaje_invalido" }` y no se retransmite.
- **R-12:** Un mensaje válido se retransmite al grupo `visita:{visitaId}` —incluida la conexión que lo envió y cualquier otro dispositivo de la misma persona— con el evento `mensaje` y el cuerpo `{ visitaId, mensajeId, texto, enviadoEn }`. El `mensajeId` es un UUID v7 generado en el servidor y las fechas van en UTC. El cuerpo **no** lleva el `sub`, el nombre ni el rol de quien escribe. Este evento es solo del chat de visita; la conversación previa a elegir usa `mensajeConversacion` (spec 030 R-14).
- **R-13:** El mensaje no se guarda en ninguna colección y no hay endpoint de historial: si el destinatario no está conectado, el mensaje no se entrega y no se recupera. La app debe decirlo en pantalla ("los mensajes no se guardan").
- **R-14:** El límite es de 30 mensajes por minuto por persona (*decisión de revisión, confirmar*: mismo umbral, llave y código que la spec 030 R-11), con ventana fija de 60 segundos y contador compartido en Redis (`chat:limite:{sub}`), incrementado y con vencimiento en una sola operación atómica como los contadores del gateway. Al pasarse, la invocación responde `{ ok: false, codigo: "limite_de_mensajes", reintentarEnSegundos }` y el mensaje no se retransmite; el contador no distingue copias, así que dos copias no duplican el límite.
- **R-15:** El hub usa Redis como puente entre copias con el prefijo de canal `knock:chat`, sobre la misma conexión de Redis del servicio (`ConnectionStrings:KnockRedis`). Con dos copias, un mensaje enviado por una persona conectada a una copia llega a la persona conectada a la otra; sin puente, los mensajes se perderían en silencio.
- **R-16:** Mensajería registra la conexión a Redis al arrancar, con la configuración y la contraseña obligatoria de la spec 003, y `/health/ready` incluye Redis: si Redis no responde, el servicio se declara no disponible y el saludo del hub responde 503 en lugar de aceptar conexiones sin puente ni límite.
- **R-17:** El interruptor `Chat:Habilitado` (falso por configuración explícita) apaga el canal: el saludo responde 503 `chat_apagado` y las conexiones abiertas se cierran al terminar su token. Sirve para cortar un abuso sin desplegar código.
- **R-18:** El gateway enruta `/hubs/chat` al servicio de Mensajería con la política de sesión, CORS `knock`, sin caché de respuestas y con el reenvío del salto de protocolo (`Upgrade`) intacto; la ruta no impone tiempo de actividad para no cortar una conexión larga. El límite por usuario del gateway sigue aplicando al saludo y a los reintentos, porque son peticiones HTTP.
- **R-19:** Se registran solo conteos y códigos: conexiones abiertas y cerradas, mensajes entregados, uniones rechazadas, límites alcanzados y errores del puente. Nunca se registran el texto de un mensaje, el token, el `sub`, el `sid`, la IP ni el identificador de la visita en texto libre.
- **R-20:** Los errores del saludo y de la conexión usan ProblemDetails con `correlationId` y `traceId` cuando provienen de una petición HTTP; los errores de una invocación llegan al cliente como el objeto `{ ok: false, codigo }` de cada método y no como una excepción con detalle interno.

## 4. Criterios de aceptación
- **CA-01:** Dada una persona con JWT válido de una visita confirmada, cuando se conecta y se une, entonces queda en la sala `visita:{visitaId}` y recibe `{ ok: true }`.
- **CA-02:** Dada una conexión sin token, con token vencido o con el `sid` cancelado por un cierre de sesión, cuando se intenta el saludo, entonces responde 401 y no se abre el canal.
- **CA-03:** Dado un token que vence mientras la conexión está abierta, cuando pasa el minuto 15, entonces la conexión se cierra y solo se restablece al renovar la sesión y reconectar.
- **CA-04:** Dado el técnico elegido conectado a la misma visita, cuando el cliente envía un mensaje, entonces el técnico recibe el evento `mensaje` con `{ visitaId, mensajeId, texto, enviadoEn }` y sin datos de quien escribe.
- **CA-05:** Dada una persona ajena a la visita, cuando pide unirse, entonces recibe `{ ok: false, codigo: "no_autorizado" }` y no recibe ningún mensaje de la sala.
- **CA-06:** Dada una visita cancelada o terminada, cuando un participante pide unirse, entonces recibe `conversacion_no_disponible`.
- **CA-07:** Dadas dos copias del servicio con dos personas en la misma visita, cuando una envía desde una copia, entonces la otra recibe el mensaje aunque esté conectada a la copia distinta.
- **CA-08:** Dado un texto de 1 001 caracteres o un cuerpo de más de 4 KB, cuando se envía, entonces responde `mensaje_invalido` y nadie recibe nada.
- **CA-09:** Dados 61 mensajes en un minuto de la misma persona, cuando llega el 61, entonces responde `limite_de_mensajes` con los segundos que faltan y no se retransmite.
- **CA-10:** Dado el mismo límite con dos copias, cuando la persona reparte sus mensajes entre las dos, entonces el total sigue siendo 30 por minuto.
- **CA-11:** Dada una conexión que no se unió a la visita, cuando intenta enviar, entonces recibe `no_unido` y no se retransmite.
- **CA-12:** Dados varios mensajes enviados, cuando se inspecciona la base `messaging`, entonces no hay colección de mensajes ni documento nuevo; solo existen las llaves `chat:limite:*` en Redis, con vencimiento.
- **CA-13:** Dado el registro de una conexión, una unión y un envío, cuando se inspecciona, entonces no contiene texto de mensajes, tokens, `sub`, `sid`, IP ni identificadores de visita.
- **CA-14:** Dado Redis caído, cuando se pide `/health/ready` o se intenta el saludo del hub, entonces el servicio responde 503 y no acepta la conexión.
- **CA-15:** Dada una visita que existe pero cuyo servicio de Mercado no responde, cuando se pide unirse, entonces recibe `no_disponible` y no se asume pertenencia.
- **CA-16:** Dado el mismo cliente con dos dispositivos conectados a la misma visita, cuando el técnico envía un mensaje, entonces ambos dispositivos lo reciben.
- **CA-17:** Dado `Chat:Habilitado` en falso, cuando se intenta el saludo, entonces responde 503 `chat_apagado`.
- **CA-18:** Dada una URL de gateway con `?access_token=…`, cuando se inspecciona la petición reenviada al servicio y los registros, entonces el servicio recibe el token en el encabezado `Authorization`, la URL ya no lo lleva y ningún registro lo contiene.

**4.1 Criterio → prueba prevista**

| Criterio | Prueba prevista |
|---|---|
| CA-01 | `Spec026ConexionTests.Se_Une_A_La_Visita_CA_026_01` |
| CA-02 | `Spec026AutenticacionTests.Rechaza_Sin_Token_O_Cancelado_CA_026_02` |
| CA-03 | `Spec026AutenticacionTests.Cierra_Al_Vencer_El_Token_CA_026_03` |
| CA-04 | `Spec026MensajesTests.Entrega_Al_Otro_Participante_CA_026_04` |
| CA-05 | `Spec026AutorizacionTests.Ajeno_No_Entra_CA_026_05` |
| CA-06 | `Spec026AutorizacionTests.Visita_Cerrada_No_Entra_CA_026_06` |
| CA-07 | `Spec026PuenteTests.Entrega_Entre_Dos_Copias_CA_026_07` |
| CA-08 | `Spec026MensajesTests.Rechaza_Texto_Invalido_CA_026_08` |
| CA-09 | `Spec026LimiteTests.Corta_Al_Mensaje_Sesenta_Y_Uno_CA_026_09` |
| CA-10 | `Spec026LimiteTests.Limite_Compartido_Entre_Copias_CA_026_10` |
| CA-11 | `Spec026AutorizacionTests.Enviar_Sin_Unirse_CA_026_11` |
| CA-12 | `Spec026PersistenciaTests.No_Guarda_Mensajes_CA_026_12` |
| CA-13 | `Spec026RegistrosTests.Sin_Datos_Sensibles_CA_026_13` |
| CA-14 | `Spec026SaludTests.Redis_Caido_Falla_Cerrado_CA_026_14` |
| CA-15 | `Spec026AutorizacionTests.Mercado_No_Responde_CA_026_15` |
| CA-16 | `Spec026MensajesTests.Dos_Dispositivos_CA_026_16` |
| CA-17 | `Spec026ApagadoTests.Chat_Apagado_CA_026_17` |
| CA-18 | `Spec026GatewayTests.Token_En_Consulta_CA_026_18` |

## 5. Contrato
**Hub**

| Elemento | Quién | Entrada | Resultado |
|---|---|---|---|
| Saludo `POST /hubs/chat/negotiate` | persona con sesión | `access_token` en la consulta | 200 (negociación), 401, 429, 503 |
| Conexión `wss://…/hubs/chat` | persona con sesión | `access_token` en la consulta | canal abierto, cerrado al vencer el token |
| `UnirseAVisita(visitaId)` | participante | `string` con el `ObjectId` de la visita | `{ ok: true }` o `{ ok: false, codigo }` |
| `SalirDeVisita(visitaId)` | participante | `string` | `{ ok: true }` |
| `EnviarMensaje(visitaId, texto)` | participante unido | `string`, `string` | `{ ok: true, mensajeId, enviadoEn }` o `{ ok: false, codigo }` |
| Evento `mensaje` (servidor → grupo) | participantes de la visita | — | `{ visitaId, mensajeId, texto, enviadoEn }` |

**Códigos de error de las invocaciones:** `no_autorizado`, `conversacion_no_disponible`, `no_disponible`, `no_unido`, `mensaje_invalido`, `limite_de_mensajes`.

**Códigos del saludo:** 401 por sesión ausente, vencida o cancelada; 429 por el límite del gateway; 503 por `chat_apagado`, Redis caído o servicio no disponible.

**Límites:** 1 a 1 000 caracteres por mensaje; 4 KB de invocación y 8 KB de mensaje entrante; 30 mensajes por minuto por persona; ventana de 60 segundos; 3 segundos de espera a Mercado; 15 minutos de vida de la conexión por token.

**Transportes:** WebSocket como primera opción y eventos enviados por el servidor como respaldo; el sondeo largo queda deshabilitado (no hay estado de sesión en el proceso y multiplica las peticiones HTTP).

## 6. Modelo de datos
**Sin colecciones nuevas.** Esta spec no escribe documentos: no crea conversaciones, mensajes, ni marcas de lectura. La visita y sus participantes viven en Mercado (`visitas`, spec 021) y son la única fuente de la pertenencia.

**Llaves de Redis**
| Llave | Tipo | Uso | Vencimiento |
|---|---|---|---|
| `chat:limite:{sub}` | contador | mensajes de la ventana actual | 60 segundos, renovado en la misma operación atómica |
| `chat:visita-cerrada:{visitaId}` | marca | visita cancelada, terminada, no presentada o bloqueada (*decisión automática, revisar*) | 30 días |
| `chat:presencia:{sub}` | presencia | el hub la escribe al aceptar una conexión al grupo `usuario:{sub}` y la renueva en cada latido; la lee el despachador de avisos (spec 037 R-08) | 60 s (renovada por latido) |
| canal `knock:chat` | pub/sub del puente | entrega entre copias | no aplica |

**Estado del proceso:** ninguna regla de negocio vive en la memoria de una copia. La única información local es la lista de salas de una conexión mientras está abierta, que se pierde al cerrarla y no se consulta por nadie más.

**Invariantes:** un mensaje no existe en ningún almacenamiento; sin Redis no hay puente ni límite, así que no hay canal; una persona fuera de la visita no puede entrar a la sala ni por error de Mercado ni por un identificador adivinado.

No hay script de `db/` para esta spec: no crea colecciones ni índices.

## 7. Arquitectura y componentes
- Módulo nuevo `src/Knock.Messaging.Api/Modules/Chat/` con `Hubs`, `Services`, `Data`, `Options`; expone `AddChatModule()` y `MapChatHub()`.
- `ChatHub` (`/hubs/chat`) solo traduce invocaciones a llamadas de servicio y responde con objetos; no valida reglas ni habla con Redis.
- `UnionService` comprueba la pertenencia con `IVisitaCatalog`, guarda las salas de la conexión y agrega al grupo; `MensajeService` valida el texto, cobra el límite y publica en el grupo.
- `IVisitaCatalog` (en `Knock.Contracts`) con la implementación `MarketVisitaCatalog` en `Data/`: cliente HTTP tipado con la dirección de `Services:Market:BaseUrl`, la sesión reenviada y el tiempo límite de configuración; se registra como `scoped` y nunca se captura en un singleton.
- `LimiteDeChat` encapsula el contador de Redis con el script de incremento y vencimiento atómicos; se reutiliza el patrón del limitador del gateway (spec 009).
- `ChatOptions` valida al arrancar el interruptor, los límites de texto y la dirección de Mercado; un valor inválido impide arrancar.
- El puente se configura con `AddSignalR().AddStackExchangeRedis(...)` sobre la conexión de `AddKnockRedis()`; el prefijo `knock:chat` es una constante, no una llave configurable por entorno.
- Mensajería pasa a registrar Redis en su arranque; `RedisExtensions` se duplica desde Identity o Market con su marcador de excepción de duplicación (EXC-019, que se registra al implementar) y `/health/ready` incluye la comprobación de Redis.
- El gateway agrega la ruta `mensajeria-chat-hub` en `ReverseProxy`, el reenvío del salto de protocolo y la lectura del token desde la consulta solo para esa ruta.
- Observabilidad: contadores y medidores de OpenTelemetry con los conteos de R-19; el `sid` y el `sub` no se adjuntan a las trazas.

## 8. Dependencias
- Spec 002 (estructura y estilo de la API), 003 (Redis, configuración y contraseña obligatoria; esta spec **cambia** la decisión de que Mensajería no usaba Redis y lo anota en `Memoria.md`), 006 (dos copias y salud), 009 (validación del token y límites del gateway), 011 (registro y trazas), 016 (renovación de la sesión).
- Spec 021 y 022: la visita, sus participantes y su estado son la fuente de la pertenencia.
- Spec 020: el cliente y el técnico llegan a la visita por la elección de cotización.
- Dependencia nueva evaluada: `Microsoft.AspNetCore.SignalR.StackExchangeRedis` con la misma versión que ASP.NET Core del repositorio (11.0.0-rc.3 mientras dure el RC), licencia MIT. `StackExchange.Redis` ya está evaluado (spec 003). En pruebas, `Microsoft.AspNetCore.SignalR.Client` (MIT). SignalR y el servidor de WebSocket vienen en el marco compartido y no agregan paquete.
- La app del cliente (frontend 016) consume el hub; la app del técnico aún no existe, así que la punta a punta con la app queda para después.
- Redis ya es un requisito del despliegue (spec 005); el chat no agrega infraestructura nueva.

## 9. Amenazas y controles
| Amenaza | Control | Caso que lo prueba |
|---|---|---|
| Escuchar una conversación ajena | Pertenencia comprobada en Mercado antes de unir; sala por visita; `no_autorizado` uniforme | CA-05, CA-15 |
| Adivinar identificadores de visita | La pertenencia no depende del identificador: se comprueba contra Mercado y el 401/404 no distingue inexistente de ajena | CA-05 |
| Fuga del token en la URL | Token aceptado solo en esa ruta, quitado antes de reenviar, nunca registrado | CA-18, CA-13 |
| Robo de una conexión abierta | Vence con el token y se cierra; el `sid` cancelado no vuelve a entrar | CA-02, CA-03 |
| Inundar el chat | 30 mensajes por minuto por persona con contador compartido; límite por usuario del gateway en el saludo | CA-09, CA-10 |
| Mensajes gigantes o con caracteres de control | 1 000 caracteres, 4 KB por invocación y 8 KB entrantes; recorte y rechazo | CA-08 |
| Enviar sin pertenecer a la visita | Unión previa obligatoria; autorización del canal por la sala | CA-11 |
| Aceptar tráfico sin puente ni límite | `/health/ready` y el saludo fallan cerrado si Redis no responde | CA-14 |
| Abuso sostenido | Interruptor `Chat:Habilitado` y cierre al vencer el token | CA-17 |
| Fuga de datos personales en registros | Conteos y códigos; sin texto, token, `sub`, `sid`, IP ni visita | CA-13 |
| Contenido ofensivo sin moderar | Riesgo aceptado: no se guarda nada; al persistir (HU-029) la moderación pasa a ser obligatoria | — |
| Enumerar participantes | El mensaje no lleva identidad de quien escribe y la sala no expone una lista de miembros | CA-04 |

## 10. Plan de pruebas
| Tipo | ¿Aplica? | Motivo |
|---|---|---|
| Unitarias | Sí | Validación del texto, ventana del límite, códigos de error y opciones de configuración |
| Integración | Sí | Dos hosts de Mensajería con Redis y MongoDB reales (`Testcontainers`) y el cliente de SignalR; Mercado simulado con un servidor HTTP de prueba |
| Seguridad negativa | Sí | Persona ajena, sin token, token cancelado, visita cerrada, envío sin unirse y límite |
| Contrato | Sí | La ruta del hub y el reenvío del token en el gateway; el contrato OpenAPI de Mensajería no cambia y se comprueba que sigue igual |
| Arquitectura | Sí | El hub no accede a Redis ni a clientes HTTP; el cliente de Mercado no se captura en un singleton |
| Mutación | Sí | Condición de pertenencia, condición del estado de la visita y corte del límite |
| Rendimiento | Sí (acotado) | Un escenario de 50 conexiones y 30 mensajes por minuto para comprobar que el puente no duplica entregas ni pierde mensajes |
| Aceptación | Sí | CA-01 a CA-18 con la tabla 4.1 |
| Humo y extremo a extremo | Sí | Flujo de visita en el entorno de evaluación con Redis real: el técnico confirmado se une a la sala, el mensaje llega al cliente en vivo y, tras cancelar la visita, el envío responde `conversacion_no_disponible` (R-10) |
| Regresión | Sí | Las suites de Mensajería, del gateway (rutas, límites y encabezados de la spec 009) y de las specs 015 a 019 siguen en verde |
| CI/CD | Sí | Job `gate` de `ci-pr.yml` en verde; `openapi-diff` confirma que ningún contrato cambió; tras el despliegue, `/health/ready` de Mensajería incluye Redis y el saludo sin token responde 401 |
| Análisis estático | Sí | Semgrep (job `sast`) sin hallazgos nuevos; regla que prohíbe registrar `access_token`; complejidad máxima 10; la duplicación de `RedisExtensions` queda cubierta por su excepción registrada |
| Vulnerabilidades y licencias | Sí | NuGetAudit, `dotnet list package --vulnerable`, OSV-Scanner y dependency-review para `Microsoft.AspNetCore.SignalR.StackExchangeRedis` y `Microsoft.AspNetCore.SignalR.Client` (MIT) sin altas ni críticas |
| DAST | Parcial (nocturno) | ZAP cubre el saludo HTTP (`/hubs/chat/negotiate`) con y sin token; el canal WebSocket se prueba con las pruebas de integración, porque ZAP no ejercita invocaciones de SignalR |

**Gates:** los de `AGENTS.md`; la prueba de las dos copias levanta dos procesos con la misma Redis y un solo cliente por copia; la prueba del gateway comprueba que la petición reenviada no lleva `access_token` en la URL; `docs/endpoints.md` documenta el hub y `docs/openapi*.json` no cambia.

## 11. Escenarios
**Positivos**
- El cliente abre el chat de su visita, el técnico se une desde su dispositivo y conversan mientras llega.
- El cliente cambia de teléfono: la conexión nueva se une a la misma visita y sigue recibiendo.
- El técnico está conectado a la otra copia y el mensaje le llega igual.
- La sesión vence a los 15 minutos y la app renueva y reconecta sin que la persona lo note.

**Negativos**
- Una persona con el identificador de una visita ajena intenta unirse.
- Un cliente envía 100 mensajes en un minuto para molestar.
- Un mensaje con un enlace de 5 000 caracteres.
- Una conexión que intenta enviar sin haberse unido.
- Un cliente con la sesión cerrada intenta volver a entrar.

**Errores esperados**
- 401 por sesión ausente, vencida o cancelada; 429 por el límite del gateway; 503 por chat apagado, Redis caído o Mercado sin responder; `no_autorizado`, `conversacion_no_disponible`, `no_disponible`, `no_unido`, `mensaje_invalido` y `limite_de_mensajes` como códigos de las invocaciones.

**Casos límite**
- Mensaje de exactamente 1 000 caracteres y de 1 001.
- El mensaje 60 y el 61 de la misma ventana.
- Vencimiento del token justo mientras se envía un mensaje.
- La visita se cancela con las dos personas conectadas: el envío responde `conversacion_no_disponible` en cuanto llega el evento de Mercado (R-10, `chat:visita-cerrada`); las conexiones abiertas se cierran al vencer su token.
- Redis se reinicia con conexiones abiertas: las conexiones sobreviven a nivel de socket, pero el siguiente mensaje falla cerrado hasta que el servicio vuelva a estar disponible.
- Dos dispositivos de la misma persona en la misma visita.

## 12. Definición de terminado
- Los 18 criterios tienen prueba en verde y la tabla 4.1 coincide con los nombres reales (`python scripts/sdd_lint.py --trace 026` sin faltantes).
- Job `gate` en verde y análisis de dependencias sin hallazgos altos ni críticos para los paquetes nuevos.
- La prueba de las dos copias demuestra la entrega entre procesos distintos y que el límite es único.
- `/health/ready` de Mensajería incluye Redis y la prueba de Redis caído falla cerrado.
- El gateway reenvía el salto de protocolo y quita `access_token` de la URL; la prueba lo comprueba.
- `docs/endpoints.md` documenta el hub y su contrato; `docs/openapi*.json` no cambia.
- `Memoria.md` anota el cambio de decisión (Mensajería usa Redis), la excepción de duplicación de `RedisExtensions` y la dependencia nueva.
- Se documenta en el PR que no hay historial de mensajes ni moderación porque nada se guarda, y que el chat previo a la elección de técnico queda fuera.
- Revisión de par y sin hallazgos de seguridad abiertos.

## 13. Decisiones
- **La visita es la conversación:** con dos participantes por visita, el identificador de la visita ya es único y su dueño (Mercado) ya sabe quiénes son; duplicar esa relación en Mensajería agregaría una copia que se desincroniza. Por eso no hay colección `conversaciones`.
- **La pertenencia se pregunta, no se copia:** se reenvía la sesión de quien se une a `GET /api/v1/visitas/{id}` (spec 022), que ya distingue dueño, técnico y ajeno con un 404 uniforme. Es el mismo patrón que `PUT /me` usa para validar la colonia (spec 017).
- **Se comprueba al unirse y no por mensaje:** una llamada HTTP por mensaje multiplicaría la carga sobre Mercado y agregaría hasta 3 segundos de espera en el camino del chat; la unión es una decisión por conexión y la sala es la autorización del socket.
- **Token en la consulta solo para el hub:** es la forma estándar de autenticar WebSocket desde la app; se acota a una ruta, el gateway lo valida y lo convierte en encabezado, y no se registra. La alternativa —dejar la ruta anónima y validar solo en el servicio— quitaría la validación de la puerta de entrada.
- **Redis como puente y como contador:** es el único almacén compartido que ya existe; con dos copias, un contador en memoria duplicaría el límite (el mismo riesgo que ya acepta la matriz de límites de los servicios) y sin puente los mensajes se perderían en silencio.
- **Falla cerrado sin Redis:** un chat que acepta conexiones sin puente entrega mensajes solo a veces, que es peor que no entregarlos; además sin Redis no hay límite.
- **Sin historial y sin moderación en esta spec:** guardar mensajes trae retención, borrado, denuncia y moderación obligatoria antes de guardar (regla 13 de la constitución); eso es HU-029 y merece su propia spec. Sin guardado, la moderación no aplica.
- **WebSocket y eventos del servidor, sin sondeo largo:** el sondeo largo obliga a afinidad de sesión en el balanceador y multiplica las peticiones; la app es móvil y puede mantener un WebSocket.
- **Sin presencia ni "escribiendo…":** son estado efímero que nadie pide todavía y que agrega llaves y tráfico; llegan con la spec del chat completo (HU-029).
- **`Chat:Habilitado` con valor explícito:** sin valor por omisión el servicio no arranca, así el interruptor no se queda encendido por olvido en un entorno donde no se quiere chat.

## 14. Decisiones tomadas sin consulta
Se tomaron de noche, con el criterio de la constitución; quien apruebe puede cambiarlas antes de firmar.

1. **El chat existe solo desde la visita confirmada.** La spec 021 sugiere un chat previo cuando el técnico no publicó contacto; esta spec lo deja fuera porque la app del técnico no existe y porque un chat antes de elegir no tiene una visita que dé la pertenencia. Alternativa: abrir la conversación por par (solicitud, técnico) y validar la pertenencia con el detalle de la solicitud (spec 020), que deja la conversación sin estado propio.
2. **No se crea la colección `conversaciones`.** Un borrador anterior de la spec 021 decía que la 026 cerraría conversaciones al consumir eventos (la versión revisada de la 021 ya no lo dice); esta spec no consume eventos ni guarda conversaciones porque hoy no hay un transporte de eventos entre servicios definido (cada servicio tiene su propio buzón). Alternativa: un endpoint interno de escritura en Mensajería más un transporte de eventos entre servicios, que es infraestructura que ninguna spec de la base define todavía.
3. **El cierre de la conversación es el estado de la visita.** No hay operación de cierre: si la visita termina, se cancela o se bloquea, la unión deja de aceptarse y los envíos se cortan con la marca `chat:visita-cerrada:{visitaId}` (R-10), sin esperar a que venza el token (*decisión automática, revisar*). Alternativa: cerrar solo al vencer el token, que deja hasta 15 minutos de ventana abierta.
4. **La dirección de Mercado es obligatoria al arrancar.** Sin ella, todas las uniones fallarían; fallar al arrancar es más honesto que servir un chat que nunca deja entrar. Alternativa: arrancar y responder `no_disponible`, como hace Identidad mientras no tenga la URL (spec 017).
5. **El límite es de 30 mensajes por minuto por persona, con ventana fija** (*decisión automática, revisar*). Es el mismo umbral de la conversación previa a elegir (spec 030 R-11) y comparte llave; la matriz de límites para el chat debe quedar en 30. Alternativa: ventana deslizante con más precisión y más memoria en Redis.
6. **El `mensajeId` es un UUID v7 generado en el servidor.** Da orden temporal sin reloj compartido y deja que el emisor reconozca su propio mensaje; no deduplica reintentos: sin `Idempotency-Key` y con el identificador generado por el servidor, un reintento tras un corte de red entrega el mensaje otra vez con otro `mensajeId` (riesgo aceptado, como en la spec 030 §15; ver §15). Alternativa: un contador por sala, que necesita estado que esta spec no guarda.
7. **El mensaje no lleva a quién lo envió.** En una conversación de dos, el receptor ya sabe quién es el otro y el emisor reconoce su propio mensaje por el `mensajeId` que recibió; así no se expone el `sub` ni el nombre a quien no lo necesita. Alternativa: incluir `de` con el `sub`, que reparte un identificador personal en cada mensaje.
8. **El chat se apaga con un interruptor de configuración.** Un abuso sostenido se corta cambiando una variable y reiniciando, sin desplegar código; el costo es un valor obligatorio más en la configuración.
9. **El sondeo largo se deshabilita.** Evita la afinidad de sesión y el costo por petición; si alguna red lo necesitara, se habilita en un solo lugar de configuración.

**Agregadas en la revisión del 9 de noviembre**

10. **La pertenencia solo lee `rol` y `estado` de la vista de la visita.** La vista del cliente (spec 022) trae el código y la dirección; deserializar solo dos campos y no registrar el cuerpo evita que esos datos queden en Mensajería. Alternativa: una ruta interna de pertenencia en Mercado, más limpia pero con una superficie nueva; se propone como mejora.
11. **El chat antes de elegir (HU-029, KNOCK-72) queda para su propia spec del Sprint 5**, que debe reutilizar este hub y su autenticación en lugar de crear otro canal.

## 15. Riesgos y dudas abiertas
- **No hay historial:** un mensaje enviado con el destinatario desconectado se pierde y no se recupera. La app debe advertirlo y HU-029 debe decidir la persistencia, la retención y la moderación antes de guardar.
- **El chat depende de Mercado para dejar entrar:** si Mercado está caído, nadie entra a una conversación existente. Se acepta porque la alternativa (asumir pertenencia) abriría conversaciones ajenas.
- **Un reintento tras un corte de red duplica el mensaje (duda abierta, con propuesta):** `EnviarMensaje` no tiene clave de idempotencia (retirada el 2026-11-10) y el `mensajeId` lo genera el servidor, así que nada deduplica un reenvío. Propuesta: aceptar el riesgo en esta etapa, con la misma mitigación de la spec 030 (la app no reenvía solos los mensajes sin confirmar y el límite de 30 por minuto acota el daño); si se quiere deduplicar después, el cliente enviaría un identificador propio opcional y el servidor lo recordaría un tiempo corto en Redis, lo que requiere una spec propia. La decide el equipo antes de aprobar.
- **La validación del token se duplica:** gateway y servicio mantienen la misma regla; una diferencia entre ambos produce rechazos intermitentes. Las pruebas deben ejercitar los dos caminos con el mismo conjunto de tokens.
- **El límite del gateway no ve las invocaciones del hub:** después del saludo, los mensajes no pasan por el gateway; el único control es el contador de Redis del servicio. Una persona que reinicia la conexión cada minuto vuelve a empezar su ventana solo si el contador vence, que es lo previsto.
- **El token viaja en la URL del cliente:** aunque el gateway lo quite antes de reenviar, el cliente lo tiene en su propia URL. La app no debe registrar sus peticiones; el riesgo residual queda del lado del dispositivo.
- **Dos copias y el puente:** la prueba de integración usa dos hosts en el mismo proceso de pruebas; no demuestra una red real entre réplicas de Container Apps. La comprobación en Azure queda para el despliegue (spec 005).
- **La versión del paquete del puente:** debe seguir la versión del marco (hoy RC); al pasar a .NET 11 final hay que actualizarla junto con el resto.
- **Redis es ahora un requisito de disponibilidad de Mensajería:** antes el servicio arrancaba sin Redis; con esta spec, sin Redis no está listo. Es coherente con la spec 006, pero cambia la operación del servicio y hay que avisarlo al líder.
- **Sin app del técnico:** la punta a punta real se prueba con el cliente de SignalR y semillas; el técnico no tiene pantalla todavía.
- **La vista de la visita se consulta en cada unión:** una reconexión en bucle de una app defectuosa produce una llamada a Mercado por intento; el límite por usuario del gateway en el saludo es lo que lo acota.

## Aprobación
- [ ] Revisor 1 — nombre: ________ fecha: ________
- [ ] Sin conflicto con la constitución
- **Estados:** Borrador (agente) → Aprobada (persona; habilita el código) → Implementada (tras el merge)
