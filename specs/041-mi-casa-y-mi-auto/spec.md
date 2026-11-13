# Spec 041 — Mi casa y mi auto

> **Estado:** Borrador · **Repositorio:** knock-backend · **Rector:** `constitution.md`
> **Origen:** `Documentacion/00_Profesores/Desarrollo_Movil/Entregables/Knock_Endpoints_CRUD.xlsx` (revisión 2026-11-02): endpoints CAS-01, CAS-02, CAS-03, CAS-04, CAS-05.
> **Claves de trabajo:** por asignar (se crea la tarea de Jira cuando se decida el alcance) · **Sprint:** por definir · **Prioridad:** por definir
> **Servicio:** Identidad (`Knock.Identity.Api`) · **Módulo:** `HomeAndCar` · **Colecciones:** `mi_casa_y_auto`
> **Responsable sugerido:** por asignar (Alexis o Guillermo) · Erick consume el contrato (frontend 031)

## 1. Objetivo
Definir y especificar los endpoints CAS-01, CAS-02, CAS-03, CAS-04, CAS-05 del Excel de endpoints para que el equipo los implemente con el mismo estilo, seguridad y pruebas que las specs 020 a 037. Como cliente, quiero registrar mis autos y las cosas de mi casa con su historial de servicios, para pedir ayuda con los datos a la mano.

## 2. Historia y alcance
**2.1 Historia**
- Como cliente, quiero registrar mis autos y las cosas de mi casa con su historial de servicios, para pedir ayuda con los datos a la mano.

**2.2 Dentro**
- `GET /api/v1/me/casa-y-auto` (CAS-01): Lista los autos y elementos de la casa.
- `POST /api/v1/me/casa-y-auto` (CAS-02): Registra un auto o un elemento de la casa.
- `PUT /api/v1/me/casa-y-auto/{id}` (CAS-03): Edita un elemento.
- `DELETE /api/v1/me/casa-y-auto/{id}` (CAS-04): Quita un elemento.
- `GET /api/v1/me/casa-y-auto/{id}/historial` (CAS-05): Trabajos terminados relacionados con ese elemento, tomados de Mercado.
- Los índices y las reglas de cada campo de las colecciones de §6, el límite de peticiones y la documentación del contrato.

**2.3 Fuera**
- La implementación: esta spec solo se escribe; las tareas se activan cuando una persona la apruebe y lo indique.
- Pantallas y textos de la app: los define la spec de frontend 031.
- Endpoints de otras colecciones o de otras specs (020 a 038) que el Excel nombra distinto: se anotan en §15.

