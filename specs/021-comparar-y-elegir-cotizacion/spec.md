# Spec 021 — Comparar y elegir cotización

> **Estado:** Borrador · **Repositorio:** knock-backend · **Rector:** `constitution.md`
> **Claves de trabajo (verificadas en Jira el 2026-11-09):** KNOCK-82 «HU-033 · Elegir una cotización (cita confirmada; se revelan teléfono y dirección)» y KNOCK-81 «HU-032 · Comparar cotizaciones (recomendadas, precio, calificación, más pronto)» (el listado de esta spec es el contrato de su pantalla)
> **Responsables sugeridos:** Guillermo (backend de HU-032 y HU-033) · Erick consume el contrato (frontend 015) · **Ola:** A · **Sprint:** 4 · **Prioridad:** P1
> **Servicio:** Mercado · **Módulo:** Requests · **Colecciones:** `solicitudes`, `cotizaciones`, `visitas`, `tecnicos`

## 1. Objetivo
Que el cliente compare hasta cinco cotizaciones con la información que necesita para decidir (precio con tope, calificación, verificación, qué cubre cada tipo de precio y qué tan pronto puede ir el técnico) y que, al elegir una, quede confirmada la cita, se revelen los datos de contacto y se cierren las demás opciones.

## 2. Historia y alcance
**2.1 Historia**
- **HU-033** — Como cliente, quiero elegir una cotización, para confirmar la cita con el técnico que más me convence.

**2.2 Dentro**
- Listar las cotizaciones de una solicitud propia, con orden y etiquetas.
- Explicar qué cubre cada tipo de precio.
- Elegir una cotización de forma atómica: una cita por solicitud, las demás quedan rechazadas.
- Revelar al cliente el contacto público del técnico y el lugar de la cita.
- Generar el código de visita para domicilio (se muestra en la spec 022).
- Publicar los eventos de aceptación, rechazo y cita confirmada.

**2.3 Fuera**
- Crear solicitudes y enviar cotizaciones (spec 020).
- Mostrar el código, validar la llegada y avisar a una persona de confianza (spec 022).
- Reprogramar, cancelar y “no se presentó” (HU-034, HU-041).
- Retirar o editar una cotización (HU-035).
- Orden “Recomendadas” con aprendizaje automático (HU-077, Sprint 5) y distancia en kilómetros.
- Chat con el técnico (spec 026 y HU-029): aquí solo se emite el evento de la elección.
- Pantallas de comparación y elección (frontend).

