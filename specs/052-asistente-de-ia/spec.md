# Spec 052 — Asistente de IA: resumen de chat, borrador de cotización, rango de precio y bitácora

> **Estado:** Borrador · **Repositorio:** knock-backend · **Rector:** `constitution.md`
> **Origen:** `Documentacion/00_Profesores/Desarrollo_Movil/Entregables/Knock_Endpoints_CRUD.xlsx` (revisión 2026-11-02): endpoints ASI-01, ASI-02, ASI-03, ASI-04, ASI-05, ASI-06.
> **Claves de trabajo:** por asignar (se crea la tarea de Jira cuando se decida el alcance) · **Sprint:** por definir · **Prioridad:** por definir
> **Servicio:** Mercado (`Knock.Market.Api`) · **Módulo:** `Assistant` · **Colecciones:** `asistente_ia`
> **Responsable sugerido:** por asignar (Alexis o Guillermo) · Erick consume el contrato (frontend 042)

## 1. Objetivo
Definir y especificar los endpoints ASI-01, ASI-02, ASI-03, ASI-04, ASI-05, ASI-06 del Excel de endpoints para que el equipo los implemente con el mismo estilo, seguridad y pruebas que las specs 020 a 037. Como persona, quiero que un asistente resuma el chat, sugiera un borrador de cotización y un rango de precio, para decidir más rápido.

## 2. Historia y alcance
**2.1 Historia**
- Como persona, quiero que un asistente resuma el chat, sugiera un borrador de cotización y un rango de precio, para decidir más rápido.

**2.2 Dentro**
- `POST /api/v1/asistente/conversaciones` (ASI-01): Un turno con el asistente Knock: clasifica la categoría, pide la foto y arma la solicitud. Si detecta un riesgo, como gas, empieza con las instrucciones de seguridad. Fi….
- `POST /api/v1/asistente/resumen-chat` (ASI-02): Resume la conversación: lo que se sabe, por confirmar y qué llevar.
- `POST /api/v1/asistente/borrador-cotizacion` (ASI-03): Borrador editable de cotización (precio, tiempo, día y nota) que explica qué tomó en cuenta.
- `GET /api/v1/asistente/rango-precio` (ASI-04): Rango de precio sugerido por aprendizaje automático.
- `PUT /api/v1/asistente/bitacora/{solicitudId}` (ASI-05): Guarda el resumen en la bitácora del técnico.
- `GET /api/v1/asistente/bitacora/{solicitudId}` (ASI-06): Lee la bitácora guardada al iniciar el trabajo.
- Los índices y las reglas de cada campo de las colecciones de §6, el límite de peticiones y la documentación del contrato.

**2.3 Fuera**
- La implementación: esta spec solo se escribe; las tareas se activan cuando una persona la apruebe y lo indique.
- Pantallas y textos de la app: los define la spec de frontend 042.
- Endpoints de otras colecciones o de otras specs (020 a 038) que el Excel nombra distinto: se anotan en §15.

