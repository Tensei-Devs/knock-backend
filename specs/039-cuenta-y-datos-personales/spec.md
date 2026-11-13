# Spec 039 — Cuenta y datos personales: cambio de número, recuperación de acceso, exportación, consentimientos y eliminación

> **Estado:** Borrador · **Repositorio:** knock-backend · **Rector:** `constitution.md`
> **Origen:** `Documentacion/00_Profesores/Desarrollo_Movil/Entregables/Knock_Endpoints_CRUD.xlsx` (revisión 2026-11-02): endpoints USR-03, USR-04, USR-05, USR-06, USR-07, USR-08, USR-09, USR-10, USR-11.
> **Claves de trabajo:** por asignar (se crea la tarea de Jira cuando se decida el alcance) · **Sprint:** por definir · **Prioridad:** por definir
> **Servicio:** Identidad (`Knock.Identity.Api`) · **Módulo:** `Account` · **Colecciones:** `usuarios`
> **Responsable sugerido:** por asignar (Alexis o Guillermo) · Erick consume el contrato (frontend 029)

## 1. Objetivo
Definir y especificar los endpoints USR-03, USR-04, USR-05, USR-06, USR-07, USR-08, USR-09, USR-10, USR-11 del Excel de endpoints para que el equipo los implemente con el mismo estilo, seguridad y pruebas que las specs 020 a 037. Como persona con cuenta, quiero cambiar mi número, recuperar mi acceso, exportar mis datos, revisar mis consentimientos y eliminar mi cuenta, para tener control de mis datos personales.

## 2. Historia y alcance
**2.1 Historia**
- Como persona con cuenta, quiero cambiar mi número, recuperar mi acceso, exportar mis datos, revisar mis consentimientos y eliminar mi cuenta, para tener control de mis datos personales.

**2.2 Dentro**
- `POST /api/v1/me/cambio-numero` (USR-03): Pide un código SMS para el número nuevo.
- `POST /api/v1/me/cambio-numero/confirmar` (USR-04): Confirma el código, cambia el celular, cierra las sesiones anteriores y avisa al número anterior.
- `POST /api/v1/recuperacion-acceso` (USR-05): Inicia la recuperación cuando la persona ya no tiene su número: técnico verificado repite selfie; cliente pasa a soporte.
- `POST /api/v1/me/exportaciones` (USR-06): Pide la descarga de todos los datos propios; se entrega en 48 horas.
- `GET /api/v1/me/consentimientos` (USR-07): Lista los consentimientos con su versión y fecha.
- `PUT /api/v1/me/consentimientos` (USR-08): Activa o retira consentimientos opcionales (promociones, encuestas, recomendaciones).
- `GET /api/v1/me/pendientes` (USR-09): Lista lo que hay pendiente antes de eliminar la cuenta: citas y cotizaciones abiertas.
- `POST /api/v1/me/eliminacion` (USR-10): Programa la eliminación de la cuenta con 7 días de gracia, tras confirmar con código SMS.
- `POST /api/v1/me/eliminacion/cancelar` (USR-11): Cancela la eliminación durante los 7 días de gracia.
- Los índices y las reglas de cada campo de las colecciones de §6, el límite de peticiones y la documentación del contrato.

**2.3 Fuera**
- La implementación: esta spec solo se escribe; las tareas se activan cuando una persona la apruebe y lo indique.
- Pantallas y textos de la app: los define la spec de frontend 029.
- Endpoints de otras colecciones o de otras specs (020 a 038) que el Excel nombra distinto: se anotan en §15.

