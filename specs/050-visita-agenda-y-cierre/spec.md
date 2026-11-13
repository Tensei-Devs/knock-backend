# Spec 050 — Visita: agenda, reprogramación, cancelación, cliente ausente, cierre y permiso de fotos

> **Estado:** Borrador · **Repositorio:** knock-backend · **Rector:** `constitution.md`
> **Origen:** `Documentacion/00_Profesores/Desarrollo_Movil/Entregables/Knock_Endpoints_CRUD.xlsx` (revisión 2026-11-02): endpoints VIS-02, VIS-04, VIS-05, VIS-06, VIS-07, VIS-09, VIS-10, VIS-11.
> **Claves de trabajo:** por asignar (se crea la tarea de Jira cuando se decida el alcance) · **Sprint:** por definir · **Prioridad:** por definir
> **Servicio:** Mercado (`Knock.Market.Api`) · **Módulo:** `Visits` · **Colecciones:** `visitas`
> **Responsable sugerido:** por asignar (Alexis o Guillermo) · Erick consume el contrato (frontend 040)

## 1. Objetivo
Definir y especificar los endpoints VIS-02, VIS-04, VIS-05, VIS-06, VIS-07, VIS-09, VIS-10, VIS-11 del Excel de endpoints para que el equipo los implemente con el mismo estilo, seguridad y pruebas que las specs 020 a 037. Como cliente o técnico, quiero ver mi agenda y poder reprogramar, cancelar, marcar ausencia, terminar o dejar el trabajo sin terminar, para cerrar cada visita con claridad.

## 2. Historia y alcance
**2.1 Historia**
- Como cliente o técnico, quiero ver mi agenda y poder reprogramar, cancelar, marcar ausencia, terminar o dejar el trabajo sin terminar, para cerrar cada visita con claridad.

**2.2 Dentro**
- `GET /api/v1/agenda` (VIS-02): Agenda del técnico: próximas visitas y solicitudes nuevas (Mi agenda).
- `POST /api/v1/visitas/{id}/reprogramar` (VIS-04): Una parte propone otra fecha.
- `POST /api/v1/visitas/{id}/reprogramar/respuesta` (VIS-05): La otra parte confirma o rechaza.
- `POST /api/v1/visitas/{id}/cancelar` (VIS-06): Cancela con motivo obligatorio. Gratis con más de 24 h; menos queda como cancelación tardía y se avisa de inmediato.
- `POST /api/v1/visitas/{id}/cliente-ausente` (VIS-07): El técnico registra que el cliente no estaba, sin penalizar. Solo dentro de la ventana de la cita.
- `POST /api/v1/visitas/{id}/terminar` (VIS-09): El técnico termina con fotos (mínimo 1) y qué hizo. No hay confirmación del cliente.
- `POST /api/v1/visitas/{id}/no-terminado` (VIS-10): El cliente reporta que no se terminó (hasta 7 días después).
- `PUT /api/v1/visitas/{id}/permiso-fotos` (VIS-11): El cliente autoriza o no publicar las fotos del trabajo.
- Los índices y las reglas de cada campo de las colecciones de §6, el límite de peticiones y la documentación del contrato.

**2.3 Fuera**
- La implementación: esta spec solo se escribe; las tareas se activan cuando una persona la apruebe y lo indique.
- Pantallas y textos de la app: los define la spec de frontend 040.
- Endpoints de otras colecciones o de otras specs (020 a 038) que el Excel nombra distinto: se anotan en §15.

