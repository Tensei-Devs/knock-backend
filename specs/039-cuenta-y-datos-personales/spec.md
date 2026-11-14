# Spec 039 — Cuenta y datos personales: cambio de número, recuperación de acceso, exportación, consentimientos y eliminación

> **Estado:** Borrador · **Repositorio:** knock-backend · **Rector:** `constitution.md`
> **Origen:** `Documentacion/00_Profesores/Desarrollo_Movil/Entregables/Knock_Endpoints_CRUD.xlsx` (revisión 2026-11-02): endpoints USR-03, USR-04, USR-05, USR-06, USR-07, USR-08, USR-09, USR-10, USR-11.
> **Claves de trabajo:** KNOCK-266 (tarea paraguas de las specs de los endpoints del Excel) · **Sprint:** por definir · **Prioridad:** por definir
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
- **R-03:** Estados de `POST /api/v1/me/cambio-numero`: 409 `phone_already_registered` (ya existe). Los códigos de negocio van en inglés y en snake_case.
- **R-04:** `POST /api/v1/me/cambio-numero/confirmar` (actualizar (acción)): Confirma el código, cambia el celular, cierra las sesiones anteriores y avisa al número anterior. Lo puede llamar: persona con sesión (cliente o técnico). Responde 200 cuando sale bien.
- **R-05:** Entrada de `POST /api/v1/me/cambio-numero/confirmar`: Cuerpo: celularNuevo, codigo. Un valor inválido responde 422 con `errors.<campo>` sin repetirlo; un cuerpo ilegible, 400.
- **R-06:** `POST /api/v1/recuperacion-acceso` (crear (acción)): Inicia la recuperación cuando la persona ya no tiene su número: técnico verificado repite selfie; cliente pasa a soporte. Lo puede llamar: cualquier persona, sin sesión. Responde 202 cuando sale bien.
- **R-07:** Entrada de `POST /api/v1/recuperacion-acceso`: Cuerpo: celularNuevo, celularAnterior, tipo. Un valor inválido responde 422 con `errors.<campo>` sin repetirlo; un cuerpo ilegible, 400.
- **R-08:** `POST /api/v1/me/exportaciones` (crear (acción)): Pide la descarga de todos los datos propios; se entrega en 48 horas. Lo puede llamar: persona con sesión (cliente o técnico). Responde 202 cuando sale bien.
- **R-09:** Estados de `POST /api/v1/me/exportaciones`: 409 `export_in_progress` (ya hay una en curso). Los códigos de negocio van en inglés y en snake_case.
- **R-10:** `GET /api/v1/me/consentimientos` (leer todos): Lista los consentimientos con su versión y fecha. Lo puede llamar: persona con sesión (cliente o técnico). Responde 200 cuando sale bien.
- **R-11:** `PUT /api/v1/me/consentimientos` (actualizar): Activa o retira consentimientos opcionales (promociones, encuestas, recomendaciones). Lo puede llamar: persona con sesión (cliente o técnico). Responde 200 cuando sale bien.
- **R-12:** Entrada de `PUT /api/v1/me/consentimientos`: Cuerpo: tipo, vigente. Un valor inválido responde 422 con `errors.<campo>` sin repetirlo; un cuerpo ilegible, 400.
- **R-13:** `GET /api/v1/me/pendientes` (leer todos): Lista lo que hay pendiente antes de eliminar la cuenta: citas y cotizaciones abiertas. Lo puede llamar: persona con sesión (cliente o técnico). Responde 200 cuando sale bien.
- **R-14:** `POST /api/v1/me/eliminacion` (crear (acción)): Programa la eliminación de la cuenta con 7 días de gracia, tras confirmar con código SMS. Lo puede llamar: persona con sesión (cliente o técnico). Responde 202 cuando sale bien.
- **R-15:** Entrada de `POST /api/v1/me/eliminacion`: Cuerpo: codigo (SMS) o texto ELIMINAR, motivo opcional. Un valor inválido responde 422 con `errors.<campo>` sin repetirlo; un cuerpo ilegible, 400.
- **R-16:** Estados de `POST /api/v1/me/eliminacion`: 409 `deletion_already_scheduled` (ya está programada). Los códigos de negocio van en inglés y en snake_case.
- **R-17:** `POST /api/v1/me/eliminacion/cancelar` (actualizar (acción)): Cancela la eliminación durante los 7 días de gracia. Lo puede llamar: persona con sesión (cliente o técnico). Responde 200 cuando sale bien.
- **R-18:** Estados de `POST /api/v1/me/eliminacion/cancelar`: 409 `no_deletion_scheduled`. Los códigos de negocio van en inglés y en snake_case.
- **R-19:** **Cambio de número (USR-03 y USR-04).** `celularNuevo` en formato E.164 de México (+52 y 10 dígitos). El código SMS vence a los 10 minutos, admite 5 intentos y se pueden pedir 3 códigos por hora y por persona. Al confirmar se cambia el celular (protegido igual que en el registro, spec 015), se cierran todas las demás sesiones (specs 016 y 018) y se avisa por SMS al número anterior.
- **R-20:** **Recuperación de acceso (USR-05).** Es pública y muy limitada (3 solicitudes por hora por IP y por número). Siempre responde 202 sin revelar si la cuenta existe. `tipo` es `tecnico` o `cliente`: el técnico verificado repite la selfie (spec 025) y el cliente pasa a soporte. No cambia ningún dato hasta que la verificación o soporte lo resuelvan.
- **R-21:** **Exportación (USR-06).** Una sola exportación en curso por persona. Se prepara en segundo plano con la tarea `PrepararExportacion`, que se agrega al catálogo de la spec 054, y se entrega en un máximo de 48 horas con un enlace firmado de 24 horas; contiene solo datos de la propia persona y se avisa al terminar.
- **R-22:** **Consentimientos (USR-07 y USR-08).** Los opcionales son `promociones`, `encuestas` y `recomendaciones`; los obligatorios (aviso de privacidad y términos) no se retiran por esta ruta. Cada cambio guarda tipo, versión y fecha, y retirar un consentimiento surte efecto de inmediato. Un `tipo` fuera de la lista responde 422.
- **R-23:** **Pendientes antes de eliminar (USR-09).** Devuelve solo conteos e identificadores de sus citas y cotizaciones abiertas (consulta interna a Mercado), sin datos de otras personas; sin paginación, con un máximo de 50.
- **R-24:** **Eliminación (USR-10 y USR-11).** Se confirma con el código SMS o con el texto exacto `ELIMINAR`; `motivo` opcional de hasta 200 caracteres. Programa `EliminarCuenta` a 7 días (spec 054) y, durante la gracia, la persona puede cancelar. Al ejecutarse borra sus datos personales, direcciones y fotos de verificación, anonimiza sus opiniones (spec 051), cierra sus sesiones y cancela sus solicitudes abiertas. La cancelación deja la tarea como `hecha` sin efecto.
- **R-25:** **Sin datos personales en registros.** Ninguna de estas rutas escribe celulares, códigos ni textos de motivo en los registros; el aviso al número anterior no repite el número nuevo completo.
- **R-26:** La persona de cada operación es siempre la del `sub` del token; ningún cuerpo, ruta o consulta acepta el identificador de otra persona para actuar en su nombre, y un identificador ajeno enviado en el cuerpo se ignora.
- **R-27:** Los errores usan ProblemDetails con `correlationId` y `traceId`; los registros llevan método, ruta, estado y correlación, sin teléfono, dirección, coordenadas, nombres ni textos escritos por la persona (spec 011).
- **R-28:** Por encima del límite de peticiones responde 429 con `Retry-After`; cada ruta tiene su límite registrado en `docs/rate-limit-matrix.md` y los cuerpos se limitan según la spec 009 (413 si lo exceden).
- **R-29:** Las rutas que crean algo no usan `Idempotency-Key` (retirado en la spec 020): la repetición se controla con la regla de estado o el índice único de §6.

