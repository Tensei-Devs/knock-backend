# Spec 037 — Registro de dispositivos y despachador de avisos push

> **Estado:** Aprobada · **Repositorio:** knock-backend · **Rector:** `constitution.md`
> **Origen:** HU-050 (KNOCK-60, «Recibir notificaciones push», capa Mobile, con su parte de backend). Fija el hueco de frontend 024 §13 (envío y registro de dispositivos) y la carga v1 que esa spec define. **Clave de Jira:** KNOCK-60 (parte de backend; crear subtarea si hace falta).
> **Servicio:** Mensajería y notificaciones · **Módulo:** Avisos · **Colecciones:** `notifications.dispositivos`, `notifications.enviados`, `notifications.pendientes`
> **Sprint:** 5 (propuesto) · **Responsable:** Alexis
> **Consumidora:** frontend 012 (identificador y registro), frontend 024 (catálogo de avisos y carga v1)

## 1. Objetivo

Que el teléfono de cada persona reciba un aviso de Knock cuando pasa algo en su solicitud, su visita o su conversación, sin que el servidor envíe texto ni datos personales dentro del aviso. Esta spec define tres cosas: el registro del identificador del dispositivo, el despachador que convierte eventos en avisos y la carga v1 que la app lee.

## 2. Historias y alcance

**2.1 Historias**
- **HU-050 (parte de backend)** — Como persona con la app instalada, quiero recibir un aviso cuando mi solicitud, mi cotización, mi visita o mi conversación cambian, para no tener que abrir la app a revisar. *Terminada cuando:* un evento del catálogo de frontend 024 §4.2 llega al teléfono con la carga v1, no se repite y no llega a quien ya lo está viendo en vivo.

**2.2 Dentro**
- `POST /api/v1/dispositivos` para registrar el identificador del dispositivo (idempotente por token), y `DELETE /api/v1/dispositivos/{id}` para quitarlo al cerrar sesión (la ruta de registro la cita backend 016 como `DIS-02` sin definirla; esta spec la define).
- El despachador: lee los eventos pendientes, arma la carga v1, deduplica y envía por Firebase Cloud Messaging.
- Las reglas de ráfaga para `mensaje_nuevo` y la regla de no avisar a quien está conectado.
- La entrega interna de los eventos de aviso de Mercado e Identidad al despachador por HTTP privado con token firmado por emisor, el límite de avisos por persona y el borrado de dispositivos al terminar la sesión (R-11 a R-13).

**2.3 Fuera**
- El texto que ve la persona (lo arma la app desde sus ARB, frontend 024 D-02).
- Las preferencias por tipo: se aplican en la app mientras no exista `PUT /me/preferencias-avisos` (HU-051, Sprint 6; frontend 024 D-10).
- Los avisos «técnico llegó» (`VisitaEnCurso`) y «cambió tu verificación» (`VerificacionDecidida`): se difieren a una spec posterior (decisión del 2026-11-12); la v1 despacha los ocho eventos de frontend 024 §4.2. Hay que dejarlo como duda abierta explícita en frontend 024.

## 3. Requisitos