## 3. Requisitos
- **R-01:** `GET /api/v1/me/casa-y-auto` (leer todos): Lista los autos y elementos de la casa. Lo puede llamar: persona con sesión (cliente o técnico). Responde 200 cuando sale bien.
- **R-02:** `POST /api/v1/me/casa-y-auto` (crear): Registra un auto o un elemento de la casa. Lo puede llamar: persona con sesión (cliente o técnico). Responde 201 cuando sale bien.
- **R-03:** Entrada de `POST /api/v1/me/casa-y-auto`: Cuerpo: tipo, nombre, anio. Un valor inválido responde 422 con `errors.<campo>` sin repetirlo; un cuerpo ilegible, 400. Reglas: `tipo`: Solo esos valores; `nombre`: Mínimo 2 caracteres; `anio`: Entre 1950 y el año actual + 1. Solo para tipo auto.
- **R-04:** `PUT /api/v1/me/casa-y-auto/{id}` (actualizar): Edita un elemento. Lo puede llamar: persona con sesión que sea dueña del recurso. Responde 200 cuando sale bien.
- **R-05:** Entrada de `PUT /api/v1/me/casa-y-auto/{id}`: Ruta: id. Cuerpo: campos. Un valor inválido responde 422 con `errors.<campo>` sin repetirlo; un cuerpo ilegible, 400.
- **R-06:** Estados de `PUT /api/v1/me/casa-y-auto/{id}`: 404. El código de negocio de cada conflicto va en inglés y en snake_case, y se define al implementar.
- **R-07:** `DELETE /api/v1/me/casa-y-auto/{id}` (borrar): Quita un elemento. Lo puede llamar: persona con sesión que sea dueña del recurso. Responde 204 cuando sale bien.
- **R-08:** Entrada de `DELETE /api/v1/me/casa-y-auto/{id}`: Ruta: id. Un valor inválido responde 422 con `errors.<campo>` sin repetirlo; un cuerpo ilegible, 400.
- **R-09:** Estados de `DELETE /api/v1/me/casa-y-auto/{id}`: 404. El código de negocio de cada conflicto va en inglés y en snake_case, y se define al implementar.
- **R-10:** `GET /api/v1/me/casa-y-auto/{id}/historial` (leer todos): Trabajos terminados relacionados con ese elemento, tomados de Mercado. Lo puede llamar: persona con sesión que sea dueña del recurso. Responde 200 cuando sale bien.
- **R-11:** Entrada de `GET /api/v1/me/casa-y-auto/{id}/historial`: Ruta: id. Un valor inválido responde 422 con `errors.<campo>` sin repetirlo.
- **R-12:** Estados de `GET /api/v1/me/casa-y-auto/{id}/historial`: 404. El código de negocio de cada conflicto va en inglés y en snake_case, y se define al implementar.
- **R-13:** `GET /api/v1/me/casa-y-auto/{id}/historial` pagina por cursor (Por cursor, 20): parámetros `cursor` y `limite`, 20 por defecto y 50 como máximo; orden: Orden por fecha descendente.
- **R-14:** La persona de cada operación es siempre la del `sub` del token; ningún cuerpo, ruta o consulta acepta el identificador de otra persona para actuar en su nombre, y un identificador ajeno enviado en el cuerpo se ignora.
- **R-15:** Los errores usan ProblemDetails con `correlationId` y `traceId`; los registros llevan método, ruta, estado y correlación, sin teléfono, dirección, coordenadas, nombres ni textos escritos por la persona (spec 011).
- **R-16:** Por encima del límite de peticiones responde 429 con `Retry-After`; cada ruta tiene su límite registrado en `docs/rate-limit-matrix.md` y los cuerpos se limitan según la spec 009 (413 si lo exceden).
- **R-17:** Las rutas que crean algo no usan `Idempotency-Key` (retirado en la spec 020): la repetición se controla con la regla de estado o el índice único de §6.

