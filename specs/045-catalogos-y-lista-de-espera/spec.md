# Spec 045 — Catálogo de oficios, oficios pedidos y lista de espera (consulta interna)

> **Estado:** Borrador · **Repositorio:** knock-backend · **Rector:** `constitution.md`
> **Origen:** `Documentacion/00_Profesores/Desarrollo_Movil/Entregables/Knock_Endpoints_CRUD.xlsx` (revisión 2026-11-02): endpoints OFI-02, OFI-03, OFI-04, OFI-05, OFP-01, OFP-02, LIS-02.
> **Claves de trabajo:** por asignar (se crea la tarea de Jira cuando se decida el alcance) · **Sprint:** por definir · **Prioridad:** por definir
> **Servicio:** Mercado (`Knock.Market.Api`) · **Módulo:** `CatalogAdmin` · **Colecciones:** `oficios`, `oficios_pedidos`, `lista_espera`
> **Responsable sugerido:** por asignar (Alexis o Guillermo) · Erick consume el contrato (frontend 035 para la app y 044 para el panel)

## 1. Objetivo
Definir y especificar los endpoints OFI-02, OFI-03, OFI-04, OFI-05, OFP-01, OFP-02, LIS-02 del Excel de endpoints para que el equipo los implemente con el mismo estilo, seguridad y pruebas que las specs 020 a 037. Como persona, quiero pedir un oficio que no existe y, como personal interno, administrar el catálogo y ver la lista de espera, para ampliar la cobertura con datos reales.

## 2. Historia y alcance
**2.1 Historia**
- Como persona, quiero pedir un oficio que no existe y, como personal interno, administrar el catálogo y ver la lista de espera, para ampliar la cobertura con datos reales.

**2.2 Dentro**
- `GET /api/v1/oficios/{clave}` (OFI-02): Detalle de un oficio.
- `GET /api/v1/admin/oficios` (OFI-03): Lista todos los oficios, incluidos los inactivos.
- `POST /api/v1/admin/oficios` (OFI-04): Crea un oficio nuevo.
- `PUT /api/v1/admin/oficios/{clave}` (OFI-05): Edita un oficio y sus parámetros de precio. Invalida la caché.
- `POST /api/v1/oficios-pedidos` (OFP-01): Guarda el oficio escrito en Otro oficio.
- `GET /api/v1/admin/oficios-pedidos` (OFP-02): Oficios más pedidos, agrupados y contados.
- `GET /api/v1/admin/lista-espera` (LIS-02): Cuántas personas esperan por zona.
- Los índices y las reglas de cada campo de las colecciones de §6, el límite de peticiones y la documentación del contrato.

**2.3 Fuera**
- La implementación: esta spec solo se escribe; las tareas se activan cuando una persona la apruebe y lo indique.
- Pantallas y textos de la app: los definen las specs de frontend 035 (app) y 044 (panel web).
- Endpoints de otras colecciones o de otras specs (020 a 038) que el Excel nombra distinto: se anotan en §15.

