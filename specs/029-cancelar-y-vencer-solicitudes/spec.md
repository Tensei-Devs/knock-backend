# Spec 029 — Cancelar y vencer solicitudes

> **Estado:** Borrador · **Repositorio:** knock-backend · **Rector:** `constitution.md`
> **Claves de trabajo:** KNOCK-70 (HU-027) y KNOCK-71 (HU-028) · **Sprint:** 5 (la spec se deja lista en el Sprint 4)
> **Servicio:** Mercado · **Módulo:** Requests · **Colecciones:** `solicitudes`, `cotizaciones` y `envios` (se amplían), `tareas_programadas` (técnica, spec 003)

## 1. Objetivo
Que solo sigan abiertas las necesidades vigentes: el cliente puede cancelar una solicitud que ya no necesita mientras no tenga cita, y las solicitudes sin elegir se cierran solas a las 48 horas. En ambos casos las cotizaciones y los envíos quedan cerrados de forma atómica, se avisa a quien corresponde y una solicitud nunca se cierra dos veces aunque el proceso corra en dos réplicas.

## 2. Historia y alcance
**2.1 Historia**
- **HU-027 (KNOCK-70)** — Como cliente, quiero cancelar una solicitud que ya no necesito, para no hacer perder tiempo a los técnicos.
- **HU-028 (KNOCK-71)** — Como cliente, quiero que mis solicitudes sin elegir se cierren solas a las 48 h, para que solo sigan abiertas las necesidades vigentes.

**2.2 Dentro**
- `POST /api/v1/solicitudes/{id}/cancelar`, idempotente, con motivo opcional.
- El vencimiento automático con la tarea programada `VencerSolicitud` y un barrido de respaldo.
- El cierre en cascada: cotizaciones `enviada` a `rechazada` y envíos a `cerrado`, en la misma transacción.
- Los eventos `SolicitudCancelada` y `SolicitudVencida` para avisar a los técnicos y al cliente.
- La tabla de estados de la solicitud y sus transiciones permitidas.
- Los datos para «Volver a pedir»: el detalle de una solicitud cerrada sigue disponible para su dueño.

**2.3 Fuera**
- Cancelar una cita confirmada (se resuelve con reprogramar o reportar, HU-034 y HU-041).
- Entregar los avisos push (HU-050 y la spec de notificaciones de Mensajería): aquí solo se escriben los eventos.
- Crear la solicitud nueva de «Volver a pedir»: es la misma `POST /api/v1/solicitudes` de la spec 020; la pantalla la llena (frontend).
- Borrar solicitudes o fotos: las cerradas se conservan.
- Cerrar las conversaciones del chat: la spec 030 las cierra leyendo el estado de la solicitud.