## 4. Criterios de aceptación
- **CA-01:** Dado el rol «persona con sesión (cliente o técnico)», cuando llama `POST /api/v1/me/cambio-numero` con datos válidos, entonces responde 202 con lo descrito en §5.
- **CA-02:** Dada una petición sin token o con token vencido, cuando llama `POST /api/v1/me/cambio-numero`, entonces responde 401 y no ejecuta la operación.
- **CA-03:** Dado un valor inválido según las reglas de §6, cuando llama `POST /api/v1/me/cambio-numero`, entonces responde 422 con `errors.<campo>`, no repite el valor y no guarda nada.
- **CA-04:** Dado un cuerpo que no se puede leer, cuando llama `POST /api/v1/me/cambio-numero`, entonces responde 400 y no guarda nada.
- **CA-05:** Dado el caso «el número nuevo ya pertenece a otra cuenta», cuando llama `POST /api/v1/me/cambio-numero`, entonces responde 409 `phone_already_registered` con ProblemDetails y no cambia el estado.
- **CA-06:** Dado el rol «persona con sesión (cliente o técnico)», cuando llama `POST /api/v1/me/cambio-numero/confirmar` con datos válidos, entonces responde 200 con lo descrito en §5.
- **CA-07:** Dada una petición sin token o con token vencido, cuando llama `POST /api/v1/me/cambio-numero/confirmar`, entonces responde 401 y no ejecuta la operación.
- **CA-08:** Dado un valor inválido según las reglas de §6, cuando llama `POST /api/v1/me/cambio-numero/confirmar`, entonces responde 422 con `errors.<campo>`, no repite el valor y no guarda nada.
- **CA-09:** Dado un cuerpo que no se puede leer, cuando llama `POST /api/v1/me/cambio-numero/confirmar`, entonces responde 400 y no guarda nada.
- **CA-10:** Dado el rol «cualquier persona, sin sesión», cuando llama `POST /api/v1/recuperacion-acceso` con datos válidos, entonces responde 202 con lo descrito en §5.
- **CA-11:** Dado un valor inválido según las reglas de §6, cuando llama `POST /api/v1/recuperacion-acceso`, entonces responde 422 con `errors.<campo>`, no repite el valor y no guarda nada.
- **CA-12:** Dado un cuerpo que no se puede leer, cuando llama `POST /api/v1/recuperacion-acceso`, entonces responde 400 y no guarda nada.
- **CA-13:** Dado el rol «persona con sesión (cliente o técnico)», cuando llama `POST /api/v1/me/exportaciones` con datos válidos, entonces responde 202 con lo descrito en §5.
- **CA-14:** Dada una petición sin token o con token vencido, cuando llama `POST /api/v1/me/exportaciones`, entonces responde 401 y no ejecuta la operación.
- **CA-15:** Dado el caso «ya hay una exportación en curso», cuando llama `POST /api/v1/me/exportaciones`, entonces responde 409 `export_in_progress` con ProblemDetails y no cambia el estado.
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
- **CA-28:** Dado el caso «la eliminación ya está programada», cuando llama `POST /api/v1/me/eliminacion`, entonces responde 409 `deletion_already_scheduled` con ProblemDetails y no cambia el estado.
- **CA-29:** Dado el rol «persona con sesión (cliente o técnico)», cuando llama `POST /api/v1/me/eliminacion/cancelar` con datos válidos, entonces responde 200 con lo descrito en §5.
- **CA-30:** Dada una petición sin token o con token vencido, cuando llama `POST /api/v1/me/eliminacion/cancelar`, entonces responde 401 y no ejecuta la operación.
- **CA-31:** Dado el caso «no hay una eliminación programada que cancelar», cuando llama `POST /api/v1/me/eliminacion/cancelar`, entonces responde 409 `no_deletion_scheduled` con ProblemDetails y no cambia el estado.
- **CA-32:** Dada una persona con sesión, cuando confirma el código del número nuevo a tiempo, entonces cambia su celular, se cierran sus otras sesiones y el número anterior recibe el aviso; con un código vencido o tras 5 intentos responde 400, y a la cuarta solicitud en una hora, 429.
- **CA-33:** Dado un número que no existe y otro que sí, cuando se pide la recuperación, entonces ambos responden 202 igual y no se cambia ningún dato; a la cuarta solicitud en una hora responde 429.
- **CA-34:** Dada una persona sin exportación en curso, cuando la pide, entonces responde 202 y se programa; una segunda mientras la primera sigue responde 409 `export_in_progress`; el archivo solo trae sus datos.
- **CA-35:** Dada una persona, cuando retira `promociones`, entonces queda `vigente: false` con fecha y versión y deja de recibir esos avisos; un tipo fuera de la lista o intentar retirar uno obligatorio responde 422.
- **CA-36:** Dada una persona con citas y cotizaciones abiertas, cuando consulta sus pendientes, entonces recibe sus conteos e identificadores y ningún dato de otra persona.
- **CA-37:** Dada una persona que confirma la eliminación, cuando pasan 7 días sin cancelar, entonces se borran sus datos y se anonimizan sus opiniones; si cancela antes, no se borra nada; pedirla dos veces responde 409 `deletion_already_scheduled`.
- **CA-38:** Dadas estas operaciones, cuando se inspeccionan los registros, entonces no aparecen celulares completos, códigos ni motivos.
- **CA-39:** Dado un cuerpo con el identificador de otra persona, cuando se llama cualquier ruta de esta spec, entonces la operación usa el `sub` del token y no el del cuerpo.
- **CA-40:** Dada una operación con datos personales, cuando se inspeccionan los registros y las respuestas de error, entonces no aparecen esos datos.
- **CA-41:** Dada una persona que supera el límite de una ruta, cuando repite la petición, entonces responde 429 con `Retry-After` y, con un cuerpo de tamaño excesivo, 413.
- **CA-42:** Dada una operación de creación repetida por mala señal, cuando llega dos veces, entonces no deja duplicados porque lo impide la regla de estado o el índice único.

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
| CA-36 | `Spec039Tests.Criterio36_CA_039_36` |
| CA-37 | `Spec039Tests.Criterio37_CA_039_37` |
| CA-38 | `Spec039Tests.Criterio38_CA_039_38` |
| CA-39 | `Spec039Tests.Criterio39_CA_039_39` |
| CA-40 | `Spec039Tests.Criterio40_CA_039_40` |
| CA-41 | `Spec039Tests.Criterio41_CA_039_41` |
| CA-42 | `Spec039Tests.Criterio42_CA_039_42` |

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

