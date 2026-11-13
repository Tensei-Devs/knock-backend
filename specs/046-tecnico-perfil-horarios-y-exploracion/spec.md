# Spec 046 — Técnico: perfil, horarios, disponibilidad, mapa y explorar

> **Estado:** Borrador · **Repositorio:** knock-backend · **Rector:** `constitution.md`
> **Origen:** `Documentacion/00_Profesores/Desarrollo_Movil/Entregables/Knock_Endpoints_CRUD.xlsx` (revisión 2026-11-02): endpoints TEC-01, TEC-02, TEC-03, TEC-04, TEC-05, TEC-06, TEC-07, TEC-08, TEC-09, TEC-10, TEC-11, TEC-13, TEC-14.
> **Claves de trabajo:** por asignar (se crea la tarea de Jira cuando se decida el alcance) · **Sprint:** por definir · **Prioridad:** por definir
> **Servicio:** Mercado (`Knock.Market.Api`) · **Módulo:** `Technicians` · **Colecciones:** `tecnicos`
> **Responsable sugerido:** por asignar (Alexis o Guillermo) · Erick consume el contrato (frontend 036)

## 1. Objetivo
Definir y especificar los endpoints TEC-01, TEC-02, TEC-03, TEC-04, TEC-05, TEC-06, TEC-07, TEC-08, TEC-09, TEC-10, TEC-11, TEC-13, TEC-14 del Excel de endpoints para que el equipo los implemente con el mismo estilo, seguridad y pruebas que las specs 020 a 037. Como técnico, quiero crear mi perfil, definir mis horarios y mi disponibilidad, y como cliente quiero explorar técnicos en el mapa, para encontrar a la persona adecuada cerca de mí.

## 2. Historia y alcance
**2.1 Historia**
- Como técnico, quiero crear mi perfil, definir mis horarios y mi disponibilidad, y como cliente quiero explorar técnicos en el mapa, para encontrar a la persona adecuada cerca de mí.

**2.2 Dentro**
- `POST /api/v1/tecnicos` (TEC-01): Registra a la persona como técnico (nombre y apellido, oficios, taller o domicilio, radio, perfil opcional y términos).
- `GET /api/v1/tecnicos` (TEC-02): Busca técnicos por oficio, colonia, distancia o radio, Disponible hoy, solo verificados y texto libre.
- `GET /api/v1/tecnicos/autocompletar` (TEC-03): Sugerencias de oficios, servicios y negocios mientras se escribe (por ejemplo, boiler, frenos).
- `GET /api/v1/mapa/tecnicos` (TEC-04): Técnicos de la zona visible del mapa, ya agrupados, para no mandarlos todos.
- `GET /api/v1/tecnicos/{id}` (TEC-05): Perfil del técnico. Sin sesión devuelve solo el resumen (foto, verificación, calificación, experiencia, descripción corta); con sesión suma servicios y precios, colonias….
- `GET /api/v1/tecnicos/{id}/llega-a-mi-colonia` (TEC-06): Dice si el técnico atiende la colonia de la persona.
- `GET /api/v1/tecnicos/{id}/fotos-trabajos` (TEC-07): Fotos de trabajos terminados que el cliente permitió publicar.
- `PUT /api/v1/tecnicos/me` (TEC-08): Edita el perfil público: negocio, años, oficios, forma de atención, radio, descripción, foto.
- `PUT /api/v1/tecnicos/me/disponibilidad` (TEC-09): Cambia el interruptor Disponible hoy.
- `GET /api/v1/tecnicos/me/horarios` (TEC-10): Horarios del técnico.
- `PUT /api/v1/tecnicos/me/horarios` (TEC-11): Cambia abierto o cerrado por día.
- `GET /api/v1/tecnicos/me/reputacion` (TEC-13): Calificación promedio, distribución por estrellas y nivel.
- `GET /api/v1/explorar` (TEC-14): Bloques de la pestaña Explorar: servicios populares, trabajos reales, cerca de ti, mejor calificados e ideas, con el filtro elegido.
- Los índices y las reglas de cada campo de las colecciones de §6, el límite de peticiones y la documentación del contrato.

**2.3 Fuera**
- La implementación: esta spec solo se escribe; las tareas se activan cuando una persona la apruebe y lo indique.
- Pantallas y textos de la app: los define la spec de frontend 036.
- Endpoints de otras colecciones o de otras specs (020 a 038) que el Excel nombra distinto: se anotan en §15.

