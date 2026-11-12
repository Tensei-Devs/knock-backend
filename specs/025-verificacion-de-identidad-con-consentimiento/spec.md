# Spec 025 — Verificación de identidad con consentimiento

> **Estado:** Aprobada · **Repositorio:** knock-backend · **Rector:** `constitution.md`
> **Claves de trabajo (verificadas en Jira el 2026-11-09):** KNOCK-79 «HU-021 · Verificar mi identidad (opcional, consentimiento biométrico)» y KNOCK-80 «HU-022 · Ver mi estado de verificación y reintentar» (ambas `candidata-recorte`); relacionadas, fuera de esta spec: KNOCK-86 «HU-067 · Revisar verificaciones (registro de quién ve las fotos)» y KNOCK-87 «HU-068 · Borrar fotos de verificación al decidir o a los 7 días (alerta a las 24 h)»
> **Responsable sugerido:** Guillermo (reasignación propuesta en el plan del sprint; hoy KNOCK-79 tiene `resp-julian`) · Erick consume el contrato (frontend 018) · **Ola:** C · **Sprint:** 4 · **Prioridad:** P3
> **Servicio:** Identidad y confianza · **Módulo:** Trust · **Colecciones:** `verificaciones`, `consentimientos` (ambas nuevas)

## 1. Objetivo
Que un técnico pueda verificar su identidad con su INE y una selfie solo si antes dio un consentimiento biométrico explícito, que sus fotos se guarden cifradas con una llave propia de esa verificación y se borren a los 7 días si nadie las revisó, y que pueda consultar en qué va su verificación y reintentarla sin preguntar a nadie.

## 2. Historia y alcance
**2.1 Historias**
- **HU-021** — Como técnico, quiero verificar mi identidad con mi INE y una selfie, para mostrar la insignia de verificado y que los clientes confíen en mí.
- **HU-022** — Como técnico, quiero ver en qué va mi verificación y reintentar si no pasó, para saber qué sigue sin preguntar a nadie.

**2.2 Dentro**
- Consentimiento biométrico separado de los términos, con su texto, versión, fecha y dispositivo.
- Creación de la verificación con nivel básico o completo y enlaces de subida de 5 minutos.
- Confirmación de cada foto: validación por contenido, cifrado con la llave de esa verificación y borrado de la cuarentena.
- Envío a revisión con el estado `en_revision` y el evento correspondiente.
- Consulta del estado propio, con motivo y consejos cuando se rechazó, y con la opción de subir de nivel.
- Borrado de las fotos y la llave a los 7 días sin decisión, y vuelta a `sin_verificar` para poder reenviar.
- Detección de una CURP ya usada por otra cuenta, sin revelar de quién es.

**2.3 Fuera**
- La revisión y la decisión del verificador: son de HU-067 (panel). Esta spec deja el modelo de datos y los estados, no la pantalla ni el endpoint del panel.
- El borrado por baja de cuenta y el registro de accesos a las fotos: son de HU-068 y HU-067.
- La captura, la revisión de calidad en el dispositivo y las pantallas: son de knock-frontend (frontend 018).
- Un proveedor automático de KYC: no se usa en esta etapa.
- La insignia pública y el nivel de verificación que muestran las cotizaciones: los consume Mercado desde `tecnicos`; esta spec solo publica el evento de la decisión.
- El aviso por push del cambio de estado: lo envía Mensajería al consumir los eventos.
- Las fotos de perfil, de solicitudes y de trabajos: son de la spec 013.