## 3. Requisitos
- **R-01:** `GET /api/v1/oficios/{clave}` (leer uno): Detalle de un oficio. Lo puede llamar: cualquier persona, sin sesión. Responde 200 cuando sale bien.
- **R-02:** Entrada de `GET /api/v1/oficios/{clave}`: Ruta: clave. Un valor inválido responde 422 con `errors.<campo>` sin repetirlo. Reglas: `clave`: Única, en minúsculas, sin espacios.
- **R-03:** Estados de `GET /api/v1/oficios/{clave}`: 404. El código de negocio de cada conflicto va en inglés y en snake_case, y se define al implementar.
- **R-04:** `GET /api/v1/oficios/{clave}` se cachea (Sí, 24 horas); la clave nunca incluye datos personales de otra persona y se invalida al escribir.
- **R-05:** `GET /api/v1/admin/oficios` (leer todos): Lista todos los oficios, incluidos los inactivos. Lo puede llamar: personal interno (Administrador) con doble verificación, spec 033. Responde 200 cuando sale bien.
- **R-06:** `POST /api/v1/admin/oficios` (crear): Crea un oficio nuevo. Lo puede llamar: personal interno (Administrador) con doble verificación, spec 033. Responde 201 cuando sale bien.
- **R-07:** Entrada de `POST /api/v1/admin/oficios`: Cuerpo: clave, nombre, modalidad, parámetros. Un valor inválido responde 422 con `errors.<campo>` sin repetirlo; un cuerpo ilegible, 400. Reglas: `clave`: Única, en minúsculas, sin espacios; `nombre`: Máximo 40 caracteres; `modalidad`: Solo esos valores.
- **R-08:** Estados de `POST /api/v1/admin/oficios`: 409. El código de negocio de cada conflicto va en inglés y en snake_case, y se define al implementar.
- **R-09:** `PUT /api/v1/admin/oficios/{clave}` (actualizar): Edita un oficio y sus parámetros de precio. Invalida la caché. Lo puede llamar: personal interno (Administrador) con doble verificación, spec 033. Responde 200 cuando sale bien.
- **R-10:** Entrada de `PUT /api/v1/admin/oficios/{clave}`: Ruta: clave. Cuerpo: campos. Un valor inválido responde 422 con `errors.<campo>` sin repetirlo; un cuerpo ilegible, 400. Reglas: `clave`: Única, en minúsculas, sin espacios.
- **R-11:** Estados de `PUT /api/v1/admin/oficios/{clave}`: 404. El código de negocio de cada conflicto va en inglés y en snake_case, y se define al implementar.
- **R-12:** `POST /api/v1/oficios-pedidos` (crear): Guarda el oficio escrito en Otro oficio. Lo puede llamar: técnico (registro). Responde 201 cuando sale bien.
- **R-13:** Entrada de `POST /api/v1/oficios-pedidos`: Cuerpo: texto. Un valor inválido responde 422 con `errors.<campo>` sin repetirlo; un cuerpo ilegible, 400. Reglas: `texto`: Máximo 60 caracteres.
- **R-14:** `GET /api/v1/admin/oficios-pedidos` (leer todos / filtro): Oficios más pedidos, agrupados y contados. Lo puede llamar: personal interno (Administrador) con doble verificación, spec 033. Responde 200 cuando sale bien.
- **R-15:** `GET /api/v1/admin/oficios-pedidos` pagina por cursor (Por cursor, 20): parámetros `cursor` y `limite`, 20 por defecto y 50 como máximo; orden: Orden por cantidad descendente.
- **R-16:** `GET /api/v1/admin/lista-espera` (leer todos / filtro): Cuántas personas esperan por zona. Lo puede llamar: personal interno (Administrador) con doble verificación, spec 033. Responde 200 cuando sale bien.
- **R-17:** `GET /api/v1/admin/lista-espera` pagina por cursor (Por cursor, 20): parámetros `cursor` y `limite`, 20 por defecto y 50 como máximo; orden: Agrupado por código postal.
- **R-18:** La persona de cada operación es siempre la del `sub` del token; ningún cuerpo, ruta o consulta acepta el identificador de otra persona para actuar en su nombre, y un identificador ajeno enviado en el cuerpo se ignora.
- **R-19:** Los errores usan ProblemDetails con `correlationId` y `traceId`; los registros llevan método, ruta, estado y correlación, sin teléfono, dirección, coordenadas, nombres ni textos escritos por la persona (spec 011).
- **R-20:** Por encima del límite de peticiones responde 429 con `Retry-After`; cada ruta tiene su límite registrado en `docs/rate-limit-matrix.md` y los cuerpos se limitan según la spec 009 (413 si lo exceden).
- **R-21:** Las rutas que crean algo no usan `Idempotency-Key` (retirado en la spec 020): la repetición se controla con la regla de estado o el índice único de §6.

