# Eventos de Knock

## CotizacionEnviada (spec 020)

Mercado inserta este evento en `outbox` dentro de la transacción que guarda la cotización. El documento contiene `_id` (igual al identificador de la cotización), `tipo`, `estado`, `creadoEn`, `solicitudId` y `cotizacionId`.

El despachador y la entrega del aviso al cliente aún no están implementados. El contrato de consumo debe acordarse antes de conectarlos; insertar el documento no demuestra la entrega.

## Eventos de Mercado en el buzón de salida (specs 020, 021 y 022)

Mercado escribe cada evento en `outbox` en la misma transacción que el cambio de negocio, con `_id` determinista `{tipo}:{agregadoId}` (un reintento no lo duplica), `estado = pendiente` y `datos` con solo identificadores y datos no personales: nunca descripción, dirección, código, nombre ni teléfono. El despachador y los consumidores son de la spec 037; insertar el documento no demuestra la entrega.

| Evento | Spec | Cuándo | `datos` |
|---|---|---|---|
| `SolicitudCreada` | 020 | Al crear la solicitud | `solicitudId`, `oficioClave`, `coloniaId`, `venceEn` |
| `CotizacionAceptada` | 021 | Al elegir, para la cotización elegida | `cotizacionId`, `solicitudId`, `tecnicoUsuarioId` |
| `CotizacionRechazada` | 021 | Al elegir, una por cada cotización no elegida | `cotizacionId`, `solicitudId`, `tecnicoUsuarioId`, `motivo` (`other_quote_chosen`) |
| `VisitaConfirmada` | 021 | Al elegir, para la visita creada | `visitaId`, `solicitudId`, `cotizacionId`, `clienteId`, `tecnicoUsuarioId`, `modalidad`, `dia`, `hora` |
| `VisitaEnCurso` | 022 | Al confirmar la llegada con el código correcto | `visitaId`, `solicitudId`, `clienteId`, `tecnicoUsuarioId` |
| `VisitaBloqueada` | 022 | Al tercer intento fallido (Identidad abre un reporte de gravedad alta) | `visitaId`, `solicitudId`, `clienteId`, `tecnicoUsuarioId` |
| `VisitaCerrada` | 022 | Al pasar la visita a `cancelada`, `terminada`, `no_presentado` o bloqueada | `visitaId`, `motivo` (`cancelada`, `terminada`, `no_presentado`, `bloqueada`) |
| `TecnicoNoSePresento` | 064 | Cuando el cliente dueño reporta que el técnico no llegó (visita `confirmada`, de 15 minutos a 2 horas después de la cita) y la visita pasa a `no_presentado` con `quienFalto = tecnico`; una sola vez por visita (`_id` `TecnicoNoSePresento:{visitaId}`). Entrega: `POST /api/v1/interno/avisos` de Mensajería (permiso `tecnico_no_se_presento`, `rol` `tecnico`, `recurso` = `visitaId`): aviso de texto fijo al técnico («El cliente reportó que no llegaste a tu visita. Si crees que es un error, contacta a soporte.»), sin nombre ni datos del cliente ni de la visita, sin penalización; idempotente por `eventoId`. | `visitaId`, `solicitudId`, `clienteId`, `tecnicoUsuarioId` |
| `AvisoConfianzaPreparado` | 022 | Cada vez que el cliente prepara el aviso (`_id` `{visitaId}:{n}`) | `visitaId`, `solicitudId`, `clienteId`, `tecnicoUsuarioId` |
| `SolicitudEnviadaATecnico` | 028 | Por cada envío de una oleada | `solicitudId`, `tecnicoId`, `oleada` |
| `SolicitudSinTecnicos` | 028 | Al completar la última oleada sin envíos anteriores | `solicitudId` |
| `SolicitudCancelada` | 029 | Al cancelar la solicitud el dueño (una sola vez; la repetición no escribe otro) | `solicitudId`, `tecnicosAvisar` (técnicos con cotización `enviada`) |
| `SolicitudVencida` | 029 | Al vencer una solicitud abierta, por la tarea o por el barrido (una sola vez) | `solicitudId`, `clienteId`, `tecnicosAvisar` |