## 3. Requisitos
- **R-01:** Los estados de la solicitud son los de la spec 020: `enviada`, `cotizando`, `cita_confirmada`, `terminada`, `calificada`, `cancelada` y `vencida`. Esta spec solo agrega dos transiciones: `enviada | cotizando → cancelada` (por el cliente) y `enviada | cotizando → vencida` (por el sistema). `cancelada` y `vencida` son finales: ninguna operación las reabre.
- **R-02:** `POST /api/v1/solicitudes/{id}/cancelar` exige JWT del cliente dueño y un cuerpo opcional `{ motivo? }`, con `motivo` en `ya_no_lo_necesito`, `lo_resolvi_por_otro_medio`, `tarda_mucho` u `otro`. Un `id` mal formado responde 400; una solicitud inexistente o ajena, 404.
- **R-03:** Si la solicitud está en `enviada` o `cotizando`, la cancelación se hace en una sola transacción: la solicitud pasa a `cancelada` solo si sigue en uno de esos estados (actualización condicionada), con `canceladaEn` y `motivoCancelacion`; sus cotizaciones `enviada` pasan a `rechazada` con `motivoRechazo = request_cancelled`; sus envíos `activo` o `cotizado` (spec 028) pasan a `cerrado` con `cerradoMotivo = request_cancelled`; y se escribe `SolicitudCancelada` en el buzón. Responde 200 `{ id, estado: "cancelada", canceladaEn, motivo }`.
- **R-04:** Cancelar es idempotente: si la solicitud ya está `cancelada`, responde 200 con el mismo cuerpo de la primera cancelación, sin escribir nada ni emitir otro evento.
- **R-05:** Si la solicitud tiene cita (`cita_confirmada`, `terminada` o `calificada`), responde 409 `request_with_appointment` con el detalle «Tu solicitud ya tiene una cita confirmada. Para cambiarla usa reprogramar o reportar.» y no cambia nada. Si está `vencida`, responde 409 `request_expired`.
- **R-06:** Una cancelación y una elección (spec 021) simultáneas de la misma solicitud: gana la primera transacción que confirma. Si gana la elección, la cancelación responde 409 `request_with_appointment`; si gana la cancelación, la elección responde 409 `request_cancelled` (código de la spec 021).
- **R-07:** `SolicitudCancelada` lleva `{ solicitudId, tecnicosAvisar: [tecnicoId] }` con los técnicos que tenían una cotización `enviada` al cancelar (los que «cotizaron»). Los técnicos con envío sin cotizar no reciben aviso: la solicitud solo desaparece de su bandeja. El evento no lleva motivo, descripción, dirección ni nombres.
- **R-08:** **Vencimiento.** El plazo es `Solicitudes:HorasVencimiento` (48 por omisión, entero de 1 a 168, validado al arrancar) y es el que usa la spec 020 para calcular `venceEn`. Al crear la solicitud, en la misma transacción, se programa `VencerSolicitud` con `claveNegocio = {solicitudId}` y `programadaPara = venceEn` en `tareas_programadas`; la ejecuta el ejecutor de la spec 028.
- **R-09:** El manejador de `VencerSolicitud`, en una sola transacción: pasa la solicitud a `vencida` solo si sigue en `enviada` o `cotizando` y `venceEn <= ahora` (actualización condicionada), con `vencidaEn`; pasa sus cotizaciones `enviada` a `rechazada` con `motivoRechazo = request_expired`; cierra sus envíos con `cerradoMotivo = request_expired`; y escribe `SolicitudVencida { solicitudId, clienteId, tecnicosAvisar }`. Si la condición no se cumple (ya tiene cita, ya se canceló o ya venció), termina sin efectos y sin evento.
- **R-10:** **Barrido de respaldo.** Cada `Solicitudes:MinutosBarrido` (10 por omisión) el ejecutor corre la tarea recurrente `BarrerVencidas`, que busca con el índice `{ estado: 1, venceEn: 1 }` hasta 100 solicitudes en `enviada` o `cotizando` con `venceEn <= ahora` y les aplica la misma lógica de R-09. Cubre las solicitudes creadas antes de esta spec y cualquier tarea perdida. La tarea recurrente se reserva como cualquier otra, así que una sola réplica la ejecuta a la vez.
- **R-11:** Con dos o más réplicas, una solicitud vence una sola vez y emite un solo `SolicitudVencida`: la actualización condicionada es la garantía; la reserva de la tarea solo evita trabajo repetido.
- **R-12:** Las operaciones sobre una solicitud vencida o cancelada responden con los códigos ya definidos: cotizar 409 `request_not_quotable` (spec 020), elegir 409 `request_expired` o `request_cancelled` (spec 021), descartar 404 porque el envío ya está cerrado (spec 028). Mientras la tarea no corre, `venceEn` ya bloquea cotizar y elegir (specs 020 y 021).
- **R-13:** **Volver a pedir.** El cliente dueño sigue viendo el detalle completo de su solicitud `cancelada` o `vencida` con `GET /api/v1/solicitudes/{id}` (spec 020), incluida su dirección, con el campo nuevo `puedeVolverAPedir: true` (falso en cualquier otro estado). La lista `GET /api/v1/solicitudes?estado=vencida` y `?estado=cancelada` las devuelve. Volver a pedir es crear una solicitud nueva; el servidor no copia ni reabre la anterior. Los técnicos ya no ven el detalle de una solicitud cerrada en la que no fueron elegidos (404).
- **R-14:** Límite de 10 cancelaciones por hora por persona, además de los del gateway; al pasarse, 429 con `Retry-After`.
- **R-15:** Las respuestas de la cancelación llevan `Cache-Control: no-store`. Los errores usan ProblemDetails con `correlationId` y `traceId`. Los registros llevan solo conteos (canceladas, vencidas por tarea, vencidas por barrido) y la latencia; nunca el motivo, el `sub` ni la descripción.