## 3. Requisitos
- **R-01:** `POST /api/v1/asistente/conversaciones` (crear): Un turno con el asistente Knock: clasifica la categoría, pide la foto y arma la solicitud. Si detecta un riesgo, como gas, empieza con las instrucciones de seguridad. Filtra datos personales antes del modelo. Lo puede llamar: persona con sesión (cliente o técnico). Responde 200 cuando sale bien.
- **R-02:** Entrada de `POST /api/v1/asistente/conversaciones`: Cuerpo: mensaje, foto opcional, contexto. Un valor inválido responde 422 con `errors.<campo>` sin repetirlo; un cuerpo ilegible, 400.
- **R-03:** `POST /api/v1/asistente/resumen-chat` (crear (acción)): Resume la conversación: lo que se sabe, por confirmar y qué llevar. Lo puede llamar: cuenta con perfil de técnico. Responde 200 cuando sale bien.
- **R-04:** Entrada de `POST /api/v1/asistente/resumen-chat`: Cuerpo: conversacionId. Un valor inválido responde 422 con `errors.<campo>` sin repetirlo; un cuerpo ilegible, 400.
- **R-05:** `POST /api/v1/asistente/borrador-cotizacion` (crear (acción)): Borrador editable de cotización (precio, tiempo, día y nota) que explica qué tomó en cuenta. Lo puede llamar: cuenta con perfil de técnico. Responde 200 cuando sale bien.
- **R-06:** Entrada de `POST /api/v1/asistente/borrador-cotizacion`: Cuerpo: conversacionId. Un valor inválido responde 422 con `errors.<campo>` sin repetirlo; un cuerpo ilegible, 400.
- **R-07:** `GET /api/v1/asistente/rango-precio` (búsqueda específica): Rango de precio sugerido por aprendizaje automático. Lo puede llamar: cuenta con perfil de técnico. Responde 200 cuando sale bien.
- **R-08:** Entrada de `GET /api/v1/asistente/rango-precio`: Query: oficio, tipoTrabajo. Un valor inválido responde 422 con `errors.<campo>` sin repetirlo.
- **R-09:** `GET /api/v1/asistente/rango-precio` se cachea (Sí, 1 hora); la clave nunca incluye datos personales de otra persona y se invalida al escribir.
- **R-10:** `PUT /api/v1/asistente/bitacora/{solicitudId}` (crear / actualizar): Guarda el resumen en la bitácora del técnico. Lo puede llamar: cuenta con perfil de técnico. Responde 200 cuando sale bien.
- **R-11:** Entrada de `PUT /api/v1/asistente/bitacora/{solicitudId}`: Ruta: solicitudId. Cuerpo: sabe, porConfirmar, queLlevar. Un valor inválido responde 422 con `errors.<campo>` sin repetirlo; un cuerpo ilegible, 400.
- **R-12:** Estados de `PUT /api/v1/asistente/bitacora/{solicitudId}`: 201. El código de negocio de cada conflicto va en inglés y en snake_case, y se define al implementar.
- **R-13:** `GET /api/v1/asistente/bitacora/{solicitudId}` (leer uno): Lee la bitácora guardada al iniciar el trabajo. Lo puede llamar: cuenta con perfil de técnico. Responde 200 cuando sale bien.
- **R-14:** Entrada de `GET /api/v1/asistente/bitacora/{solicitudId}`: Ruta: solicitudId. Un valor inválido responde 422 con `errors.<campo>` sin repetirlo.
- **R-15:** Estados de `GET /api/v1/asistente/bitacora/{solicitudId}`: 404. El código de negocio de cada conflicto va en inglés y en snake_case, y se define al implementar.
- **R-16:** La persona de cada operación es siempre la del `sub` del token; ningún cuerpo, ruta o consulta acepta el identificador de otra persona para actuar en su nombre, y un identificador ajeno enviado en el cuerpo se ignora.
- **R-17:** Los errores usan ProblemDetails con `correlationId` y `traceId`; los registros llevan método, ruta, estado y correlación, sin teléfono, dirección, coordenadas, nombres ni textos escritos por la persona (spec 011).
- **R-18:** Por encima del límite de peticiones responde 429 con `Retry-After`; cada ruta tiene su límite registrado en `docs/rate-limit-matrix.md` y los cuerpos se limitan según la spec 009 (413 si lo exceden).
- **R-19:** Las rutas que crean algo no usan `Idempotency-Key` (retirado en la spec 020): la repetición se controla con la regla de estado o el índice único de §6.