## Tareas programadas de reparto (spec 028)

Estas tareas viven en `tareas_programadas` de Mercado; no son eventos ni entregan avisos:

| Tipo | Cuándo se programa | Efecto |
|---|---|---|
| `RepartirOleada` | Al crear la solicitud se programa la oleada 1; una revisión puede programar la siguiente. | Avanza la oleada de una solicitud abierta y no vencida, crea los envíos y sus eventos, y programa `RevisarOleada` en la misma transacción. En la última oleada, si ninguna tuvo envíos, escribe `SolicitudSinTecnicos`. |
| `RevisarOleada` | Al repartir, para `ahora + Oleadas:MinutosRevision` (15 minutos por omisión). | Si la solicitud sigue abierta, hay menos del mínimo de cotizaciones activas y no se alcanzó el máximo de oleadas, programa `RepartirOleada` para la siguiente. |

Cada tarea usa `claveNegocio = {solicitudId}:{oleada}`, `programadaPara` en UTC, `estado`, `intentos`, `creadoEn` y `actualizadoEn`. La reserva agrega `reservadaHasta` y `reservaId`; su confirmación transaccional incrementa `confirmacionesReserva`. Los estados son `pendiente`, `en_proceso`, `hecha` y `fallida`. El índice único por `tipo` y `claveNegocio`, la confirmación de la reserva y el avance condicionado protegen los efectos frente a reintentos y retomas; una reserva vencida no garantiza que solo haya un manejador físicamente en ejecución.

La tarea `VencerSolicitud` (clave de negocio `{solicitudId}`) la programa la creación de la solicitud para su `venceEn`, y la recurrente `BarrerVencidas` (spec 029, clave `recurrente:{segundos}`, cada `Solicitudes:MinutosBarrido` minutos, lotes de 100) respalda las que no se vencieron. Ambas usan la misma actualización condicionada, así que una solicitud vence una sola vez aunque corran dos réplicas. Los identificadores de eventos y tareas deben tratarse como datos sensibles al correlacionarlos: no incluir descripción, dirección, nombres, teléfonos ni `sub` en sus ejemplos o registros de reparto.

## Eventos de la purga de fotos de verificación (spec 057)
Buzón de Identidad, `_id` determinista `{tipo}:{verificacionId}:1`, carga solo con identificadores.

| Evento | Destino | Carga | Productor |
|---|---|---|---|
| `VerificacionSinRevisarAlertada` | identity | `verificacionId`, `enviadoEn` | tarea `AvisarAlVerificador` (24 h) |
| `FotosDeVerificacionBorradas` | messaging | `verificacionId`, `usuarioId`, `razon` (`decidida`, `vencida`, `baja_de_cuenta`) | `PurgaDeVerificacion` |

## Evento de Mensajería en el buzón de salida (spec 030)

Mensajería inserta `MensajeNuevo` en `outbox` dentro de la misma transacción que guarda el mensaje y actualiza los contadores (`_id` determinista `MensajeNuevo:{mensajeId}`, `estado = pendiente`, `intentos = 0`). `agregadoId` es el `conversacionId`. Nunca lleva el texto del mensaje, nombres, teléfonos ni direcciones.

| Evento | Spec | Cuándo | `datos` |
|---|---|---|---|
| `MensajeNuevo` | 030 | Al guardar un mensaje enviado en una conversación | `conversacionId`, `destinatarioUsuarioId` |

El despachador de avisos (spec 037) es el único consumidor previsto: deduplica por evento, recurso y persona, y agrupa los `mensaje_nuevo` en una ventana de 5 minutos. Mientras no esté activo (`Notificaciones:PushHabilitado = false`), el documento queda pendiente y no demuestra entrega. El cliente FCM real existe y se enciende con `Notificaciones__PushHabilitado=true` (lee `Notificaciones:Fcm:ProjectId` y el secreto `fcm-service-account`; sin ellos no arranca).

## Eventos de Identidad en el buzón de salida (specs 039, 043, 053, 054 y 059)