## 3. Requisitos
- **R-01:** `GET /api/v1/solicitudes/{id}/cotizaciones` exige JWT y responde solo al cliente dueño de la solicitud. Cualquier otra persona recibe 404 uniforme; un `id` que no es `ObjectId` responde 400.
- **R-02:** La respuesta trae hasta cinco cotizaciones en estado `enviada`, `aceptada` o `rechazada`, con `{ id, estado, tipoPrecio, precio, precioMin?, precioMax?, tope, requiereAprobacion, dia, hora, nota?, cubre, etiquetas[], tecnico, motivoRechazo? }`. `motivoRechazo` aparece solo en las `rechazada` (valores `se_eligio_otra` de esta spec y `solicitud_cancelada` y `solicitud_vencida` de la spec 029; *decisión automática, revisar*). Las retiradas no se listan.
- **R-03:** La spec 020 limita cada solicitud a 5 cotizaciones vivas, así que el listado está acotado y se devuelve completo, sin cursor (excepción documentada a la spec 010, R-07, como el catálogo de oficios). `totalCotizaciones` dice cuántas se devuelven. Las etiquetas se calculan sobre todas las devueltas.
- **R-04:** `?orden` acepta `recomendadas` (omisión), `precio`, `calificacion` y `pronto`; otro valor responde 422 con `errors.orden`. `precio` ordena por `tope` de menor a mayor y deja las visitas al final; `calificacion` de mayor a menor (sin calificación al final); `pronto` por `dia` y `hora` de menor a mayor. Todo empate se resuelve por `creadoEn` ascendente y luego por `_id`, para que el orden sea estable.
- **R-05:** Mientras el aprendizaje automático no exista, `recomendadas` usa un orden determinista y documentado: primero los técnicos verificados, después la calificación de mayor a menor, después el `tope` de menor a mayor y por último el más antiguo.
- **R-06:** `etiquetas` solo puede contener `verificado`, `mejor_precio` y `puede_ir_mas_pronto`. `mejor_precio` se asigna al `tope` más bajo entre las cotizaciones que no son visita y nunca a una visita; `puede_ir_mas_pronto` a la fecha y hora más tempranas.
- **R-07:** `cubre` es un texto fijo por tipo: `visita` = “Solo visita: ir a ver el trabajo”; `fijo` = “Arreglo completo, mano de obra incluida, refacciones aparte”; `rango` = “Estimado del arreglo dentro del rango”. Si hay tipos distintos, `tiposMixtos` es verdadero para que la app muestre el aviso.
- **R-08:** La respuesta lleva `puedeElegir` y, cuando es falso, `motivo` con el mismo nombre que el error que daría la elección (`solicitud_sin_cotizaciones`, `solicitud_vencida`, `solicitud_cancelada`, `solicitud_ya_elegida`) y `visitaId` si ya se eligió. El listado no se cachea: `Cache-Control: private, no-store`, sin `ETag`. El listado **nunca** incluye `telefono`, `whatsapp`, la dirección del taller ni la del cliente.
- **R-09:** `POST /api/v1/solicitudes/{id}/elegir` exige JWT del cliente dueño y cuerpo `{ cotizacionId }` (`ObjectId`; si no, 422 `errors.cotizacionId`). Responde 200 con la cita confirmada y los datos revelados.
- **R-10:** La elección se ejecuta en una sola transacción: cambia la solicitud a `cita_confirmada` solo si estaba en `cotizando` y no venció; acepta la cotización elegida si estaba `enviada`; marca las demás `enviada` como `rechazada` con motivo `se_eligio_otra`; crea una sola `visita`; y escribe los eventos en el buzón.
- **R-11:** Dos elecciones simultáneas de la misma solicitud: gana una y la otra responde 409 `solicitud_ya_elegida`. Elegir otra vez una solicitud que ya tiene cita confirmada responde 409 `solicitud_ya_elegida`, sea la misma cotización u otra, y no duplica nada.
- **R-12:** La cotización elegida debe pertenecer a la solicitud y estar `enviada`; si no existe o es de otra solicitud, 404; si fue retirada o ya está rechazada, 409 `cotizacion_no_disponible`.
- **R-13:** Si la solicitud venció o está cancelada, responde 409 `solicitud_vencida` o `solicitud_cancelada` y no cambia nada. Si no está en `cotizando` (por ejemplo, `enviada` sin cotizaciones), 409 `solicitud_sin_cotizaciones`.
- **R-14:** La respuesta de la elección incluye `{ visitaId, solicitudId, cotizacionId, estado, modalidad, dia, hora, precio, tope, tecnico, lugar }`. `tecnico` trae `{ id, nombre, fotoUrl, verificado, nivelVerificacion, telefono?, whatsapp? }`; `lugar` trae `{ tipo: "taller" | "domicilio", direccion }`.
- **R-15:** El `telefono` y el `whatsapp` son el **contacto público** del técnico, guardado en `tecnicos.contactoPublico`; si el técnico no lo publicó, los campos van ausentes y la app ofrece el chat. Nunca se devuelve el celular personal que vive cifrado en Identidad.
- **R-16:** Para `domicilio`, `lugar.direccion` es la dirección que el cliente escribió en su solicitud (se descifra para él); para `taller`, es la dirección del taller del técnico. El contacto y la dirección solo aparecen después de elegir y en la respuesta de la elección o en el detalle de la visita (spec 022).
- **R-17:** La cita se crea en estado `confirmada`, con la `modalidad`, el `dia` y la `hora` de la cotización elegida. Para `domicilio` se genera un código de cuatro dígitos con generador criptográfico, se guarda cifrado (AES-256-GCM con llave versionada en Key Vault) y con vencimiento; para `taller` no hay código. El código no se devuelve en esta respuesta.
- **R-18:** La operación escribe `CotizacionAceptada` (para el elegido), un `CotizacionRechazada` por cada rechazada y `VisitaConfirmada` en el buzón de salida, en la misma transacción. Los eventos llevan solo identificadores y datos no personales.
- **R-19:** Los errores usan ProblemDetails con `correlationId`, `traceId` y `code` (convención de errores de la spec 020, §5). Los registros no incluyen nombres, teléfonos, direcciones ni el código.
- **R-20:** Si la transacción se aborta por un conflicto transitorio, se reintenta una vez; si vuelve a fallar o la base no responde, responde 503 `servicio_no_disponible` sin haber cambiado nada, y la app puede repetir la elección (si la solicitud ya quedó con cita, responde 409 `solicitud_ya_elegida`).
- **R-21:** Regla de privacidad: el contacto público del técnico y el lugar de la cita (dirección del cliente o del taller) solo se revelan al cliente dueño después de elegir, en esta respuesta o en la visita (spec 022). El técnico nunca recibe el teléfono del cliente.