## 3. Requisitos
- **R-01:** `GET /api/v1/agenda` (leer todos / filtro): Agenda del técnico: próximas visitas y solicitudes nuevas (Mi agenda). Lo puede llamar: cuenta con perfil de técnico. Responde 200 cuando sale bien.
- **R-02:** Entrada de `GET /api/v1/agenda`: Query: desde, hasta. Un valor inválido responde 422 con `errors.<campo>` sin repetirlo.
- **R-03:** `GET /api/v1/agenda` pagina por cursor (Por cursor, 20): parámetros `cursor` y `limite`, 20 por defecto y 50 como máximo; orden: Filtro por fechas; orden por hora.
- **R-04:** `POST /api/v1/visitas/{id}/reprogramar` (actualizar (acción)): Una parte propone otra fecha. Lo puede llamar: persona con sesión que sea participante del recurso. Responde 200 cuando sale bien.
- **R-05:** Entrada de `POST /api/v1/visitas/{id}/reprogramar`: Ruta: id. Cuerpo: fechaHora. Un valor inválido responde 422 con `errors.<campo>` sin repetirlo; un cuerpo ilegible, 400. Reglas: `fechaHora`: Futuro al crearse.
- **R-06:** Estados de `POST /api/v1/visitas/{id}/reprogramar`: 409. El código de negocio de cada conflicto va en inglés y en snake_case, y se define al implementar.
- **R-07:** `POST /api/v1/visitas/{id}/reprogramar/respuesta` (actualizar (acción)): La otra parte confirma o rechaza. Lo puede llamar: persona con sesión que sea participante del recurso. Responde 200 cuando sale bien.
- **R-08:** Entrada de `POST /api/v1/visitas/{id}/reprogramar/respuesta`: Ruta: id. Cuerpo: acepta. Un valor inválido responde 422 con `errors.<campo>` sin repetirlo; un cuerpo ilegible, 400.
- **R-09:** Estados de `POST /api/v1/visitas/{id}/reprogramar/respuesta`: 409. El código de negocio de cada conflicto va en inglés y en snake_case, y se define al implementar.
- **R-10:** `POST /api/v1/visitas/{id}/cancelar` (actualizar (acción)): Cancela con motivo obligatorio. Gratis con más de 24 h; menos queda como cancelación tardía y se avisa de inmediato. Lo puede llamar: persona con sesión que sea participante del recurso. Responde 200 cuando sale bien.
- **R-11:** Entrada de `POST /api/v1/visitas/{id}/cancelar`: Ruta: id. Cuerpo: motivo. Un valor inválido responde 422 con `errors.<campo>` sin repetirlo; un cuerpo ilegible, 400.
- **R-12:** Estados de `POST /api/v1/visitas/{id}/cancelar`: 409. El código de negocio de cada conflicto va en inglés y en snake_case, y se define al implementar.
- **R-13:** `POST /api/v1/visitas/{id}/cliente-ausente` (actualizar (acción)): El técnico registra que el cliente no estaba, sin penalizar. Solo dentro de la ventana de la cita. Lo puede llamar: cuenta con perfil de técnico. Responde 200 cuando sale bien.
- **R-14:** Entrada de `POST /api/v1/visitas/{id}/cliente-ausente`: Ruta: id. Un valor inválido responde 422 con `errors.<campo>` sin repetirlo; un cuerpo ilegible, 400.
- **R-15:** Estados de `POST /api/v1/visitas/{id}/cliente-ausente`: 409. El código de negocio de cada conflicto va en inglés y en snake_case, y se define al implementar.
- **R-16:** `POST /api/v1/visitas/{id}/terminar` (actualizar (acción)): El técnico termina con fotos (mínimo 1) y qué hizo. No hay confirmación del cliente. Lo puede llamar: cuenta con perfil de técnico. Responde 200 cuando sale bien.
- **R-17:** Entrada de `POST /api/v1/visitas/{id}/terminar`: Ruta: id. Cuerpo: descripcion, recomendacion, fotos. Un valor inválido responde 422 con `errors.<campo>` sin repetirlo; un cuerpo ilegible, 400.
- **R-18:** Estados de `POST /api/v1/visitas/{id}/terminar`: 409. El código de negocio de cada conflicto va en inglés y en snake_case, y se define al implementar.
- **R-19:** `POST /api/v1/visitas/{id}/no-terminado` (crear (acción)): El cliente reporta que no se terminó (hasta 7 días después). Lo puede llamar: persona con sesión que sea dueña del recurso. Responde 201 cuando sale bien.
- **R-20:** Entrada de `POST /api/v1/visitas/{id}/no-terminado`: Ruta: id. Cuerpo: texto. Un valor inválido responde 422 con `errors.<campo>` sin repetirlo; un cuerpo ilegible, 400.
- **R-21:** Estados de `POST /api/v1/visitas/{id}/no-terminado`: 409. El código de negocio de cada conflicto va en inglés y en snake_case, y se define al implementar.
- **R-22:** `PUT /api/v1/visitas/{id}/permiso-fotos` (actualizar): El cliente autoriza o no publicar las fotos del trabajo. Lo puede llamar: persona con sesión que sea dueña del recurso. Responde 200 cuando sale bien.
- **R-23:** Entrada de `PUT /api/v1/visitas/{id}/permiso-fotos`: Ruta: id. Cuerpo: permiso. Un valor inválido responde 422 con `errors.<campo>` sin repetirlo; un cuerpo ilegible, 400.
- **R-24:** La persona de cada operación es siempre la del `sub` del token; ningún cuerpo, ruta o consulta acepta el identificador de otra persona para actuar en su nombre, y un identificador ajeno enviado en el cuerpo se ignora.
- **R-25:** Los errores usan ProblemDetails con `correlationId` y `traceId`; los registros llevan método, ruta, estado y correlación, sin teléfono, dirección, coordenadas, nombres ni textos escritos por la persona (spec 011).
- **R-26:** Por encima del límite de peticiones responde 429 con `Retry-After`; cada ruta tiene su límite registrado en `docs/rate-limit-matrix.md` y los cuerpos se limitan según la spec 009 (413 si lo exceden).
- **R-27:** Las rutas que crean algo no usan `Idempotency-Key` (retirado en la spec 020): la repetición se controla con la regla de estado o el índice único de §6.