## 3. Requisitos
- **R-01:** `POST /api/v1/me/verificacion` exige JWT y cuerpo `{ nivel, aceptaBiometria, versionAviso, curp }`. La persona es siempre la del `sub`; la ruta no lleva identificador.
- **R-02:** `aceptaBiometria` debe ser `true`; si falta o es falso, responde 422 `consent_required` y no se crea nada. El consentimiento no puede venir premarcado desde el servidor: no hay valor por omisión.
- **R-03:** `nivel` es `basica` (INE frente y selfie) o `completa` (además INE reverso). Cualquier otro valor responde 422 con `errors.nivel` (convención de errores de la spec 020, §5).
- **R-04:** `curp` es obligatoria, de 18 caracteres con el patrón oficial (cuatro letras, seis dígitos de fecha, `H` o `M`, dos letras de entidad, tres consonantes, un homoclave alfanumérico y un dígito). Se valida el formato antes de usarla; una CURP mal formada responde 422 `curp_invalid` sin repetir el valor.
- **R-05:** La CURP no se guarda en claro ni se registra: se calcula `curpHmac = HMAC-SHA256(curpNormalizada, llaveDeKeyVault)` y solo se guarda el HMAC. La CURP en claro se descarta de la memoria en cuanto se calcula el HMAC.
- **R-06:** Si el HMAC de la CURP coincide con el de otra cuenta cuya verificación está `en_revision` o `verificada`, responde 409 `curp_duplicate` con un mensaje genérico que invita a contactar a soporte y no revela nada de la otra cuenta. Se emite `CurpDuplicada` para soporte, sin datos personales.
- **R-07:** Si la persona ya tiene una verificación `en_revision`, responde 409 `verification_under_review`. Si tiene una `en_captura`, se cancela y se crea la nueva (una sola captura viva por persona). Una verificación `verificada` no se puede repetir con el mismo nivel: pedir `basica` con `nivelOtorgado = basica` responde 409 `already_verified`; subir de nivel se hace con una nueva verificación `completa`, y pedir cualquier nivel con `nivelOtorgado = completa` responde 409 `already_fully_verified`.
- **R-08:** La creación responde 201 con `{ id, estado: "en_captura", nivel, fotos: [{ tipo, urlSubida, venceEn }], capturaVenceEn }`. Los enlaces de subida son SAS de escritura de 5 minutos, de un solo blob, sobre el contenedor privado de cuarentena de verificación; el servidor no recibe el archivo en esta llamada.
- **R-09:** Cada verificación nace con su consentimiento en `consentimientos` (`{ usuarioId, tipo: "biometria", texto, version, aceptadoEn, dispositivoId, verificacionId }`). El `dispositivoId` sale de la sesión (el `deviceId` guardado en `sesiones` para el `sid` del token, spec 015), no del cuerpo; el `texto` es el del aviso de privacidad vigente y su `version` debe coincidir con la configurada (`Verificacion:AvisoVersion`).
- **R-10:** `POST /api/v1/me/verificacion/{id}/fotos/{tipo}/confirmar` exige JWT del dueño y `tipo` igual a uno de `ine_frente`, `ine_reverso` o `selfie`, los de su nivel (`basica`: `ine_frente` y `selfie`; `completa`: también `ine_reverso`). Descarga el blob de cuarentena, valida por contenido real que es JPG o PNG y que mide 8 MB o menos, cifra el contenido con AES-256-GCM usando la llave de la verificación, guarda el resultado en el contenedor privado definitivo, borra el blob de cuarentena y marca la foto `cifrada`.
- **R-11:** La llave de la verificación es una llave de datos de 256 bits generada al crearla, envuelta con la llave maestra de Key Vault; en `verificaciones` solo se guarda la llave envuelta y la versión de la llave maestra. Cada blob lleva su nonce de 12 bytes y su etiqueta de autenticación de 16 bytes en el envoltorio del archivo, nunca en la base.
- **R-12:** Si el blob no existe, ya se confirmó o la captura venció, responde 409 (`photo_not_uploaded`, `photo_already_confirmed`, `capture_expired`). Si el contenido no es JPG/PNG o mide más de 8 MB, responde 422 `content_invalid` y borra el blob de cuarentena.
- **R-13:** `POST /api/v1/me/verificacion/{id}/enviar` exige que todas las fotos del nivel estén `cifradas`; si falta alguna, 409 `photos_missing`. Cambia el estado a `en_revision`, guarda `enviadoEn`, calcula `expiraEn = enviadoEn + 7 días` y emite `VerificacionEnviada` en el buzón.
- **R-14:** `GET /api/v1/me/verificacion` devuelve solo la verificación más reciente de la persona: `{ id, nivel, estado, motivo?, consejos[], fotos: [{ tipo, estado }], creadoEn, enviadoEn?, decididoEn?, expiraEn?, puedeReintentar }`. Si nunca hubo, responde 200 `{ estado: "sin_verificar", puedeReintentar: true }`. Nunca devuelve las fotos, sus direcciones, la CURP ni su HMAC.
- **R-15:** Un estado `no_verificada` trae `motivo` (texto del catálogo de motivos) y `consejos[]` (uno a tres); una decisión `verificada` trae el `nivelOtorgado` (`basica` o `completa`). El motivo solo lo ve el dueño; la insignia pública de Mercado nunca lo incluye.
- **R-16:** Al cumplirse `expiraEn` sin decisión, una tarea programada borra los blobs, borra la llave envuelta del documento, marca las fotos `borrada` y deja la verificación en `sin_verificar` con `motivo: fotos_borradas` y `puedeReintentar: true`. La CURP (su HMAC) se conserva para la detección de duplicados.
- **R-17:** Las fotos y la llave se borran también cuando la decisión es `no_verificada` (a los 7 días o al decidir, lo que ocurra primero) y cuando la persona cancela; el documento conserva el resultado para el historial. El borrado es idempotente: repetirlo no falla.
- **R-18:** Las respuestas llevan `Cache-Control: private, no-store`; no se cachea nada de esta funcionalidad. Los registros no incluyen la CURP, el HMAC, las direcciones de las fotos ni el contenido; los errores usan ProblemDetails con `correlationId` y `traceId`.
- **R-19:** Sin token, 401; con la verificación de otra persona, 404 uniforme (no se distingue «no existe» de «es ajena»); identificador mal formado, 400. Límites: 3 creaciones por hora y 10 confirmaciones por hora por persona, además del límite global y el de usuario del gateway.
- **R-20:** Todo el flujo exige que la cuenta no esté en eliminación programada (409 `deletion_scheduled`, como en `PUT /me`). Un cuerpo mayor a 8 KB responde 413.