## 4. Criterios de aceptación
- **CA-01:** Dado el rol «persona con sesión (cliente o técnico)», cuando llama `POST /api/v1/asistente/conversaciones` con datos válidos, entonces responde 200 con lo descrito en §5.
- **CA-02:** Dada una petición sin token o con token vencido, cuando llama `POST /api/v1/asistente/conversaciones`, entonces responde 401 y no ejecuta la operación.
- **CA-03:** Dado un valor inválido según las reglas de §6, cuando llama `POST /api/v1/asistente/conversaciones`, entonces responde 422 con `errors.<campo>`, no repite el valor y no guarda nada.
- **CA-04:** Dado un cuerpo que no se puede leer, cuando llama `POST /api/v1/asistente/conversaciones`, entonces responde 400 y no guarda nada.
- **CA-05:** Dado el rol «cuenta con perfil de técnico», cuando llama `POST /api/v1/asistente/resumen-chat` con datos válidos, entonces responde 200 con lo descrito en §5.
- **CA-06:** Dada una petición sin token o con token vencido, cuando llama `POST /api/v1/asistente/resumen-chat`, entonces responde 401 y no ejecuta la operación.
- **CA-07:** Dada una persona que no cumple el rol «cuenta con perfil de técnico», cuando llama `POST /api/v1/asistente/resumen-chat`, entonces responde 403 y no cambia nada.
- **CA-08:** Dado un valor inválido según las reglas de §6, cuando llama `POST /api/v1/asistente/resumen-chat`, entonces responde 422 con `errors.<campo>`, no repite el valor y no guarda nada.
- **CA-09:** Dado el rol «cuenta con perfil de técnico», cuando llama `POST /api/v1/asistente/borrador-cotizacion` con datos válidos, entonces responde 200 con lo descrito en §5.
- **CA-10:** Dada una petición sin token o con token vencido, cuando llama `POST /api/v1/asistente/borrador-cotizacion`, entonces responde 401 y no ejecuta la operación.
- **CA-11:** Dada una persona que no cumple el rol «cuenta con perfil de técnico», cuando llama `POST /api/v1/asistente/borrador-cotizacion`, entonces responde 403 y no cambia nada.
- **CA-12:** Dado un valor inválido según las reglas de §6, cuando llama `POST /api/v1/asistente/borrador-cotizacion`, entonces responde 422 con `errors.<campo>`, no repite el valor y no guarda nada.
- **CA-13:** Dado el rol «cuenta con perfil de técnico», cuando llama `GET /api/v1/asistente/rango-precio` con datos válidos, entonces responde 200 con lo descrito en §5.
- **CA-14:** Dada una petición sin token o con token vencido, cuando llama `GET /api/v1/asistente/rango-precio`, entonces responde 401 y no ejecuta la operación.
- **CA-15:** Dada una persona que no cumple el rol «cuenta con perfil de técnico», cuando llama `GET /api/v1/asistente/rango-precio`, entonces responde 403 y no cambia nada.
- **CA-16:** Dado un valor inválido según las reglas de §6, cuando llama `GET /api/v1/asistente/rango-precio`, entonces responde 422 con `errors.<campo>`, no repite el valor y no devuelve datos.
- **CA-17:** Dada una consulta repetida dentro del tiempo de caché, cuando llega la segunda, entonces se responde desde la caché y la base recibe una sola consulta.
- **CA-18:** Dado el rol «cuenta con perfil de técnico», cuando llama `PUT /api/v1/asistente/bitacora/{solicitudId}` con datos válidos, entonces responde 200 con lo descrito en §5.
- **CA-19:** Dada una petición sin token o con token vencido, cuando llama `PUT /api/v1/asistente/bitacora/{solicitudId}`, entonces responde 401 y no ejecuta la operación.
- **CA-20:** Dada una persona que no cumple el rol «cuenta con perfil de técnico», cuando llama `PUT /api/v1/asistente/bitacora/{solicitudId}`, entonces responde 403 y no cambia nada.
- **CA-21:** Dado un valor inválido según las reglas de §6, cuando llama `PUT /api/v1/asistente/bitacora/{solicitudId}`, entonces responde 422 con `errors.<campo>`, no repite el valor y no guarda nada.
- **CA-22:** Dado un cuerpo que no se puede leer, cuando llama `PUT /api/v1/asistente/bitacora/{solicitudId}`, entonces responde 400 y no guarda nada.
- **CA-23:** Dado el caso «recurso no disponible», cuando llama `PUT /api/v1/asistente/bitacora/{solicitudId}`, entonces responde 201 con ProblemDetails y no cambia el estado.
- **CA-24:** Dado el rol «cuenta con perfil de técnico», cuando llama `GET /api/v1/asistente/bitacora/{solicitudId}` con datos válidos, entonces responde 200 con lo descrito en §5.
- **CA-25:** Dada una petición sin token o con token vencido, cuando llama `GET /api/v1/asistente/bitacora/{solicitudId}`, entonces responde 401 y no ejecuta la operación.
- **CA-26:** Dada una persona que no cumple el rol «cuenta con perfil de técnico», cuando llama `GET /api/v1/asistente/bitacora/{solicitudId}`, entonces responde 403 y no cambia nada.
- **CA-27:** Dado un valor inválido según las reglas de §6, cuando llama `GET /api/v1/asistente/bitacora/{solicitudId}`, entonces responde 422 con `errors.<campo>`, no repite el valor y no devuelve datos.
- **CA-28:** Dado el caso «recurso no disponible», cuando llama `GET /api/v1/asistente/bitacora/{solicitudId}`, entonces responde 404 con ProblemDetails y no cambia el estado.
- **CA-29:** Dado un cuerpo con el identificador de otra persona, cuando se llama cualquier ruta de esta spec, entonces la operación usa el `sub` del token y no el del cuerpo.
- **CA-30:** Dada una operación con datos personales, cuando se inspeccionan los registros y las respuestas de error, entonces no aparecen esos datos.
- **CA-31:** Dada una persona que supera el límite de una ruta, cuando repite la petición, entonces responde 429 con `Retry-After` y, con un cuerpo de tamaño excesivo, 413.
- **CA-32:** Dada una operación de creación repetida por mala señal, cuando llega dos veces, entonces no deja duplicados porque lo impide la regla de estado o el índice único.