## 4. Criterios de aceptación
- **CA-01:** Dada una solicitud con tres cotizaciones, cuando el dueño pide el listado, entonces recibe tres, con `cubre` y `etiquetas`, y `puedeElegir` verdadero.
- **CA-02:** Dadas cotizaciones con tope 900, 1 100 y una visita de 250, cuando se ordena por `precio`, entonces quedan 900, 1 100 y la visita al final, y `mejor_precio` está en la de 900.
- **CA-03:** Dada una solicitud de otra persona, cuando se pide el listado, entonces responde 404.
- **CA-04:** Dada una cotización válida, cuando el dueño elige, entonces responde 200, la solicitud queda `cita_confirmada`, la cotización `aceptada`, las demás `rechazadas`, hay una `visita` y los eventos están en el buzón.
- **CA-05:** Dadas dos elecciones simultáneas de la misma solicitud, cuando llegan, entonces una responde 200 y la otra 409 `solicitud_ya_elegida`, y hay una sola cita.
- **CA-06:** Dada una solicitud que ya tiene cita confirmada, cuando se elige otra vez, entonces responde 409 `solicitud_ya_elegida` y no crea otra cita.
- **CA-07:** Dada una cotización de otra solicitud o retirada, cuando se elige, entonces responde 404 o 409 y no cambia nada.
- **CA-08:** Dada una solicitud vencida o cancelada, cuando se elige, entonces responde 409 y no cambia nada.
- **CA-09:** Dada una elección confirmada, cuando se inspecciona la respuesta, entonces trae el contacto público del técnico y la dirección del lugar; el listado previo no traía ninguno de los dos.
- **CA-10:** Dada una cita a domicilio, cuando se inspecciona la base, entonces hay un código de cuatro dígitos cifrado (nunca en claro) y con vencimiento; en una cita de taller no hay código.
- **CA-11:** Dado un listado de una solicitud vencida, cuando se pide, entonces responde 200 con `puedeElegir` falso y `motivo` `solicitud_vencida`.
- **CA-12:** Dada una petición sin token, cuando se pide el listado o se elige, entonces responde 401; con el token de un técnico, 404.
- **CA-13:** Dado el registro de una elección, cuando se inspecciona, entonces no contiene nombre, teléfono, dirección ni código.
- **CA-14:** Dadas dos cotizaciones del mismo tipo y tope, cuando se ordena por `recomendadas`, entonces el orden es estable y el verificado va primero.
- **CA-15:** Dado `?orden=barato` o un `id` que no es `ObjectId`, cuando se pide el listado, entonces responde 422 con `errors.orden` o 400, respectivamente.
- **CA-17:** Dada una falla de la base durante la transacción (simulada), cuando se elige, entonces responde 503 `servicio_no_disponible`, la solicitud sigue en `cotizando`, no hay `visita` ni eventos, y repetir la elección termina bien.
- **CA-18:** Dado el listado de cualquier solicitud (antes o después de elegir), cuando se inspecciona la respuesta, entonces no contiene `telefono`, `whatsapp` ni direcciones.

## 5. Contrato
| Método y ruta | Quién | Cuerpo / consulta | Respuestas |
|---|---|---|---|
| `GET /api/v1/solicitudes/{id}/cotizaciones` | cliente dueño | `orden?` (`recomendadas`, `precio`, `calificacion`, `pronto`) | 200 `{ solicitudId, cotizaciones[], totalCotizaciones, tiposMixtos, puedeElegir, motivo?, visitaId? }`, 400, 401, 404, 422, 429 |
| `POST /api/v1/solicitudes/{id}/elegir` | cliente dueño | `{ cotizacionId }` | 200 (cita), 400, 401, 404, 409, 413, 422, 429, 503 |