- **R-01:** `POST /api/v1/dispositivos` exige JWT de persona y cuerpo `{ identificador, plataforma }`, donde `identificador` es el token de registro del dispositivo (la clave natural, sin `Idempotency-Key`) y `plataforma` ∈ `android`, `ios`. Es idempotente por token: responde `201` la primera vez que la persona registra ese token y `200` en cada repetición, sin duplicar el registro. El registro se liga al `sid` de la sesión que lo hizo. Un token ya registrado por otra persona se reasigna a la persona que llega, al iniciar sesión, y el último registro gana (un teléfono cambia de cuenta al iniciar sesión, decisión del 2026-11-12): la persona anterior deja de recibir avisos en ese dispositivo.
- **R-02:** `DELETE /api/v1/dispositivos/{id}` exige JWT del dueño y responde `204` (también si ya no existe). La app lo llama al cerrar sesión. Además se borra todo dispositivo cuyo `sid` termina por cualquier vía (cierre, cierre remoto, expulsión por el límite de sesiones, reuso del token de renovación) con el evento `sesion_revocada` (R-13); como red de seguridad, el despachador no envía a un `sid` con `sesion-cancelada:{sid}` en Redis (spec 009) y los registros sin renovar en 30 días se borran por TTL. Un registro viejo no sobrevive en un teléfono compartido.
- **R-03:** El identificador se guarda cifrado en `notifications.dispositivos` y no se devuelve en ninguna respuesta ni se registra. Un token que Firebase rechace como inválido se borra del registro.
- **R-04:** La carga v1 tiene estos campos y ningún otro: `v` (siempre `1`), `tipo` (`cotizaciones`, `mensajes`, `visitas` o `sistema`), `evento` (uno de los ocho de frontend 024 §4.2), `recurso` (`ObjectId` de 24 hexadecimales; omitido en `sistema`), `aviso` (UUID del envío) y `conversacion` (solo en `mensaje_nuevo`). Nunca lleva nombres, teléfonos, direcciones, montos, fechas de la cita, códigos ni texto de mensajes (frontend 024 R-02 y §4.1).
- **R-05:** Los avisos se envían como mensajes solo de datos, con prioridad alta y sin el bloque `notification` de Firebase, para que el texto lo arme la app (frontend 024 D-02).
- **R-06:** El despachador deduplica por `eventoId` (el identificador persistente del mensaje del buzón del emisor, opaco) y persona destinataria: el mismo evento entregado dos veces se envía una sola vez; dos eventos distintos sobre el mismo recurso (por ejemplo, la segunda cotización de una solicitud) se envían ambos. Índice único `{ eventoId, destinatario }` en `notifications.enviados`, con TTL. Los envíos se registran en `notifications.enviados` con `evento`, `tipo` y el resultado, sin texto ni identificadores de recurso en claro.
- **R-07:** `mensaje_nuevo` se agrupa por conversación y persona en ventanas de 5 minutos. El primer aviso de la ventana se envía; los mensajes que lleguen después dentro de la misma ventana no generan otro aviso, y al cerrarla se envía un único aviso más si hubo mensajes nuevos (decisión 3 de la sección 8: frontend 024 propone «cada pocos minutos» y se fija el valor de 5 minutos).
- **R-08:** No se envía `mensaje_nuevo` a una persona con presencia activa. El hub escribe la clave `chat:presencia:{sub}` en Redis al aceptar una conexión al grupo `usuario:{sub}` (spec 030 R-15), con vencimiento de 60 segundos que el hub renueva en cada latido de SignalR (cada 15 segundos). Si la clave venció, se envía el aviso (*decisión de revisión, confirmar*).
- **R-09:** Un fallo de Firebase se reintenta con espera creciente hasta 3 veces; después queda registrado en `notifications.enviados` como fallido. Un fallo no reintenta el evento de dominio ni cambia la visita ni la solicitud.
- **R-10:** Las credenciales de Firebase se leen de la configuración segura del despliegue (almacén de secretos), nunca del repositorio ni de un registro. Esta spec no nombra valores.
- **R-11:** **Entrega interna de eventos de aviso.** Mercado e Identidad entregan sus eventos al despachador por `POST /api/v1/interno/avisos` del servicio de Mensajería, con ingress privado de Container Apps; la ruta no está en la lista cerrada de rutas del gateway, así que no se alcanza desde fuera. Cada emisor la llama desde su procesador del buzón de salida (outbox, spec 003) con reintentos. La llamada lleva un token firmado (HS256) con una clave HMAC **por emisor** (secretos `avisos-key-market` y `avisos-key-identity` del Key Vault: cada servicio solo puede leer el suyo, por el rol por secreto de Terraform, y Mensajería lee las dos para verificar), `iss` del emisor (`knock-market` o `knock-identity`), `aud` = `knock-notifications`, `exp` de 5 minutos como máximo, `jti` único y el claim `permisos` con los tipos que ese emisor puede enviar. No se reutiliza `jwt-signing-key`, la clave de los tokens de usuario. El cuerpo es `{ eventoId, tipo, destinatarioId, rol, recurso? }` (`rol`: `cliente` o `tecnico`) o, de Identidad, `{ eventoId, tipo: "sesion_revocada", sid }`. `tipo` sale de un catálogo cerrado (los ocho eventos de frontend 024 §4.2 y `sesion_revocada`) y quien llama **nunca** envía texto libre: el texto lo arma la app (R-05). Responde `202`, también si ya recibió ese `eventoId`; un token inválido o vencido responde 401, un `tipo` que el emisor no tiene en `permisos`, 403 `service_not_allowed`, y un `tipo` fuera del catálogo o un `rol` que no corresponde al tipo (por ejemplo `cotizacion_nueva` a un técnico), 422. **Limitación de la validación del destinatario:** el despachador valida el permiso del emisor para ese `tipo`, que el `rol` corresponda al tipo, que la persona tenga un dispositivo activo y el límite por persona (R-12); **no valida por sí mismo que la persona tenga una relación real con el `recurso`** (cliente dueño, técnico con envío, participante de la conversación), porque no lee la base del emisor (spec 001, R-03). Esa relación la fija el emisor al armar el evento dentro de la transacción de su cambio, y solo puede enviar sus propios tipos con plantillas fijas. Un emisor comprometido podría avisar a cualquier persona con dispositivo, con los tipos que tiene en `permisos`, sin texto libre, y el límite por persona acota el daño. Validarla de verdad exigiría una consulta interna por emisor, que esta spec no incluye por costo.
- **R-12:** **Límite por persona.** Máximo `Avisos:LimitePorPersonaHora` avisos por persona y hora (30 por omisión, configurable y validado al arrancar); el aviso que lo excede no se envía y queda como `limitado` en `notifications.enviados`. El límite no se aplica a `sesion_revocada`.
- **R-13:** **Sesión revocada.** Al recibir `sesion_revocada { sid }` el despachador borra los dispositivos con ese `sid` (R-02). Identidad lo emite en cada vía en que una sesión termina: cierre (spec 016, R-06), cierre remoto (spec 018), expulsión por el límite de sesiones (spec 015, R-10) y reuso del token de renovación (spec 016).