## 3. Requisitos
- **R-01:** `POST /api/v1/me/cambio-numero` (crear (acción)): Pide un código SMS para el número nuevo. Lo puede llamar: persona con sesión (cliente o técnico). Responde 202 cuando sale bien.
- **R-02:** Entrada de `POST /api/v1/me/cambio-numero`: Cuerpo: celularNuevo. Un valor inválido responde 422 con `errors.<campo>` sin repetirlo; un cuerpo ilegible, 400.
- **R-03:** Estados de `POST /api/v1/me/cambio-numero`: 409 (ya existe). El código de negocio de cada conflicto va en inglés y en snake_case, y se define al implementar.
- **R-04:** `POST /api/v1/me/cambio-numero/confirmar` (actualizar (acción)): Confirma el código, cambia el celular, cierra las sesiones anteriores y avisa al número anterior. Lo puede llamar: persona con sesión (cliente o técnico). Responde 200 cuando sale bien.
- **R-05:** Entrada de `POST /api/v1/me/cambio-numero/confirmar`: Cuerpo: celularNuevo, codigo. Un valor inválido responde 422 con `errors.<campo>` sin repetirlo; un cuerpo ilegible, 400.
- **R-06:** `POST /api/v1/recuperacion-acceso` (crear (acción)): Inicia la recuperación cuando la persona ya no tiene su número: técnico verificado repite selfie; cliente pasa a soporte. Lo puede llamar: cualquier persona, sin sesión. Responde 202 cuando sale bien.
- **R-07:** Entrada de `POST /api/v1/recuperacion-acceso`: Cuerpo: celularNuevo, celularAnterior, tipo. Un valor inválido responde 422 con `errors.<campo>` sin repetirlo; un cuerpo ilegible, 400.
- **R-08:** `POST /api/v1/me/exportaciones` (crear (acción)): Pide la descarga de todos los datos propios; se entrega en 48 horas. Lo puede llamar: persona con sesión (cliente o técnico). Responde 202 cuando sale bien.
- **R-09:** Estados de `POST /api/v1/me/exportaciones`: 409 (ya hay una en curso). El código de negocio de cada conflicto va en inglés y en snake_case, y se define al implementar.
- **R-10:** `GET /api/v1/me/consentimientos` (leer todos): Lista los consentimientos con su versión y fecha. Lo puede llamar: persona con sesión (cliente o técnico). Responde 200 cuando sale bien.
- **R-11:** `PUT /api/v1/me/consentimientos` (actualizar): Activa o retira consentimientos opcionales (promociones, encuestas, recomendaciones). Lo puede llamar: persona con sesión (cliente o técnico). Responde 200 cuando sale bien.
- **R-12:** Entrada de `PUT /api/v1/me/consentimientos`: Cuerpo: tipo, vigente. Un valor inválido responde 422 con `errors.<campo>` sin repetirlo; un cuerpo ilegible, 400.
- **R-13:** `GET /api/v1/me/pendientes` (leer todos): Lista lo que hay pendiente antes de eliminar la cuenta: citas y cotizaciones abiertas. Lo puede llamar: persona con sesión (cliente o técnico). Responde 200 cuando sale bien.
- **R-14:** `POST /api/v1/me/eliminacion` (crear (acción)): Programa la eliminación de la cuenta con 7 días de gracia, tras confirmar con código SMS. Lo puede llamar: persona con sesión (cliente o técnico). Responde 202 cuando sale bien.
- **R-15:** Entrada de `POST /api/v1/me/eliminacion`: Cuerpo: codigo (SMS) o texto ELIMINAR, motivo opcional. Un valor inválido responde 422 con `errors.<campo>` sin repetirlo; un cuerpo ilegible, 400.
- **R-16:** Estados de `POST /api/v1/me/eliminacion`: 409 (ya está programada). El código de negocio de cada conflicto va en inglés y en snake_case, y se define al implementar.
- **R-17:** `POST /api/v1/me/eliminacion/cancelar` (actualizar (acción)): Cancela la eliminación durante los 7 días de gracia. Lo puede llamar: persona con sesión (cliente o técnico). Responde 200 cuando sale bien.
- **R-18:** Estados de `POST /api/v1/me/eliminacion/cancelar`: 409. El código de negocio de cada conflicto va en inglés y en snake_case, y se define al implementar.
- **R-19:** La persona de cada operación es siempre la del `sub` del token; ningún cuerpo, ruta o consulta acepta el identificador de otra persona para actuar en su nombre, y un identificador ajeno enviado en el cuerpo se ignora.
- **R-20:** Los errores usan ProblemDetails con `correlationId` y `traceId`; los registros llevan método, ruta, estado y correlación, sin teléfono, dirección, coordenadas, nombres ni textos escritos por la persona (spec 011).
- **R-21:** Por encima del límite de peticiones responde 429 con `Retry-After`; cada ruta tiene su límite registrado en `docs/rate-limit-matrix.md` y los cuerpos se limitan según la spec 009 (413 si lo exceden).
- **R-22:** Las rutas que crean algo no usan `Idempotency-Key` (retirado en la spec 020): la repetición se controla con la regla de estado o el índice único de §6.