**Cuerpo de una cotización:** `{ id, estado, tipoPrecio, precio, precioMin?, precioMax?, tope, requiereAprobacion, dia, hora, nota?, cubre, etiquetas[], tecnico: { id, nombre, fotoUrl, calificacion, verificado, nivelVerificacion } }`.

**Cuerpo de la cita:** `{ visitaId, solicitudId, cotizacionId, estado, modalidad, dia, hora, precio, tope, tecnico: { id, nombre, fotoUrl, verificado, nivelVerificacion, telefono?, whatsapp? }, lugar: { tipo, direccion } }`.

**Códigos de error:** `solicitud_ya_elegida` (409), `solicitud_vencida` (409), `solicitud_cancelada` (409), `solicitud_sin_cotizaciones` (409), `cotizacion_no_disponible` (409), `servicio_no_disponible` (503). Errores de formato: 422 con `errors`; recurso ajeno o inexistente: 404 uniforme.

**Límites:** 5 cotizaciones por respuesta (cupo de la spec 020); cuerpo de 8 KB; 10 elecciones por minuto y 60 listados por minuto por persona (se registran en `docs/rate-limit-matrix.md`).

**Caché:** ninguna respuesta se cachea (`Cache-Control: private, no-store`).

## 6. Modelo de datos
**`visitas`** (nueva)
| Campo | Tipo | Nota |
|---|---|---|
| `_id` | ObjectId | |
| `solicitudId`, `cotizacionId` | ObjectId | |
| `clienteId`, `tecnicoUsuarioId` | string | `sub` |
| `tecnicoId` | ObjectId | `tecnicos._id` |
| `modalidad` | string | `domicilio` o `taller` |
| `dia` | string | `YYYY-MM-DD` |
| `hora` | string | `HH:mm` |
| `precio`, `tope` | int | copia de la cotización |
| `estado` | string | `confirmada`, `en_curso`, `terminada`, `cancelada`, `no_presentado` |
| `codigoCifrado`, `codigoVenceEn` | string?, date? | solo domicilio |
| `codigoUsado` | bool | falso al crear |
| `intentos` | int | 0 al crear |
| `bloqueada` | bool | la pone la spec 022 |
| `llegadaEn` | date? | la pone la spec 022 |
| `creadoEn`, `actualizadoEn` | date | UTC |

Índices: `{ solicitudId: 1 }` único; `{ clienteId: 1, dia: 1 }`; `{ tecnicoUsuarioId: 1, dia: 1 }`; `{ estado: 1, dia: 1 }`.

**Cambios en colecciones existentes**
- `solicitudes`: `cotizacionElegidaId`, `estado = cita_confirmada`.
- `cotizaciones`: `estado = aceptada | rechazada`, `motivoRechazo`, `aceptadaEn`.
- `tecnicos`: `contactoPublico: { telefono?, whatsapp? }` (opcional, lo publica el técnico; no es el celular de Identidad), `tallerDireccion?` (solo técnicos de taller) y `nivelVerificacion` (`ninguno`, `basica`, `completa`; lo actualizará el consumidor del evento de la spec 025, hoy por semilla). `verificado` es verdadero cuando `nivelVerificacion` no es `ninguno`.

## 7. Arquitectura y componentes
- `CotizacionesController` (listado) y `EleccionController` (elección), separados por responsabilidad (S6960).
- `ComparadorDeCotizaciones` es una función pura que ordena y etiqueta; recibe la lista y los datos públicos de los técnicos. Es el punto de mutación.
- `EleccionService` abre una sesión de MongoDB, hace las cuatro escrituras y el buzón, y confirma; si algo falla, no queda nada a medias.
- `VisitaFactory` genera el código (`RandomNumberGenerator`), lo cifra con `CifradoDeCampo` y calcula `codigoVenceEn` (23:59:59 del día de la cita en `America/Mexico_City`, más 24 h).
- `TecnicosStore` del módulo Requests lee solo la proyección pública de `tecnicos` (nombre, foto, calificación, verificación, contacto público); no modifica el perfil.
- El listado no usa caché; las lecturas del técnico se hacen una sola vez por solicitud y se proyectan.

## 8. Dependencias
- Spec 020 (solicitudes y cotizaciones), 003 (transacciones, outbox), 009 (JWT), 010 (sin caché personal), 013 (la foto del técnico viene del almacenamiento público), 017 (la persona).
- Spec 022 lee `visitas` y es la dueña del código, la llegada y el aviso; esta spec solo lo crea.
- Spec 026 abre el chat de la visita con la pertenencia que da `GET /api/v1/visitas/{id}` (spec 022); no necesita eventos para el chat.
- El orden “Recomendadas” con ML (HU-077) sustituye la función pura cuando exista.
- La app del cliente consume el contrato; la del técnico aún no existe.