**Códigos de error de negocio**

| ID | Estado | Código | Cuándo |
|---|---|---|---|
| USR-03 | 409 | `phone_already_registered` | el número nuevo ya pertenece a otra cuenta |
| USR-06 | 409 | `export_in_progress` | ya hay una exportación en curso |
| USR-10 | 409 | `deletion_already_scheduled` | la eliminación ya está programada |
| USR-11 | 409 | `no_deletion_scheduled` | no hay una eliminación programada que cancelar |


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
| Acceso a datos de otra persona (IDOR) | Toda consulta y escritura filtra por el `sub` del token; ajeno responde 403 o 404 | CA-39 |
| Asignación masiva o identificador ajeno en el cuerpo | El DTO no acepta identificadores de persona; se ignoran | CA-39 |
| Inyección en filtros y parámetros | Parámetros tipados con validador; nada se concatena en consultas | CA-03 a CA-04, CA-08 a CA-09, CA-11 a CA-12, CA-20 a CA-21, CA-26 a CA-27 |
| Ruta sin sesión | JWT obligatorio salvo las públicas | CA-02, CA-07, CA-14, CA-17, CA-19, CA-23, CA-25, CA-30 |
| Abuso por repetición o cuerpos grandes | Límite de peticiones, `Retry-After` y tope de cuerpo | CA-41 |
| Datos personales en registros y errores | Registros sin datos personales; ProblemDetails sin valores | CA-40 |
| Duplicados por reintento | Regla de estado o índice único, sin `Idempotency-Key` | CA-42 |