## 4. Criterios de aceptación
- **CA-01:** Dado el rol «cualquier persona, sin sesión», cuando llama `GET /api/v1/oficios/{clave}` con datos válidos, entonces responde 200 con lo descrito en §5.
- **CA-02:** Dado un valor inválido según las reglas de §6, cuando llama `GET /api/v1/oficios/{clave}`, entonces responde 422 con `errors.<campo>`, no repite el valor y no devuelve datos.
- **CA-03:** Dado el caso «recurso no disponible», cuando llama `GET /api/v1/oficios/{clave}`, entonces responde 404 con ProblemDetails y no cambia el estado.
- **CA-04:** Dada una consulta repetida dentro del tiempo de caché, cuando llega la segunda, entonces se responde desde la caché y la base recibe una sola consulta.
- **CA-05:** Dado el rol «personal interno (Administrador) con doble verificación, spec 033», cuando llama `GET /api/v1/admin/oficios` con datos válidos, entonces responde 200 con lo descrito en §5.
- **CA-06:** Dada una petición sin token o con token vencido, cuando llama `GET /api/v1/admin/oficios`, entonces responde 401 y no ejecuta la operación.
- **CA-07:** Dada una persona que no cumple el rol «personal interno (Administrador) con doble verificación, spec 033», cuando llama `GET /api/v1/admin/oficios`, entonces responde 403 y no cambia nada.
- **CA-08:** Dado el rol «personal interno (Administrador) con doble verificación, spec 033», cuando llama `POST /api/v1/admin/oficios` con datos válidos, entonces responde 201 con lo descrito en §5.
- **CA-09:** Dada una petición sin token o con token vencido, cuando llama `POST /api/v1/admin/oficios`, entonces responde 401 y no ejecuta la operación.
- **CA-10:** Dada una persona que no cumple el rol «personal interno (Administrador) con doble verificación, spec 033», cuando llama `POST /api/v1/admin/oficios`, entonces responde 403 y no cambia nada.
- **CA-11:** Dado un valor inválido según las reglas de §6, cuando llama `POST /api/v1/admin/oficios`, entonces responde 422 con `errors.<campo>`, no repite el valor y no guarda nada.
- **CA-12:** Dado un cuerpo que no se puede leer, cuando llama `POST /api/v1/admin/oficios`, entonces responde 400 y no guarda nada.
- **CA-13:** Dado el caso «recurso no disponible», cuando llama `POST /api/v1/admin/oficios`, entonces responde 409 con ProblemDetails y no cambia el estado.
- **CA-14:** Dado el rol «personal interno (Administrador) con doble verificación, spec 033», cuando llama `PUT /api/v1/admin/oficios/{clave}` con datos válidos, entonces responde 200 con lo descrito en §5.
- **CA-15:** Dada una petición sin token o con token vencido, cuando llama `PUT /api/v1/admin/oficios/{clave}`, entonces responde 401 y no ejecuta la operación.
- **CA-16:** Dada una persona que no cumple el rol «personal interno (Administrador) con doble verificación, spec 033», cuando llama `PUT /api/v1/admin/oficios/{clave}`, entonces responde 403 y no cambia nada.
- **CA-17:** Dado un valor inválido según las reglas de §6, cuando llama `PUT /api/v1/admin/oficios/{clave}`, entonces responde 422 con `errors.<campo>`, no repite el valor y no guarda nada.
- **CA-18:** Dado un cuerpo que no se puede leer, cuando llama `PUT /api/v1/admin/oficios/{clave}`, entonces responde 400 y no guarda nada.
- **CA-19:** Dado el caso «recurso no disponible», cuando llama `PUT /api/v1/admin/oficios/{clave}`, entonces responde 404 con ProblemDetails y no cambia el estado.
- **CA-20:** Dado el rol «técnico (registro)», cuando llama `POST /api/v1/oficios-pedidos` con datos válidos, entonces responde 201 con lo descrito en §5.
- **CA-21:** Dada una petición sin token o con token vencido, cuando llama `POST /api/v1/oficios-pedidos`, entonces responde 401 y no ejecuta la operación.
- **CA-22:** Dada una persona que no cumple el rol «técnico (registro)», cuando llama `POST /api/v1/oficios-pedidos`, entonces responde 404 sin revelar que el recurso existe y no cambia nada.
- **CA-23:** Dado un valor inválido según las reglas de §6, cuando llama `POST /api/v1/oficios-pedidos`, entonces responde 422 con `errors.<campo>`, no repite el valor y no guarda nada.
- **CA-24:** Dado un cuerpo que no se puede leer, cuando llama `POST /api/v1/oficios-pedidos`, entonces responde 400 y no guarda nada.
- **CA-25:** Dado el rol «personal interno (Administrador) con doble verificación, spec 033», cuando llama `GET /api/v1/admin/oficios-pedidos` con datos válidos, entonces responde 200 con lo descrito en §5.
- **CA-26:** Dada una petición sin token o con token vencido, cuando llama `GET /api/v1/admin/oficios-pedidos`, entonces responde 401 y no ejecuta la operación.
- **CA-27:** Dada una persona que no cumple el rol «personal interno (Administrador) con doble verificación, spec 033», cuando llama `GET /api/v1/admin/oficios-pedidos`, entonces responde 403 y no cambia nada.
- **CA-28:** Dado un listado con más de 20 elementos, cuando se pide sin `limite`, entonces devuelve 20 y el cursor de la página siguiente; con `limite=51` responde 422.
- **CA-29:** Dado el rol «personal interno (Administrador) con doble verificación, spec 033», cuando llama `GET /api/v1/admin/lista-espera` con datos válidos, entonces responde 200 con lo descrito en §5.
- **CA-30:** Dada una petición sin token o con token vencido, cuando llama `GET /api/v1/admin/lista-espera`, entonces responde 401 y no ejecuta la operación.
- **CA-31:** Dada una persona que no cumple el rol «personal interno (Administrador) con doble verificación, spec 033», cuando llama `GET /api/v1/admin/lista-espera`, entonces responde 403 y no cambia nada.
- **CA-32:** Dado un listado con más de 20 elementos, cuando se pide sin `limite`, entonces devuelve 20 y el cursor de la página siguiente; con `limite=51` responde 422.
- **CA-33:** Dado un cuerpo con el identificador de otra persona, cuando se llama cualquier ruta de esta spec, entonces la operación usa el `sub` del token y no el del cuerpo.
- **CA-34:** Dada una operación con datos personales, cuando se inspeccionan los registros y las respuestas de error, entonces no aparecen esos datos.
- **CA-35:** Dada una persona que supera el límite de una ruta, cuando repite la petición, entonces responde 429 con `Retry-After` y, con un cuerpo de tamaño excesivo, 413.
- **CA-36:** Dada una operación de creación repetida por mala señal, cuando llega dos veces, entonces no deja duplicados porque lo impide la regla de estado o el índice único.

