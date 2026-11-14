# Spec 042 — Contactos de confianza y técnicos de confianza

> **Estado:** Borrador · **Repositorio:** knock-backend · **Rector:** `constitution.md`
> **Origen:** `Documentacion/00_Profesores/Desarrollo_Movil/Entregables/Knock_Endpoints_CRUD.xlsx` (revisión 2026-11-02): endpoints CON-01, CON-02, CON-03, TCO-01, TCO-02, TCO-03.
> **Claves de trabajo:** KNOCK-266 (tarea paraguas de las specs de los endpoints del Excel) · **Sprint:** por definir · **Prioridad:** por definir
> **Servicio:** Identidad (`Knock.Identity.Api`) · **Módulo:** `Trust` · **Colecciones:** `contactos_confianza`, `tecnicos_confianza`
> **Responsable sugerido:** por asignar (Alexis o Guillermo) · Erick consume el contrato (frontend 032)

## 1. Objetivo
Definir y especificar los endpoints CON-01, CON-02, CON-03, TCO-01, TCO-02, TCO-03 del Excel de endpoints para que el equipo los implemente con el mismo estilo, seguridad y pruebas que las specs 020 a 037. Como cliente, quiero guardar personas y técnicos de confianza, para avisarles de mis visitas y volver a llamar a quien ya me atendió bien.

## 2. Historia y alcance
**2.1 Historia**
- Como cliente, quiero guardar personas y técnicos de confianza, para avisarles de mis visitas y volver a llamar a quien ya me atendió bien.

**2.2 Dentro**
- `GET /api/v1/me/contactos-confianza` (CON-01): Lista los contactos.
- `POST /api/v1/me/contactos-confianza` (CON-02): Agrega un contacto.
- `DELETE /api/v1/me/contactos-confianza/{id}` (CON-03): Quita un contacto.
- `GET /api/v1/me/tecnicos-confianza` (TCO-01): Lista los técnicos guardados.
- `PUT /api/v1/me/tecnicos-confianza/{tecnicoId}` (TCO-02): Guarda a un técnico de confianza (requiere cuenta).
- `DELETE /api/v1/me/tecnicos-confianza/{tecnicoId}` (TCO-03): Quita un técnico de la lista.
- Los índices y las reglas de cada campo de las colecciones de §6, el límite de peticiones y la documentación del contrato.

**2.3 Fuera**
- La implementación: esta spec solo se escribe; las tareas se activan cuando una persona la apruebe y lo indique.
- Pantallas y textos de la app: los define la spec de frontend 032.
- Endpoints de otras colecciones o de otras specs (020 a 038) que el Excel nombra distinto: se anotan en §15.