Identidad escribe cada evento en su `outbox` con `_id` determinista `{tipo}:{recursoId}:{version}` (`OutboxReglas.BuildId`), un `destino` (`market`, `messaging` o `identity`) y una carga solo de identificadores (`OutboxReglas.Validate` rechaza campos como nombre, teléfono, dirección, texto, correo o CURP). El procesador de la bandeja (spec 054) los entrega por las rutas internas de cada servicio (`docs/endpoints.md`, filas `/api/v1/interno/*`) con un token de servicio de 5 minutos; 202 también si ya los recibió. Los tipos permitidos salen de `OutboxReglas.Tipos`.

| Evento | Spec | Destino | Cuándo | Carga | Consumidor |
|---|---|---|---|---|---|
| `CuentaSuspendida` | 059 | market | Al suspender de forma preventiva la cuenta por un reporte de gravedad alta | `eventoId`, `usuarioId`, `reporteId`, `desde` (`_id` `CuentaSuspendida:{reporteId}:1`) | `POST /api/v1/interno/eventos` de Mercado: el perfil del técnico pasa a `suspendido` y cada cita próxima genera el aviso `visita_afectada` al cliente |
| `CuentaReactivada` | 059 | market | Al levantar la suspensión | `eventoId`, `usuarioId`, `desde` (`_id` `CuentaReactivada:{reporteId}:1`) | `POST /api/v1/interno/eventos` de Mercado: el perfil vuelve a `activo` |
| `CuentaEliminada` | 039 | market | Al vencer el plazo de gracia de la eliminación (tarea `EliminarCuenta`, 7 días) | `sub` (`_id` `CuentaEliminada:{sub}:1`) | `POST /api/v1/interno/cuentas/{usuarioId}/eliminada` de Mercado: cancela visitas confirmadas y anonimiza opiniones |
| `sesion_revocada` | 037 | messaging | Al revocar una sesión (cambio de número, suspensión) | `sid` (`_id` `sesion_revocada:{sid}:1`) | `POST /api/v1/interno/avisos` de Mensajería: borra los dispositivos de esa sesión |
| `BloqueoCreado` | 043 | market y messaging (un evento por destino) | Al bloquear a una persona | `eventoId`, `bloqueaId`, `bloqueadoId`, `ocurridoEn` (`_id` `BloqueoCreado:{bloqueoId}:{destino}:1`) | `POST /api/v1/interno/bloqueos` de cada servicio: mantiene su proyección `bloqueos` |
| `BloqueoEliminado` | 043 | market y messaging | Al quitar el bloqueo | igual que `BloqueoCreado` | igual; repetirlo no cambia nada |
| `ApelacionResuelta` | 060 | identity | Al resolver desde el panel la apelación de una persona suspendida | `eventoId`, `reporteId`, `decision` (`_id` `ApelacionResuelta:{reporteId}:1`) | Solo auditoría: la entrega interna la da por aceptada sin llamar a ningún servicio; la persona no tiene dispositivos y no recibe push |
| `ReporteDecidido` | 053 | messaging | Al decidir un reporte en el panel; uno por cada parte, sin datos de la otra | `eventoId`, `usuarioId`, `reporteId`, `rol`, `accion` | `POST /api/v1/interno/avisos` de Mensajería (spec 037, CA-20): aviso `reporte_decidido` de texto fijo (categoría `sistema`, sin rol); la entrega manda solo `eventoId`, `tipo`, `destinatarioId` y `recurso` (el reporte), nunca la gravedad, la acción ni el motivo |
| `TecnicoOcultado` | 053 | market | Al decidir con gravedad alta que oculta al técnico | `eventoId`, `usuarioId`, `reporteId` | `POST /api/v1/interno/tecnicos-ocultados` de Mercado: el técnico deja de aparecer en búsqueda y perfil público |