**4.1 Criterio → prueba prevista**

| Criterio | Prueba prevista |
|---|---|
| CA-01 | `Spec045Tests.Criterio01_CA_045_01` |
| CA-02 | `Spec045Tests.Criterio02_CA_045_02` |
| CA-03 | `Spec045Tests.Criterio03_CA_045_03` |
| CA-04 | `Spec045Tests.Criterio04_CA_045_04` |
| CA-05 | `Spec045Tests.Criterio05_CA_045_05` |
| CA-06 | `Spec045Tests.Criterio06_CA_045_06` |
| CA-07 | `Spec045Tests.Criterio07_CA_045_07` |
| CA-08 | `Spec045Tests.Criterio08_CA_045_08` |
| CA-09 | `Spec045Tests.Criterio09_CA_045_09` |
| CA-10 | `Spec045Tests.Criterio10_CA_045_10` |
| CA-11 | `Spec045Tests.Criterio11_CA_045_11` |
| CA-12 | `Spec045Tests.Criterio12_CA_045_12` |
| CA-13 | `Spec045Tests.Criterio13_CA_045_13` |
| CA-14 | `Spec045Tests.Criterio14_CA_045_14` |
| CA-15 | `Spec045Tests.Criterio15_CA_045_15` |
| CA-16 | `Spec045Tests.Criterio16_CA_045_16` |
| CA-17 | `Spec045Tests.Criterio17_CA_045_17` |
| CA-18 | `Spec045Tests.Criterio18_CA_045_18` |
| CA-19 | `Spec045Tests.Criterio19_CA_045_19` |
| CA-20 | `Spec045Tests.Criterio20_CA_045_20` |
| CA-21 | `Spec045Tests.Criterio21_CA_045_21` |
| CA-22 | `Spec045Tests.Criterio22_CA_045_22` |
| CA-23 | `Spec045Tests.Criterio23_CA_045_23` |
| CA-24 | `Spec045Tests.Criterio24_CA_045_24` |
| CA-25 | `Spec045Tests.Criterio25_CA_045_25` |
| CA-26 | `Spec045Tests.Criterio26_CA_045_26` |
| CA-27 | `Spec045Tests.Criterio27_CA_045_27` |
| CA-28 | `Spec045Tests.Criterio28_CA_045_28` |
| CA-29 | `Spec045Tests.Criterio29_CA_045_29` |
| CA-30 | `Spec045Tests.Criterio30_CA_045_30` |
| CA-31 | `Spec045Tests.Criterio31_CA_045_31` |
| CA-32 | `Spec045Tests.Criterio32_CA_045_32` |
| CA-33 | `Spec045Tests.Criterio33_CA_045_33` |
| CA-34 | `Spec045Tests.Criterio34_CA_045_34` |
| CA-35 | `Spec045Tests.Criterio35_CA_045_35` |
| CA-36 | `Spec045Tests.Criterio36_CA_045_36` |