## 3. Requisitos
- **R-01:** `GET /api/v1/me/contactos-confianza` (leer todos): Lista los contactos. Lo puede llamar: persona con sesión (cliente o técnico). Responde 200 cuando sale bien.
- **R-02:** `POST /api/v1/me/contactos-confianza` (crear): Agrega un contacto. Lo puede llamar: persona con sesión (cliente o técnico). Responde 201 cuando sale bien.
- **R-03:** Entrada de `POST /api/v1/me/contactos-confianza`: Cuerpo: nombre, celular. Un valor inválido responde 422 con `errors.<campo>` sin repetirlo; un cuerpo ilegible, 400. Reglas: `nombre`: Mínimo 2 caracteres; `celular`: 10 dígitos. Cifrado.
- **R-04:** `DELETE /api/v1/me/contactos-confianza/{id}` (borrar): Quita un contacto. Lo puede llamar: persona con sesión que sea dueña del recurso. Responde 204 cuando sale bien.
- **R-05:** Entrada de `DELETE /api/v1/me/contactos-confianza/{id}`: Ruta: id. Un valor inválido responde 422 con `errors.<campo>` sin repetirlo; un cuerpo ilegible, 400.
- **R-06:** Estados de `DELETE /api/v1/me/contactos-confianza/{id}`: 404. El código de negocio de cada conflicto va en inglés y en snake_case, y se define al implementar.
- **R-07:** `GET /api/v1/me/tecnicos-confianza` (leer todos): Lista los técnicos guardados. Lo puede llamar: persona con sesión (cliente o técnico). Responde 200 cuando sale bien.
- **R-08:** `GET /api/v1/me/tecnicos-confianza` pagina por cursor (Por cursor, 20): parámetros `cursor` y `limite`, 20 por defecto y 50 como máximo; orden: Orden por fecha descendente.
- **R-09:** `PUT /api/v1/me/tecnicos-confianza/{tecnicoId}` (crear): Guarda a un técnico de confianza (requiere cuenta). Lo puede llamar: persona con sesión (cliente o técnico). Responde 200 cuando sale bien.
- **R-10:** Entrada de `PUT /api/v1/me/tecnicos-confianza/{tecnicoId}`: Ruta: tecnicoId. Un valor inválido responde 422 con `errors.<campo>` sin repetirlo; un cuerpo ilegible, 400. Reglas: `tecnicoId`: Debe existir en tecnicos.
- **R-11:** Estados de `PUT /api/v1/me/tecnicos-confianza/{tecnicoId}`: 201; 404 `technician_not_found`. Los códigos de negocio van en inglés y en snake_case.
- **R-12:** `DELETE /api/v1/me/tecnicos-confianza/{tecnicoId}` (borrar): Quita un técnico de la lista. Lo puede llamar: persona con sesión (cliente o técnico). Responde 204 cuando sale bien.
- **R-13:** Entrada de `DELETE /api/v1/me/tecnicos-confianza/{tecnicoId}`: Ruta: tecnicoId. Un valor inválido responde 422 con `errors.<campo>` sin repetirlo; un cuerpo ilegible, 400. Reglas: `tecnicoId`: Debe existir en tecnicos.
- **R-14:** Estados de `DELETE /api/v1/me/tecnicos-confianza/{tecnicoId}`: 404 `trusted_technician_not_found`. Los códigos de negocio van en inglés y en snake_case.
- **R-15:** **Contactos de confianza (CON-01 a CON-03).** `nombre` de 2 a 60 caracteres y `celular` de 10 dígitos de México, cifrado a nivel de campo y nunca repetido en registros; hasta 5 contactos por persona (el sexto responde 409 `contact_limit_reached`) y sin repetir el mismo celular en la lista de una persona. Un contacto ajeno responde 404.
- **R-16:** **Consentimiento del contacto.** Agregar a alguien como contacto no le envía nada: el primer mensaje solo sale cuando la persona usa «Avisar a alguien de confianza» (spec 022) y lleva la opción de dejar de recibirlos; un contacto que pidió no recibir más queda marcado y no se le envía.
- **R-17:** **Técnicos de confianza (TCO-01 a TCO-03).** Guardar a un técnico requiere cuenta y un técnico visible; `PUT` es idempotente: 201 la primera vez y 200 al repetir, sin duplicar. La lista se pagina por cursor (20 por defecto, máximo 50) con el más reciente primero. Quitar un técnico que no está en la lista responde 404.
- **R-18:** **Técnico no disponible.** Un técnico que se oculta o se bloquea sale de la lista de la persona al consultarla (no se borra el registro) y no se le pueden mandar solicitudes directas.
- **R-19:** La persona de cada operación es siempre la del `sub` del token; ningún cuerpo, ruta o consulta acepta el identificador de otra persona para actuar en su nombre, y un identificador ajeno enviado en el cuerpo se ignora.
- **R-20:** Los errores usan ProblemDetails con `correlationId` y `traceId`; los registros llevan método, ruta, estado y correlación, sin teléfono, dirección, coordenadas, nombres ni textos escritos por la persona (spec 011).
- **R-21:** Por encima del límite de peticiones responde 429 con `Retry-After`; cada ruta tiene su límite registrado en `docs/rate-limit-matrix.md` y los cuerpos se limitan según la spec 009 (413 si lo exceden).
- **R-22:** Las rutas que crean algo no usan `Idempotency-Key` (retirado en la spec 020): la repetición se controla con la regla de estado o el índice único de §6.