## 9. Amenazas y controles
| Amenaza | Control | Caso que lo prueba |
|---|---|---|
| Elegir o ver cotizaciones de otra persona | Filtro por `sub` en la solicitud y en la cotización; 404 para el resto | CA-03, CA-12 |
| Dos citas por una solicitud | Índice único `{ solicitudId }` y transacción con estado condicionado | CA-05 |
| Manipular el precio al elegir | Se copia de la cotización guardada; el cliente solo manda el identificador | CA-04 |
| Revelar contacto antes de elegir | El listado proyecta solo datos públicos del perfil; el contacto y la dirección salen en la respuesta de la elección | CA-09 |
| Fuga del código | Se guarda cifrado, no se devuelve aquí y no se registra | CA-10, CA-13 |
| Enumerar solicitudes ajenas | 404 uniforme después de autenticar | CA-03 |
| Repetición o doble toque | El índice único de `visitas` por solicitud y la escritura condicionada impiden una segunda cita; responde 409 | CA-06 |
| Cotización retirada que gana | La transacción exige `estado = enviada` | CA-07 |
| Datos personales en registros | Filtro de datos personales; eventos solo con identificadores | CA-13 |

## 10. Plan de pruebas
| Tipo | ¿Aplica? | Motivo |
|---|---|---|
| Unitarias | Sí | Orden, etiquetas, textos de `cubre`, generación y cifrado del código |
| Integración | Sí | Transacción completa, concurrencia y buzón con MongoDB real |
| Seguridad negativa | Sí | IDOR, token de técnico, cotización retirada, doble elección |
| Contrato | Sí | OpenAPI de Mercado regenerado |
| Arquitectura | Sí | El controller no toca MongoDB; el módulo no escribe en `tecnicos` |
| Mutación | Sí | `ComparadorDeCotizaciones` y la condición de estado de la transacción |
| Rendimiento | No | Máximo cinco cotizaciones por solicitud |
| Aceptación | Sí | CA-01 a CA-18 con la tabla 4.1 |
| Base de datos | Sí | `DatabaseTest`: `visitas` y sus índices (único `{ solicitudId }`) se crean de forma idempotente |
| Regresión | Sí | Las suites de la spec 020 y de las specs 015 a 019 siguen en verde; `dotnet test Knock.slnx -c Release` completo en cada PR |
| CI/CD | Sí | Job `gate` de `ci-pr.yml` (build-test, openapi-diff con `oasdiff`, sast, sca, secrets) en verde; smoke de `/health/ready` tras el despliegue |
| Análisis estático | Sí | Semgrep (job `sast`) sin hallazgos nuevos; analizadores de .NET con advertencias como errores y complejidad máxima 10 |
| Vulnerabilidades y licencias | Sí | NuGetAudit, `dotnet list package --vulnerable`, OSV-Scanner y dependency-review sin altas ni críticas; sin dependencias nuevas |
| DAST | Sí (nocturno) | ZAP de API con token de prueba contra las dos rutas; no bloquea el PR |

**4.1 Criterio → prueba prevista**

| Criterio | Prueba prevista |
|---|---|
| CA-01 | `Spec021ListadoTests.Lista_Cotizaciones_CA_021_01` |
| CA-02 | `Spec021OrdenTests.Orden_Por_Precio_CA_021_02` |
| CA-03 | `Spec021SeguridadTests.Solicitud_Ajena_CA_021_03` |
| CA-04 | `Spec021EleccionTests.Elige_Cotizacion_CA_021_04` |
| CA-05 | `Spec021ConcurrenciaTests.Dos_Elecciones_CA_021_05` |
| CA-06 | `Spec021EleccionTests.Eleccion_Repetida_Responde_409_CA_021_06` |
| CA-07 | `Spec021EleccionTests.Cotizacion_No_Disponible_CA_021_07` |
| CA-08 | `Spec021EleccionTests.Solicitud_No_Elegible_CA_021_08` |
| CA-09 | `Spec021ContactoTests.Revela_Solo_Despues_CA_021_09` |
| CA-10 | `Spec021CodigoTests.Genera_Codigo_Cifrado_CA_021_10` |
| CA-11 | `Spec021ListadoTests.Vencida_Sin_Eleccion_CA_021_11` |
| CA-12 | `Spec021SeguridadTests.Exige_Sesion_Cliente_CA_021_12` |
| CA-13 | `Spec021RegistrosTests.Sin_Datos_Personales_CA_021_13` |
| CA-14 | `Spec021OrdenTests.Recomendadas_Estable_CA_021_14` |
| CA-15 | `Spec021ValidacionTests.Orden_E_Id_Invalidos_CA_021_15` |
| CA-17 | `Spec021EleccionTests.Falla_De_Transaccion_CA_021_17` |
| CA-18 | `Spec021ContactoTests.Listado_Sin_Contacto_CA_021_18` |