## 4. Criterios de aceptación
- **CA-01:** Dado el rol «cuenta con perfil de técnico», cuando llama `GET /api/v1/agenda` con datos válidos, entonces responde 200 con lo descrito en §5.
- **CA-02:** Dada una petición sin token o con token vencido, cuando llama `GET /api/v1/agenda`, entonces responde 401 y no ejecuta la operación.
- **CA-03:** Dada una persona que no cumple el rol «cuenta con perfil de técnico», cuando llama `GET /api/v1/agenda`, entonces responde 403 y no cambia nada.
- **CA-04:** Dado un valor inválido según las reglas de §6, cuando llama `GET /api/v1/agenda`, entonces responde 422 con `errors.<campo>`, no repite el valor y no devuelve datos.
- **CA-05:** Dado un listado con más de 20 elementos, cuando se pide sin `limite`, entonces devuelve 20 y el cursor de la página siguiente; con `limite=51` responde 422.
- **CA-06:** Dado el rol «persona con sesión que sea participante del recurso», cuando llama `POST /api/v1/visitas/{id}/reprogramar` con datos válidos, entonces responde 200 con lo descrito en §5.
- **CA-07:** Dada una petición sin token o con token vencido, cuando llama `POST /api/v1/visitas/{id}/reprogramar`, entonces responde 401 y no ejecuta la operación.
- **CA-08:** Dada una persona que no cumple el rol «persona con sesión que sea participante del recurso», cuando llama `POST /api/v1/visitas/{id}/reprogramar`, entonces responde 403 y no cambia nada.
- **CA-09:** Dado un valor inválido según las reglas de §6, cuando llama `POST /api/v1/visitas/{id}/reprogramar`, entonces responde 422 con `errors.<campo>`, no repite el valor y no guarda nada.
- **CA-10:** Dado un cuerpo que no se puede leer, cuando llama `POST /api/v1/visitas/{id}/reprogramar`, entonces responde 400 y no guarda nada.
- **CA-11:** Dado el caso «recurso no disponible», cuando llama `POST /api/v1/visitas/{id}/reprogramar`, entonces responde 409 con ProblemDetails y no cambia el estado.
- **CA-12:** Dado el rol «persona con sesión que sea participante del recurso», cuando llama `POST /api/v1/visitas/{id}/reprogramar/respuesta` con datos válidos, entonces responde 200 con lo descrito en §5.
- **CA-13:** Dada una petición sin token o con token vencido, cuando llama `POST /api/v1/visitas/{id}/reprogramar/respuesta`, entonces responde 401 y no ejecuta la operación.
- **CA-14:** Dada una persona que no cumple el rol «persona con sesión que sea participante del recurso», cuando llama `POST /api/v1/visitas/{id}/reprogramar/respuesta`, entonces responde 403 y no cambia nada.
- **CA-15:** Dado un valor inválido según las reglas de §6, cuando llama `POST /api/v1/visitas/{id}/reprogramar/respuesta`, entonces responde 422 con `errors.<campo>`, no repite el valor y no guarda nada.
- **CA-16:** Dado un cuerpo que no se puede leer, cuando llama `POST /api/v1/visitas/{id}/reprogramar/respuesta`, entonces responde 400 y no guarda nada.
- **CA-17:** Dado el caso «recurso no disponible», cuando llama `POST /api/v1/visitas/{id}/reprogramar/respuesta`, entonces responde 409 con ProblemDetails y no cambia el estado.
- **CA-18:** Dado el rol «persona con sesión que sea participante del recurso», cuando llama `POST /api/v1/visitas/{id}/cancelar` con datos válidos, entonces responde 200 con lo descrito en §5.
- **CA-19:** Dada una petición sin token o con token vencido, cuando llama `POST /api/v1/visitas/{id}/cancelar`, entonces responde 401 y no ejecuta la operación.
- **CA-20:** Dada una persona que no cumple el rol «persona con sesión que sea participante del recurso», cuando llama `POST /api/v1/visitas/{id}/cancelar`, entonces responde 403 y no cambia nada.
- **CA-21:** Dado un valor inválido según las reglas de §6, cuando llama `POST /api/v1/visitas/{id}/cancelar`, entonces responde 422 con `errors.<campo>`, no repite el valor y no guarda nada.
- **CA-22:** Dado un cuerpo que no se puede leer, cuando llama `POST /api/v1/visitas/{id}/cancelar`, entonces responde 400 y no guarda nada.
- **CA-23:** Dado el caso «recurso no disponible», cuando llama `POST /api/v1/visitas/{id}/cancelar`, entonces responde 409 con ProblemDetails y no cambia el estado.
- **CA-24:** Dado el rol «cuenta con perfil de técnico», cuando llama `POST /api/v1/visitas/{id}/cliente-ausente` con datos válidos, entonces responde 200 con lo descrito en §5.
- **CA-25:** Dada una petición sin token o con token vencido, cuando llama `POST /api/v1/visitas/{id}/cliente-ausente`, entonces responde 401 y no ejecuta la operación.
- **CA-26:** Dada una persona que no cumple el rol «cuenta con perfil de técnico», cuando llama `POST /api/v1/visitas/{id}/cliente-ausente`, entonces responde 403 y no cambia nada.
- **CA-27:** Dado un valor inválido según las reglas de §6, cuando llama `POST /api/v1/visitas/{id}/cliente-ausente`, entonces responde 422 con `errors.<campo>`, no repite el valor y no guarda nada.
- **CA-28:** Dado el caso «recurso no disponible», cuando llama `POST /api/v1/visitas/{id}/cliente-ausente`, entonces responde 409 con ProblemDetails y no cambia el estado.
- **CA-29:** Dado el rol «cuenta con perfil de técnico», cuando llama `POST /api/v1/visitas/{id}/terminar` con datos válidos, entonces responde 200 con lo descrito en §5.
- **CA-30:** Dada una petición sin token o con token vencido, cuando llama `POST /api/v1/visitas/{id}/terminar`, entonces responde 401 y no ejecuta la operación.
- **CA-31:** Dada una persona que no cumple el rol «cuenta con perfil de técnico», cuando llama `POST /api/v1/visitas/{id}/terminar`, entonces responde 403 y no cambia nada.
- **CA-32:** Dado un valor inválido según las reglas de §6, cuando llama `POST /api/v1/visitas/{id}/terminar`, entonces responde 422 con `errors.<campo>`, no repite el valor y no guarda nada.
- **CA-33:** Dado un cuerpo que no se puede leer, cuando llama `POST /api/v1/visitas/{id}/terminar`, entonces responde 400 y no guarda nada.
- **CA-34:** Dado el caso «recurso no disponible», cuando llama `POST /api/v1/visitas/{id}/terminar`, entonces responde 409 con ProblemDetails y no cambia el estado.
- **CA-35:** Dado el rol «persona con sesión que sea dueña del recurso», cuando llama `POST /api/v1/visitas/{id}/no-terminado` con datos válidos, entonces responde 201 con lo descrito en §5.
- **CA-36:** Dada una petición sin token o con token vencido, cuando llama `POST /api/v1/visitas/{id}/no-terminado`, entonces responde 401 y no ejecuta la operación.
- **CA-37:** Dada una persona que no cumple el rol «persona con sesión que sea dueña del recurso», cuando llama `POST /api/v1/visitas/{id}/no-terminado`, entonces responde 403 y no cambia nada.
- **CA-38:** Dado un valor inválido según las reglas de §6, cuando llama `POST /api/v1/visitas/{id}/no-terminado`, entonces responde 422 con `errors.<campo>`, no repite el valor y no guarda nada.
- **CA-39:** Dado un cuerpo que no se puede leer, cuando llama `POST /api/v1/visitas/{id}/no-terminado`, entonces responde 400 y no guarda nada.
- **CA-40:** Dado el caso «recurso no disponible», cuando llama `POST /api/v1/visitas/{id}/no-terminado`, entonces responde 409 con ProblemDetails y no cambia el estado.
- **CA-41:** Dado el rol «persona con sesión que sea dueña del recurso», cuando llama `PUT /api/v1/visitas/{id}/permiso-fotos` con datos válidos, entonces responde 200 con lo descrito en §5.
- **CA-42:** Dada una petición sin token o con token vencido, cuando llama `PUT /api/v1/visitas/{id}/permiso-fotos`, entonces responde 401 y no ejecuta la operación.
- **CA-43:** Dada una persona que no cumple el rol «persona con sesión que sea dueña del recurso», cuando llama `PUT /api/v1/visitas/{id}/permiso-fotos`, entonces responde 403 y no cambia nada.
- **CA-44:** Dado un valor inválido según las reglas de §6, cuando llama `PUT /api/v1/visitas/{id}/permiso-fotos`, entonces responde 422 con `errors.<campo>`, no repite el valor y no guarda nada.
- **CA-45:** Dado un cuerpo con el identificador de otra persona, cuando se llama cualquier ruta de esta spec, entonces la operación usa el `sub` del token y no el del cuerpo.
- **CA-46:** Dada una operación con datos personales, cuando se inspeccionan los registros y las respuestas de error, entonces no aparecen esos datos.
- **CA-47:** Dada una persona que supera el límite de una ruta, cuando repite la petición, entonces responde 429 con `Retry-After` y, con un cuerpo de tamaño excesivo, 413.
- **CA-48:** Dada una operación de creación repetida por mala señal, cuando llega dos veces, entonces no deja duplicados porque lo impide la regla de estado o el índice único.