## 3. Requisitos
- **R-01:** `POST /api/v1/tecnicos` (crear): Registra a la persona como técnico (nombre y apellido, oficios, taller o domicilio, radio, perfil opcional y términos). Lo puede llamar: persona con sesión (cliente o técnico). Responde 201 cuando sale bien.
- **R-02:** Entrada de `POST /api/v1/tecnicos`: Cuerpo: oficios, atencion, direccionTaller o radioKm, perfil opcional, aceptaTerminos. Un valor inválido responde 422 con `errors.<campo>` sin repetirlo; un cuerpo ilegible, 400. Reglas: `oficios`: Al menos uno; `atencion`: Solo esos valores; `direccionTaller`: Obligatoria si atiende en taller (mínimo 8 caracteres). No se muestra si atiende a domici…; `radioKm`: 3, 5, 8 o 12.
- **R-03:** Estados de `POST /api/v1/tecnicos`: 409 (ya es técnico). El código de negocio de cada conflicto va en inglés y en snake_case, y se define al implementar.
- **R-04:** `GET /api/v1/tecnicos` (leer todos / filtro / búsqueda): Busca técnicos por oficio, colonia, distancia o radio, Disponible hoy, solo verificados y texto libre. Lo puede llamar: cualquier persona para el resumen y una persona con sesión para el detalle. Responde 200 cuando sale bien.
- **R-05:** Entrada de `GET /api/v1/tecnicos`: Query: oficio, colonia, lat, lng, radioKm, disponibleHoy, soloVerificados, q, orden, cursor, limite. Un valor inválido responde 422 con `errors.<campo>` sin repetirlo. Reglas: `radioKm`: 3, 5, 8 o 12; `disponibleHoy`: Necesario para recibir solicitudes.
- **R-06:** `GET /api/v1/tecnicos` pagina por cursor (Por cursor, 20 (máx. 50)): parámetros `cursor` y `limite`, 20 por defecto y 50 como máximo; orden: Orden: distancia, calificacion o recomendadas.
- **R-07:** `GET /api/v1/tecnicos` se cachea (Sí, unos segundos); la clave nunca incluye datos personales de otra persona y se invalida al escribir.
- **R-08:** `GET /api/v1/tecnicos/autocompletar` (autocompletado): Sugerencias de oficios, servicios y negocios mientras se escribe (por ejemplo, boiler, frenos). Lo puede llamar: cualquier persona, sin sesión. Responde 200 cuando sale bien.
- **R-09:** Entrada de `GET /api/v1/tecnicos/autocompletar`: Query: q (mínimo 2 caracteres). Un valor inválido responde 422 con `errors.<campo>` sin repetirlo.
- **R-10:** `GET /api/v1/tecnicos/autocompletar` se cachea (Sí, unos segundos); la clave nunca incluye datos personales de otra persona y se invalida al escribir.
- **R-11:** `GET /api/v1/mapa/tecnicos` (búsqueda específica): Técnicos de la zona visible del mapa, ya agrupados, para no mandarlos todos. Lo puede llamar: cualquier persona, sin sesión. Responde 200 cuando sale bien.
- **R-12:** Entrada de `GET /api/v1/mapa/tecnicos`: Query: norte, sur, este, oeste, oficio, zoom. Un valor inválido responde 422 con `errors.<campo>` sin repetirlo.
- **R-13:** `GET /api/v1/mapa/tecnicos` se cachea (Sí, unos segundos); la clave nunca incluye datos personales de otra persona y se invalida al escribir.
- **R-14:** `GET /api/v1/tecnicos/{id}` (leer uno): Perfil del técnico. Sin sesión devuelve solo el resumen (foto, verificación, calificación, experiencia, descripción corta); con sesión suma servicios y precios, colonias, fotos y horarios. Lo puede llamar: cualquier persona para el resumen y una persona con sesión para el detalle. Responde 200 cuando sale bien.
- **R-15:** Entrada de `GET /api/v1/tecnicos/{id}`: Ruta: id. Un valor inválido responde 422 con `errors.<campo>` sin repetirlo.
- **R-16:** Estados de `GET /api/v1/tecnicos/{id}`: 404. El código de negocio de cada conflicto va en inglés y en snake_case, y se define al implementar.
- **R-17:** `GET /api/v1/tecnicos/{id}` se cachea (Sí, 1 minuto); la clave nunca incluye datos personales de otra persona y se invalida al escribir.
- **R-18:** `GET /api/v1/tecnicos/{id}/llega-a-mi-colonia` (búsqueda específica): Dice si el técnico atiende la colonia de la persona. Lo puede llamar: persona con sesión (cliente o técnico). Responde 200 cuando sale bien.
- **R-19:** Entrada de `GET /api/v1/tecnicos/{id}/llega-a-mi-colonia`: Ruta: id. Query: colonia. Un valor inválido responde 422 con `errors.<campo>` sin repetirlo.
- **R-20:** Estados de `GET /api/v1/tecnicos/{id}/llega-a-mi-colonia`: 404. El código de negocio de cada conflicto va en inglés y en snake_case, y se define al implementar.
- **R-21:** `GET /api/v1/tecnicos/{id}/llega-a-mi-colonia` se cachea (Sí, 1 minuto); la clave nunca incluye datos personales de otra persona y se invalida al escribir.
- **R-22:** `GET /api/v1/tecnicos/{id}/fotos-trabajos` (leer todos): Fotos de trabajos terminados que el cliente permitió publicar. Lo puede llamar: persona con sesión (cliente o técnico). Responde 200 cuando sale bien.
- **R-23:** Entrada de `GET /api/v1/tecnicos/{id}/fotos-trabajos`: Ruta: id. Un valor inválido responde 422 con `errors.<campo>` sin repetirlo.
- **R-24:** Estados de `GET /api/v1/tecnicos/{id}/fotos-trabajos`: 404. El código de negocio de cada conflicto va en inglés y en snake_case, y se define al implementar.
- **R-25:** `GET /api/v1/tecnicos/{id}/fotos-trabajos` pagina por cursor (Por cursor, 20): parámetros `cursor` y `limite`, 20 por defecto y 50 como máximo; orden: Orden por fecha descendente.
- **R-26:** `GET /api/v1/tecnicos/{id}/fotos-trabajos` se cachea (Sí, 1 minuto); la clave nunca incluye datos personales de otra persona y se invalida al escribir.
- **R-27:** `PUT /api/v1/tecnicos/me` (actualizar): Edita el perfil público: negocio, años, oficios, forma de atención, radio, descripción, foto. Lo puede llamar: cuenta con perfil de técnico. Responde 200 cuando sale bien.
- **R-28:** Entrada de `PUT /api/v1/tecnicos/me`: Cuerpo: campos a cambiar. Un valor inválido responde 422 con `errors.<campo>` sin repetirlo; un cuerpo ilegible, 400.
- **R-29:** `PUT /api/v1/tecnicos/me/disponibilidad` (actualizar): Cambia el interruptor Disponible hoy. Lo puede llamar: cuenta con perfil de técnico. Responde 200 cuando sale bien.
- **R-30:** Entrada de `PUT /api/v1/tecnicos/me/disponibilidad`: Cuerpo: disponibleHoy. Un valor inválido responde 422 con `errors.<campo>` sin repetirlo; un cuerpo ilegible, 400. Reglas: `disponibleHoy`: Necesario para recibir solicitudes.
- **R-31:** `GET /api/v1/tecnicos/me/horarios` (leer uno): Horarios del técnico. Lo puede llamar: cuenta con perfil de técnico. Responde 200 cuando sale bien.
- **R-32:** `PUT /api/v1/tecnicos/me/horarios` (actualizar): Cambia abierto o cerrado por día. Lo puede llamar: cuenta con perfil de técnico. Responde 200 cuando sale bien.
- **R-33:** Entrada de `PUT /api/v1/tecnicos/me/horarios`: Cuerpo: horarios. Un valor inválido responde 422 con `errors.<campo>` sin repetirlo; un cuerpo ilegible, 400.
- **R-34:** `GET /api/v1/tecnicos/me/reputacion` (leer uno): Calificación promedio, distribución por estrellas y nivel. Lo puede llamar: cuenta con perfil de técnico. Responde 200 cuando sale bien.
- **R-35:** `GET /api/v1/tecnicos/me/reputacion` se cachea (Sí, 1 minuto); la clave nunca incluye datos personales de otra persona y se invalida al escribir.
- **R-36:** `GET /api/v1/explorar` (búsqueda específica): Bloques de la pestaña Explorar: servicios populares, trabajos reales, cerca de ti, mejor calificados e ideas, con el filtro elegido. Lo puede llamar: cualquier persona para el resumen y una persona con sesión para el detalle. Responde 200 cuando sale bien.
- **R-37:** Entrada de `GET /api/v1/explorar`: Query: filtro (todo|auto|agua|luz), colonia. Un valor inválido responde 422 con `errors.<campo>` sin repetirlo.
- **R-38:** `GET /api/v1/explorar` se cachea (Sí, 1 minuto); la clave nunca incluye datos personales de otra persona y se invalida al escribir.
- **R-39:** La persona de cada operación es siempre la del `sub` del token; ningún cuerpo, ruta o consulta acepta el identificador de otra persona para actuar en su nombre, y un identificador ajeno enviado en el cuerpo se ignora.
- **R-40:** Los errores usan ProblemDetails con `correlationId` y `traceId`; los registros llevan método, ruta, estado y correlación, sin teléfono, dirección, coordenadas, nombres ni textos escritos por la persona (spec 011).
- **R-41:** Por encima del límite de peticiones responde 429 con `Retry-After`; cada ruta tiene su límite registrado en `docs/rate-limit-matrix.md` y los cuerpos se limitan según la spec 009 (413 si lo exceden).
- **R-42:** Las rutas que crean algo no usan `Idempotency-Key` (retirado en la spec 020): la repetición se controla con la regla de estado o el índice único de §6.