## 4. Criterios de aceptación
- **CA-01:** Dada una solicitud `cotizando` con dos cotizaciones `enviada` y tres envíos, cuando el dueño cancela, entonces responde 200 con `Cache-Control: no-store`, la solicitud queda `cancelada`, las dos cotizaciones `rechazada` con `request_cancelled`, los envíos `cerrado` y hay un `SolicitudCancelada` con los dos técnicos que cotizaron.
- **CA-02:** Dada una solicitud con cita confirmada, terminada o calificada, cuando el dueño intenta cancelar, entonces responde 409 `request_with_appointment` con la explicación y no cambia nada.
- **CA-03:** Dada una solicitud de otra persona o inexistente, cuando se intenta cancelar, entonces responde 404.
- **CA-04:** Dada una solicitud ya cancelada, cuando se cancela otra vez, entonces responde 200 con el mismo cuerpo y no hay un segundo evento.
- **CA-05:** Dada una cancelación y una elección simultáneas, cuando terminan, entonces exactamente una gana y la otra responde 409 con el código de R-06.
- **CA-06:** Dada una solicitud `enviada` o `cotizando` que cumple 48 horas sin cita, cuando corre `VencerSolicitud`, entonces queda `vencida`, sus cotizaciones `enviada` pasan a `rechazada` con `request_expired`, sus envíos se cierran y hay un `SolicitudVencida` con el `clienteId`.
- **CA-07:** Dada una solicitud que ya tiene cita, cuando llega la hora de `VencerSolicitud`, entonces no cambia y no hay evento.
- **CA-08:** Dadas dos réplicas del ejecutor y una solicitud vencida, cuando corren la tarea y el barrido a la vez, entonces la solicitud vence una sola vez y hay un solo `SolicitudVencida`.
- **CA-09:** Dada una solicitud creada sin tarea de vencimiento y con `venceEn` pasado, cuando corre el barrido, entonces queda `vencida`; y dadas 150 solicitudes vencidas, cuando corre un barrido, entonces procesa 100 y el siguiente procesa las 50 restantes.
- **CA-10:** Dada una solicitud vencida o cancelada, cuando el dueño pide su detalle, entonces responde 200 con descripción, fotos, oficio, colonia, franja, dirección y `puedeVolverAPedir: true`, y aparece en `GET /api/v1/solicitudes?estado=vencida` (o `cancelada`); y un técnico no elegido que pide el detalle recibe 404.
- **CA-11:** Dada una solicitud vencida, cuando el dueño intenta cancelarla, entonces responde 409 `request_expired`; y cuando un técnico intenta cotizarla, 409 `request_not_quotable`.
- **CA-12:** Dado un cuerpo con `motivo` fuera de la lista o un `id` mal formado, cuando se cancela, entonces responde 400 y no cambia nada.
- **CA-13:** Dada una petición sin token, cuando se cancela, entonces responde 401; y dadas 11 cancelaciones en una hora de la misma persona, cuando llega la 11, entonces responde 429 con `Retry-After`.
- **CA-14:** Dado `Solicitudes:HorasVencimiento = 0` o `200`, cuando arranca el servicio, entonces falla; y con 24, una solicitud nueva tiene `venceEn = creadoEn + 24 h` y su tarea programada a esa hora.
- **CA-15:** Dados los eventos y los registros de una cancelación y un vencimiento, cuando se inspeccionan, entonces no contienen motivo, descripción, dirección, nombres ni `sub` en texto libre.

**4.1 Criterio → prueba prevista**