**4.1 Criterio → prueba prevista**

| Criterio | Prueba prevista |
|---|---|
| CA-01 | `Spec052Tests.Criterio01_CA_052_01` |
| CA-02 | `Spec052Tests.Criterio02_CA_052_02` |
| CA-03 | `Spec052Tests.Criterio03_CA_052_03` |
| CA-04 | `Spec052Tests.Criterio04_CA_052_04` |
| CA-05 | `Spec052Tests.Criterio05_CA_052_05` |
| CA-06 | `Spec052Tests.Criterio06_CA_052_06` |
| CA-07 | `Spec052Tests.Criterio07_CA_052_07` |
| CA-08 | `Spec052Tests.Criterio08_CA_052_08` |
| CA-09 | `Spec052Tests.Criterio09_CA_052_09` |
| CA-10 | `Spec052Tests.Criterio10_CA_052_10` |
| CA-11 | `Spec052Tests.Criterio11_CA_052_11` |
| CA-12 | `Spec052Tests.Criterio12_CA_052_12` |
| CA-13 | `Spec052Tests.Criterio13_CA_052_13` |
| CA-14 | `Spec052Tests.Criterio14_CA_052_14` |
| CA-15 | `Spec052Tests.Criterio15_CA_052_15` |
| CA-16 | `Spec052Tests.Criterio16_CA_052_16` |
| CA-17 | `Spec052Tests.Criterio17_CA_052_17` |
| CA-18 | `Spec052Tests.Criterio18_CA_052_18` |
| CA-19 | `Spec052Tests.Criterio19_CA_052_19` |
| CA-20 | `Spec052Tests.Criterio20_CA_052_20` |
| CA-21 | `Spec052Tests.Criterio21_CA_052_21` |
| CA-22 | `Spec052Tests.Criterio22_CA_052_22` |
| CA-23 | `Spec052Tests.Criterio23_CA_052_23` |
| CA-24 | `Spec052Tests.Criterio24_CA_052_24` |
| CA-25 | `Spec052Tests.Criterio25_CA_052_25` |
| CA-26 | `Spec052Tests.Criterio26_CA_052_26` |
| CA-27 | `Spec052Tests.Criterio27_CA_052_27` |
| CA-28 | `Spec052Tests.Criterio28_CA_052_28` |
| CA-29 | `Spec052Tests.Criterio29_CA_052_29` |
| CA-30 | `Spec052Tests.Criterio30_CA_052_30` |
| CA-31 | `Spec052Tests.Criterio31_CA_052_31` |
| CA-32 | `Spec052Tests.Criterio32_CA_052_32` |

## 5. Contrato
| ID | Método y ruta | Quién | Entrada | Respuestas |
|---|---|---|---|---|
| ASI-01 | `POST /api/v1/asistente/conversaciones` | persona con sesión (cliente o técnico) | Cuerpo: mensaje, foto opcional, contexto | 200; 400; 401; 422; 429 (límite de gasto) |
| ASI-02 | `POST /api/v1/asistente/resumen-chat` | cuenta con perfil de técnico | Cuerpo: conversacionId | 200; 401; 403; 429 |
| ASI-03 | `POST /api/v1/asistente/borrador-cotizacion` | cuenta con perfil de técnico | Cuerpo: conversacionId | 200; 401; 403; 429 |
| ASI-04 | `GET /api/v1/asistente/rango-precio` | cuenta con perfil de técnico | Query: oficio, tipoTrabajo | 200; 401; 403 |
| ASI-05 | `PUT /api/v1/asistente/bitacora/{solicitudId}` | cuenta con perfil de técnico | Ruta: solicitudId. Cuerpo: sabe, porConfirmar, queLlevar | 200; 201; 400; 401; 403 |
| ASI-06 | `GET /api/v1/asistente/bitacora/{solicitudId}` | cuenta con perfil de técnico | Ruta: solicitudId | 200; 401; 403; 404 |

