# Spec 037 — Registro de dispositivos y despachador de avisos push

> **Estado:** Borrador · **Repositorio:** knock-backend · **Rector:** `constitution.md`
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

**2.3 Fuera**
- El texto que ve la persona (lo arma la app desde sus ARB, frontend 024 D-02).
- Las preferencias por tipo: se aplican en la app mientras no exista `PUT /me/preferencias-avisos` (HU-051, Sprint 6; frontend 024 D-10).
- El transporte entre servicios más allá del buzón: los eventos de aviso de Mercado y de Mensajería salen por su buzón de salida (outbox, spec 003) en la misma transacción que el cambio, y este despachador los lee de ahí. Si el buzón no alcanza para esta carga (volumen, latencia), el cambio de transporte lo decide una spec posterior; es un riesgo abierto (§8.1).

## 3. Requisitos

- **R-01:** `POST /api/v1/dispositivos` exige JWT de persona y cuerpo `{ identificador, plataforma }`, donde `identificador` es el token de registro del dispositivo (la clave natural, sin `Idempotency-Key`) y `plataforma` ∈ `android`, `ios`. Es idempotente por token: responde `201` la primera vez que la persona registra ese token y `200` en cada repetición, sin duplicar el registro. Un token ya registrado por otra persona se reasigna a la persona que llega, al iniciar sesión (*decisión de revisión, confirmar*: un teléfono cambia de cuenta al iniciar sesión).
- **R-02:** `DELETE /api/v1/dispositivos/{id}` exige JWT del dueño y responde `204` (también si ya no existe). La app lo llama al cerrar sesión.
- **R-03:** El identificador se guarda cifrado en `notifications.dispositivos` y no se devuelve en ninguna respuesta ni se registra. Un token que Firebase rechace como inválido se borra del registro.
- **R-04:** La carga v1 tiene estos campos y ningún otro: `v` (siempre `1`), `tipo` (`cotizaciones`, `mensajes`, `visitas` o `sistema`), `evento` (uno de los ocho de frontend 024 §4.2), `recurso` (`ObjectId` de 24 hexadecimales; omitido en `sistema`), `aviso` (UUID del envío) y `conversacion` (solo en `mensaje_nuevo`). Nunca lleva nombres, teléfonos, direcciones, montos, fechas de la cita, códigos ni texto de mensajes (frontend 024 R-02 y §4.1).
- **R-05:** Los avisos se envían como mensajes solo de datos, con prioridad alta y sin el bloque `notification` de Firebase, para que el texto lo arme la app (frontend 024 D-02).
- **R-06:** El despachador deduplica por `evento`, `recurso` y persona destinataria: un mismo evento a la misma persona se envía una sola vez, aunque el buzón lo entregue dos veces. Los envíos se registran en `notifications.enviados` con `evento`, `tipo` y el resultado, sin texto ni identificadores de recurso en claro.
- **R-07:** `mensaje_nuevo` se agrupa por conversación y persona en ventanas de 5 minutos. El primer aviso de la ventana se envía; los mensajes que lleguen después dentro de la misma ventana no generan otro aviso, y al cerrarla se envía un único aviso más si hubo mensajes nuevos (*decisión de revisión, confirmar*: frontend 024 propone «cada pocos minutos»; el valor de 5 minutos lo confirma José).
- **R-08:** No se envía `mensaje_nuevo` a una persona con presencia activa. El hub escribe la clave `chat:presencia:{sub}` en Redis al aceptar una conexión al grupo `usuario:{sub}` (spec 030 R-15), con vencimiento de 60 segundos que el hub renueva en cada latido de SignalR (cada 15 segundos). Si la clave venció, se envía el aviso (*decisión de revisión, confirmar*).
- **R-09:** Un fallo de Firebase se reintenta con espera creciente hasta 3 veces; después queda registrado en `notifications.enviados` como fallido. Un fallo no reintenta el evento de dominio ni cambia la visita ni la solicitud.
- **R-10:** Las credenciales de Firebase se leen de la configuración segura del despliegue (almacén de secretos), nunca del repositorio ni de un registro. Esta spec no nombra valores.