| Criterio | Prueba prevista |
|---|---|
| CA-01 | `Spec029CancelarTests.Cancela_En_Cascada_CA_029_01` |
| CA-02 | `Spec029CancelarTests.Rechaza_Con_Cita_CA_029_02` |
| CA-03 | `Spec029CancelarTests.Oculta_Solicitud_Ajena_CA_029_03` |
| CA-04 | `Spec029CancelarTests.Cancelar_Es_Idempotente_CA_029_04` |
| CA-05 | `Spec029ConcurrenciaTests.Cancelar_Contra_Elegir_CA_029_05` |
| CA-06 | `Spec029VencerTests.Vence_En_Cascada_CA_029_06` |
| CA-07 | `Spec029VencerTests.No_Vence_Con_Cita_CA_029_07` |
| CA-08 | `Spec029ConcurrenciaTests.Vence_Una_Sola_Vez_CA_029_08` |
| CA-09 | `Spec029BarridoTests.Barrido_De_Respaldo_Por_Lotes_CA_029_09` |
| CA-10 | `Spec029VolverAPedirTests.Detalle_De_Solicitud_Cerrada_CA_029_10` |
| CA-11 | `Spec029EstadosTests.Estados_Finales_CA_029_11` |
| CA-12 | `Spec029ValidacionTests.Rechaza_Cuerpo_Invalido_CA_029_12` |
| CA-13 | `Spec029SeguridadTests.Exige_Sesion_Y_Limite_CA_029_13` |
| CA-14 | `Spec029ConfiguracionTests.Plazo_Configurable_CA_029_14` |
| CA-15 | `Spec029RegistrosTests.Sin_Datos_Personales_CA_029_15` |

## 5. Contrato
| Método y ruta | Quién | Cuerpo | Respuestas |
|---|---|---|---|
| `POST /api/v1/solicitudes/{id}/cancelar` | cliente dueño | `{ motivo? }` | 200 `{ id, estado, canceladaEn, motivo }` (el mismo cuerpo si ya estaba cancelada), 400, 401, 404, 409, 429 |
| `GET /api/v1/solicitudes/{id}` (spec 020) | dueña | — | agrega `puedeVolverAPedir` |

**Tabla de transiciones que agrega esta spec**

| Desde | Hacia | Quién | Condición |
|---|---|---|---|
| `enviada`, `cotizando` | `cancelada` | cliente dueño | sin cita |
| `enviada`, `cotizando` | `vencida` | sistema | `venceEn <= ahora` y sin cita |

**Códigos de error:** `request_with_appointment` (409, nuevo), `request_expired` (409, de la spec 021). Errores de forma: 400 con `errors.motivo`.

**Eventos (buzón de Mercado):** `SolicitudCancelada { solicitudId, tecnicosAvisar[] }`, `SolicitudVencida { solicitudId, clienteId, tecnicosAvisar[] }`. Tareas: `VencerSolicitud` (una por solicitud) y `BarrerVencidas` (recurrente).

**Límites:** 10 cancelaciones por hora por persona; plazo de vencimiento de 1 a 168 horas (48 por omisión); barrido de 100 solicitudes cada 10 minutos.

## 6. Modelo de datos
**`solicitudes`** (spec 020; campos nuevos)
| Campo | Tipo | Nota |
|---|---|---|
| `canceladaEn` | date? | UTC |
| `motivoCancelacion` | string? | uno de R-02; nunca se registra en logs |
| `vencidaEn` | date? | UTC |

Índices: se usa `{ estado: 1, venceEn: 1 }` (ya definido en la spec 020).

**`cotizaciones`** (spec 020): `motivoRechazo` admite además `request_cancelled` y `request_expired` (la spec 021 ya define `se_eligio_otra`).

**`envios`** (spec 028): `cerradoMotivo` usa `request_cancelled` y `request_expired`.

**`tareas_programadas`** (forma de la spec 028): `VencerSolicitud` con `claveNegocio = {solicitudId}`; `BarrerVencidas` con `claveNegocio = recurrente` que el manejador reprograma al terminar.

El script `db/` de esta spec no crea colecciones: solo comprueba que existan los índices que usa y programa `BarrerVencidas` si no existe (idempotente).

## 7. Arquitectura y componentes
- `CancelacionController` (S6960) recibe y valida con `CancelacionValidator`; delega en `CierreDeSolicitudService`.
- `CierreDeSolicitudService` concentra la cascada (solicitud, cotizaciones, envíos y evento) para los dos motivos de cierre, con una sola transacción; evita dos implementaciones de la misma regla.
- `TransicionesDeSolicitud` (función pura, `Domain/`): decide si una transición es válida desde el estado actual; es el punto de mutación.
- `VencerSolicitudHandler` y `BarrerVencidasHandler` implementan `IManejadorDeTarea` del ejecutor de la spec 028.
- `SolicitudService` (spec 020) programa `VencerSolicitud` en la transacción de creación y lee el plazo de `SolicitudesOptions`.
- Sin estado en el proceso.