## 10. Plan de pruebas
Las pruebas citan el caso como `CA_039_NN` en el nombre (para `python scripts/sdd_lint.py --trace 039`).

| Tipo | ¿Aplica? | Qué se prueba / herramienta |
|---|---|---|
| Unitarias | Sí | Validadores, reglas de estado y cálculos del dominio |
| Integración | Sí | Cada ruta con la base real en contenedor (Testcontainers) y `MarketApiFactory`/fábrica del servicio |
| Seguridad negativa | Sí | 401, 403/404, 422, 413 y 429 (CA-02 a CA-04, CA-07 a CA-09, CA-11 a CA-12, CA-14, CA-17, CA-19 a CA-21, CA-23, CA-25 a CA-27, CA-30, CA-39, CA-41) |
| Base de datos | Sí | Índices y reglas de cada colección de §6 |
| Contrato | Sí | OpenAPI regenerado y comparado con `contracts/` |
| Mutación | Sí | Meta del módulo según la spec 008 |
| Rendimiento | No aplica | Sin ruta crítica; se mide solo si la spec 034 lo pide |

## 11. Escenarios
- **Cambio de número:** la persona pide el cambio → recibe el código en el número nuevo → lo confirma → se cierran sus otras sesiones y el número anterior recibe el aviso.
- **Eliminar cuenta:** la persona ve sus pendientes → confirma con el código → la cuenta queda programada para 7 días → si se arrepiente, la cancela desde Perfil.
- **Exportar datos:** la persona pide su exportación → recibe un aviso cuando está lista → descarga el archivo con un enlace de 24 horas.

