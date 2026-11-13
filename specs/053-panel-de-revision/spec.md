# Spec 053 — Panel de revisión: verificaciones de identidad y reportes

> **Estado:** Borrador · **Repositorio:** knock-backend · **Rector:** `constitution.md`
> **Origen:** `Documentacion/00_Profesores/Desarrollo_Movil/Entregables/Knock_Endpoints_CRUD.xlsx` (revisión 2026-11-02): endpoints VER-03, VER-04, VER-05, REP-04, REP-05.
> **Claves de trabajo:** por asignar (se crea la tarea de Jira cuando se decida el alcance) · **Sprint:** por definir · **Prioridad:** por definir
> **Servicio:** Identidad (`Knock.Identity.Api`) · **Módulo:** `Moderation` · **Colecciones:** `verificaciones`, `reportes`
> **Responsable sugerido:** por asignar (Alexis o Guillermo) · Erick consume el contrato (frontend 043)

## 1. Objetivo
Definir y especificar los endpoints VER-03, VER-04, VER-05, REP-04, REP-05 del Excel de endpoints para que el equipo los implemente con el mismo estilo, seguridad y pruebas que las specs 020 a 037. Como personal de verificación o moderación, quiero revisar verificaciones y reportes y decidir, para mantener la confianza en la plataforma.

## 2. Historia y alcance
**2.1 Historia**
- Como personal de verificación o moderación, quiero revisar verificaciones y reportes y decidir, para mantener la confianza en la plataforma.

**2.2 Dentro**
- `GET /api/v1/admin/verificaciones` (VER-03): Cola de revisión del panel.
- `GET /api/v1/admin/verificaciones/{id}` (VER-04): Abre una verificación; entrega enlaces de 5 minutos a las fotos y registra quién las abrió.
- `POST /api/v1/admin/verificaciones/{id}/decision` (VER-05): Aprueba o rechaza con motivo; destruye la llave de las fotos.
- `GET /api/v1/admin/reportes` (REP-04): Cola de moderación.
- `POST /api/v1/admin/reportes/{id}/decision` (REP-05): Asigna gravedad y sanción; avisa a las dos partes sin revelar datos de la otra.
- Los índices y las reglas de cada campo de las colecciones de §6, el límite de peticiones y la documentación del contrato.

**2.3 Fuera**
- La implementación: esta spec solo se escribe; las tareas se activan cuando una persona la apruebe y lo indique.
- Pantallas y textos de la app: los define la spec de frontend 043.
- Endpoints de otras colecciones o de otras specs (020 a 038) que el Excel nombra distinto: se anotan en §15.