## 4. Contrato API

| Método y ruta | Quién | Cuerpo | Respuesta | Errores |
|---|---|---|---|---|
| `POST /api/v1/dispositivos` | persona con sesión | `{ identificador, plataforma }` | `201` primera vez; `200` si el token ya estaba registrado para esa persona | `400`, `401`, `422`, `429` |
| `DELETE /api/v1/dispositivos/{id}` | dueño | — | `204` | `401`, `404`, `429` |

**Códigos de error:** no hay códigos de negocio propios; los errores de campo (por ejemplo, una `plataforma` fuera de la lista) salen como 422 con `errors`, el cuerpo ilegible como 400, y 401 y 429 como en la convención de la spec 002 (R-08 a R-12).

**Eventos que despacha** (frontend 024 §4.2): `solicitud_nueva`, `solicitud_sin_tecnicos`, `cotizacion_nueva`, `cotizacion_aceptada`, `solicitud_cancelada`, `solicitud_vencida`, `cotizacion_vencida`, `mensaje_nuevo`. Cada uno con su `tipo` y su `recurso` según esa tabla.

## 5. Seguridad

| Amenaza | Control | Prueba |
|---|---|---|
| Registrar el dispositivo de otra persona | Reasignación solo al iniciar sesión; respuesta sin identificador | CA-02 |
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

## 7. Pruebas

| Tipo | ¿Aplica? | Qué cubre |
|---|---|---|
| Unitarias | Sí | Reglas de R-04, R-06, R-07 (ventana de 5 minutos) y R-08 (presencia vencida) |
| Integración | Sí | MongoDB y Redis reales (contenedores); Firebase simulado (cliente HTTP de prueba); buzón de salida de Mercado y Mensajería |
| Seguridad negativa | Sí | Identificador ajeno, carga con campos extra, registros sin datos, token inválido de Firebase |
| Contrato (CON) | Sí | `openapi-diff` contra `contracts/openapi.v1.json` y ejemplos de carga v1 de frontend 024 (`app/test/fixtures/avisos/*.json`) |
| Arquitectura | Sí | El despachador no lee colecciones de Mercado ni de Mensajería; solo los eventos del buzón |
| Regresión | Sí | Suites de specs 020, 021, 026 y 030 (eventos del buzón) en verde; frontend 012 y 024 |
| Mutación | Sí (acotada) | Solo reglas de R-06, R-07 y R-08 (deduplicación, ventana y presencia) |
| Humo | Sí | En el entorno de evaluación: `POST /api/v1/dispositivos` sin token responde 401 y con token responde 201 o 200 |
| E2E | No aplica en backend | El recorrido de recibir el aviso en un teléfono de prueba lo cubre frontend 024 (E2E) |
| Carga | Sí (acotada) | Ráfaga de 1 000 eventos de aviso a 200 personas destinatarias: sin pérdidas ni duplicados |
| CI gates | Sí | Análisis, formato, pruebas, cobertura, escáneres (Semgrep, CodeQL, detección de secretos y OSV) y `oasdiff` |

## 8. Riesgos y decisiones

1. **Dependencia del transporte entre servicios.** El despachador necesita los eventos de Mercado y de Mensajería. La decisión de revisión es que salen por el buzón de salida (outbox, spec 003) de cada servicio emisor, en la misma transacción que el cambio. Riesgo abierto: si el buzón no alcanza para el volumen de avisos, hace falta una spec de transporte; sin ella, CA-03 no se puede cerrar de punta a punta. Responsable: Alexis. *Decisión de revisión, confirmar.*
2. **Mensajes solo de datos (R-05).** Garantiza que el servidor no escriba texto para la persona, a costa de que la app debe armar el aviso siempre (frontend 024 D-02). *Decisión de revisión, confirmar.*
3. **Ventana de 5 minutos para `mensaje_nuevo` (R-07).** Evita ráfagas; el valor exacto lo confirma José. *Decisión de revisión, confirmar.*
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

## Aprobación

- [ ] Revisor 1 — pendiente (José)