**4.1 Criterio → prueba prevista**

| Criterio | Prueba prevista |
|---|---|
| CA-01 | `Spec050Tests.Criterio01_CA_050_01` |
| CA-02 | `Spec050Tests.Criterio02_CA_050_02` |
| CA-03 | `Spec050Tests.Criterio03_CA_050_03` |
| CA-04 | `Spec050Tests.Criterio04_CA_050_04` |
| CA-05 | `Spec050Tests.Criterio05_CA_050_05` |
| CA-06 | `Spec050Tests.Criterio06_CA_050_06` |
| CA-07 | `Spec050Tests.Criterio07_CA_050_07` |
| CA-08 | `Spec050Tests.Criterio08_CA_050_08` |
| CA-09 | `Spec050Tests.Criterio09_CA_050_09` |
| CA-10 | `Spec050Tests.Criterio10_CA_050_10` |
| CA-11 | `Spec050Tests.Criterio11_CA_050_11` |
| CA-12 | `Spec050Tests.Criterio12_CA_050_12` |
| CA-13 | `Spec050Tests.Criterio13_CA_050_13` |
| CA-14 | `Spec050Tests.Criterio14_CA_050_14` |
| CA-15 | `Spec050Tests.Criterio15_CA_050_15` |
| CA-16 | `Spec050Tests.Criterio16_CA_050_16` |
| CA-17 | `Spec050Tests.Criterio17_CA_050_17` |
| CA-18 | `Spec050Tests.Criterio18_CA_050_18` |
| CA-19 | `Spec050Tests.Criterio19_CA_050_19` |
| CA-20 | `Spec050Tests.Criterio20_CA_050_20` |
| CA-21 | `Spec050Tests.Criterio21_CA_050_21` |
| CA-22 | `Spec050Tests.Criterio22_CA_050_22` |
| CA-23 | `Spec050Tests.Criterio23_CA_050_23` |
| CA-24 | `Spec050Tests.Criterio24_CA_050_24` |
| CA-25 | `Spec050Tests.Criterio25_CA_050_25` |
| CA-26 | `Spec050Tests.Criterio26_CA_050_26` |
| CA-27 | `Spec050Tests.Criterio27_CA_050_27` |
| CA-28 | `Spec050Tests.Criterio28_CA_050_28` |
| CA-29 | `Spec050Tests.Criterio29_CA_050_29` |
| CA-30 | `Spec050Tests.Criterio30_CA_050_30` |
| CA-31 | `Spec050Tests.Criterio31_CA_050_31` |
| CA-32 | `Spec050Tests.Criterio32_CA_050_32` |
| CA-33 | `Spec050Tests.Criterio33_CA_050_33` |
| CA-34 | `Spec050Tests.Criterio34_CA_050_34` |
| CA-35 | `Spec050Tests.Criterio35_CA_050_35` |
| CA-36 | `Spec050Tests.Criterio36_CA_050_36` |
| CA-37 | `Spec050Tests.Criterio37_CA_050_37` |
| CA-38 | `Spec050Tests.Criterio38_CA_050_38` |
| CA-39 | `Spec050Tests.Criterio39_CA_050_39` |
| CA-40 | `Spec050Tests.Criterio40_CA_050_40` |
| CA-41 | `Spec050Tests.Criterio41_CA_050_41` |
| CA-42 | `Spec050Tests.Criterio42_CA_050_42` |
| CA-43 | `Spec050Tests.Criterio43_CA_050_43` |
| CA-44 | `Spec050Tests.Criterio44_CA_050_44` |
| CA-45 | `Spec050Tests.Criterio45_CA_050_45` |
| CA-46 | `Spec050Tests.Criterio46_CA_050_46` |
| CA-47 | `Spec050Tests.Criterio47_CA_050_47` |
| CA-48 | `Spec050Tests.Criterio48_CA_050_48` |