## 8. Dependencias
- Spec 003 (transacciones, buzón, `tareas_programadas`), 009 (JWT y límites), 020 (estados, `venceEn`, detalle y lista), 021 (elección y sus códigos), 028 (ejecutor de tareas y envíos).
- La spec 030 lee el estado de la solicitud para cerrar las conversaciones previas.
- La entrega de avisos (HU-050) consume `SolicitudCancelada` y `SolicitudVencida`.
- Frontend (pantallas de Actividad y del detalle) consume la cancelación y `puedeVolverAPedir`.

## 9. Amenazas y controles
| Amenaza | Control | Caso que lo prueba |
|---|---|---|
| Cancelar la solicitud de otra persona | Filtro por `sub` del dueño; 404 uniforme | CA-03 |
| Cancelar una cita confirmada para dejar plantado al técnico | 409 `request_with_appointment`; la cita tiene su propio flujo | CA-02 |
| Cancelación y elección simultáneas que dejan estados mezclados | Transacción con actualizaciones condicionadas | CA-05 |
| Doble vencimiento y doble aviso con dos réplicas | Actualización condicionada y evento solo si cambió | CA-08 |
| Solicitudes que nunca vencen por una tarea perdida | Barrido de respaldo por índice | CA-09 |
| Reabrir una solicitud cerrada | Estados finales; volver a pedir crea otra | CA-10, CA-11 |
| Un técnico sigue viendo la dirección de una solicitud cerrada | Detalle solo para el dueño y el técnico elegido | CA-10 |
| Abuso de cancelaciones | 10 por hora por persona | CA-13 |
| Datos personales en eventos y registros | Solo identificadores y conteos | CA-15 |

## 10. Plan de pruebas
| Tipo | ¿Aplica? | Motivo |
|---|---|---|
| Unitarias | Sí | `TransicionesDeSolicitud`, validador del cuerpo, opciones del plazo |
| Integración | Sí | MongoDB real con `Testcontainers` y `TimeProvider` falso: cascada, idempotencia, carrera con la elección, dos ejecutores y barrido por lotes |
| Seguridad negativa | Sí | Solicitud ajena, sin token, sin clave, límite, técnico no elegido sobre una solicitud cerrada |
| Contrato | Sí | Ruta nueva, `request_with_appointment` y `puedeVolverAPedir` en el OpenAPI de Mercado |
| Base de datos | Sí | Script idempotente que comprueba índices y programa el barrido |
| Arquitectura | Sí | El controller no toca MongoDB; los manejadores solo dependen de servicios del módulo |
| Mutación | Sí | `TransicionesDeSolicitud` y la condición del vencimiento (meta 70 %) |
| Humo y extremo a extremo | Sí | Flujo en el entorno de evaluación con dos ejecutores: pedir → cancelar (con cascada) y pedir → vencer sin elección (barrido); la ruta de cancelación sin token responde 401 |
| Regresión | Sí | Specs 020, 021 y 028 en verde con la cascada nueva |
| Rendimiento | No | Volumen del piloto; el barrido está acotado a 100 por pasada |
| CI/CD y análisis | Sí | Semgrep, Gitleaks, Trivy y dependencias sin hallazgos nuevos; sin paquetes nuevos |
| Aceptación | Sí | CA-01 a CA-15 con la tabla 4.1 |

**Gates:** los de `AGENTS.md`; `python scripts/sdd_lint.py --trace 029`.

## 11. Escenarios
**Positivos**
- El cliente cancela una solicitud sin cotizaciones: queda cancelada y nadie recibe aviso.
- El cliente cancela con dos cotizaciones: los dos técnicos reciben aviso.
- Una solicitud sin elegir cumple 48 horas: vence y el cliente puede volver a pedirla.

**Negativos**
- Cancelar con cita confirmada, cancelar una ajena, cancelar sin sesión.
- Cotizar o elegir una solicitud vencida.

**Errores esperados**
- 400 (`errors.motivo`, id mal formado), 401, 404, 409 (`request_with_appointment`, `request_expired`), 429.