## 4. Criterios de aceptación
- **CA-01:** Dado el rol «persona con sesión (cliente o técnico)», cuando llama `POST /api/v1/tecnicos` con datos válidos, entonces responde 201 con lo descrito en §5.
- **CA-02:** Dada una petición sin token o con token vencido, cuando llama `POST /api/v1/tecnicos`, entonces responde 401 y no ejecuta la operación.
- **CA-03:** Dado un valor inválido según las reglas de §6, cuando llama `POST /api/v1/tecnicos`, entonces responde 422 con `errors.<campo>`, no repite el valor y no guarda nada.
- **CA-04:** Dado un cuerpo que no se puede leer, cuando llama `POST /api/v1/tecnicos`, entonces responde 400 y no guarda nada.
- **CA-05:** Dado el caso «ya es técnico», cuando llama `POST /api/v1/tecnicos`, entonces responde 409 con ProblemDetails y no cambia el estado.
- **CA-06:** Dado el rol «cualquier persona para el resumen y una persona con sesión para el detalle», cuando llama `GET /api/v1/tecnicos` con datos válidos, entonces responde 200 con lo descrito en §5.
- **CA-07:** Dado un valor inválido según las reglas de §6, cuando llama `GET /api/v1/tecnicos`, entonces responde 422 con `errors.<campo>`, no repite el valor y no devuelve datos.
- **CA-08:** Dado un listado con más de 20 elementos, cuando se pide sin `limite`, entonces devuelve 20 y el cursor de la página siguiente; con `limite=51` responde 422.
- **CA-09:** Dada una consulta repetida dentro del tiempo de caché, cuando llega la segunda, entonces se responde desde la caché y la base recibe una sola consulta.
- **CA-10:** Dado el rol «cualquier persona, sin sesión», cuando llama `GET /api/v1/tecnicos/autocompletar` con datos válidos, entonces responde 200 con lo descrito en §5.
- **CA-11:** Dado un valor inválido según las reglas de §6, cuando llama `GET /api/v1/tecnicos/autocompletar`, entonces responde 422 con `errors.<campo>`, no repite el valor y no devuelve datos.
- **CA-12:** Dada una consulta repetida dentro del tiempo de caché, cuando llega la segunda, entonces se responde desde la caché y la base recibe una sola consulta.
- **CA-13:** Dado el rol «cualquier persona, sin sesión», cuando llama `GET /api/v1/mapa/tecnicos` con datos válidos, entonces responde 200 con lo descrito en §5.
- **CA-14:** Dado un valor inválido según las reglas de §6, cuando llama `GET /api/v1/mapa/tecnicos`, entonces responde 422 con `errors.<campo>`, no repite el valor y no devuelve datos.
- **CA-15:** Dada una consulta repetida dentro del tiempo de caché, cuando llega la segunda, entonces se responde desde la caché y la base recibe una sola consulta.
- **CA-16:** Dado el rol «cualquier persona para el resumen y una persona con sesión para el detalle», cuando llama `GET /api/v1/tecnicos/{id}` con datos válidos, entonces responde 200 con lo descrito en §5.
- **CA-17:** Dado un valor inválido según las reglas de §6, cuando llama `GET /api/v1/tecnicos/{id}`, entonces responde 422 con `errors.<campo>`, no repite el valor y no devuelve datos.
- **CA-18:** Dado el caso «recurso no disponible», cuando llama `GET /api/v1/tecnicos/{id}`, entonces responde 404 con ProblemDetails y no cambia el estado.
- **CA-19:** Dada una consulta repetida dentro del tiempo de caché, cuando llega la segunda, entonces se responde desde la caché y la base recibe una sola consulta.
- **CA-20:** Dado el rol «persona con sesión (cliente o técnico)», cuando llama `GET /api/v1/tecnicos/{id}/llega-a-mi-colonia` con datos válidos, entonces responde 200 con lo descrito en §5.
- **CA-21:** Dada una petición sin token o con token vencido, cuando llama `GET /api/v1/tecnicos/{id}/llega-a-mi-colonia`, entonces responde 401 y no ejecuta la operación.
- **CA-22:** Dado un valor inválido según las reglas de §6, cuando llama `GET /api/v1/tecnicos/{id}/llega-a-mi-colonia`, entonces responde 422 con `errors.<campo>`, no repite el valor y no devuelve datos.
- **CA-23:** Dado el caso «recurso no disponible», cuando llama `GET /api/v1/tecnicos/{id}/llega-a-mi-colonia`, entonces responde 404 con ProblemDetails y no cambia el estado.
- **CA-24:** Dada una consulta repetida dentro del tiempo de caché, cuando llega la segunda, entonces se responde desde la caché y la base recibe una sola consulta.
- **CA-25:** Dado el rol «persona con sesión (cliente o técnico)», cuando llama `GET /api/v1/tecnicos/{id}/fotos-trabajos` con datos válidos, entonces responde 200 con lo descrito en §5.
- **CA-26:** Dada una petición sin token o con token vencido, cuando llama `GET /api/v1/tecnicos/{id}/fotos-trabajos`, entonces responde 401 y no ejecuta la operación.
- **CA-27:** Dado un valor inválido según las reglas de §6, cuando llama `GET /api/v1/tecnicos/{id}/fotos-trabajos`, entonces responde 422 con `errors.<campo>`, no repite el valor y no devuelve datos.
- **CA-28:** Dado el caso «recurso no disponible», cuando llama `GET /api/v1/tecnicos/{id}/fotos-trabajos`, entonces responde 404 con ProblemDetails y no cambia el estado.
- **CA-29:** Dado un listado con más de 20 elementos, cuando se pide sin `limite`, entonces devuelve 20 y el cursor de la página siguiente; con `limite=51` responde 422.
- **CA-30:** Dada una consulta repetida dentro del tiempo de caché, cuando llega la segunda, entonces se responde desde la caché y la base recibe una sola consulta.
- **CA-31:** Dado el rol «cuenta con perfil de técnico», cuando llama `PUT /api/v1/tecnicos/me` con datos válidos, entonces responde 200 con lo descrito en §5.
- **CA-32:** Dada una petición sin token o con token vencido, cuando llama `PUT /api/v1/tecnicos/me`, entonces responde 401 y no ejecuta la operación.
- **CA-33:** Dada una persona que no cumple el rol «cuenta con perfil de técnico», cuando llama `PUT /api/v1/tecnicos/me`, entonces responde 404 sin revelar que el recurso existe y no cambia nada.
- **CA-34:** Dado un valor inválido según las reglas de §6, cuando llama `PUT /api/v1/tecnicos/me`, entonces responde 422 con `errors.<campo>`, no repite el valor y no guarda nada.
- **CA-35:** Dado un cuerpo que no se puede leer, cuando llama `PUT /api/v1/tecnicos/me`, entonces responde 400 y no guarda nada.
- **CA-36:** Dado el rol «cuenta con perfil de técnico», cuando llama `PUT /api/v1/tecnicos/me/disponibilidad` con datos válidos, entonces responde 200 con lo descrito en §5.
- **CA-37:** Dada una petición sin token o con token vencido, cuando llama `PUT /api/v1/tecnicos/me/disponibilidad`, entonces responde 401 y no ejecuta la operación.
- **CA-38:** Dada una persona que no cumple el rol «cuenta con perfil de técnico», cuando llama `PUT /api/v1/tecnicos/me/disponibilidad`, entonces responde 403 y no cambia nada.
- **CA-39:** Dado un valor inválido según las reglas de §6, cuando llama `PUT /api/v1/tecnicos/me/disponibilidad`, entonces responde 422 con `errors.<campo>`, no repite el valor y no guarda nada.
- **CA-40:** Dado el rol «cuenta con perfil de técnico», cuando llama `GET /api/v1/tecnicos/me/horarios` con datos válidos, entonces responde 200 con lo descrito en §5.
- **CA-41:** Dada una petición sin token o con token vencido, cuando llama `GET /api/v1/tecnicos/me/horarios`, entonces responde 401 y no ejecuta la operación.
- **CA-42:** Dada una persona que no cumple el rol «cuenta con perfil de técnico», cuando llama `GET /api/v1/tecnicos/me/horarios`, entonces responde 404 sin revelar que el recurso existe y no cambia nada.
- **CA-43:** Dado el rol «cuenta con perfil de técnico», cuando llama `PUT /api/v1/tecnicos/me/horarios` con datos válidos, entonces responde 200 con lo descrito en §5.
- **CA-44:** Dada una petición sin token o con token vencido, cuando llama `PUT /api/v1/tecnicos/me/horarios`, entonces responde 401 y no ejecuta la operación.
- **CA-45:** Dada una persona que no cumple el rol «cuenta con perfil de técnico», cuando llama `PUT /api/v1/tecnicos/me/horarios`, entonces responde 404 sin revelar que el recurso existe y no cambia nada.
- **CA-46:** Dado un valor inválido según las reglas de §6, cuando llama `PUT /api/v1/tecnicos/me/horarios`, entonces responde 422 con `errors.<campo>`, no repite el valor y no guarda nada.
- **CA-47:** Dado un cuerpo que no se puede leer, cuando llama `PUT /api/v1/tecnicos/me/horarios`, entonces responde 400 y no guarda nada.
- **CA-48:** Dado el rol «cuenta con perfil de técnico», cuando llama `GET /api/v1/tecnicos/me/reputacion` con datos válidos, entonces responde 200 con lo descrito en §5.
- **CA-49:** Dada una petición sin token o con token vencido, cuando llama `GET /api/v1/tecnicos/me/reputacion`, entonces responde 401 y no ejecuta la operación.
- **CA-50:** Dada una persona que no cumple el rol «cuenta con perfil de técnico», cuando llama `GET /api/v1/tecnicos/me/reputacion`, entonces responde 404 sin revelar que el recurso existe y no cambia nada.
- **CA-51:** Dada una consulta repetida dentro del tiempo de caché, cuando llega la segunda, entonces se responde desde la caché y la base recibe una sola consulta.
- **CA-52:** Dado el rol «cualquier persona para el resumen y una persona con sesión para el detalle», cuando llama `GET /api/v1/explorar` con datos válidos, entonces responde 200 con lo descrito en §5.
- **CA-53:** Dado un valor inválido según las reglas de §6, cuando llama `GET /api/v1/explorar`, entonces responde 422 con `errors.<campo>`, no repite el valor y no devuelve datos.
- **CA-54:** Dada una consulta repetida dentro del tiempo de caché, cuando llega la segunda, entonces se responde desde la caché y la base recibe una sola consulta.
- **CA-55:** Dado un cuerpo con el identificador de otra persona, cuando se llama cualquier ruta de esta spec, entonces la operación usa el `sub` del token y no el del cuerpo.
- **CA-56:** Dada una operación con datos personales, cuando se inspeccionan los registros y las respuestas de error, entonces no aparecen esos datos.
- **CA-57:** Dada una persona que supera el límite de una ruta, cuando repite la petición, entonces responde 429 con `Retry-After` y, con un cuerpo de tamaño excesivo, 413.
- **CA-58:** Dada una operación de creación repetida por mala señal, cuando llega dos veces, entonces no deja duplicados porque lo impide la regla de estado o el índice único.