## 5. Contrato
| ID | Método y ruta | Quién | Entrada | Respuestas |
|---|---|---|---|---|
| VIS-02 | `GET /api/v1/agenda` | cuenta con perfil de técnico | Query: desde, hasta | 200; 401; 403 |
| VIS-04 | `POST /api/v1/visitas/{id}/reprogramar` | persona con sesión que sea participante del recurso | Ruta: id. Cuerpo: fechaHora | 200; 400; 401; 403; 409 |
| VIS-05 | `POST /api/v1/visitas/{id}/reprogramar/respuesta` | persona con sesión que sea participante del recurso | Ruta: id. Cuerpo: acepta | 200; 400; 401; 403; 409 |
| VIS-06 | `POST /api/v1/visitas/{id}/cancelar` | persona con sesión que sea participante del recurso | Ruta: id. Cuerpo: motivo | 200; 400; 401; 403; 409 |
| VIS-07 | `POST /api/v1/visitas/{id}/cliente-ausente` | cuenta con perfil de técnico | Ruta: id | 200; 401; 403; 409 |
| VIS-09 | `POST /api/v1/visitas/{id}/terminar` | cuenta con perfil de técnico | Ruta: id. Cuerpo: descripcion, recomendacion, fotos | 200; 400; 401; 403; 409; 422 |
| VIS-10 | `POST /api/v1/visitas/{id}/no-terminado` | persona con sesión que sea dueña del recurso | Ruta: id. Cuerpo: texto | 201; 400; 401; 403; 409 |
| VIS-11 | `PUT /api/v1/visitas/{id}/permiso-fotos` | persona con sesión que sea dueña del recurso | Ruta: id. Cuerpo: permiso | 200; 401; 403 |