## 4. Criterios de aceptación
- **CA-01:** Dado el rol «persona con sesión (cliente o técnico)», cuando llama `POST /api/v1/me/cambio-numero` con datos válidos, entonces responde 202 con lo descrito en §5.
- **CA-02:** Dada una petición sin token o con token vencido, cuando llama `POST /api/v1/me/cambio-numero`, entonces responde 401 y no ejecuta la operación.
- **CA-03:** Dado un valor inválido según las reglas de §6, cuando llama `POST /api/v1/me/cambio-numero`, entonces responde 422 con `errors.<campo>`, no repite el valor y no guarda nada.
- **CA-04:** Dado un cuerpo que no se puede leer, cuando llama `POST /api/v1/me/cambio-numero`, entonces responde 400 y no guarda nada.
- **CA-05:** Dado el caso «ya existe», cuando llama `POST /api/v1/me/cambio-numero`, entonces responde 409 con ProblemDetails y no cambia el estado.
- **CA-06:** Dado el rol «persona con sesión (cliente o técnico)», cuando llama `POST /api/v1/me/cambio-numero/confirmar` con datos válidos, entonces responde 200 con lo descrito en §5.
- **CA-07:** Dada una petición sin token o con token vencido, cuando llama `POST /api/v1/me/cambio-numero/confirmar`, entonces responde 401 y no ejecuta la operación.
- **CA-08:** Dado un valor inválido según las reglas de §6, cuando llama `POST /api/v1/me/cambio-numero/confirmar`, entonces responde 422 con `errors.<campo>`, no repite el valor y no guarda nada.
- **CA-09:** Dado un cuerpo que no se puede leer, cuando llama `POST /api/v1/me/cambio-numero/confirmar`, entonces responde 400 y no guarda nada.
- **CA-10:** Dado el rol «cualquier persona, sin sesión», cuando llama `POST /api/v1/recuperacion-acceso` con datos válidos, entonces responde 202 con lo descrito en §5.
- **CA-11:** Dado un valor inválido según las reglas de §6, cuando llama `POST /api/v1/recuperacion-acceso`, entonces responde 422 con `errors.<campo>`, no repite el valor y no guarda nada.
- **CA-12:** Dado un cuerpo que no se puede leer, cuando llama `POST /api/v1/recuperacion-acceso`, entonces responde 400 y no guarda nada.
- **CA-13:** Dado el rol «persona con sesión (cliente o técnico)», cuando llama `POST /api/v1/me/exportaciones` con datos válidos, entonces responde 202 con lo descrito en §5.
- **CA-14:** Dada una petición sin token o con token vencido, cuando llama `POST /api/v1/me/exportaciones`, entonces responde 401 y no ejecuta la operación.
- **CA-15:** Dado el caso «ya hay una en curso», cuando llama `POST /api/v1/me/exportaciones`, entonces responde 409 con ProblemDetails y no cambia el estado.
- **CA-16:** Dado el rol «persona con sesión (cliente o técnico)», cuando llama `GET /api/v1/me/consentimientos` con datos válidos, entonces responde 200 con lo descrito en §5.
- **CA-17:** Dada una petición sin token o con token vencido, cuando llama `GET /api/v1/me/consentimientos`, entonces responde 401 y no ejecuta la operación.
- **CA-18:** Dado el rol «persona con sesión (cliente o técnico)», cuando llama `PUT /api/v1/me/consentimientos` con datos válidos, entonces responde 200 con lo descrito en §5.
- **CA-19:** Dada una petición sin token o con token vencido, cuando llama `PUT /api/v1/me/consentimientos`, entonces responde 401 y no ejecuta la operación.
- **CA-20:** Dado un valor inválido según las reglas de §6, cuando llama `PUT /api/v1/me/consentimientos`, entonces responde 422 con `errors.<campo>`, no repite el valor y no guarda nada.
- **CA-21:** Dado un cuerpo que no se puede leer, cuando llama `PUT /api/v1/me/consentimientos`, entonces responde 400 y no guarda nada.
- **CA-22:** Dado el rol «persona con sesión (cliente o técnico)», cuando llama `GET /api/v1/me/pendientes` con datos válidos, entonces responde 200 con lo descrito en §5.
- **CA-23:** Dada una petición sin token o con token vencido, cuando llama `GET /api/v1/me/pendientes`, entonces responde 401 y no ejecuta la operación.
- **CA-24:** Dado el rol «persona con sesión (cliente o técnico)», cuando llama `POST /api/v1/me/eliminacion` con datos válidos, entonces responde 202 con lo descrito en §5.
- **CA-25:** Dada una petición sin token o con token vencido, cuando llama `POST /api/v1/me/eliminacion`, entonces responde 401 y no ejecuta la operación.
- **CA-26:** Dado un valor inválido según las reglas de §6, cuando llama `POST /api/v1/me/eliminacion`, entonces responde 422 con `errors.<campo>`, no repite el valor y no guarda nada.
- **CA-27:** Dado un cuerpo que no se puede leer, cuando llama `POST /api/v1/me/eliminacion`, entonces responde 400 y no guarda nada.
- **CA-28:** Dado el caso «ya está programada», cuando llama `POST /api/v1/me/eliminacion`, entonces responde 409 con ProblemDetails y no cambia el estado.
- **CA-29:** Dado el rol «persona con sesión (cliente o técnico)», cuando llama `POST /api/v1/me/eliminacion/cancelar` con datos válidos, entonces responde 200 con lo descrito en §5.
- **CA-30:** Dada una petición sin token o con token vencido, cuando llama `POST /api/v1/me/eliminacion/cancelar`, entonces responde 401 y no ejecuta la operación.
- **CA-31:** Dado el caso «recurso no disponible», cuando llama `POST /api/v1/me/eliminacion/cancelar`, entonces responde 409 con ProblemDetails y no cambia el estado.
- **CA-32:** Dado un cuerpo con el identificador de otra persona, cuando se llama cualquier ruta de esta spec, entonces la operación usa el `sub` del token y no el del cuerpo.
- **CA-33:** Dada una operación con datos personales, cuando se inspeccionan los registros y las respuestas de error, entonces no aparecen esos datos.
- **CA-34:** Dada una persona que supera el límite de una ruta, cuando repite la petición, entonces responde 429 con `Retry-After` y, con un cuerpo de tamaño excesivo, 413.
- **CA-35:** Dada una operación de creación repetida por mala señal, cuando llega dos veces, entonces no deja duplicados porque lo impide la regla de estado o el índice único.