## 4. Criterios de aceptación
- **CA-01:** Dado un técnico sin consentimiento, cuando crea la verificación, entonces responde 422 `consent_required` y no hay documento de verificación.
- **CA-02:** Dado un consentimiento aceptado, cuando crea la verificación, entonces hay un documento en `consentimientos` con el texto, la versión, la fecha, el dispositivo de la sesión y el identificador de la verificación.
- **CA-03:** Dada una creación válida de nivel básico, cuando responde, entonces trae dos enlaces de subida (INE frente y selfie) de 5 minutos, uno por tipo, y el estado es `en_captura`.
- **CA-04:** Dada una creación de nivel completo, cuando responde, entonces trae tres enlaces; una de nivel básico con un `tipo` de nivel completo en la confirmación responde 409 `type_invalid` (el `tipo` es válido, pero no corresponde al nivel de esa verificación); un `nivel` desconocido al crear responde 422 con `errors.nivel`.
- **CA-05:** Dada una CURP mal formada, cuando se envía, entonces responde 422 `curp_invalid`, no repite el valor y no guarda nada.
- **CA-06:** Dada una CURP válida, cuando se guarda, entonces el documento tiene un HMAC y ningún campo contiene la CURP en claro.
- **CA-07:** Dada una CURP que ya tiene otra cuenta en revisión o verificada, cuando se envía, entonces responde 409 `curp_duplicate`, el mensaje no menciona a la otra cuenta y hay un `CurpDuplicada` en el buzón.
- **CA-08:** Dada una verificación ya `en_revision`, cuando se intenta crear otra, entonces responde 409 `verification_under_review`.
- **CA-09:** Dado un blob de cuarentena con un JPG, cuando se confirma, entonces el blob definitivo está cifrado, el de cuarentena ya no existe y la foto queda `cifrada`.
- **CA-10:** Dado un blob que es un PDF renombrado o mide más de 8 MB, cuando se confirma, entonces responde 422 `content_invalid` y no queda ningún blob.
- **CA-11:** Dado un enlace de subida vencido, cuando se intenta confirmar, entonces responde 409 (`photo_not_uploaded` o `capture_expired`) y la app puede pedir uno nuevo sin dejar una verificación a medias.
- **CA-12:** Dadas todas las fotos cifradas, cuando se envía, entonces el estado es `en_revision`, `expiraEn` es 7 días después y hay un `VerificacionEnviada` en el buzón.
- **CA-13:** Dada una verificación con una foto sin confirmar, cuando se envía, entonces responde 409 `photos_missing` y sigue en `en_captura`.
- **CA-14:** Dado un técnico `en_revision`, cuando consulta su estado, entonces ve el nivel, las fotos por tipo y estado, y `expiraEn`, sin direcciones ni CURP.
- **CA-15:** Dado un rechazo, cuando consulta su estado, entonces ve `no_verificada` con el motivo y los consejos, y `puedeReintentar: true`.
- **CA-16:** Dados 7 días en `en_revision` sin decisión, cuando corre la tarea de vencimiento, entonces los blobs y la llave se borraron, el estado es `sin_verificar` con `motivo: fotos_borradas` y puede reenviar.
- **CA-17:** Dada la verificación de otra persona, cuando se consulta o se confirma una foto, entonces responde 404; sin token, 401.
- **CA-18:** Dado el registro de una creación, confirmación o consulta, cuando se inspecciona, entonces no contiene CURP, HMAC, direcciones de fotos ni contenido.
- **CA-19:** Dada una persona con `nivelOtorgado = basica`, cuando pide otra verificación `basica`, entonces responde 409 `already_verified`; con `nivelOtorgado = completa`, cualquier nivel responde 409 `already_fully_verified`.
- **CA-20:** Dadas dos creaciones simultáneas con la misma CURP desde dos cuentas, cuando ambas llegan a `en_revision`, entonces solo una lo logra (índice único sobre `curpVigente`) y la otra responde 409 `curp_duplicate`.
- **CA-21:** Dada una cuenta en eliminación programada, cuando crea, confirma o envía, entonces responde 409 `deletion_scheduled` y no cambia nada; dado un cuerpo de más de 8 KB, responde 413.