## 4. Contrato API

| Método y ruta | Quién | Cuerpo | Respuesta | Errores |
|---|---|---|---|---|
| `POST /api/v1/dispositivos` | persona con sesión | `{ identificador, plataforma }` | `201` primera vez; `200` si el token ya estaba registrado para esa persona | `400`, `401`, `422`, `429` |
| `DELETE /api/v1/dispositivos/{id}` | dueño | — | `204` | `401`, `429` |
| `POST /api/v1/interno/avisos` | servicio emisor (ruta interna, fuera del gateway) | `{ eventoId, tipo, destinatarioId, rol, recurso? }` o `{ eventoId, tipo: "sesion_revocada", sid }` | `202` (también si ya se recibió ese `eventoId`) | `401`, `403 service_not_allowed`, `422`, `429` |

**Códigos de error:** solo la ruta interna tiene códigos propios (`service_not_allowed`, 403); los errores de campo (por ejemplo, una `plataforma` fuera de la lista) salen como 422 con `errors`, el cuerpo ilegible como 400, y 401 y 429 como en la convención de la spec 002 (R-08 a R-12).

**Eventos que despacha** (frontend 024 §4.2): `solicitud_nueva`, `solicitud_sin_tecnicos`, `cotizacion_nueva`, `cotizacion_aceptada`, `solicitud_cancelada`, `solicitud_vencida`, `cotizacion_vencida`, `mensaje_nuevo`. Cada uno con su `tipo` y su `recurso` según esa tabla.

## 5. Seguridad