**4.1 Criterio → prueba prevista**

| Criterio | Prueba prevista |
|---|---|
| CA-01 | `Spec039Tests.Criterio01_CA_039_01` |
| CA-02 | `Spec039Tests.Criterio02_CA_039_02` |
| CA-03 | `Spec039Tests.Criterio03_CA_039_03` |
| CA-04 | `Spec039Tests.Criterio04_CA_039_04` |
| CA-05 | `Spec039Tests.Criterio05_CA_039_05` |
| CA-06 | `Spec039Tests.Criterio06_CA_039_06` |
| CA-07 | `Spec039Tests.Criterio07_CA_039_07` |
| CA-08 | `Spec039Tests.Criterio08_CA_039_08` |
| CA-09 | `Spec039Tests.Criterio09_CA_039_09` |
| CA-10 | `Spec039Tests.Criterio10_CA_039_10` |
| CA-11 | `Spec039Tests.Criterio11_CA_039_11` |
| CA-12 | `Spec039Tests.Criterio12_CA_039_12` |
| CA-13 | `Spec039Tests.Criterio13_CA_039_13` |
| CA-14 | `Spec039Tests.Criterio14_CA_039_14` |
| CA-15 | `Spec039Tests.Criterio15_CA_039_15` |
| CA-16 | `Spec039Tests.Criterio16_CA_039_16` |
| CA-17 | `Spec039Tests.Criterio17_CA_039_17` |
| CA-18 | `Spec039Tests.Criterio18_CA_039_18` |
| CA-19 | `Spec039Tests.Criterio19_CA_039_19` |
| CA-20 | `Spec039Tests.Criterio20_CA_039_20` |
| CA-21 | `Spec039Tests.Criterio21_CA_039_21` |
| CA-22 | `Spec039Tests.Criterio22_CA_039_22` |
| CA-23 | `Spec039Tests.Criterio23_CA_039_23` |
| CA-24 | `Spec039Tests.Criterio24_CA_039_24` |
| CA-25 | `Spec039Tests.Criterio25_CA_039_25` |
| CA-26 | `Spec039Tests.Criterio26_CA_039_26` |
| CA-27 | `Spec039Tests.Criterio27_CA_039_27` |
| CA-28 | `Spec039Tests.Criterio28_CA_039_28` |
| CA-29 | `Spec039Tests.Criterio29_CA_039_29` |
| CA-30 | `Spec039Tests.Criterio30_CA_039_30` |
| CA-31 | `Spec039Tests.Criterio31_CA_039_31` |
| CA-32 | `Spec039Tests.Criterio32_CA_039_32` |
| CA-33 | `Spec039Tests.Criterio33_CA_039_33` |
| CA-34 | `Spec039Tests.Criterio34_CA_039_34` |
| CA-35 | `Spec039Tests.Criterio35_CA_039_35` |