## 4. Criterios de aceptación
- **CA-01:** Dado el rol «persona con sesión (cliente o técnico)», cuando llama `GET /api/v1/me/contactos-confianza` con datos válidos, entonces responde 200 con lo descrito en §5.
- **CA-02:** Dada una petición sin token o con token vencido, cuando llama `GET /api/v1/me/contactos-confianza`, entonces responde 401 y no ejecuta la operación.
- **CA-03:** Dado el rol «persona con sesión (cliente o técnico)», cuando llama `POST /api/v1/me/contactos-confianza` con datos válidos, entonces responde 201 con lo descrito en §5.
- **CA-04:** Dada una petición sin token o con token vencido, cuando llama `POST /api/v1/me/contactos-confianza`, entonces responde 401 y no ejecuta la operación.
- **CA-05:** Dado un valor inválido según las reglas de §6, cuando llama `POST /api/v1/me/contactos-confianza`, entonces responde 422 con `errors.<campo>`, no repite el valor y no guarda nada.
- **CA-06:** Dado un cuerpo que no se puede leer, cuando llama `POST /api/v1/me/contactos-confianza`, entonces responde 400 y no guarda nada.
- **CA-07:** Dado el rol «persona con sesión que sea dueña del recurso», cuando llama `DELETE /api/v1/me/contactos-confianza/{id}` con datos válidos, entonces responde 204 con lo descrito en §5.
- **CA-08:** Dada una petición sin token o con token vencido, cuando llama `DELETE /api/v1/me/contactos-confianza/{id}`, entonces responde 401 y no ejecuta la operación.
- **CA-09:** Dada una persona que no cumple el rol «persona con sesión que sea dueña del recurso», cuando llama `DELETE /api/v1/me/contactos-confianza/{id}`, entonces responde 403 y no cambia nada.
- **CA-10:** Dado un valor inválido según las reglas de §6, cuando llama `DELETE /api/v1/me/contactos-confianza/{id}`, entonces responde 422 con `errors.<campo>`, no repite el valor y no guarda nada.
- **CA-11:** Dado el caso «recurso no disponible», cuando llama `DELETE /api/v1/me/contactos-confianza/{id}`, entonces responde 404 con ProblemDetails y no cambia el estado.
- **CA-12:** Dado el rol «persona con sesión (cliente o técnico)», cuando llama `GET /api/v1/me/tecnicos-confianza` con datos válidos, entonces responde 200 con lo descrito en §5.
- **CA-13:** Dada una petición sin token o con token vencido, cuando llama `GET /api/v1/me/tecnicos-confianza`, entonces responde 401 y no ejecuta la operación.
- **CA-14:** Dado un listado con más de 20 elementos, cuando se pide sin `limite`, entonces devuelve 20 y el cursor de la página siguiente; con `limite=51` responde 422.
- **CA-15:** Dado el rol «persona con sesión (cliente o técnico)», cuando llama `PUT /api/v1/me/tecnicos-confianza/{tecnicoId}` con datos válidos, entonces responde 200 con lo descrito en §5.
- **CA-16:** Dada una petición sin token o con token vencido, cuando llama `PUT /api/v1/me/tecnicos-confianza/{tecnicoId}`, entonces responde 401 y no ejecuta la operación.
- **CA-17:** Dado un valor inválido según las reglas de §6, cuando llama `PUT /api/v1/me/tecnicos-confianza/{tecnicoId}`, entonces responde 422 con `errors.<campo>`, no repite el valor y no guarda nada.
- **CA-18:** Dado el caso «recurso no disponible», cuando llama `PUT /api/v1/me/tecnicos-confianza/{tecnicoId}`, entonces responde 201 con ProblemDetails y no cambia el estado.
- **CA-19:** Dado el caso «el técnico no existe o está oculto», cuando llama `PUT /api/v1/me/tecnicos-confianza/{tecnicoId}`, entonces responde 404 `technician_not_found` con ProblemDetails y no cambia el estado.
- **CA-20:** Dado el rol «persona con sesión (cliente o técnico)», cuando llama `DELETE /api/v1/me/tecnicos-confianza/{tecnicoId}` con datos válidos, entonces responde 204 con lo descrito en §5.
- **CA-21:** Dada una petición sin token o con token vencido, cuando llama `DELETE /api/v1/me/tecnicos-confianza/{tecnicoId}`, entonces responde 401 y no ejecuta la operación.
- **CA-22:** Dado un valor inválido según las reglas de §6, cuando llama `DELETE /api/v1/me/tecnicos-confianza/{tecnicoId}`, entonces responde 422 con `errors.<campo>`, no repite el valor y no guarda nada.
- **CA-23:** Dado el caso «ese técnico no está en la lista de la persona», cuando llama `DELETE /api/v1/me/tecnicos-confianza/{tecnicoId}`, entonces responde 404 `trusted_technician_not_found` con ProblemDetails y no cambia el estado.
- **CA-24:** Dada una persona con 5 contactos, cuando agrega otro, entonces responde 409 `contact_limit_reached`; con un celular repetido o de menos de 10 dígitos responde 422; y un contacto ajeno responde 404.
- **CA-25:** Dado un contacto recién agregado, cuando no se ha usado el aviso, entonces no recibe ningún mensaje; y uno que pidió no recibir más no recibe avisos posteriores.
- **CA-26:** Dada una persona, cuando guarda al mismo técnico dos veces, entonces la primera responde 201 y la segunda 200 y queda un solo registro; y quitar uno que no tiene guardado responde 404.
- **CA-27:** Dado un técnico oculto, cuando su cliente consulta su lista, entonces ya no aparece; al volver a ser visible, reaparece.
- **CA-28:** Dado un cuerpo con el identificador de otra persona, cuando se llama cualquier ruta de esta spec, entonces la operación usa el `sub` del token y no el del cuerpo.
- **CA-29:** Dada una operación con datos personales, cuando se inspeccionan los registros y las respuestas de error, entonces no aparecen esos datos.
- **CA-30:** Dada una persona que supera el límite de una ruta, cuando repite la petición, entonces responde 429 con `Retry-After` y, con un cuerpo de tamaño excesivo, 413.
- **CA-31:** Dada una operación de creación repetida por mala señal, cuando llega dos veces, entonces no deja duplicados porque lo impide la regla de estado o el índice único.