| Amenaza | Control | Prueba |
|---|---|---|
| Registrar el dispositivo de otra persona | Reasignación solo al iniciar sesión; respuesta sin identificador | CA-02 |
| Un servicio hace pasar avisos que no le tocan | Token firmado con clave HMAC propia por emisor, `iss`, `aud`, vencimiento y permisos por tipo; clave aislada por secreto en el Key Vault | CA-14, CA-15 |
| Un emisor comprometido avisa a una persona sin relación con el recurso (**limitación aceptada**: el despachador no valida esa relación, R-11) | Permisos por tipo en el token, plantillas fijas sin texto libre y límite por persona (R-12); la relación la fija el emisor en la transacción de su cambio | CA-14, CA-17 |
| Texto libre o destinatario con un rol que no corresponde | Solo `tipo` del catálogo cerrado, `destinatarioId` y `rol`; el rol se valida contra el tipo | CA-16 |
| Inundar a una persona de avisos | Límite por persona (R-12) | CA-17 |
| Aviso al teléfono de otra persona tras cerrar sesión | Registro ligado al `sid`, borrado por cierre y por revocación, TTL de 30 días | CA-18 |
| Filtrar datos en la carga | Lista cerrada de campos (R-04) | CA-04 |
| Avisos repetidos o en ráfaga | Deduplicación (R-06) y ventana de `mensaje_nuevo` (R-07) | CA-03, CA-05 |
| Identificador en registros o en respuestas | Cifrado, sin devolverlo y solo `evento`, `tipo` y resultado en registros (R-03, R-06) | CA-07, CA-13 |

## 6. Criterios de aceptación

- **CA-01:** Dado un dispositivo nuevo, cuando la persona lo registra, entonces responde `201`; al repetir el mismo token, responde `200` sin duplicar el registro. (R-01)
- **CA-02:** Dado un identificador de otra persona en sesión activa, cuando se intenta registrar, entonces no se devuelve ni se modifica el identificador ajeno hasta que esa persona inicie sesión. (R-01)
- **CA-03:** Dado el mismo evento de `cotizacion_nueva` entregado dos veces, cuando el despachador lo procesa, entonces se envía un solo aviso. (R-06)
- **CA-04:** Dada cualquier carga enviada, cuando se revisa, entonces solo tiene los campos de R-04 y ninguno contiene datos personales. (R-04)
- **CA-05:** Dado un mensaje nuevo en cada una de cinco conversaciones dentro de un minuto, cuando el despachador los procesa, entonces envía un aviso por conversación; tres mensajes más de una de esas conversaciones dentro de la misma ventana de 5 minutos no generan avisos adicionales hasta que la ventana cierra. (R-07)
- **CA-06:** Dado un destinatario con presencia activa en `usuario:{sub}`, cuando llega `mensaje_nuevo`, entonces no se envía el aviso push; si su presencia venció, sí se envía. (R-08)
- **CA-07:** Dado un envío, cuando se revisan los registros y `notifications.enviados`, entonces no aparecen identificadores de dispositivo, texto ni `recurso` en claro. (R-03, R-06)
- **CA-08:** Dado un token rechazado por Firebase como inválido, cuando se procesa, entonces se borra del registro y no se reintenta. (R-03)
- **CA-09:** Dada una sesión que cierra, cuando la app llama a `DELETE /api/v1/dispositivos/{id}`, entonces responde `204`, y si el dispositivo ya no existe también responde `204`. (R-02)
- **CA-10:** Dado un evento de aviso, cuando se arma el mensaje, entonces es solo de datos, con prioridad alta y sin bloque `notification`. (R-05)
- **CA-11:** Dado un fallo transitorio de Firebase, cuando se envía, entonces reintenta hasta 3 veces con espera creciente; si sigue fallando, queda como fallido en `notifications.enviados` sin cambiar la visita ni la solicitud. (R-09)
- **CA-12:** Dadas las credenciales de Firebase, cuando se revisan el repositorio, la configuración del código y los registros, entonces no aparece ningún valor de credencial. (R-10)
- **CA-13:** Dado un dispositivo registrado, cuando se revisa la base y cualquier respuesta, entonces el identificador está cifrado en `notifications.dispositivos` y no aparece en claro. (R-03)
- **CA-14:** Dado un token de servicio firmado con otra clave, con `iss` o `aud` distintos o vencido, cuando llama a `POST /api/v1/interno/avisos`, entonces responde 401; dado un emisor sin permiso para ese `tipo`, responde 403 `service_not_allowed`; y ninguna de las llamadas crea un envío. (R-11)
- **CA-15:** Dada la ruta interna, cuando se llama a través del gateway, entonces no se enruta; y dado un servicio que intenta leer la clave de otro emisor en el Key Vault, entonces el acceso se niega. (R-11)
- **CA-16:** Dado un cuerpo con texto libre, un `tipo` fuera del catálogo o un `rol` que no corresponde al tipo, cuando se envía, entonces responde 422 y no se envía nada. (R-11)
- **CA-17:** Dado el límite por persona de 30 por hora, cuando llegan 31 avisos para la misma persona dentro de la hora, entonces el 31 no se envía y queda `limitado`, y otra persona no se ve afectada. (R-12)
- **CA-18:** Dado un dispositivo registrado con el `sid` de una sesión, cuando esa sesión termina por cierre, cierre remoto, expulsión o reuso del token, entonces el dispositivo se borra y no recibe avisos; y dado un teléfono compartido donde la persona B inicia sesión después de la A, entonces el registro de A no sobrevive. (R-01, R-02, R-13)
- **CA-19:** Dados dos eventos distintos sobre el mismo recurso (la segunda cotización de una solicitud) y uno de ellos entregado dos veces, cuando se procesan, entonces se envían dos avisos y no tres. (R-06)