**Filtros, orden, paginación y caché**

| ID | Filtros y orden | Paginación | Caché |
|---|---|---|---|
| ASI-01 | No aplica | No | No |
| ASI-02 | No aplica | No | No |
| ASI-03 | No aplica | No | No |
| ASI-04 | No aplica | No | Sí, 1 hora |
| ASI-05 | No aplica | No | No |
| ASI-06 | No aplica | No | No |

**Códigos de error:** los de forma salen como 422 con `errors` por campo y los de sesión y límite como 401 y 429; los conflictos de negocio llevan un código en inglés y snake_case que se fija al implementar y se agrega al catálogo.

## 6. Modelo de datos
**Colección `asistente_ia`.** Reúne las funciones del módulo Asistente, que vive dentro del servicio de Mercado, y dos colecciones de apoyo. El asistente Knock ayuda a quien no sabe qué oficio necesita: pregunta qué pasa y desde cuándo, exige una foto, clasifica la categoría y arma la solicitud. También resume el chat para el técnico y propone un borrador de cotización, y el aprendizaje automático da el orden Recomendadas y u…

| Campo | Tipo | Obligatorio | Reglas y validaciones | Índice |
|---|---|---|---|---|
| bitacoras._id | ObjectId | Sí | Lo genera la base | Llave primaria |
| bitacoras.solicitudId | ObjectId | Sí | Debe existir | Compuesto |
| bitacoras.tecnicoId | ObjectId | Sí | Debe existir | Compuesto |
| bitacoras.sabe | Lista de textos | Sí |  | No |
| bitacoras.porConfirmar | Lista de textos | Sí |  | No |
| bitacoras.queLlevar | Lista de textos | Sí |  | No |
| bitacoras.creadoEn | Fecha y hora UTC | Sí | Lo asigna el servidor | No |
| uso_ia.usuarioId | ObjectId | Sí | Debe existir | Compuesto único |
| uso_ia.dia | Fecha | Sí |  | Compuesto único |
| uso_ia.unidades | Entero | Sí | No puede pasar el límite diario | No |

| Tipo | Campos | Para qué sirve |
|---|---|---|
| Llave principal | _id | Identifica de forma única cada documento. La genera la base de datos |
| Único compuesto | bitacoras: solicitudId + tecnicoId | Una bitácora por solicitud y técnico |
| Único compuesto | uso_ia: usuarioId + dia | Un registro de consumo por persona y día |

## 7. Arquitectura y componentes
- Proyecto `Knock.Market.Api`, módulo `Assistant` con las capas del estilo de la spec 002: `Controllers`, `Dtos`, `Validators`, `Services`, `Data` y `Hosting`.
- Cada ruta recibe sus parámetros en objetos tipados con validador registrado, nunca como `string` suelto, y consulta con filtros por el `sub` del token.
- Las rutas bajo `/admin` usan la política de rol de la spec 033 y no están disponibles sin la sesión del panel.
- La caché, la paginación por cursor y los límites usan las piezas de las specs 009 y 010.
- El contrato se publica en OpenAPI y en `docs/endpoints.md`; los límites, en `docs/rate-limit-matrix.md`.

## 8. Dependencias
- Spec 024 de este repositorio.
- Spec 030 de este repositorio.
- Spec 031 de este repositorio.
- Specs 002, 003, 009, 010 y 011 (estilo, bases de datos, seguridad, caché y registros).
- Frontend: spec 042 consume este contrato.