**4.1 Criterio → prueba prevista**

| Criterio | Prueba prevista |
|---|---|
| CA-01 | `Spec025ConsentimientoTests.Sin_Consentimiento_CA_025_01` |
| CA-02 | `Spec025ConsentimientoTests.Registra_Consentimiento_CA_025_02` |
| CA-03 | `Spec025CreacionTests.Enlaces_De_Subida_Nivel_Basico_CA_025_03` |
| CA-04 | `Spec025CreacionTests.Nivel_Completo_Y_Tipo_Ajeno_CA_025_04` |
| CA-05 | `Spec025CurpTests.Curp_Mal_Formada_CA_025_05` |
| CA-06 | `Spec025CurpTests.Curp_Solo_Como_Hmac_CA_025_06` |
| CA-07 | `Spec025CurpTests.Curp_Duplicada_CA_025_07` |
| CA-08 | `Spec025CreacionTests.Rechaza_Segunda_En_Revision_CA_025_08` |
| CA-09 | `Spec025FotosTests.Confirma_Y_Cifra_CA_025_09` |
| CA-10 | `Spec025FotosTests.Rechaza_Contenido_No_Valido_CA_025_10` |
| CA-11 | `Spec025FotosTests.Enlace_Vencido_CA_025_11` |
| CA-12 | `Spec025EnvioTests.Envia_A_Revision_CA_025_12` |
| CA-13 | `Spec025EnvioTests.Faltan_Fotos_CA_025_13` |
| CA-14 | `Spec025EstadoTests.Estado_En_Revision_CA_025_14` |
| CA-15 | `Spec025EstadoTests.Estado_No_Verificada_CA_025_15` |
| CA-16 | `Spec025VencimientoTests.Borra_A_Los_Siete_Dias_CA_025_16` |
| CA-17 | `Spec025SeguridadTests.Verificacion_Ajena_CA_025_17` |
| CA-18 | `Spec025RegistrosTests.Sin_Datos_Sensibles_CA_025_18` |
| CA-19 | `Spec025CreacionTests.Ya_Verificado_CA_025_19` |
| CA-20 | `Spec025CurpTests.Curp_Simultanea_CA_025_20` |
| CA-21 | `Spec025SeguridadTests.Eliminacion_Programada_Y_Cuerpo_CA_025_21` |

## 5. Contrato
| Método y ruta | Quién | Cuerpo | Respuestas |
|---|---|---|---|
| `POST /api/v1/me/verificacion` | persona | `{ nivel, aceptaBiometria, versionAviso, curp }` | 201 `{ id, estado, nivel, fotos[], capturaVenceEn }`, 400, 401, 409, 413, 422, 429 |
| `POST /api/v1/me/verificacion/{id}/fotos/{tipo}/confirmar` | dueño | vacío | 200 `{ tipo, estado }`, 400, 401, 404, 409, 422, 429 |
| `POST /api/v1/me/verificacion/{id}/enviar` | dueño | vacío | 200 `{ id, estado, enviadoEn, expiraEn }`, 401, 404, 409, 429 |
| `GET /api/v1/me/verificacion` | persona | — | 200 (estado), 401, 429 |

**Cuerpos de respuesta**
- Creación: `{ id, estado: "en_captura", nivel, fotos: [{ tipo, urlSubida, venceEn }], capturaVenceEn }`.
- Estado: `{ id?, nivel?, estado, motivo?, consejos[], nivelOtorgado?, fotos: [{ tipo, estado }], creadoEn?, enviadoEn?, decididoEn?, expiraEn?, puedeReintentar }`.
- Estados: `sin_verificar`, `en_captura`, `en_revision`, `verificada`, `no_verificada`.
- Estados de foto: `pendiente`, `cifrada`, `borrada`.

**Códigos de error:** `consent_required` (422), `curp_invalid` (422), `curp_duplicate` (409), `verification_under_review` (409), `already_verified` (409), `already_fully_verified` (409), `type_invalid` (409), `photo_not_uploaded`, `photo_already_confirmed`, `capture_expired`, `content_invalid`, `photos_missing`, `deletion_scheduled`.

**Límites:** 8 MB y 8 KB de cuerpo; SAS de escritura de 5 minutos; 3 creaciones y 10 confirmaciones por hora por persona; 7 días de vida de las fotos; CURP de 18 caracteres; `consejos` de 1 a 3.