## 5. Contrato
| ID | Método y ruta | Quién | Entrada | Respuestas |
|---|---|---|---|---|
| USR-03 | `POST /api/v1/me/cambio-numero` | persona con sesión (cliente o técnico) | Cuerpo: celularNuevo | 202; 400; 401; 409 (ya existe); 429 |
| USR-04 | `POST /api/v1/me/cambio-numero/confirmar` | persona con sesión (cliente o técnico) | Cuerpo: celularNuevo, codigo | 200; 400; 401; 429 |
| USR-05 | `POST /api/v1/recuperacion-acceso` | cualquier persona, sin sesión | Cuerpo: celularNuevo, celularAnterior, tipo | 202; 400; 429 |
| USR-06 | `POST /api/v1/me/exportaciones` | persona con sesión (cliente o técnico) | Ninguno | 202; 401; 409 (ya hay una en curso); 429 |
| USR-07 | `GET /api/v1/me/consentimientos` | persona con sesión (cliente o técnico) | Ninguno | 200; 401 |
| USR-08 | `PUT /api/v1/me/consentimientos` | persona con sesión (cliente o técnico) | Cuerpo: tipo, vigente | 200; 400; 401 |
| USR-09 | `GET /api/v1/me/pendientes` | persona con sesión (cliente o técnico) | Ninguno | 200; 401 |
| USR-10 | `POST /api/v1/me/eliminacion` | persona con sesión (cliente o técnico) | Cuerpo: codigo (SMS) o texto ELIMINAR, motivo opcional | 202; 400; 401; 409 (ya está programada); 429 |
| USR-11 | `POST /api/v1/me/eliminacion/cancelar` | persona con sesión (cliente o técnico) | Ninguno | 200; 401; 409 |

**Filtros, orden, paginación y caché**

| ID | Filtros y orden | Paginación | Caché |
|---|---|---|---|
| USR-03 | No aplica | No | No |
| USR-04 | No aplica | No | No |
| USR-05 | No aplica | No | No |
| USR-06 | No aplica | No | No |
| USR-07 | No aplica | No | No |
| USR-08 | No aplica | No | No |
| USR-09 | No aplica | No | No |
| USR-10 | No aplica | No | No |
| USR-11 | No aplica | No | No |