Dónde existe cada uno: `sesion_revocada`, `CuentaEliminada`, `BloqueoCreado` y `BloqueoEliminado` (PR #264) están en sprint-05 con sus consumidores en Mercado y Mensajería. `CuentaSuspendida` y `CuentaReactivada` se escriben en sprint-05 (`AccountSuspension`), pero su consumidor `/interno/eventos` de Mercado, `visita_afectada` y los consumidores de `/interno/cuentas/...` están en el PR #272 de integración de Identidad, sin fusionar. `ReporteDecidido` y `TecnicoOcultado` (y el consumidor de `TecnicoOcultado` en Mercado) esperan al PR de Identidad de B (spec 053), sin fusionar.

`visita_afectada` no es un evento de la bandeja sino un tipo de aviso (rol `cliente`, recurso de `visitas`): Mercado lo entrega a `POST /api/v1/interno/avisos` por cada cita próxima del técnico suspendido (PR #272, sin fusionar).

## Eventos de Mercado: elección, permiso de fotos y reporte (specs 048, 050, 053 y 054)

Mismo formato que los eventos de Mercado de arriba (`_id` `{tipo}:{agregadoId}`, `estado = pendiente`, `datos` solo con identificadores).

| Evento | Spec | Cuándo | `datos` | Consumidor |
|---|---|---|---|---|
| `CotizacionElegida` | 048 | Al elegir la cotización, además de `CotizacionAceptada` y `CotizacionRechazada` | `solicitudId`, `cotizacionId`, `tecnicoUsuarioId` | `POST /api/v1/interno/eventos` de Mensajería (permiso `CotizacionElegida`): cierra las demás conversaciones de la solicitud sin borrar mensajes. Se escribe en sprint-05 |
| `PermisoFotosCambiado` | 050 | Al cambiar el permiso del cliente de publicar las fotos del trabajo (`PUT /api/v1/visitas/{id}/permiso-fotos`; `_id` `PermisoFotosCambiado:{visitaId}:{ticks}`) | `visitaId`, `solicitudId`, `clienteId`, `tecnicoUsuarioId`, `permiso` | Quien publica o retira las fotos del trabajo (consumidor pendiente). En sprint-05 |
| `ReporteNoTerminadoCreado` | 050, 053 | Al reportar el cliente que la visita no se terminó (`POST /api/v1/visitas/{id}/no-terminado`) | `visitaId`, `solicitudId`, `clienteId`, `tecnicoUsuarioId`; el texto no viaja | `POST /api/v1/interno/reportes-no-terminados` de Identidad: lee el texto en `GET /api/v1/interno/visitas/{visitaId}/reporte-no-terminado` y abre el reporte en la cola del panel. El evento se escribe en sprint-05; el despachador y el consumidor están en el PR de Identidad de B (spec 053), sin fusionar |
| `OpinionModerada` | 061 | Al ocultar o mostrar una opinión desde el panel (`POST /api/v1/admin/opiniones/{id}/ocultar` o `/mostrar`) y solo si cambió su estado; `_id` `OpinionModerada:{opinionId}:{ticks}` | `opinionId`, `tecnicoId`, `tecnicoUsuarioId`, `accion` (`ocultada` o `mostrada`), `porId`; nunca el texto ni el motivo | `POST /api/v1/interno/opiniones-moderadas` de Identidad (permiso `opinion-moderada`): auditoría `opinion_ocultada` u `opinion_mostrada`; y, solo al ocultar, `POST /api/v1/interno/avisos` de Mensajería (permiso `opinion_oculta`, `rol` `tecnico`): aviso de texto fijo al técnico sin motivo ni datos de la opinión. Se marca entregado cuando todos los destinos responden 202 |

## Tareas programadas de vencimiento (specs 029 y 050)

Viven en `tareas_programadas` de Mercado (mismo formato que las de reparto de arriba):

| Tipo | Cuándo se programa | Efecto |
|---|---|---|
| `VencerSolicitud` (spec 029, R-09) | Al crear la solicitud, para su `venceEn` (el catálogo de Identidad la lista con 48 h) | Vence la solicitud de su clave de negocio; sin efectos si ya tiene cita, está cancelada o ya venció. El manejador está en sprint-05 |
| `CerrarPeriodoDeOpinion` (spec 054) | Al terminar la visita (`POST /api/v1/visitas/{id}/terminar`), 30 días después | Cierra el plazo de opinar; una visita ya calificada lo cierra igual, sin efecto. Se programa en sprint-05; el manejador `CerrarPeriodoDeOpinion` está en el PR #265 |