## 6. Modelo de datos
**`verificaciones`** (base `identity`)
| Campo | Tipo | Nota |
|---|---|---|
| `_id` | ObjectId | |
| `usuarioId` | string | `sub` del JWT |
| `nivel` | string | `basica`, `completa` |
| `estado` | string | `en_captura`, `en_revision`, `verificada`, `no_verificada`, `sin_verificar` |
| `fotos` | array | `{ tipo, estado, blob, confirmadoEn?, borradoEn? }`; `blob` es la ruta interna, nunca pública |
| `llaveEnvuelta`, `llaveKekVersion` | string | llave de datos de la verificación; se borra al vencer o al decidir |
| `curpHmac` | string | HMAC-SHA256 con llave de Key Vault; nunca la CURP |
| `curpVigente` | bool | verdadero solo en `en_revision` o `verificada`; base del índice único parcial |
| `motivo`, `consejos` | string, string[] | rechazo o borrado |
| `nivelOtorgado` | string? | lo escribe la decisión (HU-067) |
| `decididoPor`, `decididoEn` | string?, date? | lo escribe la decisión (HU-067) |
| `creadoEn`, `enviadoEn?`, `expiraEn?` | date | UTC |
| `capturaVenceEn` | date | `creadoEn + 24 h` |

Índices: `{ usuarioId: 1, creadoEn: -1 }`; `{ estado: 1, expiraEn: 1 }` para la tarea de vencimiento; `{ curpHmac: 1 }` único y parcial con `curpVigente: true`. `curpVigente` es un booleano que vale verdadero solo mientras el estado es `en_revision` o `verificada` y se cambia en la misma escritura que el estado (un filtro parcial con `$in` sobre `estado` no es portable a DocumentDB).

**`consentimientos`** (base `identity`)
| Campo | Tipo | Nota |
|---|---|---|
| `_id` | ObjectId | |
| `usuarioId` | string | `sub` |
| `tipo` | string | `biometria` |
| `texto`, `version` | string | aviso aceptado |
| `aceptadoEn` | date | UTC |
| `dispositivoId` | string | de `sesiones`, no del cuerpo |
| `verificacionId` | ObjectId | una constancia por verificación |

Índices: `{ usuarioId: 1, aceptadoEn: -1 }` y `{ verificacionId: 1 }` único.

**Invariantes:** la CURP no existe en claro en ninguna colección; sin llave envuelta no se puede leer una foto; una persona tiene una sola verificación viva; el HMAC es único entre verificaciones vigentes.

El script `db/` de esta spec crea las colecciones y los índices de forma idempotente.

## 7. Arquitectura y componentes
- Módulo nuevo `src/Knock.Identity.Api/Modules/Trust/` con `Controllers`, `Dtos`, `Validators`, `Services`, `Domain`, `Data`, `Events`; expone `AddTrustModule()`.
- `VerificacionesController` (crear y consultar), `FotosVerificacionController` (confirmar) y `EnvioVerificacionController` (enviar), separados por responsabilidad (S6960).
- `VerificacionService` orquesta consentimiento, CURP, enlaces, fotos, envío y vencimiento; no toca Blob ni MongoDB directamente.
- `VerificacionStore` es el único punto con MongoDB del módulo; `FotoVerificacionStore` el único con Blob (cuarentena y definitivo) y Key Vault.
- `CifradoDeVerificacion` encapsula AES-256-GCM con la llave de la verificación y el envoltorio del blob; `CurpHmac` encapsula el HMAC y nunca devuelve el valor original.
- `VencimientoVerificaciones` es un servicio en segundo plano que corre cada hora, toma las verificaciones con `expiraEn` vencido y borra de forma idempotente; usa `tareas_programadas` de la spec 003 para no repetir el borrado entre réplicas.
- La decisión (`verificada` / `no_verificada`) la escribe `VerificacionDecisionService`, que esta spec define y deja sin endpoint; HU-067 lo expone.
- Reutiliza `SesionesStore` para leer el `dispositivoId`, el cifrado de campo y las llaves de Key Vault de Identity, y el buzón `outbox`.
- Sin caché: los estados cambian con cada paso y las fotos no se cachean nunca.