**Códigos de error:** los de forma salen como 422 con `errors` por campo y los de sesión y límite como 401 y 429; los conflictos de negocio llevan un código en inglés y snake_case que se fija al implementar y se agrega al catálogo.

## 6. Modelo de datos
**Colección `usuarios`.** Guarda la cuenta de cada persona que usa Knock. Una sola cuenta sirve para ser cliente y para ser técnico: si la persona se registra como técnico, su perfil de técnico vive en la colección tecnicos del servicio de Mercado y aquí solo queda la marca de que es técnico. Es la colección más consultada, porque todas las peticiones con sesión empiezan identificando a la persona. No existe una tabla de…

| Campo | Tipo | Obligatorio | Reglas y validaciones | Índice |
|---|---|---|---|---|
| _id | ObjectId | Sí | Lo genera la base | Llave primaria |
| celularCifrado | Texto | Sí | Cifrado con AES-256-GCM y llave de Key Vault (nonce de 12 bytes). Nunca se registra ni se devuelve en una res… | No |
| celularHash | Texto | Sí | HMAC-SHA256 del celular de 10 dígitos (sin +52) con llave de Key Vault. Único: un celular corresponde a una s… | Único |
| sub | Texto | Sí | UUID v8 derivado con SHA-256 del uid de Firebase (TT-API-12). No cambia. Único. El uid no se guarda | Único |
| perfilCompleto | Booleano | Sí | Por defecto falso; pasa a verdadero cuando existe el nombre (USR-02) | No |
| nombre | Texto | No | Opcional al crear la cuenta; se pide al completar el perfil (USR-02). De 2 a 80 caracteres: letras (con acent… | No |
| fotoUrl | Texto | No | Foto reducida a 320 px. Solo se acepta una dirección de nuestro almacenamiento o CDN. Si no hay, la app muest… | No |
| colonia | Texto | No | Debe existir en la colección colonias | No |
| codigoPostal | Texto | No | 5 dígitos | No |
| esTecnico | Booleano | Sí | Por defecto falso | No |
| consentimientos | Lista de documentos | Sí | Al crear la cuenta se guardan dos: aviso_privacidad y terminos, cada uno con su versión vigente. El consentim… | No |
| estado | Texto | Sí | Solo estos valores | Compuesto con eliminarEn |
| eliminarEn | Fecha y hora UTC | No | Se borra si la persona cancela la eliminación | Sí (para el proceso de borrado) |
| creadoEn | Fecha y hora UTC | Sí | Lo asigna el servidor | No |
| actualizadoEn | Fecha y hora UTC | Sí | Lo asigna el servidor | No |

| Tipo | Campos | Para qué sirve |
|---|---|---|
| Llave principal | _id | Identifica de forma única cada documento. La genera la base de datos |
| Único | celularHash | Que un celular corresponda a una sola cuenta y buscarlo al iniciar sesión sin descifrarlo |
| Único | sub | Encontrar la cuenta a partir del token y evitar dos cuentas con el mismo sub |
| Compuesto | estado + eliminarEn | Que el proceso de fondo encuentre las cuentas cuyos 7 días de gracia ya vencieron |

## 7. Arquitectura y componentes
- Proyecto `Knock.Identity.Api`, módulo `Account` con las capas del estilo de la spec 002: `Controllers`, `Dtos`, `Validators`, `Services`, `Data` y `Hosting`.
- Cada ruta recibe sus parámetros en objetos tipados con validador registrado, nunca como `string` suelto, y consulta con filtros por el `sub` del token.
- Las rutas bajo `/admin` usan la política de rol de la spec 033 y no están disponibles sin la sesión del panel.
- La caché, la paginación por cursor y los límites usan las piezas de las specs 009 y 010.
- El contrato se publica en OpenAPI y en `docs/endpoints.md`; los límites, en `docs/rate-limit-matrix.md`.

## 8. Dependencias
- Spec 015 de este repositorio.
- Spec 016 de este repositorio.
- Spec 017 de este repositorio.
- Spec 018 de este repositorio.
- Spec 025 de este repositorio.
- Specs 002, 003, 009, 010 y 011 (estilo, bases de datos, seguridad, caché y registros).
- Frontend: spec 029 consume este contrato.

## 9. Amenazas y controles
| Amenaza | Control | Casos que lo prueban |
|---|---|---|
| Acceso a datos de otra persona (IDOR) | Toda consulta y escritura filtra por el `sub` del token; ajeno responde 403 o 404 | CA-32 |
| Asignación masiva o identificador ajeno en el cuerpo | El DTO no acepta identificadores de persona; se ignoran | CA-32 |
| Inyección en filtros y parámetros | Parámetros tipados con validador; nada se concatena en consultas | CA-03 a CA-04, CA-08 a CA-09, CA-11 a CA-12, CA-20 a CA-21, CA-26 a CA-27 |
| Ruta sin sesión | JWT obligatorio salvo las públicas | CA-02, CA-07, CA-14, CA-17, CA-19, CA-23, CA-25, CA-30 |
| Abuso por repetición o cuerpos grandes | Límite de peticiones, `Retry-After` y tope de cuerpo | CA-34 |
| Datos personales en registros y errores | Registros sin datos personales; ProblemDetails sin valores | CA-33 |
| Duplicados por reintento | Regla de estado o índice único, sin `Idempotency-Key` | CA-35 |

## 10. Plan de pruebas
Las pruebas citan el caso como `CA_039_NN` en el nombre (para `python scripts/sdd_lint.py --trace 039`).

| Tipo | ¿Aplica? | Qué se prueba / herramienta |
|---|---|---|
| Unitarias | Sí | Validadores, reglas de estado y cálculos del dominio |
| Integración | Sí | Cada ruta con la base real en contenedor (Testcontainers) y `MarketApiFactory`/fábrica del servicio |
| Seguridad negativa | Sí | 401, 403/404, 422, 413 y 429 (CA-02 a CA-04, CA-07 a CA-09, CA-11 a CA-12, CA-14, CA-17, CA-19 a CA-21, CA-23, CA-25 a CA-27, CA-30, CA-32, CA-34) |
| Base de datos | Sí | Índices y reglas de cada colección de §6 |
| Contrato | Sí | OpenAPI regenerado y comparado con `contracts/` |
| Mutación | Sí | Meta del módulo según la spec 008 |
| Rendimiento | No aplica | Sin ruta crítica; se mide solo si la spec 034 lo pide |

## 11. Escenarios
- **USR-03** — pantallas: Mis datos.
- **USR-04** — pantallas: Mis datos.
- **USR-05** — pantallas: Inicio de sesión (¿Ya no tienes ese número?).
- **USR-06** — pantallas: Privacidad y datos.
- **USR-07** — pantallas: Privacidad y datos.
- **USR-08** — pantallas: Privacidad y datos.
- **USR-09** — pantallas: Eliminar cuenta (paso 1).
- **USR-10** — pantallas: Eliminar cuenta (paso 2).
- **USR-11** — pantallas: Eliminada; Mi cuenta.

## 12. Definición de terminado
- Los criterios de aceptación están cubiertos por pruebas en verde y `sdd_lint --trace 039` sin faltantes.
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
- La recuperación de acceso del técnico verificado repite la selfie y la del cliente pasa a soporte: confirmar con la spec 025 qué parte vive aquí y cuál allí.
- La exportación y la eliminación de cuenta tocan colecciones de otros servicios: definir si se coordinan con eventos de la bandeja de salida (spec 003) o con llamadas internas.
- Los plazos de gracia de la eliminación (cancelable) deben confirmarse con el texto legal vigente (landing, KNOCK-218).
- Esta spec se generó a partir del Excel; antes de aprobarla hay que revisar los límites, los textos de error y los plazos con el equipo.

## Aprobación
- [ ] Revisor 1 — nombre: _____________________ fecha: __________
- [ ] Sin conflicto con la constitución
- **Estados:** Borrador → Aprobada (una persona; habilita el código) → Implementada (tras el merge)