**4.1 Criterio → prueba prevista**

| Criterio | Prueba prevista |
|---|---|
| CA-01 | `Spec046Tests.Criterio01_CA_046_01` |
| CA-02 | `Spec046Tests.Criterio02_CA_046_02` |
| CA-03 | `Spec046Tests.Criterio03_CA_046_03` |
| CA-04 | `Spec046Tests.Criterio04_CA_046_04` |
| CA-05 | `Spec046Tests.Criterio05_CA_046_05` |
| CA-06 | `Spec046Tests.Criterio06_CA_046_06` |
| CA-07 | `Spec046Tests.Criterio07_CA_046_07` |
| CA-08 | `Spec046Tests.Criterio08_CA_046_08` |
| CA-09 | `Spec046Tests.Criterio09_CA_046_09` |
| CA-10 | `Spec046Tests.Criterio10_CA_046_10` |
| CA-11 | `Spec046Tests.Criterio11_CA_046_11` |
| CA-12 | `Spec046Tests.Criterio12_CA_046_12` |
| CA-13 | `Spec046Tests.Criterio13_CA_046_13` |
| CA-14 | `Spec046Tests.Criterio14_CA_046_14` |
| CA-15 | `Spec046Tests.Criterio15_CA_046_15` |
| CA-16 | `Spec046Tests.Criterio16_CA_046_16` |
| CA-17 | `Spec046Tests.Criterio17_CA_046_17` |
| CA-18 | `Spec046Tests.Criterio18_CA_046_18` |
| CA-19 | `Spec046Tests.Criterio19_CA_046_19` |
| CA-20 | `Spec046Tests.Criterio20_CA_046_20` |
| CA-21 | `Spec046Tests.Criterio21_CA_046_21` |
| CA-22 | `Spec046Tests.Criterio22_CA_046_22` |
| CA-23 | `Spec046Tests.Criterio23_CA_046_23` |
| CA-24 | `Spec046Tests.Criterio24_CA_046_24` |
| CA-25 | `Spec046Tests.Criterio25_CA_046_25` |
| CA-26 | `Spec046Tests.Criterio26_CA_046_26` |
| CA-27 | `Spec046Tests.Criterio27_CA_046_27` |
| CA-28 | `Spec046Tests.Criterio28_CA_046_28` |
| CA-29 | `Spec046Tests.Criterio29_CA_046_29` |
| CA-30 | `Spec046Tests.Criterio30_CA_046_30` |
| CA-31 | `Spec046Tests.Criterio31_CA_046_31` |
| CA-32 | `Spec046Tests.Criterio32_CA_046_32` |
| CA-33 | `Spec046Tests.Criterio33_CA_046_33` |
| CA-34 | `Spec046Tests.Criterio34_CA_046_34` |
| CA-35 | `Spec046Tests.Criterio35_CA_046_35` |
| CA-36 | `Spec046Tests.Criterio36_CA_046_36` |
| CA-37 | `Spec046Tests.Criterio37_CA_046_37` |
| CA-38 | `Spec046Tests.Criterio38_CA_046_38` |
| CA-39 | `Spec046Tests.Criterio39_CA_046_39` |
| CA-40 | `Spec046Tests.Criterio40_CA_046_40` |
| CA-41 | `Spec046Tests.Criterio41_CA_046_41` |
| CA-42 | `Spec046Tests.Criterio42_CA_046_42` |
| CA-43 | `Spec046Tests.Criterio43_CA_046_43` |
| CA-44 | `Spec046Tests.Criterio44_CA_046_44` |
| CA-45 | `Spec046Tests.Criterio45_CA_046_45` |
| CA-46 | `Spec046Tests.Criterio46_CA_046_46` |
| CA-47 | `Spec046Tests.Criterio47_CA_046_47` |
| CA-48 | `Spec046Tests.Criterio48_CA_046_48` |
| CA-49 | `Spec046Tests.Criterio49_CA_046_49` |
| CA-50 | `Spec046Tests.Criterio50_CA_046_50` |
| CA-51 | `Spec046Tests.Criterio51_CA_046_51` |
| CA-52 | `Spec046Tests.Criterio52_CA_046_52` |
| CA-53 | `Spec046Tests.Criterio53_CA_046_53` |
| CA-54 | `Spec046Tests.Criterio54_CA_046_54` |
| CA-55 | `Spec046Tests.Criterio55_CA_046_55` |
| CA-56 | `Spec046Tests.Criterio56_CA_046_56` |
| CA-57 | `Spec046Tests.Criterio57_CA_046_57` |
| CA-58 | `Spec046Tests.Criterio58_CA_046_58` |