## 8. Dependencias
- Spec 002 (estructura), 003 (MongoDB, Redis, outbox, tareas programadas), 009 (JWT y límites), 013 (reglas de archivos y moderación; esta spec usa el contenedor privado de verificación, que 013 deja fuera de su alcance), 015 y 016 (sesión y `dispositivoId`), 017 (perfil y eliminación programada).
- Key Vault: llave maestra de verificación y llave del HMAC de la CURP; contenedores privados `verificaciones-cuarentena` y `verificaciones` de la cuenta de almacenamiento de verificación (ADR-007: cuenta separada de la pública).
- Los eventos `VerificacionEnviada`, `VerificacionDecidida` y `CurpDuplicada` los consumen Mensajería (aviso) y Mercado (nivel de `tecnicos`), fuera de esta spec.
- La decisión depende de HU-067; sin el panel, en este sprint nada pasa a `verificada` salvo por semilla o decisión manual.
- La app (frontend 018) consume el contrato.

## 9. Amenazas y controles
| Amenaza | Control | Caso que lo prueba |
|---|---|---|
| Usar datos biométricos sin consentimiento | Consentimiento propio, sin valor por omisión y guardado con texto, versión, fecha y dispositivo | CA-01, CA-02 |
| Fuga de la CURP | HMAC con llave de Key Vault; la CURP no se guarda ni se registra | CA-05, CA-06, CA-18 |
| Suplantar la identidad de otra persona | CURP duplicada detectada sin revelar la otra cuenta, y evento para soporte | CA-07 |
| Fuga de las fotos de INE y selfie | Contenedor privado, SAS de 5 minutos, cifrado con llave por verificación y sin URL pública | CA-09, CA-11 |
| Conservar las fotos más de lo necesario | Borrado a los 7 días o al decidir, blob y llave | CA-16 |
| Subir archivos maliciosos disfrazados | Validación por contenido real, solo JPG/PNG, 8 MB y recodificación al cifrar | CA-10 |
| Leer o confirmar fotos de otra persona | Todo por `sub`; 404 uniforme | CA-17 |
| Repetir la verificación para saltarse un rechazo | Una sola verificación viva y una sola `en_revision` | CA-08 |
| Enumerar verificaciones ajenas | 404 después de autenticar, sin distinguir inexistente de ajena | CA-17 |
| Fuga en registros | Sin CURP, HMAC, direcciones ni contenido | CA-18 |
| Cuenta en eliminación programada | 409 antes de crear nada | CA-21 |
| Usar `curp_duplicate` como oráculo para saber si una CURP está registrada | Límite de 3 creaciones por hora, mensaje genérico, evento a soporte y la CURP solo se compara contra verificaciones vigentes | CA-07, CA-20 |
| Carrera entre dos cuentas con la misma CURP | Índice único parcial sobre `curpVigente` | CA-20 |

## 10. Plan de pruebas
| Tipo | ¿Aplica? | Motivo |
|---|---|---|
| Unitarias | Sí | Validación de CURP, HMAC, estados, motivos, consejos y reglas de nivel |
| Integración | Sí | MongoDB, Redis, Azurite y Key Vault simulados con `Testcontainers`/doble: creación, confirmación, cifrado, envío y borrado |
| Seguridad negativa | Sí | Verificación ajena, sin consentimiento, CURP inválida, contenido no válido y límites |
| Contrato | Sí | OpenAPI de Identity regenerado y comparado |
| Arquitectura | Sí | El controller no accede a Blob, Key Vault ni MongoDB |
| Mutación | Sí | Regla de nivel, condición de vencimiento y detección de CURP duplicada |
| Rendimiento | No | El volumen del piloto y el tamaño de las fotos no lo exigen |
| Aceptación | Sí | CA-01 a CA-21 con la tabla 4.1 |
| Base de datos | Sí | `DatabaseTest`: `verificaciones` y `consentimientos` con sus índices, incluido el único parcial por `curpVigente`, de forma idempotente |
| Regresión | Sí | Las suites de Identity (specs 015 a 018) siguen en verde; `GET /me` y las sesiones no cambian |
| CI/CD | Sí | Job `gate` de `ci-pr.yml` (build-test, openapi-diff de Identity, sast, sca, secrets) en verde; smoke de `/health/ready` tras el despliegue |
| Análisis estático | Sí | Semgrep (job `sast`) sin hallazgos nuevos, con regla que prohíbe registrar `curp`; complejidad máxima 10 |
| Vulnerabilidades y licencias | Sí | NuGetAudit, `dotnet list package --vulnerable`, OSV-Scanner y dependency-review sin altas ni críticas; los paquetes de Blob y Key Vault se evalúan si son nuevos en Identity |
| DAST | Sí (nocturno) | ZAP de API con token de prueba contra las cuatro rutas; no bloquea el PR |

**Gates:** los de `AGENTS.md`; OpenAPI regenerado con `python scripts/export_openapi.py --service identity`; la prueba de integración comprueba que el blob definitivo no contiene la firma JPG en claro (está cifrado) y que la cuarentena quedó vacía.