## 5. Contrato
| ID | Método y ruta | Quién | Entrada | Respuestas |
|---|---|---|---|---|
| OFI-02 | `GET /api/v1/oficios/{clave}` | cualquier persona, sin sesión | Ruta: clave | 200; 404 |
| OFI-03 | `GET /api/v1/admin/oficios` | personal interno (Administrador) con doble verificación, spec 033 | Ninguno | 200; 401; 403 |
| OFI-04 | `POST /api/v1/admin/oficios` | personal interno (Administrador) con doble verificación, spec 033 | Cuerpo: clave, nombre, modalidad, parámetros | 201; 400; 401; 403; 409 |
| OFI-05 | `PUT /api/v1/admin/oficios/{clave}` | personal interno (Administrador) con doble verificación, spec 033 | Ruta: clave. Cuerpo: campos | 200; 400; 401; 403; 404 |
| OFP-01 | `POST /api/v1/oficios-pedidos` | técnico (registro) | Cuerpo: texto | 201; 400; 401 |
| OFP-02 | `GET /api/v1/admin/oficios-pedidos` | personal interno (Administrador) con doble verificación, spec 033 | Ninguno | 200; 401; 403 |
| LIS-02 | `GET /api/v1/admin/lista-espera` | personal interno (Administrador) con doble verificación, spec 033 | Ninguno | 200; 401; 403 |

**Filtros, orden, paginación y caché**

| ID | Filtros y orden | Paginación | Caché |
|---|---|---|---|
| OFI-02 | No aplica | No | Sí, 24 horas |
| OFI-03 | No aplica | No | No |
| OFI-04 | No aplica | No | No |
| OFI-05 | No aplica | No | No |
| OFP-01 | No aplica | No | No |
| OFP-02 | Orden por cantidad descendente | Por cursor, 20 | No |
| LIS-02 | Agrupado por código postal | Por cursor, 20 | No |

**Códigos de error:** los de forma salen como 422 con `errors` por campo y los de sesión y límite como 401 y 429; los conflictos de negocio llevan un código en inglés y snake_case que se fija al implementar y se agrega al catálogo.