## 4. Criterios de aceptación
- **CA-01:** Dado el rol «persona con sesión (cliente o técnico)», cuando llama `GET /api/v1/me/casa-y-auto` con datos válidos, entonces responde 200 con lo descrito en §5.
- **CA-02:** Dada una petición sin token o con token vencido, cuando llama `GET /api/v1/me/casa-y-auto`, entonces responde 401 y no ejecuta la operación.
- **CA-03:** Dado el rol «persona con sesión (cliente o técnico)», cuando llama `POST /api/v1/me/casa-y-auto` con datos válidos, entonces responde 201 con lo descrito en §5.
- **CA-04:** Dada una petición sin token o con token vencido, cuando llama `POST /api/v1/me/casa-y-auto`, entonces responde 401 y no ejecuta la operación.
- **CA-05:** Dado un valor inválido según las reglas de §6, cuando llama `POST /api/v1/me/casa-y-auto`, entonces responde 422 con `errors.<campo>`, no repite el valor y no guarda nada.
- **CA-06:** Dado un cuerpo que no se puede leer, cuando llama `POST /api/v1/me/casa-y-auto`, entonces responde 400 y no guarda nada.
- **CA-07:** Dado el rol «persona con sesión que sea dueña del recurso», cuando llama `PUT /api/v1/me/casa-y-auto/{id}` con datos válidos, entonces responde 200 con lo descrito en §5.
- **CA-08:** Dada una petición sin token o con token vencido, cuando llama `PUT /api/v1/me/casa-y-auto/{id}`, entonces responde 401 y no ejecuta la operación.
- **CA-09:** Dada una persona que no cumple el rol «persona con sesión que sea dueña del recurso», cuando llama `PUT /api/v1/me/casa-y-auto/{id}`, entonces responde 403 y no cambia nada.
- **CA-10:** Dado un valor inválido según las reglas de §6, cuando llama `PUT /api/v1/me/casa-y-auto/{id}`, entonces responde 422 con `errors.<campo>`, no repite el valor y no guarda nada.
- **CA-11:** Dado un cuerpo que no se puede leer, cuando llama `PUT /api/v1/me/casa-y-auto/{id}`, entonces responde 400 y no guarda nada.
- **CA-12:** Dado el caso «recurso no disponible», cuando llama `PUT /api/v1/me/casa-y-auto/{id}`, entonces responde 404 con ProblemDetails y no cambia el estado.
- **CA-13:** Dado el rol «persona con sesión que sea dueña del recurso», cuando llama `DELETE /api/v1/me/casa-y-auto/{id}` con datos válidos, entonces responde 204 con lo descrito en §5.
- **CA-14:** Dada una petición sin token o con token vencido, cuando llama `DELETE /api/v1/me/casa-y-auto/{id}`, entonces responde 401 y no ejecuta la operación.
- **CA-15:** Dada una persona que no cumple el rol «persona con sesión que sea dueña del recurso», cuando llama `DELETE /api/v1/me/casa-y-auto/{id}`, entonces responde 403 y no cambia nada.
- **CA-16:** Dado un valor inválido según las reglas de §6, cuando llama `DELETE /api/v1/me/casa-y-auto/{id}`, entonces responde 422 con `errors.<campo>`, no repite el valor y no guarda nada.
- **CA-17:** Dado el caso «recurso no disponible», cuando llama `DELETE /api/v1/me/casa-y-auto/{id}`, entonces responde 404 con ProblemDetails y no cambia el estado.
- **CA-18:** Dado el rol «persona con sesión que sea dueña del recurso», cuando llama `GET /api/v1/me/casa-y-auto/{id}/historial` con datos válidos, entonces responde 200 con lo descrito en §5.
- **CA-19:** Dada una petición sin token o con token vencido, cuando llama `GET /api/v1/me/casa-y-auto/{id}/historial`, entonces responde 401 y no ejecuta la operación.
- **CA-20:** Dada una persona que no cumple el rol «persona con sesión que sea dueña del recurso», cuando llama `GET /api/v1/me/casa-y-auto/{id}/historial`, entonces responde 403 y no cambia nada.
- **CA-21:** Dado un valor inválido según las reglas de §6, cuando llama `GET /api/v1/me/casa-y-auto/{id}/historial`, entonces responde 422 con `errors.<campo>`, no repite el valor y no devuelve datos.
- **CA-22:** Dado el caso «recurso no disponible», cuando llama `GET /api/v1/me/casa-y-auto/{id}/historial`, entonces responde 404 con ProblemDetails y no cambia el estado.
- **CA-23:** Dado un listado con más de 20 elementos, cuando se pide sin `limite`, entonces devuelve 20 y el cursor de la página siguiente; con `limite=51` responde 422.
- **CA-24:** Dado un cuerpo con el identificador de otra persona, cuando se llama cualquier ruta de esta spec, entonces la operación usa el `sub` del token y no el del cuerpo.
- **CA-25:** Dada una operación con datos personales, cuando se inspeccionan los registros y las respuestas de error, entonces no aparecen esos datos.
- **CA-26:** Dada una persona que supera el límite de una ruta, cuando repite la petición, entonces responde 429 con `Retry-After` y, con un cuerpo de tamaño excesivo, 413.
- **CA-27:** Dada una operación de creación repetida por mala señal, cuando llega dos veces, entonces no deja duplicados porque lo impide la regla de estado o el índice único.

**4.1 Criterio → prueba prevista**