## 5. Contrato
| ID | Método y ruta | Quién | Entrada | Respuestas |
|---|---|---|---|---|
| TEC-01 | `POST /api/v1/tecnicos` | persona con sesión (cliente o técnico) | Cuerpo: oficios, atencion, direccionTaller o radioKm, perfil opcional, aceptaTerminos | 201; 400; 401; 409 (ya es técnico); 422 |
| TEC-02 | `GET /api/v1/tecnicos` | cualquier persona para el resumen y una persona con sesión para el detalle | Query: oficio, colonia, lat, lng, radioKm, disponibleHoy, soloVerificados, q, orden, cursor, limite | 200; 400 |
| TEC-03 | `GET /api/v1/tecnicos/autocompletar` | cualquier persona, sin sesión | Query: q (mínimo 2 caracteres) | 200; 400 |
| TEC-04 | `GET /api/v1/mapa/tecnicos` | cualquier persona, sin sesión | Query: norte, sur, este, oeste, oficio, zoom | 200; 400 |
| TEC-05 | `GET /api/v1/tecnicos/{id}` | cualquier persona para el resumen y una persona con sesión para el detalle | Ruta: id | 200; 404 |
| TEC-06 | `GET /api/v1/tecnicos/{id}/llega-a-mi-colonia` | persona con sesión (cliente o técnico) | Ruta: id. Query: colonia | 200; 401; 404 |
| TEC-07 | `GET /api/v1/tecnicos/{id}/fotos-trabajos` | persona con sesión (cliente o técnico) | Ruta: id | 200; 401; 404 |
| TEC-08 | `PUT /api/v1/tecnicos/me` | cuenta con perfil de técnico | Cuerpo: campos a cambiar | 200; 400; 401; 422 |
| TEC-09 | `PUT /api/v1/tecnicos/me/disponibilidad` | cuenta con perfil de técnico | Cuerpo: disponibleHoy | 200; 401; 403 |
| TEC-10 | `GET /api/v1/tecnicos/me/horarios` | cuenta con perfil de técnico | Ninguno | 200; 401 |
| TEC-11 | `PUT /api/v1/tecnicos/me/horarios` | cuenta con perfil de técnico | Cuerpo: horarios | 200; 400; 401 |
| TEC-13 | `GET /api/v1/tecnicos/me/reputacion` | cuenta con perfil de técnico | Ninguno | 200; 401 |
| TEC-14 | `GET /api/v1/explorar` | cualquier persona para el resumen y una persona con sesión para el detalle | Query: filtro (todo/auto/agua/luz), colonia | 200 |