## 3. Requisitos
- **R-01:** `GET /api/v1/admin/verificaciones` (leer todos / filtro): Cola de revisión del panel. Lo puede llamar: personal interno (Verificador (doble verificación)) con doble verificación, spec 033. Responde 200 cuando sale bien.
- **R-02:** Entrada de `GET /api/v1/admin/verificaciones`: Query: estado, desde, hasta. Un valor inválido responde 422 con `errors.<campo>` sin repetirlo. Reglas: `estado`: Solo esos valores. A los 7 días sin revisión vuelve a sin_verificar.
- **R-03:** `GET /api/v1/admin/verificaciones` pagina por cursor (Por cursor, 20): parámetros `cursor` y `limite`, 20 por defecto y 50 como máximo; orden: Filtro por estado y antigüedad; orden por enviadaEn ascendente.
- **R-04:** `GET /api/v1/admin/verificaciones/{id}` (leer uno): Abre una verificación; entrega enlaces de 5 minutos a las fotos y registra quién las abrió. Lo puede llamar: personal interno (Verificador (doble verificación)) con doble verificación, spec 033. Responde 200 cuando sale bien.
- **R-05:** Entrada de `GET /api/v1/admin/verificaciones/{id}`: Ruta: id. Un valor inválido responde 422 con `errors.<campo>` sin repetirlo.
- **R-06:** Estados de `GET /api/v1/admin/verificaciones/{id}`: 404; 410 (fotos ya destruidas). El código de negocio de cada conflicto va en inglés y en snake_case, y se define al implementar.
- **R-07:** `POST /api/v1/admin/verificaciones/{id}/decision` (actualizar (acción)): Aprueba o rechaza con motivo; destruye la llave de las fotos. Lo puede llamar: personal interno (Verificador (doble verificación)) con doble verificación, spec 033. Responde 200 cuando sale bien.
- **R-08:** Entrada de `POST /api/v1/admin/verificaciones/{id}/decision`: Cuerpo: decision (aprobada|rechazada), motivo. Un valor inválido responde 422 con `errors.<campo>` sin repetirlo; un cuerpo ilegible, 400. Reglas: `motivo`: Obligatorio si el estado es no_verificado.
- **R-09:** Estados de `POST /api/v1/admin/verificaciones/{id}/decision`: 404; 409. El código de negocio de cada conflicto va en inglés y en snake_case, y se define al implementar.
- **R-10:** `GET /api/v1/admin/reportes` (leer todos / filtro): Cola de moderación. Lo puede llamar: personal interno (Moderación (doble verificación)) con doble verificación, spec 033. Responde 200 cuando sale bien.
- **R-11:** Entrada de `GET /api/v1/admin/reportes`: Query: gravedad, estado, desde, hasta. Un valor inválido responde 422 con `errors.<campo>` sin repetirlo. Reglas: `gravedad`: La asigna la moderación; `estado`: Solo esos valores.
- **R-12:** `GET /api/v1/admin/reportes` pagina por cursor (Por cursor, 20): parámetros `cursor` y `limite`, 20 por defecto y 50 como máximo; orden: Filtro por gravedad y estado; orden por gravedad y antigüedad.
- **R-13:** `POST /api/v1/admin/reportes/{id}/decision` (actualizar (acción)): Asigna gravedad y sanción; avisa a las dos partes sin revelar datos de la otra. Lo puede llamar: personal interno (Moderación (doble verificación)) con doble verificación, spec 033. Responde 200 cuando sale bien.
- **R-14:** Entrada de `POST /api/v1/admin/reportes/{id}/decision`: Cuerpo: gravedad, sancion, motivo. Un valor inválido responde 422 con `errors.<campo>` sin repetirlo; un cuerpo ilegible, 400. Reglas: `motivo`: Solo esos valores; `gravedad`: La asigna la moderación.
- **R-15:** Estados de `POST /api/v1/admin/reportes/{id}/decision`: 404. El código de negocio de cada conflicto va en inglés y en snake_case, y se define al implementar.
- **R-16:** La persona de cada operación es siempre la del `sub` del token; ningún cuerpo, ruta o consulta acepta el identificador de otra persona para actuar en su nombre, y un identificador ajeno enviado en el cuerpo se ignora.
- **R-17:** Los errores usan ProblemDetails con `correlationId` y `traceId`; los registros llevan método, ruta, estado y correlación, sin teléfono, dirección, coordenadas, nombres ni textos escritos por la persona (spec 011).
- **R-18:** Por encima del límite de peticiones responde 429 con `Retry-After`; cada ruta tiene su límite registrado en `docs/rate-limit-matrix.md` y los cuerpos se limitan según la spec 009 (413 si lo exceden).
- **R-19:** Las rutas que crean algo no usan `Idempotency-Key` (retirado en la spec 020): la repetición se controla con la regla de estado o el índice único de §6.