| Criterio | Prueba prevista |
|---|---|
| CA-01 | `Spec041Tests.Criterio01_CA_041_01` |
| CA-02 | `Spec041Tests.Criterio02_CA_041_02` |
| CA-03 | `Spec041Tests.Criterio03_CA_041_03` |
| CA-04 | `Spec041Tests.Criterio04_CA_041_04` |
| CA-05 | `Spec041Tests.Criterio05_CA_041_05` |
| CA-06 | `Spec041Tests.Criterio06_CA_041_06` |
| CA-07 | `Spec041Tests.Criterio07_CA_041_07` |
| CA-08 | `Spec041Tests.Criterio08_CA_041_08` |
| CA-09 | `Spec041Tests.Criterio09_CA_041_09` |
| CA-10 | `Spec041Tests.Criterio10_CA_041_10` |
| CA-11 | `Spec041Tests.Criterio11_CA_041_11` |
| CA-12 | `Spec041Tests.Criterio12_CA_041_12` |
| CA-13 | `Spec041Tests.Criterio13_CA_041_13` |
| CA-14 | `Spec041Tests.Criterio14_CA_041_14` |
| CA-15 | `Spec041Tests.Criterio15_CA_041_15` |
| CA-16 | `Spec041Tests.Criterio16_CA_041_16` |
| CA-17 | `Spec041Tests.Criterio17_CA_041_17` |
| CA-18 | `Spec041Tests.Criterio18_CA_041_18` |
| CA-19 | `Spec041Tests.Criterio19_CA_041_19` |
| CA-20 | `Spec041Tests.Criterio20_CA_041_20` |
| CA-21 | `Spec041Tests.Criterio21_CA_041_21` |
| CA-22 | `Spec041Tests.Criterio22_CA_041_22` |
| CA-23 | `Spec041Tests.Criterio23_CA_041_23` |
| CA-24 | `Spec041Tests.Criterio24_CA_041_24` |
| CA-25 | `Spec041Tests.Criterio25_CA_041_25` |
| CA-26 | `Spec041Tests.Criterio26_CA_041_26` |
| CA-27 | `Spec041Tests.Criterio27_CA_041_27` |

## 5. Contrato
| ID | Método y ruta | Quién | Entrada | Respuestas |
|---|---|---|---|---|
| CAS-01 | `GET /api/v1/me/casa-y-auto` | persona con sesión (cliente o técnico) | Ninguno | 200; 401 |
| CAS-02 | `POST /api/v1/me/casa-y-auto` | persona con sesión (cliente o técnico) | Cuerpo: tipo, nombre, anio | 201; 400; 401; 422 |
| CAS-03 | `PUT /api/v1/me/casa-y-auto/{id}` | persona con sesión que sea dueña del recurso | Ruta: id. Cuerpo: campos | 200; 400; 401; 403; 404 |
| CAS-04 | `DELETE /api/v1/me/casa-y-auto/{id}` | persona con sesión que sea dueña del recurso | Ruta: id | 204; 401; 403; 404 |
| CAS-05 | `GET /api/v1/me/casa-y-auto/{id}/historial` | persona con sesión que sea dueña del recurso | Ruta: id | 200; 401; 403; 404 |

**Filtros, orden, paginación y caché**

| ID | Filtros y orden | Paginación | Caché |
|---|---|---|---|
| CAS-01 | tipo (auto/casa) | No (pocos) | No |
| CAS-02 | No aplica | No | No |
| CAS-03 | No aplica | No | No |
| CAS-04 | No aplica | No | No |
| CAS-05 | Orden por fecha descendente | Por cursor, 20 | No |

**Códigos de error:** los de forma salen como 422 con `errors` por campo y los de sesión y límite como 401 y 429; los conflictos de negocio llevan un código en inglés y snake_case que se fija al implementar y se agrega al catálogo.

## 6. Modelo de datos
**Colección `mi_casa_y_auto`.** Guarda los autos y las cosas de la casa que el cliente registra para pedir servicios más rápido. El historial del auto no se captura a mano: sale de los trabajos reales terminados por Knock. Sirve a la pantalla Mi casa y mi auto y al aviso de mantenimiento (por ejemplo, que ya toca el cambio de aceite)

| Campo | Tipo | Obligatorio | Reglas y validaciones | Índice |
|---|---|---|---|---|
| _id | ObjectId | Sí | Lo genera la base | Llave primaria |
| usuarioId | ObjectId | Sí | Debe existir en usuarios | Sí |
| tipo | Texto | Sí | Solo esos valores | Compuesto con usuarioId |
| nombre | Texto | Sí | Mínimo 2 caracteres | No |
| anio | Entero | No | Entre 1950 y el año actual + 1. Solo para tipo auto | No |
| creadoEn | Fecha y hora UTC | Sí | Lo asigna el servidor | No |

| Tipo | Campos | Para qué sirve |
|---|---|---|
| Llave principal | _id | Identifica de forma única cada documento. La genera la base de datos |
| Compuesto | usuarioId + tipo | Listar los autos o las cosas de la casa de una persona |