**Gates:** los de `AGENTS.md`; contrato de Mercado regenerado y `oasdiff` sin cambios incompatibles; la prueba de concurrencia se corre con dos peticiones reales en paralelo.

## 11. Escenarios
**Positivos**
- Tres cotizaciones de tipos distintos; ordenar por precio, por calificación y por pronto.
- Elegir una cotización a domicilio y ver el contacto público y la dirección.
- Elegir una cotización de taller y ver la dirección del taller, sin código.

**Negativos**
- Elegir una cotización de otra solicitud.
- Elegir dos veces la misma solicitud (la segunda responde 409 `solicitud_ya_elegida`).
- Intentar ver el listado con el token del técnico.
- Solicitud cancelada o vencida.

**Errores esperados**
- 400 por identificador mal formado, 401 sin sesión, 404 por recurso ajeno, 409 por estado, 429 por límite, 500 por fallo de transacción.

**Casos límite**
- Cero cotizaciones; una cotización; cinco; seis (se devuelven cinco y `totalCotizaciones`).
- Empate de tope y de calificación.
- Cotización retirada entre el listado y la elección.
- Elección en el segundo exacto del vencimiento.
- Repetición de la elección sobre una solicitud ya elegida (409 `solicitud_ya_elegida`) y repetición tras un 503 (termina bien).

## 12. Definición de terminado
- Los 18 criterios tienen prueba en verde y la tabla 4.1 coincide con los nombres reales (`python scripts/sdd_lint.py --trace 021` sin faltantes).
- La prueba de concurrencia demuestra una sola cita y un solo conjunto de eventos.
- Las colecciones y los índices se crean de forma idempotente y la prueba de base de datos los compara.
- OpenAPI regenerado, `docs/endpoints.md` actualizado, `Memoria.md` con la entrada.
- Semillas de KNOCK-246 con `contactoPublico` y dirección de taller en al menos un técnico ficticio.
- Job `gate` en verde, Semgrep y análisis de dependencias sin hallazgos altos ni críticos.
- Revisión de par y sin hallazgos de seguridad abiertos.

## 13. Decisiones
- **Una transacción y un índice único:** la promesa “una cita por solicitud” no puede depender de una revisión posterior; la base la garantiza.
- **El código se genera aquí y se muestra en la spec 022:** la elección es el único momento en que se crea la cita; separar la generación de la creación dejaría citas sin código.
- **El código se cifra, no solo se hashea:** el cliente tiene que poder volver a verlo al abrir la visita y sin conexión; con un hash no se podría mostrar. La llave vive en Key Vault y el valor nunca sale en listados ni registros.
- **El contacto público del técnico vive en Mercado:** es un dato de negocio que el técnico decide publicar; el celular personal sigue cifrado en Identidad y no se comparte.
- **`recomendadas` determinista hasta que exista el ML:** sin el modelo, un orden explicable es mejor que uno aleatorio; la función se sustituye sin cambiar el contrato.
- **Visitas al final al ordenar por precio:** una visita no es comparable con un arreglo completo; el aviso de tipos mixtos lo explica.