## 4. Criterios de aceptación
- **CA-01:** Dado el rol «personal interno (Verificador (doble verificación)) con doble verificación, spec 033», cuando llama `GET /api/v1/admin/verificaciones` con datos válidos, entonces responde 200 con lo descrito en §5.
- **CA-02:** Dada una petición sin token o con token vencido, cuando llama `GET /api/v1/admin/verificaciones`, entonces responde 401 y no ejecuta la operación.
- **CA-03:** Dada una persona que no cumple el rol «personal interno (Verificador (doble verificación)) con doble verificación, spec 033», cuando llama `GET /api/v1/admin/verificaciones`, entonces responde 403 y no cambia nada.
- **CA-04:** Dado un valor inválido según las reglas de §6, cuando llama `GET /api/v1/admin/verificaciones`, entonces responde 422 con `errors.<campo>`, no repite el valor y no devuelve datos.
- **CA-05:** Dado un listado con más de 20 elementos, cuando se pide sin `limite`, entonces devuelve 20 y el cursor de la página siguiente; con `limite=51` responde 422.
- **CA-06:** Dado el rol «personal interno (Verificador (doble verificación)) con doble verificación, spec 033», cuando llama `GET /api/v1/admin/verificaciones/{id}` con datos válidos, entonces responde 200 con lo descrito en §5.
- **CA-07:** Dada una petición sin token o con token vencido, cuando llama `GET /api/v1/admin/verificaciones/{id}`, entonces responde 401 y no ejecuta la operación.
- **CA-08:** Dada una persona que no cumple el rol «personal interno (Verificador (doble verificación)) con doble verificación, spec 033», cuando llama `GET /api/v1/admin/verificaciones/{id}`, entonces responde 403 y no cambia nada.
- **CA-09:** Dado un valor inválido según las reglas de §6, cuando llama `GET /api/v1/admin/verificaciones/{id}`, entonces responde 422 con `errors.<campo>`, no repite el valor y no devuelve datos.
- **CA-10:** Dado el caso «recurso no disponible», cuando llama `GET /api/v1/admin/verificaciones/{id}`, entonces responde 404 con ProblemDetails y no cambia el estado.
- **CA-11:** Dado el caso «fotos ya destruidas», cuando llama `GET /api/v1/admin/verificaciones/{id}`, entonces responde 410 con ProblemDetails y no cambia el estado.
- **CA-12:** Dado el rol «personal interno (Verificador (doble verificación)) con doble verificación, spec 033», cuando llama `POST /api/v1/admin/verificaciones/{id}/decision` con datos válidos, entonces responde 200 con lo descrito en §5.
- **CA-13:** Dada una petición sin token o con token vencido, cuando llama `POST /api/v1/admin/verificaciones/{id}/decision`, entonces responde 401 y no ejecuta la operación.
- **CA-14:** Dada una persona que no cumple el rol «personal interno (Verificador (doble verificación)) con doble verificación, spec 033», cuando llama `POST /api/v1/admin/verificaciones/{id}/decision`, entonces responde 403 y no cambia nada.
- **CA-15:** Dado un valor inválido según las reglas de §6, cuando llama `POST /api/v1/admin/verificaciones/{id}/decision`, entonces responde 422 con `errors.<campo>`, no repite el valor y no guarda nada.
- **CA-16:** Dado un cuerpo que no se puede leer, cuando llama `POST /api/v1/admin/verificaciones/{id}/decision`, entonces responde 400 y no guarda nada.
- **CA-17:** Dado el caso «recurso no disponible», cuando llama `POST /api/v1/admin/verificaciones/{id}/decision`, entonces responde 404 con ProblemDetails y no cambia el estado.
- **CA-18:** Dado el caso «recurso no disponible», cuando llama `POST /api/v1/admin/verificaciones/{id}/decision`, entonces responde 409 con ProblemDetails y no cambia el estado.
- **CA-19:** Dado el rol «personal interno (Moderación (doble verificación)) con doble verificación, spec 033», cuando llama `GET /api/v1/admin/reportes` con datos válidos, entonces responde 200 con lo descrito en §5.
- **CA-20:** Dada una petición sin token o con token vencido, cuando llama `GET /api/v1/admin/reportes`, entonces responde 401 y no ejecuta la operación.
- **CA-21:** Dada una persona que no cumple el rol «personal interno (Moderación (doble verificación)) con doble verificación, spec 033», cuando llama `GET /api/v1/admin/reportes`, entonces responde 403 y no cambia nada.
- **CA-22:** Dado un valor inválido según las reglas de §6, cuando llama `GET /api/v1/admin/reportes`, entonces responde 422 con `errors.<campo>`, no repite el valor y no devuelve datos.
- **CA-23:** Dado un listado con más de 20 elementos, cuando se pide sin `limite`, entonces devuelve 20 y el cursor de la página siguiente; con `limite=51` responde 422.
- **CA-24:** Dado el rol «personal interno (Moderación (doble verificación)) con doble verificación, spec 033», cuando llama `POST /api/v1/admin/reportes/{id}/decision` con datos válidos, entonces responde 200 con lo descrito en §5.
- **CA-25:** Dada una petición sin token o con token vencido, cuando llama `POST /api/v1/admin/reportes/{id}/decision`, entonces responde 401 y no ejecuta la operación.
- **CA-26:** Dada una persona que no cumple el rol «personal interno (Moderación (doble verificación)) con doble verificación, spec 033», cuando llama `POST /api/v1/admin/reportes/{id}/decision`, entonces responde 403 y no cambia nada.
- **CA-27:** Dado un valor inválido según las reglas de §6, cuando llama `POST /api/v1/admin/reportes/{id}/decision`, entonces responde 422 con `errors.<campo>`, no repite el valor y no guarda nada.
- **CA-28:** Dado un cuerpo que no se puede leer, cuando llama `POST /api/v1/admin/reportes/{id}/decision`, entonces responde 400 y no guarda nada.
- **CA-29:** Dado el caso «recurso no disponible», cuando llama `POST /api/v1/admin/reportes/{id}/decision`, entonces responde 404 con ProblemDetails y no cambia el estado.
- **CA-30:** Dado un cuerpo con el identificador de otra persona, cuando se llama cualquier ruta de esta spec, entonces la operación usa el `sub` del token y no el del cuerpo.
- **CA-31:** Dada una operación con datos personales, cuando se inspeccionan los registros y las respuestas de error, entonces no aparecen esos datos.
- **CA-32:** Dada una persona que supera el límite de una ruta, cuando repite la petición, entonces responde 429 con `Retry-After` y, con un cuerpo de tamaño excesivo, 413.
- **CA-33:** Dada una operación de creación repetida por mala señal, cuando llega dos veces, entonces no deja duplicados porque lo impide la regla de estado o el índice único.