## 7. Pruebas

| Tipo | ¿Aplica? | Qué cubre |
|---|---|---|
| Unitarias | Sí | Reglas de R-04, R-06, R-07 (ventana de 5 minutos) y R-08 (presencia vencida) |
| Integración | Sí | MongoDB y Redis reales (contenedores); Firebase simulado (cliente HTTP de prueba); entrega interna de Mercado e Identidad (R-11) |
| Seguridad negativa | Sí | Identificador ajeno, carga con campos extra, registros sin datos, token inválido de Firebase, token de servicio inválido o sin permiso, texto libre y límite por persona |
| Contrato (CON) | Sí | `openapi-diff` contra `contracts/openapi.v1.json` y ejemplos de carga v1 de frontend 024 (`app/test/fixtures/avisos/*.json`) |
| Arquitectura | Sí | El despachador no lee colecciones de otro servicio; solo recibe eventos por la ruta interna de R-11, que el gateway no enruta |
| Regresión | Sí | Suites de specs 020, 021, 026 y 030 (eventos del buzón) en verde; frontend 012 y 024 |
| Mutación | Sí (acotada) | Solo reglas de R-06, R-07 y R-08 (deduplicación, ventana y presencia) |
| Humo | Sí | En el entorno de evaluación: `POST /api/v1/dispositivos` sin token responde 401 y con token responde 201 o 200 |
| E2E | No aplica en backend | El recorrido de recibir el aviso en un teléfono de prueba lo cubre frontend 024 (E2E) |
| Carga | Sí (acotada) | Ráfaga de 1 000 eventos de aviso a 200 personas destinatarias: sin pérdidas ni duplicados |
| CI gates | Sí | Análisis, formato, pruebas, cobertura, escáneres (Semgrep, CodeQL, detección de secretos y OSV) y `oasdiff` |

## 8. Riesgos y decisiones