## 6. Modelo de datos
**Colección `oficios`.** Catálogo de los servicios que se pueden pedir: mecánica automotriz, plomería y electricidad. Los oficios son datos y no están escritos en el código, para poder abrir nuevas categorías sin reprogramar. Cada oficio dice si se atiende en taller o a domicilio, y eso decide qué pasos muestra la app: la dirección y el código de visita solo aplican a domicilio

| Campo | Tipo | Obligatorio | Reglas y validaciones | Índice |
|---|---|---|---|---|
| _id | ObjectId | Sí | Lo genera la base | Llave primaria |
| clave | Texto | Sí | Única, en minúsculas, sin espacios | Único |
| nombre | Texto | Sí | Máximo 40 caracteres | No |
| modalidad | Texto | Sí | Solo esos valores | No |
| frasesSugeridas | Lista de textos | No |  | No |
| topePorcentaje | Decimal | Sí | Entre 0 y 100 | No |
| topeFijoPorcentaje | Decimal | Sí | Entre 0 y 100 | No |
| pisoAprobacion | Entero | Sí | Mayor que 0 | No |
| visitaMaxima | Entero | Sí | Mayor que 0 | No |
| activo | Booleano | Sí | Por defecto verdadero | Compuesto |

| Tipo | Campos | Para qué sirve |
|---|---|---|
| Llave principal | _id | Identifica de forma única cada documento. La genera la base de datos |
| Único | clave | Buscar un oficio por su clave y evitar duplicados |
| Normal | activo | Listar solo los oficios activos |

**Colección `oficios_pedidos`.** Cuando alguien se registra como técnico y elige Otro oficio, lo que escribe se guarda aquí. Esa persona queda registrada y su oficio aparece en su perfil, pero no recibe solicitudes porque todavía no existe la categoría. La lista sirve para decidir qué oficio abrir después

| Campo | Tipo | Obligatorio | Reglas y validaciones | Índice |
|---|---|---|---|---|
| _id | ObjectId | Sí | Lo genera la base | Llave primaria |
| texto | Texto | Sí | Máximo 60 caracteres | No |
| textoNormalizado | Texto | Sí | La calcula el servidor | Sí |
| usuarioId | ObjectId | Sí | Debe existir en usuarios | No |
| creadoEn | Fecha y hora UTC | Sí | Lo asigna el servidor | No |

| Tipo | Campos | Para qué sirve |
|---|---|---|
| Llave principal | _id | Identifica de forma única cada documento. La genera la base de datos |
| Normal | textoNormalizado | Contar cuántas personas piden el mismo oficio |

**Colección `lista_espera`.** Cuando alguien está fuera de las colonias que Knock cubre, la app se lo avisa y le ofrece apuntarse a una lista de espera. Sirve para saber dónde abrir después

| Campo | Tipo | Obligatorio | Reglas y validaciones | Índice |
|---|---|---|---|---|
| _id | ObjectId | Sí | Lo genera la base | Llave primaria |
| codigoPostal | Texto | Sí | 5 dígitos | Sí |
| celular | Texto cifrado | No | 10 dígitos | No |
| aceptaAviso | Booleano | Sí | Debe ser verdadero | No |
| creadoEn | Fecha y hora UTC | Sí | Lo asigna el servidor | No |

| Tipo | Campos | Para qué sirve |
|---|---|---|
| Llave principal | _id | Identifica de forma única cada documento. La genera la base de datos |
| Normal | codigoPostal | Contar cuántas personas esperan en cada zona |

## 7. Arquitectura y componentes
- Proyecto `Knock.Market.Api`, módulo `CatalogAdmin` con las capas del estilo de la spec 002: `Controllers`, `Dtos`, `Validators`, `Services`, `Data` y `Hosting`.
- Cada ruta recibe sus parámetros en objetos tipados con validador registrado, nunca como `string` suelto, y consulta con filtros por el `sub` del token.
- Las rutas bajo `/admin` usan la política de rol de la spec 033 y no están disponibles sin la sesión del panel.
- La caché, la paginación por cursor y los límites usan las piezas de las specs 009 y 010.
- El contrato se publica en OpenAPI y en `docs/endpoints.md`; los límites, en `docs/rate-limit-matrix.md`.