**4.1 Criterio → prueba prevista**

| Criterio | Prueba prevista |
|---|---|
| CA-01 | `Spec053Tests.Criterio01_CA_053_01` |
| CA-02 | `Spec053Tests.Criterio02_CA_053_02` |
| CA-03 | `Spec053Tests.Criterio03_CA_053_03` |
| CA-04 | `Spec053Tests.Criterio04_CA_053_04` |
| CA-05 | `Spec053Tests.Criterio05_CA_053_05` |
| CA-06 | `Spec053Tests.Criterio06_CA_053_06` |
| CA-07 | `Spec053Tests.Criterio07_CA_053_07` |
| CA-08 | `Spec053Tests.Criterio08_CA_053_08` |
| CA-09 | `Spec053Tests.Criterio09_CA_053_09` |
| CA-10 | `Spec053Tests.Criterio10_CA_053_10` |
| CA-11 | `Spec053Tests.Criterio11_CA_053_11` |
| CA-12 | `Spec053Tests.Criterio12_CA_053_12` |
| CA-13 | `Spec053Tests.Criterio13_CA_053_13` |
| CA-14 | `Spec053Tests.Criterio14_CA_053_14` |
| CA-15 | `Spec053Tests.Criterio15_CA_053_15` |
| CA-16 | `Spec053Tests.Criterio16_CA_053_16` |
| CA-17 | `Spec053Tests.Criterio17_CA_053_17` |
| CA-18 | `Spec053Tests.Criterio18_CA_053_18` |
| CA-19 | `Spec053Tests.Criterio19_CA_053_19` |
| CA-20 | `Spec053Tests.Criterio20_CA_053_20` |
| CA-21 | `Spec053Tests.Criterio21_CA_053_21` |
| CA-22 | `Spec053Tests.Criterio22_CA_053_22` |
| CA-23 | `Spec053Tests.Criterio23_CA_053_23` |
| CA-24 | `Spec053Tests.Criterio24_CA_053_24` |
| CA-25 | `Spec053Tests.Criterio25_CA_053_25` |
| CA-26 | `Spec053Tests.Criterio26_CA_053_26` |
| CA-27 | `Spec053Tests.Criterio27_CA_053_27` |
| CA-28 | `Spec053Tests.Criterio28_CA_053_28` |
| CA-29 | `Spec053Tests.Criterio29_CA_053_29` |
| CA-30 | `Spec053Tests.Criterio30_CA_053_30` |
| CA-31 | `Spec053Tests.Criterio31_CA_053_31` |
| CA-32 | `Spec053Tests.Criterio32_CA_053_32` |
| CA-33 | `Spec053Tests.Criterio33_CA_053_33` |

## 5. Contrato
| ID | Método y ruta | Quién | Entrada | Respuestas |
|---|---|---|---|---|
| VER-03 | `GET /api/v1/admin/verificaciones` | personal interno (Verificador (doble verificación)) con doble verificación, spec 033 | Query: estado, desde, hasta | 200; 401; 403 |
| VER-04 | `GET /api/v1/admin/verificaciones/{id}` | personal interno (Verificador (doble verificación)) con doble verificación, spec 033 | Ruta: id | 200; 401; 403; 404; 410 (fotos ya destruidas) |
| VER-05 | `POST /api/v1/admin/verificaciones/{id}/decision` | personal interno (Verificador (doble verificación)) con doble verificación, spec 033 | Cuerpo: decision (aprobada/rechazada), motivo | 200; 400; 401; 403; 404; 409 |
| REP-04 | `GET /api/v1/admin/reportes` | personal interno (Moderación (doble verificación)) con doble verificación, spec 033 | Query: gravedad, estado, desde, hasta | 200; 401; 403 |
| REP-05 | `POST /api/v1/admin/reportes/{id}/decision` | personal interno (Moderación (doble verificación)) con doble verificación, spec 033 | Cuerpo: gravedad, sancion, motivo | 200; 400; 401; 403; 404 |