## 7. Arquitectura y componentes
- Proyecto `Knock.Identity.Api`, módulo `HomeAndCar` con las capas del estilo de la spec 002: `Controllers`, `Dtos`, `Validators`, `Services`, `Data` y `Hosting`.
- Cada ruta recibe sus parámetros en objetos tipados con validador registrado, nunca como `string` suelto, y consulta con filtros por el `sub` del token.
- Las rutas bajo `/admin` usan la política de rol de la spec 033 y no están disponibles sin la sesión del panel.
- La caché, la paginación por cursor y los límites usan las piezas de las specs 009 y 010.
- El contrato se publica en OpenAPI y en `docs/endpoints.md`; los límites, en `docs/rate-limit-matrix.md`.

## 8. Dependencias
- Spec 020 de este repositorio.
- Spec 022 de este repositorio.
- Specs 002, 003, 009, 010 y 011 (estilo, bases de datos, seguridad, caché y registros).
- Frontend: spec 031 consume este contrato.

## 9. Amenazas y controles
| Amenaza | Control | Casos que lo prueban |
|---|---|---|
| Acceso a datos de otra persona (IDOR) | Toda consulta y escritura filtra por el `sub` del token; ajeno responde 403 o 404 | CA-09, CA-15, CA-20, CA-24 |
| Asignación masiva o identificador ajeno en el cuerpo | El DTO no acepta identificadores de persona; se ignoran | CA-09, CA-15, CA-20, CA-24 |
| Inyección en filtros y parámetros | Parámetros tipados con validador; nada se concatena en consultas | CA-05 a CA-06, CA-10 a CA-11, CA-16, CA-21 |
| Ruta sin sesión | JWT obligatorio salvo las públicas | CA-02, CA-04, CA-08, CA-14, CA-19 |
| Abuso por repetición o cuerpos grandes | Límite de peticiones, `Retry-After` y tope de cuerpo | CA-26 |
| Datos personales en registros y errores | Registros sin datos personales; ProblemDetails sin valores | CA-25 |
| Duplicados por reintento | Regla de estado o índice único, sin `Idempotency-Key` | CA-27 |

## 10. Plan de pruebas
Las pruebas citan el caso como `CA_041_NN` en el nombre (para `python scripts/sdd_lint.py --trace 041`).

| Tipo | ¿Aplica? | Qué se prueba / herramienta |
|---|---|---|
| Unitarias | Sí | Validadores, reglas de estado y cálculos del dominio |
| Integración | Sí | Cada ruta con la base real en contenedor (Testcontainers) y `MarketApiFactory`/fábrica del servicio |
| Seguridad negativa | Sí | 401, 403/404, 422, 413 y 429 (CA-02, CA-04 a CA-06, CA-08 a CA-11, CA-14 a CA-16, CA-19 a CA-21, CA-24, CA-26) |
| Base de datos | Sí | Índices y reglas de cada colección de §6 |
| Contrato | Sí | OpenAPI regenerado y comparado con `contracts/` |
| Mutación | Sí | Meta del módulo según la spec 008 |
| Rendimiento | No aplica | Sin ruta crítica; se mide solo si la spec 034 lo pide |

## 11. Escenarios
- **CAS-01** — pantallas: Mi casa y mi auto.
- **CAS-02** — pantallas: Mi casa y mi auto.
- **CAS-03** — pantallas: Mi casa y mi auto.
- **CAS-04** — pantallas: Mi casa y mi auto.
- **CAS-05** — pantallas: Mi casa y mi auto.

## 12. Definición de terminado
- Los criterios de aceptación están cubiertos por pruebas en verde y `sdd_lint --trace 041` sin faltantes.
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
- El historial de cada elemento se arma con visitas terminadas: confirmar de qué colección sale y si se materializa o se consulta.
- Confirmar límites (cantidad de elementos y tamaño de las notas).
- Esta spec se generó a partir del Excel; antes de aprobarla hay que revisar los límites, los textos de error y los plazos con el equipo.

## Aprobación
- [ ] Revisor 1 — nombre: _____________________ fecha: __________
- [ ] Sin conflicto con la constitución
- **Estados:** Borrador → Aprobada (una persona; habilita el código) → Implementada (tras el merge)