**Filtros, orden, paginación y caché**

| ID | Filtros y orden | Paginación | Caché |
|---|---|---|---|
| VIS-02 | Filtro por fechas; orden por hora | Por cursor, 20 | No |
| VIS-04 | No aplica | No | No |
| VIS-05 | No aplica | No | No |
| VIS-06 | No aplica | No | No |
| VIS-07 | No aplica | No | No |
| VIS-09 | No aplica | No | No |
| VIS-10 | No aplica | No | No |
| VIS-11 | No aplica | No | No |

**Códigos de error:** los de forma salen como 422 con `errors` por campo y los de sesión y límite como 401 y 429; los conflictos de negocio llevan un código en inglés y snake_case que se fija al implementar y se agrega al catálogo.

## 6. Modelo de datos
**Colección `visitas`.** Nace cuando el cliente elige una cotización. Si el servicio es a domicilio es una visita y lleva un código de 4 dígitos que el cliente pide al técnico en la puerta. Si es en taller es una cita, sin código, con día, hora y dirección del taller. Concentra la agenda, la llegada, reprogramar, cancelar, el cliente ausente y terminar el trabajo con fotos obligatorias

| Campo | Tipo | Obligatorio | Reglas y validaciones | Índice |
|---|---|---|---|---|
| _id | ObjectId | Sí | Lo genera la base | Llave primaria |
| solicitudId | ObjectId | Sí | Debe existir | Sí |
| cotizacionId | ObjectId | Sí | Una por visita | Único |
| clienteId | ObjectId | Sí | Debe existir en usuarios | Compuesto |
| tecnicoId | ObjectId | Sí | Debe existir en tecnicos | Compuesto |
| tipo | Texto | Sí | Según el oficio | No |
| fechaHora | Fecha y hora UTC | Sí | Futuro al crearse | Compuesto |
| codigoHuella | Texto | No | Un solo uso, con vencimiento. Solo visita a domicilio | No |
| intentosCodigo | Entero | Sí | Se bloquea tras varios intentos | No |
| llegadaEn | Fecha y hora UTC | No |  | No |
| estado | Texto | Sí | Solo transiciones válidas | Compuesto |
| reprogramacion | Documento | No | La otra parte confirma | No |
| cancelacion | Documento | No | Motivo obligatorio | No |
| trabajo | Documento | No | Al terminar: al menos 1 foto y descripción | No |
| permisoPublicarFotos | Booleano | No | Sin permiso no van a Explorar ni al perfil | No |
| limiteNoTerminado | Fecha y hora UTC | No |  | No |
| creadoEn | Fecha y hora UTC | Sí | Lo asigna el servidor | No |