**4.1 Criterio → prueba prevista**

| Criterio | Prueba prevista |
|---|---|
| CA-01 | `Spec042Tests.Criterio01_CA_042_01` |
| CA-02 | `Spec042Tests.Criterio02_CA_042_02` |
| CA-03 | `Spec042Tests.Criterio03_CA_042_03` |
| CA-04 | `Spec042Tests.Criterio04_CA_042_04` |
| CA-05 | `Spec042Tests.Criterio05_CA_042_05` |
| CA-06 | `Spec042Tests.Criterio06_CA_042_06` |
| CA-07 | `Spec042Tests.Criterio07_CA_042_07` |
| CA-08 | `Spec042Tests.Criterio08_CA_042_08` |
| CA-09 | `Spec042Tests.Criterio09_CA_042_09` |
| CA-10 | `Spec042Tests.Criterio10_CA_042_10` |
| CA-11 | `Spec042Tests.Criterio11_CA_042_11` |
| CA-12 | `Spec042Tests.Criterio12_CA_042_12` |
| CA-13 | `Spec042Tests.Criterio13_CA_042_13` |
| CA-14 | `Spec042Tests.Criterio14_CA_042_14` |
| CA-15 | `Spec042Tests.Criterio15_CA_042_15` |
| CA-16 | `Spec042Tests.Criterio16_CA_042_16` |
| CA-17 | `Spec042Tests.Criterio17_CA_042_17` |
| CA-18 | `Spec042Tests.Criterio18_CA_042_18` |
| CA-19 | `Spec042Tests.Criterio19_CA_042_19` |
| CA-20 | `Spec042Tests.Criterio20_CA_042_20` |
| CA-21 | `Spec042Tests.Criterio21_CA_042_21` |
| CA-22 | `Spec042Tests.Criterio22_CA_042_22` |
| CA-23 | `Spec042Tests.Criterio23_CA_042_23` |
| CA-24 | `Spec042Tests.Criterio24_CA_042_24` |
| CA-25 | `Spec042Tests.Criterio25_CA_042_25` |
| CA-26 | `Spec042Tests.Criterio26_CA_042_26` |
| CA-27 | `Spec042Tests.Criterio27_CA_042_27` |
| CA-28 | `Spec042Tests.Criterio28_CA_042_28` |
| CA-29 | `Spec042Tests.Criterio29_CA_042_29` |
| CA-30 | `Spec042Tests.Criterio30_CA_042_30` |
| CA-31 | `Spec042Tests.Criterio31_CA_042_31` |

## 5. Contrato
| ID | Método y ruta | Quién | Entrada | Respuestas |
|---|---|---|---|---|
| CON-01 | `GET /api/v1/me/contactos-confianza` | persona con sesión (cliente o técnico) | Ninguno | 200; 401 |
| CON-02 | `POST /api/v1/me/contactos-confianza` | persona con sesión (cliente o técnico) | Cuerpo: nombre, celular | 201; 400; 401; 422 |
| CON-03 | `DELETE /api/v1/me/contactos-confianza/{id}` | persona con sesión que sea dueña del recurso | Ruta: id | 204; 401; 403; 404 |
| TCO-01 | `GET /api/v1/me/tecnicos-confianza` | persona con sesión (cliente o técnico) | Ninguno | 200; 401 |
| TCO-02 | `PUT /api/v1/me/tecnicos-confianza/{tecnicoId}` | persona con sesión (cliente o técnico) | Ruta: tecnicoId | 200; 201; 401; 404 |
| TCO-03 | `DELETE /api/v1/me/tecnicos-confianza/{tecnicoId}` | persona con sesión (cliente o técnico) | Ruta: tecnicoId | 204; 401; 404 |