## 9. Amenazas y controles
| Amenaza | Control | Casos que lo prueban |
|---|---|---|
| Acceso a datos de otra persona (IDOR) | Toda consulta y escritura filtra por el `sub` del token; ajeno responde 403 o 404 | CA-07, CA-11, CA-15, CA-20, CA-26, CA-29 |
| Asignación masiva o identificador ajeno en el cuerpo | El DTO no acepta identificadores de persona; se ignoran | CA-07, CA-11, CA-15, CA-20, CA-26, CA-29 |
| Inyección en filtros y parámetros | Parámetros tipados con validador; nada se concatena en consultas | CA-03 a CA-04, CA-08, CA-12, CA-16, CA-21 a CA-22, CA-27 |
| Ruta sin sesión | JWT obligatorio salvo las públicas | CA-02, CA-06, CA-10, CA-14, CA-19, CA-25 |
| Abuso por repetición o cuerpos grandes | Límite de peticiones, `Retry-After` y tope de cuerpo | CA-31 |
| Datos personales en registros y errores | Registros sin datos personales; ProblemDetails sin valores | CA-30 |
| Duplicados por reintento | Regla de estado o índice único, sin `Idempotency-Key` | CA-32 |

## 10. Plan de pruebas
Las pruebas citan el caso como `CA_052_NN` en el nombre (para `python scripts/sdd_lint.py --trace 052`).

| Tipo | ¿Aplica? | Qué se prueba / herramienta |
|---|---|---|
| Unitarias | Sí | Validadores, reglas de estado y cálculos del dominio |
| Integración | Sí | Cada ruta con la base real en contenedor (Testcontainers) y `MarketApiFactory`/fábrica del servicio |
| Seguridad negativa | Sí | 401, 403/404, 422, 413 y 429 (CA-02 a CA-04, CA-06 a CA-08, CA-10 a CA-12, CA-14 a CA-16, CA-19 a CA-22, CA-25 a CA-27, CA-29, CA-31) |
| Base de datos | Sí | Índices y reglas de cada colección de §6 |
| Contrato | Sí | OpenAPI regenerado y comparado con `contracts/` |
| Mutación | Sí | Meta del módulo según la spec 008 |
| Rendimiento | No aplica | Sin ruta crítica; se mide solo si la spec 034 lo pide |

## 11. Escenarios
- **ASI-01** — pantallas: Asistente Knock (No sé cuál).
- **ASI-02** — pantallas: Mensajes del técnico (Resumir la conversación).
- **ASI-03** — pantallas: Chat del técnico (Armar cotización con IA).
- **ASI-04** — pantallas: Cotizar.
- **ASI-05** — pantallas: Chat del técnico (Guardar en bitácora).
- **ASI-06** — pantallas: Iniciar trabajo.

## 12. Definición de terminado
- Los criterios de aceptación están cubiertos por pruebas en verde y `sdd_lint --trace 052` sin faltantes.
- El CI en verde, un par revisó el cambio y `Memoria.md` al día.
- OpenAPI, `docs/endpoints.md` y `docs/rate-limit-matrix.md` actualizados.

## 13. Decisiones
- La paginación, la caché y los roles de cada ruta salen de la hoja de cada colección del Excel de endpoints (revisión 2026-11-02).
- Las rutas de la persona viven bajo `/me` o por identificador con dueño verificado; las del personal interno, bajo `/admin`.

## 14. Decisiones tomadas sin consulta
- **D-01:** Se toma el Excel de endpoints como fuente del contrato porque es el insumo acordado para las specs; cualquier diferencia con las specs 020 a 038 se resuelve a favor de la spec numerada.
- **D-02:** Los conflictos de negocio llevan códigos en inglés y snake_case definidos al implementar, siguiendo la convención de errores de la spec 002.
- **D-03:** No se usa `Idempotency-Key`, aunque el Excel lo menciona en creaciones.

## 15. Riesgos y dudas abiertas
- Depende del cliente de Azure OpenAI y sus límites por persona y por día (spec 024) y del filtro de datos personales: no se activa sin el recurso aprobado.
- Confirmar qué datos del chat pueden enviarse al modelo (sin teléfono ni dirección).
- La bitácora por solicitud guarda texto generado: definir retención.
- Esta spec se generó a partir del Excel; antes de aprobarla hay que revisar los límites, los textos de error y los plazos con el equipo.

## Aprobación
- [ ] Revisor 1 — nombre: _____________________ fecha: __________
- [ ] Sin conflicto con la constitución
- **Estados:** Borrador → Aprobada (una persona; habilita el código) → Implementada (tras el merge)