| Tipo | Campos | Para qué sirve |
|---|---|---|
| Llave principal | _id | Identifica de forma única cada documento. La genera la base de datos |
| Compuesto | tecnicoId + estado + fechaHora | Agenda y Próximas visitas del técnico |
| Compuesto | clienteId + estado + fechaHora | Visitas y citas del cliente en Actividad |
| Único | cotizacionId | Una visita por cotización elegida |

## 7. Arquitectura y componentes
- Proyecto `Knock.Market.Api`, módulo `Visits` con las capas del estilo de la spec 002: `Controllers`, `Dtos`, `Validators`, `Services`, `Data` y `Hosting`.
- Cada ruta recibe sus parámetros en objetos tipados con validador registrado, nunca como `string` suelto, y consulta con filtros por el `sub` del token.
- Las rutas bajo `/admin` usan la política de rol de la spec 033 y no están disponibles sin la sesión del panel.
- La caché, la paginación por cursor y los límites usan las piezas de las specs 009 y 010.
- El contrato se publica en OpenAPI y en `docs/endpoints.md`; los límites, en `docs/rate-limit-matrix.md`.

## 8. Dependencias
- Spec 022 de este repositorio.
- Spec 031 de este repositorio.
- Spec 032 de este repositorio.
- Specs 002, 003, 009, 010 y 011 (estilo, bases de datos, seguridad, caché y registros).
- Frontend: spec 040 consume este contrato.