## 11. Escenarios
**Positivos**
- Un técnico acepta el aviso, sube INE frente y selfie, confirma las dos fotos y envía a revisión.
- Sube de nivel: verificado básico, crea una verificación completa con el reverso.
- A los 7 días sin revisión, recibe el aviso de que sus fotos se borraron y puede reenviar.
- Un rechazo le muestra el motivo y los consejos, y puede intentarlo de nuevo.

**Negativos**
- Intenta tomar fotos sin aceptar el consentimiento.
- Manda un PDF renombrado como JPG.
- Usa el enlace de subida después de 5 minutos.
- Intenta ver la verificación de otra persona.
- Envía una CURP que ya está en revisión en otra cuenta.

**Errores esperados**
- 400 por identificador mal formado o `tipo` desconocido de la ruta, 422 por `nivel`, 401 sin sesión, 404 por verificación ajena, 409 por estado, duplicado o `tipo` que no corresponde al nivel, 413 por cuerpo, 422 por consentimiento, CURP o contenido, 429 por límite.

**Casos límite**
- CURP en minúsculas y con espacios alrededor.
- Foto de exactamente 8 MB y de 8 MB más un byte.
- Verificación que vence mientras se confirma la última foto.
- Confirmación repetida de la misma foto.
- Decisión que llega un minuto después del vencimiento (gana quien escriba primero; el borrado es idempotente).
- Reenvío inmediato después del borrado.

## 12. Definición de terminado
- Los 21 criterios tienen prueba en verde y la tabla 4.1 coincide con los nombres reales (`python scripts/sdd_lint.py --trace 025` sin faltantes).
- Job `gate` en verde, Semgrep y análisis de dependencias sin hallazgos altos ni críticos.
- La prueba de integración demuestra que la CURP no está en claro, que el blob está cifrado y que la cuarentena queda vacía.
- `db/` crea `verificaciones` y `consentimientos` con sus índices, incluido el único parcial del HMAC, de forma idempotente.
- OpenAPI de Identity regenerado, `docs/endpoints.md` actualizado y `Memoria.md` con la entrada.
- Se documenta en el PR que la decisión del verificador (HU-067) queda fuera y que sin ella nada llega a `verificada`.
- Revisión de par y sin hallazgos de seguridad abiertos.

## 13. Decisiones
- **La verificación vive en Identidad, no en Mercado:** es un dato de confianza de la persona y comparte llaves y sesiones con `Me`; Mercado solo consume el nivel por evento. Por eso la ruta es `/me/verificacion` y no `/tecnicos/me/verificacion` como decía la ficha de HU-022.
- **Consentimiento por verificación:** un consentimiento viejo no cubre fotos nuevas; guardar uno por envío deja la constancia exacta de qué aceptó y cuándo.
- **Llave por verificación, envuelta por Key Vault:** una llave por persona y trámite limita el daño de una fuga; la maestra nunca sale de Key Vault.
- **Borrado a los 7 días o al decidir:** las fotos de identificación no se conservan más de lo necesario; el resultado queda, la imagen no.
- **La CURP se guarda como HMAC:** sirve para detectar duplicados y no se puede revertir; no se necesita el valor para nada más.
- **El servidor cifra después de la subida directa:** se conserva el patrón aprobado de enlace firmado y cuarentena (spec 013 y TT-API-05) y el cifrado con llave propia ocurre en el servidor, que es el único que puede cifrar con una llave que el teléfono no debe tener.
- **Sin endpoint de decisión en esta spec:** el panel de HU-067 tiene sus propias reglas, permisos y auditoría; adelantarlo aquí sería una superficie de administración sin diseño.
- **Sin caché:** el estado cambia con cada paso y una foto nunca se cachea.

## 14. Decisiones tomadas sin consulta
Se tomaron de noche, con el criterio de la constitución; quien apruebe puede cambiarlas antes de firmar.