1. **Transporte entre servicios (decisión del 2026-11-12).** HTTP interno con ingress privado, fuera del gateway, y token firmado con clave HMAC por emisor (R-11); el buzón de salida (outbox, spec 003) de cada emisor sigue siendo el origen de los eventos, con reintentos. Hoy no existe autenticación entre servicios: la única llamada interna (Identidad a Mercado) reenvía el `Authorization` del usuario, y `jwt-signing-key` es una sola clave compartida por el gateway, Identidad y Mercado, que no sirve para permisos por servicio. Riesgo residual: el despachador no lee la base del emisor (spec 001, R-03) y no puede verificar por sí mismo la relación entre el destinatario y el recurso; la fija el emisor en la transacción de su cambio, y el token solo le permite sus propios tipos con plantillas fijas, sin texto libre. Hay dos secretos nuevos en el Key Vault (`avisos-key-market` y `avisos-key-identity`), con permiso de lectura por secreto, y una ruta que el gateway no debe enrutar (specs 005 y 009). Responsable: Alexis.
2. **Mensajes solo de datos (R-05).** Garantiza que el servidor no escriba texto para la persona, a costa de que la app debe armar el aviso siempre (frontend 024 D-02). *Decisión de revisión, confirmar.*
3. **Ventana de 5 minutos para `mensaje_nuevo` (R-07).** Evita ráfagas sin retrasar el primer aviso, que sale de inmediato; solo se agrupan los siguientes. Es un valor por defecto: cambiarlo no altera ninguna otra regla de la spec.
4. **Presencia en Redis (R-08).** La clave `chat:presencia:{sub}` la escribe el hub con TTL de 60 segundos renovado por latido. Si Redis no responde, se envía el aviso aunque la persona esté conectada: es el fallo más barato (un aviso de más, nunca uno de menos).

## Trazabilidad CA → prueba

| CA | Prueba |
|---|---|
| CA-01 | `Spec037DispositivosTests.Registro_201_Y_Repetido_200_CA_037_01` |
| CA-02 | `Spec037DispositivosTests.Identificador_Ajeno_No_Se_Devuelve_CA_037_02` |
| CA-03 | `Spec037DespachoTests.Evento_Duplicado_Un_Aviso_CA_037_03` |
| CA-04 | `Spec037DespachoTests.Carga_Solo_Campos_v1_CA_037_04` |
| CA-05 | `Spec037DespachoTests.Ventana_Mensaje_Nuevo_CA_037_05` |
| CA-06 | `Spec037DespachoTests.Presencia_Suprime_Aviso_CA_037_06` |
| CA-07 | `Spec037DespachoTests.Registros_Sin_Identificadores_CA_037_07` |
| CA-08 | `Spec037DespachoTests.Token_Invalido_Se_Borra_CA_037_08` |
| CA-09 | `Spec037DispositivosTests.Delete_Idempotente_204_CA_037_09` |
| CA-10 | `Spec037DespachoTests.Mensaje_Solo_Datos_CA_037_10` |
| CA-11 | `Spec037DespachoTests.Reintentos_Y_Fallo_Registrado_CA_037_11` |
| CA-12 | `Spec037SecretosTests.Sin_Credenciales_En_Repositorio_CA_037_12` |
| CA-13 | `Spec037DispositivosTests.Identificador_Cifrado_CA_037_13` |
| CA-14 | `Spec037InternoTests.Token_De_Servicio_Invalido_O_Sin_Permiso_CA_037_14` |
| CA-15 | `Spec037InternoTests.Ruta_No_Enrutada_Y_Clave_Aislada_CA_037_15` |
| CA-16 | `Spec037InternoTests.Sin_Texto_Libre_Ni_Rol_Invalido_CA_037_16` |
| CA-17 | `Spec037DespachoTests.Limite_Por_Persona_CA_037_17` |
| CA-18 | `Spec037DispositivosTests.Sesion_Terminada_Borra_Dispositivo_CA_037_18` |
| CA-19 | `Spec037DespachoTests.Eventos_Distintos_Mismo_Recurso_CA_037_19` |

## Aprobación

- [x] Revisor 1 — nombre: José Julián Hernández Hernández fecha: 2026-11-12
- [x] Sin conflicto con la constitución