**Filtros, orden, paginación y caché**

| ID | Filtros y orden | Paginación | Caché |
|---|---|---|---|
| CON-01 | No aplica | No (pocos) | No |
| CON-02 | No aplica | No | No |
| CON-03 | No aplica | No | No |
| TCO-01 | Orden por fecha descendente | Por cursor, 20 | No |
| TCO-02 | No aplica | No | No |
| TCO-03 | No aplica | No | No |

**Códigos de error de negocio**

| ID | Estado | Código | Cuándo |
|---|---|---|---|
| TCO-02 | 404 | `technician_not_found` | el técnico no existe o está oculto |
| TCO-03 | 404 | `trusted_technician_not_found` | ese técnico no está en la lista de la persona |


**Códigos de error:** los de forma salen como 422 con `errors` por campo y los de sesión y límite como 401 y 429; los conflictos de negocio llevan un código en inglés y snake_case que se fija al implementar y se agrega al catálogo.

## 6. Modelo de datos
**Colección `contactos_confianza`.** Guarda a las personas a las que el cliente quiere avisar cuando recibe una visita a domicilio: quién va, cuándo y la identidad verificada del técnico. El mensaje nunca incluye el código de visita. Para elegir un contacto la app usa el selector del sistema o el número tecleado, sin pedir el permiso de contactos

| Campo | Tipo | Obligatorio | Reglas y validaciones | Índice |
|---|---|---|---|---|
| _id | ObjectId | Sí | Lo genera la base | Llave primaria |
| usuarioId | ObjectId | Sí | Debe existir en usuarios | Sí |
| nombre | Texto | Sí | Mínimo 2 caracteres | No |
| celular | Texto cifrado | Sí | 10 dígitos. Cifrado | No |
| creadoEn | Fecha y hora UTC | Sí | Lo asigna el servidor | No |

| Tipo | Campos | Para qué sirve |
|---|---|---|
| Llave principal | _id | Identifica de forma única cada documento. La genera la base de datos |
| Normal | usuarioId | Listar los contactos de una persona |

**Colección `tecnicos_confianza`.** Lista de técnicos que el cliente guardó para volver a contratarlos con un toque (Volver a contratar). Es una relación simple de uno a muchos: no hay recorridos de varios saltos, por eso no se usa una base de grafos

| Campo | Tipo | Obligatorio | Reglas y validaciones | Índice |
|---|---|---|---|---|
| _id | ObjectId | Sí | Lo genera la base | Llave primaria |
| clienteId | ObjectId | Sí | Debe existir en usuarios | Compuesto único |
| tecnicoId | ObjectId | Sí | Debe existir en tecnicos | Compuesto único |
| guardadoEn | Fecha y hora UTC | Sí | Lo asigna el servidor | No |

| Tipo | Campos | Para qué sirve |
|---|---|---|
| Llave principal | _id | Identifica de forma única cada documento. La genera la base de datos |
| Único compuesto | clienteId + tecnicoId | No guardar dos veces y listar los guardados de un cliente |

## 7. Arquitectura y componentes
- Proyecto `Knock.Identity.Api`, módulo `Trust` con las capas del estilo de la spec 002: `Controllers`, `Dtos`, `Validators`, `Services`, `Data` y `Hosting`.
- Cada ruta recibe sus parámetros en objetos tipados con validador registrado, nunca como `string` suelto, y consulta con filtros por el `sub` del token.
- Las rutas bajo `/admin` usan la política de rol de la spec 033 y no están disponibles sin la sesión del panel.
- La caché, la paginación por cursor y los límites usan las piezas de las specs 009 y 010.
- El contrato se publica en OpenAPI y en `docs/endpoints.md`; los límites, en `docs/rate-limit-matrix.md`.

## 8. Dependencias
- Spec 022 de este repositorio.
- Spec 037 de este repositorio.
- Specs 002, 003, 009, 010 y 011 (estilo, bases de datos, seguridad, caché y registros).
- Frontend: spec 032 consume este contrato.