**Filtros, orden, paginación y caché**

| ID | Filtros y orden | Paginación | Caché |
|---|---|---|---|
| TEC-01 | No aplica | No | No |
| TEC-02 | Orden: distancia, calificacion o recomendadas | Por cursor, 20 (máx. 50) | Sí, unos segundos |
| TEC-03 | Máximo 8 sugerencias | No | Sí, unos segundos |
| TEC-04 | Filtro por oficio; agrupados por zona | Hasta 200 por solicitud | Sí, unos segundos |
| TEC-05 | No aplica | No | Sí, 1 minuto |
| TEC-06 | No aplica | No | Sí, 1 minuto |
| TEC-07 | Orden por fecha descendente | Por cursor, 20 | Sí, 1 minuto |
| TEC-08 | No aplica | No | No (invalida caché del perfil) |
| TEC-09 | No aplica | No | No |
| TEC-10 | No aplica | No | No |
| TEC-11 | No aplica | No | No |
| TEC-13 | No aplica | No | Sí, 1 minuto |
| TEC-14 | Filtro por categoría | No (cada bloque limitado) | Sí, 1 minuto |

**Códigos de error:** los de forma salen como 422 con `errors` por campo y los de sesión y límite como 401 y 429; los conflictos de negocio llevan un código en inglés y snake_case que se fija al implementar y se agrega al catálogo.