## 12. Definición de terminado
- Los criterios de aceptación están cubiertos por pruebas en verde y `sdd_lint --trace 039` sin faltantes.
- El CI en verde, un par revisó el cambio y `Memoria.md` al día.
- OpenAPI, `docs/endpoints.md` y `docs/rate-limit-matrix.md` actualizados.

## 13. Decisiones
- La paginación, la caché y los roles de cada ruta salen de la hoja de cada colección del Excel de endpoints (revisión 2026-11-02).
- Las rutas de la persona viven bajo `/me` o por identificador con dueño verificado; las del personal interno, bajo `/admin`.

## 14. Decisiones tomadas sin consulta
- **D-01:** Se toma el Excel de endpoints como fuente del contrato porque es el insumo acordado para las specs; cualquier diferencia con las specs 020 a 038 se resuelve a favor de la spec numerada.
- **D-02:** Los conflictos de negocio llevan códigos en inglés y snake_case (los de §5 y, si falta alguno, los que se fijen al implementar), siguiendo la convención de errores de la spec 002.
- **D-03:** No se usa `Idempotency-Key`, aunque el Excel lo menciona en creaciones.
- **D-04:** El código de cambio de número vence a los 10 minutos y admite 5 intentos, igual que el registro (spec 015).
- **D-05:** La recuperación siempre responde 202 para no revelar si el número existe.
- **D-06:** La eliminación no se bloquea por citas abiertas: se avisa de los pendientes y al ejecutarse se cancelan.
- **D-07:** La exportación sale en un solo archivo con un enlace de 24 horas.

## 15. Riesgos y dudas abiertas
- La recuperación de acceso del cliente pasa a soporte: no hay spec de soporte; confirmar cómo se atiende (panel, correo o formulario).
- Confirmar si «código SMS o texto ELIMINAR» significa uno u otro según el caso (se propuso aceptar cualquiera de los dos).
- La exportación y la eliminación tocan colecciones de otros servicios: definir si se coordinan con eventos de la bandeja de salida (spec 054) o con llamadas internas.
- El texto legal de los plazos (7 días de gracia, 48 horas de exportación) debe coincidir con el aviso de privacidad vigente (KNOCK-218).

## Aprobación
- [ ] Revisor 1 — nombre: _____________________ fecha: __________
- [ ] Sin conflicto con la constitución
- **Estados:** Borrador → Aprobada (una persona; habilita el código) → Implementada (tras el merge)