**Filtros, orden, paginación y caché**

| ID | Filtros y orden | Paginación | Caché |
|---|---|---|---|
| VER-03 | Filtro por estado y antigüedad; orden por enviadaEn ascendente | Por cursor, 20 | No |
| VER-04 | No aplica | No | No |
| VER-05 | No aplica | No | No |
| REP-04 | Filtro por gravedad y estado; orden por gravedad y antigüedad | Por cursor, 20 | No |
| REP-05 | No aplica | No | No |

**Códigos de error:** los de forma salen como 422 con `errors` por campo y los de sesión y límite como 401 y 429; los conflictos de negocio llevan un código en inglés y snake_case que se fija al implementar y se agrega al catálogo.

## 6. Modelo de datos
**Colección `verificaciones`.** Registra el proceso de verificación de identidad de un técnico. Es opcional y la revisa una persona del equipo desde el panel. Esta colección NO guarda las fotos de INE ni de selfie: esas viven cifradas con una llave propia en un almacenamiento privado, y se destruyen al decidir o a los 7 días. Aquí queda el resultado, la fecha, quién decidió y la huella de la CURP

| Campo | Tipo | Obligatorio | Reglas y validaciones | Índice |
|---|---|---|---|---|
| _id | ObjectId | Sí | Lo genera la base | Llave primaria |
| usuarioId | ObjectId | Sí | Debe existir en usuarios | Sí |
| nivel | Texto | Sí | Solo esos valores | No |
| estado | Texto | Sí | Solo esos valores. A los 7 días sin revisión vuelve a sin_verificar | Compuesto con enviadaEn |
| consentimientoId | ObjectId | Sí | Obligatorio antes de subir fotos | No |
| fotosRef | Documento | No | Se borra al destruir la llave | No |
| curpHuella | Texto | No | Única entre verificaciones aprobadas | Único parcial |
| motivo | Texto | No | Obligatorio si el estado es no_verificado | No |
| decididaPor | ObjectId | No | Debe tener el rol verificador | No |
| enviadaEn | Fecha y hora UTC | Sí | Lo asigna el servidor | Sí |
| decididaEn | Fecha y hora UTC | No |  | No |

| Tipo | Campos | Para qué sirve |
|---|---|---|
| Llave principal | _id | Identifica de forma única cada documento. La genera la base de datos |
| Normal | usuarioId | Ver el estado de verificación de un técnico |
| Compuesto | estado + enviadaEn | Cola de pendientes del panel, ordenada por antigüedad, y la alerta a las 24 horas |
| Único parcial | curpHuella (solo verificadas) | Impedir dos cuentas de la misma persona |

**Colección `reportes`.** Guarda los reportes que una persona hace contra otra: el cliente contra un técnico, o el técnico contra un cliente. La respuesta depende de la gravedad: crítica con suspensión preventiva, alta que oculta al técnico de nuevas solicitudes, media con advertencia y baja con mediación. Cada reporte se puede apelar una vez

| Campo | Tipo | Obligatorio | Reglas y validaciones | Índice |
|---|---|---|---|---|
| _id | ObjectId | Sí | Lo genera la base | Llave primaria |
| reportaId | ObjectId | Sí | Debe existir en usuarios | Sí |
| reportadoId | ObjectId | Sí | Distinto de reportaId | Sí |
| referencia | Documento | No |  | No |
| motivo | Texto | Sí | Solo esos valores | No |
| descripcion | Texto | Sí | Pasa por la moderación de contenido | No |
| evidencia | Lista de textos | No | Máximo 5 | No |
| gravedad | Texto | No | La asigna la moderación | Compuesto con estado |
| estado | Texto | Sí | Solo esos valores | Compuesto con gravedad |
| decision | Documento | No |  | No |
| apelacion | Documento | No | Solo una apelación por reporte | No |
| creadoEn | Fecha y hora UTC | Sí | Lo asigna el servidor | Sí |

| Tipo | Campos | Para qué sirve |
|---|---|---|
| Llave principal | _id | Identifica de forma única cada documento. La genera la base de datos |
| Compuesto | gravedad + estado + creadoEn | Cola de moderación ordenada por gravedad y antigüedad, y plazos de respuesta |
| Normal | reportadoId | Contar reportes y advertencias de una cuenta para la suspensión gradual |
| Normal | reportaId | Que la persona vea sus propios reportes |