## 6. Modelo de datos
**Colección `tecnicos`.** Guarda el perfil de cada técnico: lo que ven los clientes (presentación, oficios, horarios, nivel, verificación) y lo que usa el sistema para repartir solicitudes (disponibilidad, radio y ubicación aproximada). Es la colección más consultada por la búsqueda y el mapa. Un técnico es la misma cuenta que el cliente: este documento se enlaza con usuarios. El perfil devuelve solo un resumen a quien no…

| Campo | Tipo | Obligatorio | Reglas y validaciones | Índice |
|---|---|---|---|---|
| _id | ObjectId | Sí | Lo genera la base | Llave primaria |
| usuarioId | ObjectId | Sí | Única; debe existir en usuarios | Único |
| nombreNegocio | Texto | No | Máximo 60 caracteres | Texto |
| oficios | Lista de textos | Sí | Al menos uno | Compuesto |
| otroOficio | Texto | No | Si es el único oficio, no recibe solicitudes | No |
| atencion | Texto | Sí | Solo esos valores | Compuesto |
| direccionTaller | Texto cifrado | No | Obligatoria si atiende en taller (mínimo 8 caracteres). No se muestra si atiende a domicilio | No |
| radioKm | Entero | No | 3, 5, 8 o 12 | No |
| ubicacion | Punto (GeoJSON) | Sí | Aproximada, no exacta | 2dsphere |
| descripcion | Texto | No | Máximo 300 caracteres | Texto |
| anios | Entero | No | De 0 a 60 | No |
| horarios | Documento | No |  | No |
| disponibleHoy | Booleano | Sí | Necesario para recibir solicitudes | Compuesto |
| verificacion | Texto | Sí | Copia del resultado de Identidad | Compuesto |
| reputacion | Documento | Sí | Calculado por el servicio | Sí (promedio) |
| fotoUrl | Texto | No | Moderada; reducida | No |
| ocultoHasta | Fecha y hora UTC | No | Lo asigna la moderación | No |
| creadoEn | Fecha y hora UTC | Sí | Lo asigna el servidor | No |

| Tipo | Campos | Para qué sirve |
|---|---|---|
| Llave principal | _id | Identifica de forma única cada documento. La genera la base de datos |
| 2dsphere | ubicacion | Buscar técnicos cerca de un punto (mapa y lista) y por radio |
| Compuesto | oficios + disponibleHoy + verificacion | Filtros de la búsqueda y reparto de solicitudes por oleadas |
| Único | usuarioId | Un perfil de técnico por cuenta |
| Texto (español) | nombreNegocio + descripcion + servicios | Autocompletado y búsqueda por texto |
| Normal | reputacion.promedio | Orden por calificación |