## 8. Dependencias
- Spec 023 de este repositorio.
- Spec 033 de este repositorio.
- Specs 002, 003, 009, 010 y 011 (estilo, bases de datos, seguridad, caché y registros).
- Frontend: las specs 035 (app) y 044 (panel web) consumen este contrato.

## 9. Amenazas y controles
| Amenaza | Control | Casos que lo prueban |
|---|---|---|
| Acceso a datos de otra persona (IDOR) | Toda consulta y escritura filtra por el `sub` del token; ajeno responde 403 o 404 | CA-07, CA-10, CA-16, CA-22, CA-27, CA-31, CA-33 |
| Asignación masiva o identificador ajeno en el cuerpo | El DTO no acepta identificadores de persona; se ignoran | CA-07, CA-10, CA-16, CA-22, CA-27, CA-31, CA-33 |
| Inyección en filtros y parámetros | Parámetros tipados con validador; nada se concatena en consultas | CA-02, CA-11 a CA-12, CA-17 a CA-18, CA-23 a CA-24 |
| Ruta sin sesión | JWT obligatorio salvo las públicas | CA-06, CA-09, CA-15, CA-21, CA-26, CA-30 |
| Abuso por repetición o cuerpos grandes | Límite de peticiones, `Retry-After` y tope de cuerpo | CA-35 |
| Datos personales en registros y errores | Registros sin datos personales; ProblemDetails sin valores | CA-34 |
| Duplicados por reintento | Regla de estado o índice único, sin `Idempotency-Key` | CA-36 |

## 10. Plan de pruebas
Las pruebas citan el caso como `CA_045_NN` en el nombre (para `python scripts/sdd_lint.py --trace 045`).

| Tipo | ¿Aplica? | Qué se prueba / herramienta |
|---|---|---|
| Unitarias | Sí | Validadores, reglas de estado y cálculos del dominio |
| Integración | Sí | Cada ruta con la base real en contenedor (Testcontainers) y `MarketApiFactory`/fábrica del servicio |
| Seguridad negativa | Sí | 401, 403/404, 422, 413 y 429 (CA-02, CA-06 a CA-07, CA-09 a CA-12, CA-15 a CA-18, CA-21 a CA-24, CA-26 a CA-27, CA-30 a CA-31, CA-33, CA-35) |
| Base de datos | Sí | Índices y reglas de cada colección de §6 |
| Contrato | Sí | OpenAPI regenerado y comparado con `contracts/` |
| Mutación | Sí | Meta del módulo según la spec 008 |
| Rendimiento | No aplica | Sin ruta crítica; se mide solo si la spec 034 lo pide |

## 11. Escenarios
- **OFI-02** — pantallas: Pedir cotización.
- **OFI-03** — pantallas: Panel de administración.
- **OFI-04** — pantallas: Panel de administración.
- **OFI-05** — pantallas: Panel de administración.
- **OFP-01** — pantallas: Registro de técnico.
- **OFP-02** — pantallas: Panel de administración.
- **LIS-02** — pantallas: Panel de administración.

## 12. Definición de terminado
- Los criterios de aceptación están cubiertos por pruebas en verde y `sdd_lint --trace 045` sin faltantes.
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
- Las rutas de administración exigen el panel con doble factor (spec 033): confirmar los roles que pueden crear y editar oficios.
- La lista de espera ya se escribe con la spec 023; aquí solo se agrega su lectura interna.
- Esta spec se generó a partir del Excel; antes de aprobarla hay que revisar los límites, los textos de error y los plazos con el equipo.

## Aprobación
- [ ] Revisor 1 — nombre: _____________________ fecha: __________
- [ ] Sin conflicto con la constitución
- **Estados:** Borrador → Aprobada (una persona; habilita el código) → Implementada (tras el merge)