## 7. Arquitectura y componentes
- Proyecto `Knock.Identity.Api`, módulo `Moderation` con las capas del estilo de la spec 002: `Controllers`, `Dtos`, `Validators`, `Services`, `Data` y `Hosting`.
- Cada ruta recibe sus parámetros en objetos tipados con validador registrado, nunca como `string` suelto, y consulta con filtros por el `sub` del token.
- Las rutas bajo `/admin` usan la política de rol de la spec 033 y no están disponibles sin la sesión del panel.
- La caché, la paginación por cursor y los límites usan las piezas de las specs 009 y 010.
- El contrato se publica en OpenAPI y en `docs/endpoints.md`; los límites, en `docs/rate-limit-matrix.md`.

## 8. Dependencias
- Spec 025 de este repositorio.
- Spec 031 de este repositorio.
- Spec 033 de este repositorio.
- Specs 002, 003, 009, 010 y 011 (estilo, bases de datos, seguridad, caché y registros).
- Frontend: spec 043 consume este contrato.

## 9. Amenazas y controles
| Amenaza | Control | Casos que lo prueban |
|---|---|---|
| Acceso a datos de otra persona (IDOR) | Toda consulta y escritura filtra por el `sub` del token; ajeno responde 403 o 404 | CA-03, CA-08, CA-14, CA-21, CA-26, CA-30 |
| Asignación masiva o identificador ajeno en el cuerpo | El DTO no acepta identificadores de persona; se ignoran | CA-03, CA-08, CA-14, CA-21, CA-26, CA-30 |
| Inyección en filtros y parámetros | Parámetros tipados con validador; nada se concatena en consultas | CA-04, CA-09, CA-15 a CA-16, CA-22, CA-27 a CA-28 |
| Ruta sin sesión | JWT obligatorio salvo las públicas | CA-02, CA-07, CA-13, CA-20, CA-25 |
| Abuso por repetición o cuerpos grandes | Límite de peticiones, `Retry-After` y tope de cuerpo | CA-32 |
| Datos personales en registros y errores | Registros sin datos personales; ProblemDetails sin valores | CA-31 |
| Duplicados por reintento | Regla de estado o índice único, sin `Idempotency-Key` | CA-33 |

## 10. Plan de pruebas
Las pruebas citan el caso como `CA_053_NN` en el nombre (para `python scripts/sdd_lint.py --trace 053`).

| Tipo | ¿Aplica? | Qué se prueba / herramienta |
|---|---|---|
| Unitarias | Sí | Validadores, reglas de estado y cálculos del dominio |
| Integración | Sí | Cada ruta con la base real en contenedor (Testcontainers) y `MarketApiFactory`/fábrica del servicio |
| Seguridad negativa | Sí | 401, 403/404, 422, 413 y 429 (CA-02 a CA-04, CA-07 a CA-09, CA-13 a CA-16, CA-20 a CA-22, CA-25 a CA-28, CA-30, CA-32) |
| Base de datos | Sí | Índices y reglas de cada colección de §6 |
| Contrato | Sí | OpenAPI regenerado y comparado con `contracts/` |
| Mutación | Sí | Meta del módulo según la spec 008 |
| Rendimiento | No aplica | Sin ruta crítica; se mide solo si la spec 034 lo pide |

## 11. Escenarios
- **VER-03** — pantallas: Panel de administración.
- **VER-04** — pantallas: Panel de administración.
- **VER-05** — pantallas: Panel de administración.
- **REP-04** — pantallas: Panel de administración.
- **REP-05** — pantallas: Panel de administración.

## 12. Definición de terminado
- Los criterios de aceptación están cubiertos por pruebas en verde y `sdd_lint --trace 053` sin faltantes.
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
- Exige el panel con doble factor (spec 033) y los roles Verificador y Moderación: no se activa sin esa entrada.
- Las fotos de verificación se ven solo con enlaces firmados de vida corta (spec 025): confirmar auditoría de cada vista.
- Esta spec se generó a partir del Excel; antes de aprobarla hay que revisar los límites, los textos de error y los plazos con el equipo.

## Aprobación
- [ ] Revisor 1 — nombre: _____________________ fecha: __________
- [ ] Sin conflicto con la constitución
- **Estados:** Borrador → Aprobada (una persona; habilita el código) → Implementada (tras el merge)