**Casos límite**
- Cancelación en el mismo segundo en que vence (gana la primera transacción; la otra no tiene efecto).
- `venceEn` exactamente igual a ahora (vence).
- Réplica que se cae con `VencerSolicitud` reservada (otra la toma al terminar el arrendamiento).
- Solicitud creada antes de desplegar esta spec (la vence el barrido).
- 100 y 101 solicitudes vencidas en el mismo barrido.

## 12. Definición de terminado
- Los 15 criterios tienen prueba en verde y la tabla 4.1 coincide con los nombres reales.
- La prueba con dos ejecutores demuestra un solo vencimiento y un solo evento.
- `docs/events.md` registra los eventos y tareas; `docs/endpoints.md` la ruta nueva y el campo `puedeVolverAPedir`.
- OpenAPI regenerado y `Memoria.md` con la entrada del cambio.
- Revisión de par y sin hallazgos de seguridad abiertos.

## 13. Decisiones
- **Una sola cascada para cancelar y vencer:** las dos cierran lo mismo; una sola clase evita que diverjan.
- **Tarea por solicitud más barrido:** la tarea da puntualidad; el barrido da garantía si una tarea se pierde o la solicitud es anterior a esta spec.
- **Estados finales:** reabrir una solicitud cerrada complicaría oleadas, tareas y avisos; volver a pedir crea una nueva.

## 14. Decisiones tomadas sin consulta
Se tomaron de noche, con el criterio de la constitución; quien apruebe puede cambiarlas antes de firmar.

1. **Las cotizaciones de una solicitud cerrada pasan a `rechazada` con un motivo, no a un estado nuevo `vencida`.** La ficha de HU-028 dice «sus cotizaciones también»; usar `rechazada` con `motivoRechazo` evita ampliar los estados de la spec 020. Alternativa: estado `vencida` en `cotizaciones`.
2. **Solo se avisa a los técnicos que cotizaron.** Es lo que dice la ficha de HU-027; los que solo recibieron la solicitud dejan de verla en la bandeja. Alternativa: avisar a todos los envíos, que genera avisos sin valor.
3. **Cancelar es idempotente por estado, sin clave.** Una segunda cancelación responde 200 con el mismo cuerpo y no repite el evento.
4. **Motivo opcional y cerrado a una lista.** Sirve para métricas sin texto libre que haya que moderar. Alternativa: texto libre, que exigiría moderación (spec 031).
5. **«Volver a pedir» no tiene ruta propia.** El detalle ya trae los datos y la creación ya existe; una ruta de «repetir» duplicaría la validación de la spec 020. Alternativa: `POST /solicitudes/{id}/repetir`.
6. **Barrido cada 10 minutos de 100 en 100.** Con el volumen del piloto alcanza de sobra y acota la carga de cada pasada.
7. **El plazo sale de configuración con rango de 1 a 168 horas.** La ficha pide «plazo configurable (48 h)»; el rango impide un valor que cierre todo al instante o nunca.

## 15. Riesgos y dudas abiertas
- **`Idempotency-Key` retirada por ahora (decisión del 2026-11-10).** Se quitó de la cancelación, que ya es idempotente por estado. Se retoma con una spec propia si se decide volver a exigirla en las operaciones que cambian estado.
- **Conflicto con la spec 020 (borrador):** el plazo deja de ser una constante (R-08), la creación programa `VencerSolicitud` en su transacción y el detalle agrega `puedeVolverAPedir`. Hay que reflejarlo en la 020.
- **Conflicto con la spec 021 (borrador):** su `motivoRechazo` debe admitir `request_cancelled` y `request_expired`; su elección ya responde `request_cancelled` y `request_expired`, que se conservan.
- **Depende del ejecutor de la spec 028:** si la 029 se implementa antes, el ejecutor se construye aquí (misma forma) y la 028 lo reutiliza.
- **Hueco:** sin spec de entrega de avisos (HU-050), el cliente no recibe el aviso de vencimiento en el teléfono; solo verá el estado en Actividad.
- **Retraso de hasta 15 segundos entre `venceEn` y el estado `vencida`:** mientras tanto `venceEn` ya bloquea cotizar y elegir; la lista puede mostrar la solicitud abierta unos segundos.

## Aprobación
- [ ] Revisor 1 — nombre: ________ fecha: ________
- [ ] Sin conflicto con la constitución
- **Estados:** Borrador (agente) → Aprobada (persona; habilita el código) → Implementada (tras el merge)