## 9. Amenazas y controles
| Amenaza | Control | Casos que lo prueban |
|---|---|---|
| Acceso a datos de otra persona (IDOR) | Toda consulta y escritura filtra por el `sub` del token; ajeno responde 403 o 404 | CA-03, CA-08, CA-14, CA-20, CA-26, CA-31, CA-37, CA-43, CA-45 |
| Asignación masiva o identificador ajeno en el cuerpo | El DTO no acepta identificadores de persona; se ignoran | CA-03, CA-08, CA-14, CA-20, CA-26, CA-31, CA-37, CA-43, CA-45 |
| Inyección en filtros y parámetros | Parámetros tipados con validador; nada se concatena en consultas | CA-04, CA-09 a CA-10, CA-15 a CA-16, CA-21 a CA-22, CA-27, CA-32 a CA-33, CA-38 a CA-39, CA-44 |
| Ruta sin sesión | JWT obligatorio salvo las públicas | CA-02, CA-07, CA-13, CA-19, CA-25, CA-30, CA-36, CA-42 |
| Abuso por repetición o cuerpos grandes | Límite de peticiones, `Retry-After` y tope de cuerpo | CA-47 |
| Datos personales en registros y errores | Registros sin datos personales; ProblemDetails sin valores | CA-46 |
| Duplicados por reintento | Regla de estado o índice único, sin `Idempotency-Key` | CA-48 |

## 10. Plan de pruebas
Las pruebas citan el caso como `CA_050_NN` en el nombre (para `python scripts/sdd_lint.py --trace 050`).

| Tipo | ¿Aplica? | Qué se prueba / herramienta |
|---|---|---|
| Unitarias | Sí | Validadores, reglas de estado y cálculos del dominio |
| Integración | Sí | Cada ruta con la base real en contenedor (Testcontainers) y `MarketApiFactory`/fábrica del servicio |
| Seguridad negativa | Sí | 401, 403/404, 422, 413 y 429 (CA-02 a CA-04, CA-07 a CA-10, CA-13 a CA-16, CA-19 a CA-22, CA-25 a CA-27, CA-30 a CA-33, CA-36 a CA-39, CA-42 a CA-45, CA-47) |
| Base de datos | Sí | Índices y reglas de cada colección de §6 |
| Contrato | Sí | OpenAPI regenerado y comparado con `contracts/` |
| Mutación | Sí | Meta del módulo según la spec 008 |
| Rendimiento | No aplica | Sin ruta crítica; se mide solo si la spec 034 lo pide |

## 11. Escenarios
- **VIS-02** — pantallas: Trabajo; Inicio (Mi agenda).
- **VIS-04** — pantallas: Visita; Cita; Trabajo.
- **VIS-05** — pantallas: Visita; Cita; Trabajo.
- **VIS-06** — pantallas: Visita; Cita; Trabajo (Cancelar la visita).
- **VIS-07** — pantallas: Trabajo (El cliente no estaba).
- **VIS-09** — pantallas: Terminar servicio.
- **VIS-10** — pantallas: Actividad (No se terminó).
- **VIS-11** — pantallas: Calificar.

## 12. Definición de terminado
- Los criterios de aceptación están cubiertos por pruebas en verde y `sdd_lint --trace 050` sin faltantes.
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
- VIS-08 (aviso a la persona de confianza) ya lo cubre la spec 022 con `/visitas/{id}/aviso`.
- Las penalizaciones por cancelar o ausencia no están definidas en el Excel: confirmar con el equipo antes de implementar.
- El permiso de fotos del trabajo interactúa con la moderación (spec 031) y las imágenes (spec 032).
- Esta spec se generó a partir del Excel; antes de aprobarla hay que revisar los límites, los textos de error y los plazos con el equipo.

## Aprobación
- [ ] Revisor 1 — nombre: _____________________ fecha: __________
- [ ] Sin conflicto con la constitución
- **Estados:** Borrador → Aprobada (una persona; habilita el código) → Implementada (tras el merge)