## 7. Arquitectura y componentes
- Proyecto `Knock.Market.Api`, módulo `Technicians` con las capas del estilo de la spec 002: `Controllers`, `Dtos`, `Validators`, `Services`, `Data` y `Hosting`.
- Cada ruta recibe sus parámetros en objetos tipados con validador registrado, nunca como `string` suelto, y consulta con filtros por el `sub` del token.
- Las rutas bajo `/admin` usan la política de rol de la spec 033 y no están disponibles sin la sesión del panel.
- La caché, la paginación por cursor y los límites usan las piezas de las specs 009 y 010.
- El contrato se publica en OpenAPI y en `docs/endpoints.md`; los límites, en `docs/rate-limit-matrix.md`.

## 8. Dependencias
- Spec 023 de este repositorio.
- Spec 025 de este repositorio.
- Spec 027 de este repositorio.
- Spec 036 de este repositorio.
- Specs 002, 003, 009, 010 y 011 (estilo, bases de datos, seguridad, caché y registros).
- Frontend: spec 036 consume este contrato.

## 9. Amenazas y controles
| Amenaza | Control | Casos que lo prueban |
|---|---|---|
| Acceso a datos de otra persona (IDOR) | Toda consulta y escritura filtra por el `sub` del token; ajeno responde 403 o 404 | CA-33, CA-38, CA-42, CA-45, CA-50, CA-55 |
| Asignación masiva o identificador ajeno en el cuerpo | El DTO no acepta identificadores de persona; se ignoran | CA-33, CA-38, CA-42, CA-45, CA-50, CA-55 |
| Inyección en filtros y parámetros | Parámetros tipados con validador; nada se concatena en consultas | CA-03 a CA-04, CA-07, CA-11, CA-14, CA-17, CA-22, CA-27, CA-34 a CA-35, CA-39, CA-46 a CA-47, CA-53 |
| Ruta sin sesión | JWT obligatorio salvo las públicas | CA-02, CA-21, CA-26, CA-32, CA-37, CA-41, CA-44, CA-49 |
| Abuso por repetición o cuerpos grandes | Límite de peticiones, `Retry-After` y tope de cuerpo | CA-57 |
| Datos personales en registros y errores | Registros sin datos personales; ProblemDetails sin valores | CA-56 |
| Duplicados por reintento | Regla de estado o índice único, sin `Idempotency-Key` | CA-58 |

## 10. Plan de pruebas
Las pruebas citan el caso como `CA_046_NN` en el nombre (para `python scripts/sdd_lint.py --trace 046`).

| Tipo | ¿Aplica? | Qué se prueba / herramienta |
|---|---|---|
| Unitarias | Sí | Validadores, reglas de estado y cálculos del dominio |
| Integración | Sí | Cada ruta con la base real en contenedor (Testcontainers) y `MarketApiFactory`/fábrica del servicio |
| Seguridad negativa | Sí | 401, 403/404, 422, 413 y 429 (CA-02 a CA-04, CA-07, CA-11, CA-14, CA-17, CA-21 a CA-22, CA-26 a CA-27, CA-32 a CA-35, CA-37 a CA-39, CA-41 a CA-42, CA-44 a CA-47, CA-49 a CA-50, CA-53, CA-55, CA-57) |
| Base de datos | Sí | Índices y reglas de cada colección de §6 |
| Contrato | Sí | OpenAPI regenerado y comparado con `contracts/` |
| Mutación | Sí | Meta del módulo según la spec 008 |
| Rendimiento | No aplica | Sin ruta crítica; se mide solo si la spec 034 lo pide |

## 11. Escenarios
- **TEC-01** — pantallas: Registrarse como técnico.
- **TEC-02** — pantallas: Inicio (lista); Buscar; Explorar.
- **TEC-03** — pantallas: Buscar; Inicio (barra de búsqueda).
- **TEC-04** — pantallas: Inicio (mapa).
- **TEC-05** — pantallas: Perfil del técnico.
- **TEC-06** — pantallas: Perfil del técnico.
- **TEC-07** — pantallas: Perfil del técnico; Explorar (Trabajos reales).
- **TEC-08** — pantallas: Mi perfil público; Mi negocio.
- **TEC-09** — pantallas: Trabajo.
- **TEC-10** — pantallas: Mi perfil público.
- **TEC-11** — pantallas: Mi perfil público.
- **TEC-13** — pantallas: Mi reputación.
- **TEC-14** — pantallas: Explorar.

## 12. Definición de terminado
- Los criterios de aceptación están cubiertos por pruebas en verde y `sdd_lint --trace 046` sin faltantes.
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
- TEC-12 (resumen del técnico) ya lo cubre la spec 036 con la ruta `/tecnicos/me/trabajo`: el Excel conserva el nombre antiguo.
- La búsqueda por texto vive en la spec 027 (`/busqueda`); aquí se confirma que `/tecnicos` y `/explorar` no la duplican.
- La verificación de identidad del técnico (spec 025) condiciona qué perfiles se muestran: confirmar el filtro.
- Esta spec se generó a partir del Excel; antes de aprobarla hay que revisar los límites, los textos de error y los plazos con el equipo.

## Aprobación
- [ ] Revisor 1 — nombre: _____________________ fecha: __________
- [ ] Sin conflicto con la constitución
- **Estados:** Borrador → Aprobada (una persona; habilita el código) → Implementada (tras el merge)