1. **La CURP la envía la app, no se lee en el servidor.** La ficha de HU-021 supone una lectura en el dispositivo (CA7 habla de «la CURP leída»). El servidor no hace OCR; recibe la CURP validada por formato y la convierte en HMAC. Alternativa: leer la CURP de la foto con un servicio de OCR, que agrega un proveedor y un costo que la historia no pide.
2. **Una sola captura viva por persona.** Crear una nueva cancela la anterior en lugar de acumular borradores y blobs. Alternativa: permitir varias y limpiar por vencimiento, con más basura y más riesgo.
3. **La confirmación de fotos es una ruta por tipo** (`/fotos/{tipo}/confirmar`) en vez de una sola que reciba el identificador del archivo: el tipo es parte del contrato con la app y evita que se confirme una foto en el lugar de otra.
4. **El `dispositivoId` se toma de la sesión.** Es el único valor confiable; el cuerpo podría mentir. Alternativa: pedirlo en el cuerpo, como hace el registro de la spec 015 donde todavía no hay sesión.
5. **`capturaVenceEn` de 24 h.** Una captura a medias no puede quedar abierta para siempre porque el blob de cuarentena ocupa espacio y la llave existe sin uso. Alternativa: 7 días como las fotos enviadas, que deja basura de personas que abandonaron el flujo.
6. **El estado de una cuenta sin trámite es `sin_verificar`, no un 404.** La pantalla necesita distinguir «no has empezado» de «no existe la ruta», y el 404 se reserva para lo ajeno.
7. **Se conserva el HMAC al vencer.** Sirve para que la misma CURP no se use en dos cuentas después de un borrado; el HMAC no permite recuperar el número.
8. **El evento de la decisión se emite desde el servicio de decisión, no desde el endpoint del panel.** Así la regla de «un evento por decisión» vive con la transición y no con la pantalla que la provoca.
9. **Los motivos y consejos son un catálogo de textos del servidor.** La app no inventa motivos y el panel (HU-067) elige de una lista; así no se filtran datos de otras cuentas en un texto libre.

**Agregadas en la revisión del 9 de noviembre**

10. **Las fotos de verificación no pasan por la API de moderación.** La constitución (§2.13) pide moderar toda imagen antes de guardarla, pero también prohíbe que la IA reciba datos personales sensibles (§2.14); una INE y una selfie son datos biométricos, nunca se publican y solo las ve el verificador. Se privilegia §2.14 y se anota como conflicto para que el equipo lo resuelva. Alternativa: moderarlas con Azure AI Content Safety, que envía biometría a un servicio de IA.
11. **Cualquier persona con sesión puede verificarse, no solo quien ya es técnico.** La verificación es opcional (KNOCK-79) y puede ser previa a ofrecer servicios. Alternativa: exigir `esTecnico`, que bloquea el alta de técnicos verificados desde el inicio.
12. **`curpVigente` en lugar de un índice parcial por estado.** Los filtros parciales con `$in` no son portables a DocumentDB; un booleano mantenido en la misma escritura da la misma garantía.
13. **El consentimiento biométrico vive en una colección propia, no en el arreglo `consentimientos` de `usuarios` (spec 015).** Se guarda uno por verificación con su texto exacto; meterlo en `usuarios` haría crecer el documento del perfil, que se cachea. Alternativa: el arreglo de `usuarios`, coherente con la spec 015 pero menos trazable.
14. **Errores de formato en 422 y de ruta en 400**, como el resto de las specs 020 a 026.
15. **Etiquetas de Jira:** KNOCK-79 y KNOCK-80 tienen `servicio-mercado` y `modulo-catalog`, pero esta spec ubica la verificación en Identidad (módulo `Trust`, como KNOCK-86 y 87); conviene corregir las etiquetas.

## 15. Riesgos y dudas abiertas
- **Sin HU-067 nada se aprueba:** el flujo completo termina en `en_revision` y a los 7 días se borra. Para la demo hay que decidir con semilla o con una decisión manual; es la brecha más importante de esta spec.
- **El estado `verificada` y la insignia pública no se propagan solos:** el evento `VerificacionDecidida` existe, pero Mercado todavía no lo consume para actualizar `tecnicos.nivelVerificacion`. Mientras tanto, la insignia de las cotizaciones sale de las semillas.
- **El cifrado se prueba con Key Vault simulado:** la llave real y su versión solo se ejercitan en Azure; la prueba de integración usa un doble que envuelve y desenvuelve igual.
- **La recodificación de la foto puede degradar la legibilidad de la INE:** se recodifica para quitar metadatos; el panel de revisión debe confirmar que se lee. Si no, se ajusta la calidad en un solo lugar.
- **La cuarentena puede acumular blobs de capturas abandonadas:** el vencimiento de 24 h los marca, pero el borrado de blobs se hace en la misma tarea horaria; una captura muy reciente puede vivir hasta una hora de más.
- **La CURP es un dato de alto valor aunque se guarde como HMAC:** con la llave de Key Vault se pueden probar CURP candidatas. La llave debe rotar y el acceso a Key Vault es de solo el servicio.
- **El límite de 3 creaciones por hora puede molestar a quien se equivoca tres veces:** se puede subir sin cambiar el código; el valor está en configuración.

## Aprobación
- [x] Revisor 1 — nombre: José Julián Hernández Hernández fecha: 2026-11-11
- [x] Sin conflicto con la constitución
- **Estados:** Borrador (agente) → Aprobada (persona; habilita el código) → Implementada (tras el merge)