## 9. Amenazas y controles
| Amenaza | Control | Casos que lo prueban |
|---|---|---|
| Acceso a datos de otra persona (IDOR) | Toda consulta y escritura filtra por el `sub` del token; ajeno responde 403 o 404 | CA-09, CA-28 |
| Asignación masiva o identificador ajeno en el cuerpo | El DTO no acepta identificadores de persona; se ignoran | CA-09, CA-28 |
| Inyección en filtros y parámetros | Parámetros tipados con validador; nada se concatena en consultas | CA-05 a CA-06, CA-10, CA-17, CA-22 |
| Ruta sin sesión | JWT obligatorio salvo las públicas | CA-02, CA-04, CA-08, CA-13, CA-16, CA-21 |
| Abuso por repetición o cuerpos grandes | Límite de peticiones, `Retry-After` y tope de cuerpo | CA-30 |
| Datos personales en registros y errores | Registros sin datos personales; ProblemDetails sin valores | CA-29 |
| Duplicados por reintento | Regla de estado o índice único, sin `Idempotency-Key` | CA-31 |

## 10. Plan de pruebas
Las pruebas citan el caso como `CA_042_NN` en el nombre (para `python scripts/sdd_lint.py --trace 042`).

| Tipo | ¿Aplica? | Qué se prueba / herramienta |
|---|---|---|
| Unitarias | Sí | Validadores, reglas de estado y cálculos del dominio |
| Integración | Sí | Cada ruta con la base real en contenedor (Testcontainers) y `MarketApiFactory`/fábrica del servicio |
| Seguridad negativa | Sí | 401, 403/404, 422, 413 y 429 (CA-02, CA-04 a CA-06, CA-08 a CA-10, CA-13, CA-16 a CA-17, CA-21 a CA-22, CA-28, CA-30) |
| Base de datos | Sí | Índices y reglas de cada colección de §6 |
| Contrato | Sí | OpenAPI regenerado y comparado con `contracts/` |
| Mutación | Sí | Meta del módulo según la spec 008 |
| Rendimiento | No aplica | Sin ruta crítica; se mide solo si la spec 034 lo pide |

## 11. Escenarios
- **Agregar contacto:** la persona abre «Contactos de confianza» → agrega a su hermana con su celular → queda guardado sin enviarle nada.
- **Guardar técnico:** al terminar una visita, el cliente toca «Guardar como de confianza» → el técnico aparece en su lista.
- **Límite:** con 5 contactos, la app pide quitar uno antes de agregar otro.

## 12. Definición de terminado
- Los criterios de aceptación están cubiertos por pruebas en verde y `sdd_lint --trace 042` sin faltantes.
- El CI en verde, un par revisó el cambio y `Memoria.md` al día.
- OpenAPI, `docs/endpoints.md` y `docs/rate-limit-matrix.md` actualizados.

## 13. Decisiones
- La paginación, la caché y los roles de cada ruta salen de la hoja de cada colección del Excel de endpoints (revisión 2026-11-02).
- Las rutas de la persona viven bajo `/me` o por identificador con dueño verificado; las del personal interno, bajo `/admin`.

## 14. Decisiones tomadas sin consulta
- **D-01:** Se toma el Excel de endpoints como fuente del contrato porque es el insumo acordado para las specs; cualquier diferencia con las specs 020 a 038 se resuelve a favor de la spec numerada.
- **D-02:** Los conflictos de negocio llevan códigos en inglés y snake_case (los de §5 y, si falta alguno, los que se fijen al implementar), siguiendo la convención de errores de la spec 002.
- **D-03:** No se usa `Idempotency-Key`, aunque el Excel lo menciona en creaciones.
- **D-04:** Máximo 5 contactos de confianza por persona: el Excel no fija el límite.
- **D-05:** Guardar un contacto no le envía ningún mensaje; el primer mensaje sale solo al usar el aviso.
- **D-06:** Un técnico oculto o bloqueado deja de verse en la lista sin borrar el registro.

## 15. Riesgos y dudas abiertas
- El aviso a un contacto de confianza lo envía la spec 022: confirmar que el mensaje de baja («no recibir más») se maneja allí y no se duplica aquí.
- Confirmar el máximo de 5 contactos y la lista de técnicos de confianza (el Excel no fija máximo).
- Confirmar si «solicitud directa a un técnico de confianza» entra a la spec 028.

## Aprobación
- [ ] Revisor 1 — nombre: _____________________ fecha: __________
- [ ] Sin conflicto con la constitución
- **Estados:** Borrador → Aprobada (una persona; habilita el código) → Implementada (tras el merge)