## 14. Decisiones tomadas sin consulta
1. **Ruta `POST /solicitudes/{id}/elegir` en lugar de `POST /cotizaciones/{id}/aceptar`.** El insumo la propone y mantiene el permiso en la solicitud; alternativa: la ruta de la ficha de HU-033, que obliga a comprobar la solicitud por dentro y permite elegir con un identificador de cotización adivinado.
2. **No se devuelve `distanciaKm`.** Para calcularla haría falta guardar la ubicación del cliente, que la constitución y HU-009 prohíben. Alternativa: calcularla con la colonia, que da una aproximación engañosa.
3. **`recomendadas` sin ML.** El modelo es de Sprint 5; mientras tanto, el orden por verificación y calificación es el que la ficha describe como criterio de confianza.
4. **El teléfono y el WhatsApp del técnico salen de `tecnicos.contactoPublico`, no de Identidad.** Evita una interfaz entre servicios y separa el contacto público del celular personal. Alternativa: pedir el celular a Identidad, que agrega una llamada, un permiso y un riesgo de fuga.
5. **El código no se devuelve en la elección.** La app lo pide a la visita (spec 022); así el código no viaja en la respuesta de una operación que se registra y se reintenta. Alternativa: devolverlo una sola vez y que la app lo guarde; se pierde al reinstalar.
6. **Las cotizaciones retiradas no se listan.** La ficha de HU-035 no está implementada, pero el estado existe; ocultarlas evita ofrecer algo que ya no está.
7. **La visita nace en `confirmada` y su ciclo vive en la spec 022.** Repartir la creación entre dos specs dejaría una ventana sin cita.
8. **Sin repetición idempotente.** Una segunda elección de la misma solicitud responde 409 `solicitud_ya_elegida`; no se guarda ninguna clave (ver §15).

**Agregadas en la revisión del 9 de noviembre**

9. **El listado no se pagina.** El cupo de 5 cotizaciones vivas de la spec 020 lo acota; se documenta como excepción a la spec 010, igual que el catálogo de oficios. Alternativa: cursor sobre 5 elementos, que complica la pantalla sin beneficio.
10. **503 en lugar de 500 cuando la transacción falla.** Es una falla de dependencia y la operación es repetible sin cambiar nada; 500 sugiere un error del programa. Alternativa: 500, que la app trataría como no recuperable.
11. **`motivo` usa los mismos nombres que los errores.** El borrador tenía `sin_cotizaciones` y `ya_elegida` en el listado y `solicitud_sin_cotizaciones` y `solicitud_ya_elegida` en la elección; con un solo nombre la app usa un solo texto.
12. **KNOCK-81 (HU-032) se cubre aquí del lado del backend.** La ficha es de pantalla, pero el orden y las etiquetas son reglas del servidor. Alternativa: una spec aparte solo para el listado, que duplicaría el modelo.

## 15. Riesgos y dudas abiertas
- **`Idempotency-Key` retirada por ahora (decisión del 2026-11-10).** Se quitó de la elección. Sin ella, si la respuesta de una elección exitosa se pierde por la red, el reintento recibe 409 `solicitud_ya_elegida`: la app debe consultar la solicitud (`GET /api/v1/solicitudes/{id}`) antes de mostrar un error, porque la cita ya existe. Se retoma con una spec propia si se decide volver a exigirla.
- **El ML de “Recomendadas” no existe:** el orden es determinista y puede no coincidir con el que el equipo espera para la demo; se sustituye en Sprint 5 sin cambiar el contrato.
- **El contacto público del técnico puede no estar cargado:** si las semillas no lo traen, el cliente solo verá el chat. Hay que sembrarlo en `db/` para la demo.
- **La dirección del cliente se copia a `solicitudes`:** si el cliente la cambia después, la cita conserva la del momento; es lo correcto para el servicio, pero hay que decirlo en la pantalla.
- **Sin app del técnico:** el técnico no puede ver la cita ni el teléfono del cliente; la demo se apoya en semillas y en el cliente.
- **Transacciones de DocumentDB:** la elección falla completa si la instancia no las admite; se comprueba en el despliegue, como en la spec 015.
- **El chat depende de la spec 026:** sin el hub, el cliente y el técnico solo tienen el contacto público y la visita; el evento de la elección queda en el buzón y no se usa para el chat.
- **Etiquetas de Jira:** KNOCK-81 y KNOCK-82 están etiquetadas `servicio-mensajeria`, pero la elección vive en Mercado (módulo Requests) por compartir transacción con la solicitud; conviene corregir las etiquetas.
- **El código de cuatro dígitos tiene 10 000 combinaciones:** el control real es el límite de intentos y el bloqueo de la spec 022, no la entropía.

## Aprobación
- [ ] Revisor 1 — nombre: ________ fecha: ________
- [ ] Sin conflicto con la constitución
- **Estados:** Borrador (agente) → Aprobada (persona; habilita el código) → Implementada (tras el merge)
