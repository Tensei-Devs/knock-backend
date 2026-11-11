# Spec 033 — Panel interno con permisos y doble factor (backend)

> **Estado:** Borrador · **Repositorio:** knock-backend · **Rector:** `constitution.md`
> **Clave de trabajo:** KNOCK-75 (HU-066) · **Otra parte en:** knock-frontend, spec 023 «Panel web interno»
> **Servicio:** Identidad y confianza · **Módulo:** `Panel` (nuevo) · **Colecciones:** `panel_cuentas`, `panel_sesiones`, `panel_auditoria` (nuevas) · **Llaves de Redis:** `panel:desafio:{id}`, `panel:fallos:{sub}`, `panel:fallos-ip:{ip}`, `panel:cuenta:{sub}`
> **Sprint:** Sprint 5 (Jira) · **Responsable sugerido:** Ángel Alexis Blas Castillo (lo nombra la ficha; en Jira está sin asignar)

## 1. Objetivo
Que una persona del equipo de Knock entre al panel web interno con su celular verificado por Firebase y un segundo factor TOTP, reciba una sesión del panel separada de la sesión de la app, y que cada acción del panel se autorice en el servidor por permiso, con cierre inmediato al cambiar sus permisos y un registro de auditoría de cada acceso y cada acción. Esta spec entrega el inicio de sesión, la política de permisos reutilizable y la auditoría; las pantallas y acciones de negocio del panel (HU-067, HU-069, HU-070) usarán esta base.

## 2. Historia y alcance
**2.1 Historia**
- **HU-066 (KNOCK-75)** — Como persona del equipo de Knock, quiero entrar al panel interno con permisos y doble factor, para operar sin acceder a más datos de los necesarios.

**2.2 Dentro**
- Cuatro endpoints de sesión del panel: verificar el celular, verificar el TOTP, renovar y cerrar (las rutas que propone la spec 023 de frontend).
- Cuentas del panel ligadas a una cuenta existente de la app (`usuarios.sub`), con roles, permisos derivados, estado y secreto TOTP cifrado.
- Token de acceso del panel con destinatario y llave propios, distinto del de la app; renovación con cookie `HttpOnly`.
- Política de autorización por permiso (`RequierePermiso`) para los endpoints del panel, con detección de permisos cambiados.
- Auditoría de solo inserción de accesos, acciones permitidas y denegadas, y cambios administrativos.
- Herramienta interna de línea de comandos para dar de alta cuentas, cambiar roles, deshabilitar y reiniciar el TOTP.
- Límites de intentos y bloqueos temporales.
- Rutas del gateway, CORS con credenciales solo para el origen del panel y límites por IP.

**2.3 Fuera**
- Las pantallas del panel (spec 023 de frontend).
- Los endpoints de negocio del panel: revisar verificaciones (HU-067), reportes, reseñas, catálogos y métricas (HU-069, HU-070). Cada uno declarará su permiso con la política de esta spec.
- Consultar la auditoría desde el panel (no hay permiso para ello en la lista de HU-066; queda como duda abierta).
- Recuperar un TOTP perdido por autoservicio: lo reinicia otra persona con la herramienta interna.
- Inicio de sesión de la app (specs 015 a 018) y envío de SMS (lo hace Firebase en el navegador).
- Llaves de seguridad físicas (WebAuthn) y códigos de respaldo.

## 3. Requisitos
**Cuentas, roles y permisos**
- **R-01:** Una cuenta del panel es un documento de `panel_cuentas` ligado por `sub` a una cuenta existente de `usuarios` (spec 015). Ninguna cuenta de la app obtiene acceso al panel por sí sola: no hay endpoint que cree o modifique cuentas del panel; solo la herramienta interna de R-25.
- **R-02:** Los permisos son la lista cerrada `verification:review`, `reports:resolve`, `reviews:moderate`, `catalog:manage` y `metrics:read`. Los roles son la lista cerrada `administrador` (los cinco), `verificador` (`verification:review`), `moderador` (`reports:resolve`, `reviews:moderate`), `catalogo` (`catalog:manage`) y `analista` (`metrics:read`). El mapa de rol a permisos vive en código y cambiarlo exige una spec; una cuenta tiene de uno a cinco roles.
- **R-03:** Cada cuenta tiene `estado` (`activa` o `deshabilitada`) y `permisosVersion`, un entero que aumenta en uno con cada cambio de roles, estado o TOTP.

**Primer paso: celular**
- **R-04:** `POST /api/v1/panel/sesion/celular` es público en el gateway, con el límite por IP de las rutas de autenticación. Cuerpo `{ idToken }` de 1 a 4 096 caracteres; cuerpo máximo de 8 KB. Un cuerpo inválido responde 422 con `errors` (uno ilegible, 400) sin llamar a Firebase.
- **R-05:** El servicio valida `idToken` con el mismo verificador de Firebase de la spec 015 (firma, emisor, destinatario, vencimiento y celular verificado) y deriva el `sub` igual que ella. Un token inválido responde 401 `invalid_credentials`.
- **R-06:** Si no existe una cuenta del panel con ese `sub`, si está `deshabilitada`, si la cuenta de la app está suspendida o si no tiene secreto TOTP, responde 403 `no_panel_access`, con el mismo cuerpo en todos los casos. No se crea nada.
- **R-07:** Si la cuenta procede, crea un desafío de un solo uso en Redis (`panel:desafio:{desafioId}`, `desafioId` de 128 bits aleatorios en base64url) con el `sub` y un contador de intentos, que vence a los 5 minutos, y responde 200 `{ desafioId, requiereTotp: true, venceEn }`.

**Segundo paso: TOTP**
- **R-08:** `POST /api/v1/panel/sesion/totp` es público en el gateway, con el mismo límite por IP. Cuerpo `{ desafioId, codigo }`; `codigo` son exactamente 6 dígitos; cuerpo máximo de 1 KB. Un cuerpo inválido responde 422 (uno ilegible, 400).
- **R-09:** El TOTP sigue RFC 6238: HMAC-SHA1, 6 dígitos, pasos de 30 segundos y una ventana de ±1 paso. Un paso ya aceptado no se vuelve a aceptar: la cuenta guarda `totpUltimoPaso` y la aceptación es una actualización condicionada a que el paso nuevo sea mayor (protege contra la repetición del mismo código y contra dos peticiones simultáneas). El código se compara en tiempo constante.
- **R-10:** Un desafío inexistente, vencido o ya usado, o un código incorrecto o repetido, responde 401 `invalid_credentials` con el mismo cuerpo, sin decir cuál falló. Cada código incorrecto suma un intento al desafío; al quinto el desafío se borra.
- **R-11:** Los fallos se cuentan también por cuenta (`panel:fallos:{sub}`) y por IP (`panel:fallos-ip:{ip}`) en una ventana fija de 15 minutos compartida entre réplicas. Con 5 fallos de la cuenta o 10 de la IP en la ventana, todo intento de los dos pasos para esa cuenta o IP responde 429 `limit_exceeded` con `Retry-After` hasta que la ventana termina, aunque el código sea correcto. Un acceso exitoso no reinicia el contador de la IP.
- **R-12:** Con el código correcto, el desafío se consume de forma atómica (no puede usarse dos veces) y se crea una sesión del panel. La respuesta es 200 `{ accessToken, expiraEn, permisos, nombre }` con `Set-Cookie` del token de renovación (R-16). `permisos` es la lista de permisos efectivos de los roles de la cuenta; `nombre` es el nombre visible guardado en la cuenta del panel.

**Sesión del panel**
- **R-13:** El token de acceso del panel es un JWT HMAC-SHA256 firmado con una llave **distinta** de la de la app (`PanelJwt:SigningKey`, al menos 32 bytes, en Key Vault), con destinatario `knock-panel`, el mismo emisor, vida de 15 minutos y los claims `sub`, `sid`, `pv` (la `permisosVersion` vigente al emitirlo) y `jti`. No lleva los permisos.
- **R-14:** El gateway valida los tokens del panel solo en `/api/v1/panel/**` y rechaza con 401 un token de la app en esas rutas y un token del panel en cualquier otra ruta. Aplica a las sesiones del panel la cancelación por `sid` de la spec 009 (`sesion-cancelada:{sid}`).
- **R-15:** Cada sesión del panel es un documento de `panel_sesiones` con la huella SHA-256 del token de renovación vigente, las últimas 5 huellas usadas, `creadaEn`, `ultimaActividad`, `renovacionVenceEn` y `venceEn`. La vida máxima de una sesión es de 8 horas desde el acceso con TOTP; después hay que repetir los dos pasos. Una cuenta tiene como máximo 3 sesiones abiertas; al abrir la cuarta se revoca la más antigua.
- **R-16:** El token de renovación es aleatorio de 256 bits, se guarda solo como huella y viaja únicamente en la cookie `__Secure-knock-panel-rt` con `HttpOnly`, `Secure`, `SameSite=None`, `Partitioned`, `Path=/api/v1/panel/sesion` y una vida de 20 minutos. Nunca aparece en un cuerpo de respuesta.
- **R-17:** `POST /api/v1/panel/sesion/renovar` no lleva cuerpo y exige la cookie, el encabezado `X-Knock-Panel: 1` y un encabezado `Origin` idéntico al origen configurado del panel; sin el encabezado o con otro origen responde 403 `origin_not_allowed`. Rota el token de renovación con una actualización condicionada a la huella vigente (de dos renovaciones simultáneas solo una gana) y responde como R-12 con la cookie nueva. Un token desconocido, vencido, de una sesión revocada o que superó las 8 horas responde 401 `invalid_session` y borra la cookie. Un token que coincide con una huella usada se trata como robado: se revocan todas las sesiones del panel de esa cuenta, se cancelan sus `sid` en Redis y se registra en la auditoría.
- **R-18:** Antes de renovar, el servicio vuelve a leer la cuenta: si está deshabilitada o su `permisosVersion` cambió, responde 401 `permissions_changed`, revoca la sesión y borra la cookie.
- **R-19:** `POST /api/v1/panel/sesion/cerrar` exige el token del panel y el encabezado `X-Knock-Panel: 1` (sin él responde 403 `origin_not_allowed`); revoca la sesión del `sid`, escribe `sesion-cancelada:{sid}` en Redis por el resto de la vida del token, borra la cookie y responde 204. Repetirlo con el mismo token responde 401.

**Autorización por permiso**
- **R-20:** Todo endpoint del panel (salvo los cuatro de sesión) declara exactamente un permiso con el atributo `RequierePermiso`; una prueba de arquitectura falla si un controller bajo `/api/v1/panel/` no lo declara. Sin declaración, la política deniega.
- **R-21:** En cada petición a un endpoint con permiso, el servicio lee la cuenta del panel (por caché, R-24) y: si está deshabilitada o su `permisosVersion` no coincide con el claim `pv`, responde 401 `permissions_changed` y revoca la sesión; si la cuenta no tiene el permiso del endpoint, responde 403 `insufficient_permission`. La decisión se toma siempre con los roles vigentes en el servidor, nunca con lo que diga el cliente.
- **R-22:** Los endpoints del panel responden `Cache-Control: no-store` y las respuestas de sesión no se comprimen (llevan tokens, spec 010, R-09).

**Auditoría**
- **R-23:** Se escribe un documento en `panel_auditoria` por cada: paso de acceso (exitoso, fallido o bloqueado), renovación, cierre, reuso de token, acción con permiso (permitida o denegada) y cambio hecho con la herramienta interna. Lleva `fecha`, `tipo`, `evento`, `resultado`, `sub` del personal cuando se conoce, `sid`, `permiso`, plantilla de la ruta, `recursoId` cuando la acción toca un recurso, `ipHmac` (HMAC-SHA256 de la IP con la llave de búsqueda de la spec 009) y `correlationId`. Nunca lleva celular, códigos, tokens, `idToken`, cuerpos ni datos de la persona sobre la que se actúa más allá del identificador del recurso. La colección es de solo inserción para el servicio (sin endpoints ni código que actualice o borre) y sus documentos vencen a los 365 días.
- **R-24:** Si la auditoría no se puede escribir, la acción se niega con 503 `audit_unavailable` (falla cerrada contra el repudio); en los pasos de acceso, el acceso no se concede. La cuenta del panel se cachea en Redis (`panel:cuenta:{sub}`, 60 segundos) y toda escritura de la herramienta interna borra esa clave antes de terminar (constitución, principio 9).

**Herramienta interna**
- **R-25:** La herramienta interna es un modo de línea de comandos del ensamblado de Identity (`panel alta`, `panel roles`, `panel deshabilitar`, `panel habilitar`, `panel reiniciar-totp`) que corre sin levantar el servidor web, con la misma configuración del entorno, y solo la ejecuta una persona con acceso al entorno. `alta` recibe el celular de una cuenta existente de la app (lo busca por `celularHash`, nunca lo guarda en claro), el nombre visible (1 a 60 caracteres) y los roles; genera un secreto TOTP de 160 bits y muestra **una sola vez** en la consola la dirección `otpauth://` para registrarlo en una app de autenticación; nunca lo escribe en un registro. Cada comando incrementa `permisosVersion` cuando corresponde, borra la caché de la cuenta, revoca sus sesiones del panel y escribe la auditoría.
- **R-26:** El secreto TOTP se guarda cifrado con AES-256-GCM y una llave versionada propia del panel en Key Vault (`panel-totp-key`), distinta de la del celular. En Development la herramienta acepta `--secreto-de-prueba` para crear una cuenta ficticia con un secreto conocido de las pruebas; en cualquier otro entorno esa opción falla.

**Generales**
- **R-27:** El gateway enruta `/api/v1/panel/**` al servicio de Identidad. Los cuatro endpoints de sesión llevan la política de límite estricto por IP (10 por minuto, como `/auth`); el resto, la política por usuario con el `sub` del token del panel. CORS permite credenciales solo para el origen del panel (`Cors:Panel:Origen`, exacto, `https`) y solo en `/api/v1/panel/**`; los demás orígenes no reciben `Access-Control-Allow-Credentials`.
- **R-28:** Si Firebase, Redis o MongoDB no responden, los pasos de acceso responden 503 `unavailable` con `Retry-After`, sin conceder acceso. Los errores usan ProblemDetails con `code`, `correlationId` y `traceId`. Los registros técnicos no contienen celular, `idToken`, códigos, tokens, `desafioId`, secretos TOTP ni la dirección `otpauth://`.

## 4. Criterios de aceptación
- **CA-01:** Dada una cuenta del panel con rol `administrador` y TOTP registrado, cuando envía un `idToken` válido y después el código TOTP vigente, entonces recibe 200 con `accessToken` de destinatario `knock-panel`, los cinco permisos, su nombre y la cookie de renovación con `HttpOnly`, `Secure`, `SameSite=None`, `Partitioned` y la ruta de sesión, y queda una sesión y dos registros de auditoría de acceso.
- **CA-02:** Dado un desafío vigente, cuando el código TOTP falta, no tiene 6 dígitos, es incorrecto o es de un paso fuera de la ventana, entonces responde 422 (los dos primeros) o 401 `invalid_credentials` (los dos últimos) con el mismo cuerpo, sin token ni cookie.
- **CA-03:** Dado un celular cuya cuenta de la app no tiene cuenta del panel, o una cuenta del panel deshabilitada, cuando envía su `idToken`, entonces responde 403 `no_panel_access` con el mismo cuerpo en ambos casos y no se crea desafío.
- **CA-04:** Dada una sesión con solo el rol `moderador`, cuando pide un endpoint de prueba que exige `verification:review`, entonces responde 403 `insufficient_permission` y queda un registro de auditoría con resultado `denegado`; con un endpoint que exige `reviews:moderate`, responde 200 y queda uno `permitido`.
- **CA-05:** Dada una sesión abierta, cuando la herramienta interna cambia los roles de la cuenta, entonces la siguiente petición con el token anterior responde 401 `permissions_changed`, la sesión queda revocada y la renovación con su cookie responde 401.
- **CA-06:** Dado un código TOTP ya aceptado, cuando se presenta otra vez con un desafío nuevo dentro de su ventana, entonces responde 401 `invalid_credentials`.
- **CA-07:** Dadas dos peticiones simultáneas con el mismo desafío y el mismo código correcto, cuando terminan, entonces una responde 200 y la otra 401, y existe una sola sesión.
- **CA-08:** Dados 5 códigos incorrectos de la misma cuenta en 15 minutos, repartidos entre dos réplicas, cuando llega el sexto intento aunque sea correcto, entonces responde 429 `limit_exceeded` con `Retry-After`; dados 10 fallos desde una IP con cuentas distintas, el undécimo intento de esa IP responde 429.
- **CA-09:** Dado un desafío con 5 códigos incorrectos o con más de 5 minutos, cuando se usa, entonces responde 401 `invalid_credentials` y el desafío ya no existe en Redis.
- **CA-10:** Dado un token de acceso de la app (destinatario `knock-app`), cuando se presenta en una ruta del panel, entonces el gateway responde 401; dado un token del panel en una ruta de la app, también 401.
- **CA-11:** Dada una cookie vigente, el encabezado `X-Knock-Panel: 1` y el origen del panel, cuando se renueva, entonces responde 200 con un token nuevo y otra cookie, y la cookie anterior ya no sirve.
- **CA-12:** Dada una renovación sin `X-Knock-Panel`, con otro `Origin` o sin `Origin`, cuando se pide, entonces responde 403 `origin_not_allowed` y no rota nada.
- **CA-13:** Dadas dos renovaciones simultáneas con la misma cookie, cuando terminan, entonces una responde 200 y la otra 401; dado después el token anterior, cuando se presenta, entonces responde 401, todas las sesiones del panel de la cuenta quedan revocadas y hay un registro de auditoría de reuso.
- **CA-14:** Dada una sesión iniciada hace más de 8 horas, cuando se renueva, entonces responde 401 `invalid_session` aunque la cookie sea reciente.
- **CA-15:** Dada una sesión abierta, cuando se cierra, entonces responde 204, la cookie se borra, existe `sesion-cancelada:{sid}` en Redis y el mismo token responde 401 en cualquier ruta del panel.
- **CA-16:** Dada una cuenta con 3 sesiones del panel abiertas, cuando abre otra, entonces la más antigua queda revocada y siguen 3.
- **CA-17:** Dada la base `identity`, cuando se inspeccionan `panel_cuentas` y `panel_sesiones`, entonces el secreto TOTP solo aparece cifrado, el token de renovación solo como huella y no hay celular en claro.
- **CA-18:** Dada la auditoría sin poder escribirse, cuando se intenta un acceso o una acción con permiso, entonces responde 503 `audit_unavailable` y no concede el acceso ni ejecuta la acción.
- **CA-19:** Dados los registros técnicos y la auditoría de un acceso completo, de uno fallido y de un alta con la herramienta, cuando se inspeccionan, entonces no contienen celular, `idToken`, códigos, tokens, `desafioId`, secreto TOTP ni dirección `otpauth://`.
- **CA-20:** Dado un controller bajo `/api/v1/panel/` sin `RequierePermiso`, cuando corren las pruebas de arquitectura, entonces fallan; dado un endpoint con permiso sin declaración en tiempo de ejecución, la política responde 403.
- **CA-21:** Dada la herramienta interna, cuando se da de alta una cuenta con un celular que no tiene cuenta de la app, con un rol fuera de la lista o con `--secreto-de-prueba` fuera de Development, entonces falla sin crear nada; con datos válidos crea la cuenta, muestra la dirección `otpauth://` una sola vez y escribe la auditoría.
- **CA-22:** Dadas más de 10 peticiones por minuto desde una IP a los endpoints de sesión del panel, cuando llega la undécima, entonces el gateway responde 429 con `Retry-After`.
- **CA-23:** Dada una petición preliminar de CORS desde el origen del panel a una ruta del panel, cuando se responde, entonces permite credenciales y el encabezado `X-Knock-Panel`; desde otro origen o hacia una ruta que no es del panel, no permite credenciales.
- **CA-24:** Dado Firebase, Redis o MongoDB sin responder, cuando se intenta cualquiera de los dos pasos, entonces responde 503 `unavailable` y no concede acceso.
- **CA-25:** Dada una cuenta suspendida en la app (spec 015) con cuenta del panel activa, cuando envía su `idToken`, entonces responde 403 `no_panel_access`.

**4.1 Criterio → prueba prevista**

| Criterio | Prueba prevista |
|---|---|
| CA-01 | `Spec033AccesoTests.Acceso_Con_Totp_CA_033_01` |
| CA-02 | `Spec033AccesoTests.Totp_Invalido_No_Entra_CA_033_02` |
| CA-03 | `Spec033AccesoTests.Sin_Cuenta_De_Panel_403_CA_033_03` |
| CA-04 | `Spec033PermisosTests.Permiso_Insuficiente_403_CA_033_04` |
| CA-05 | `Spec033PermisosTests.Permisos_Cambiaron_Revoca_CA_033_05` |
| CA-06 | `Spec033TotpTests.Codigo_Repetido_Se_Rechaza_CA_033_06` |
| CA-07 | `Spec033TotpTests.Desafio_De_Un_Solo_Uso_CA_033_07` |
| CA-08 | `Spec033LimitesTests.Bloqueo_Por_Cuenta_E_Ip_CA_033_08` |
| CA-09 | `Spec033TotpTests.Desafio_Agotado_O_Vencido_CA_033_09` |
| CA-10 | `Spec033GatewayTests.Tokens_No_Se_Cruzan_CA_033_10` |
| CA-11 | `Spec033RenovacionTests.Renueva_Y_Rota_CA_033_11` |
| CA-12 | `Spec033RenovacionTests.Origen_No_Permitido_CA_033_12` |
| CA-13 | `Spec033RenovacionTests.Reuso_Revoca_Todo_CA_033_13` |
| CA-14 | `Spec033RenovacionTests.Vida_Maxima_Ocho_Horas_CA_033_14` |
| CA-15 | `Spec033CierreTests.Cierra_Y_Cancela_Sid_CA_033_15` |
| CA-16 | `Spec033SesionesTests.Maximo_Tres_Sesiones_CA_033_16` |
| CA-17 | `Spec033DatosTests.Secretos_Solo_Cifrados_CA_033_17` |
| CA-18 | `Spec033AuditoriaTests.Falla_Cerrado_Sin_Auditoria_CA_033_18` |
| CA-19 | `Spec033RegistrosTests.Sin_Datos_Sensibles_CA_033_19` |
| CA-20 | `Spec033ArquitecturaTests.Todo_Endpoint_Declara_Permiso_CA_033_20` |
| CA-21 | `Spec033HerramientaTests.Alta_Valida_Y_Rechazos_CA_033_21` |
| CA-22 | `Spec033GatewayTests.Limite_Por_Ip_En_Sesion_CA_033_22` |
| CA-23 | `Spec033GatewayTests.Cors_Con_Credenciales_Solo_Panel_CA_033_23` |
| CA-24 | `Spec033DisponibilidadTests.Dependencias_Caidas_503_CA_033_24` |
| CA-25 | `Spec033AccesoTests.Cuenta_Suspendida_403_CA_033_25` |

## 5. Contrato
| Método y ruta | Quién | Entrada | Respuestas |
|---|---|---|---|
| `POST /api/v1/panel/sesion/celular` | público (límite por IP) | `{ idToken }` | 200 `{ desafioId, requiereTotp: true, venceEn }`; 400 (cuerpo ilegible), 401 `invalid_credentials`, 403 `no_panel_access`, 429 `limit_exceeded`, 503 `unavailable` |
| `POST /api/v1/panel/sesion/totp` | público (límite por IP) | `{ desafioId, codigo }` | 200 `{ accessToken, expiraEn, permisos[], nombre }` + `Set-Cookie`; 400 (cuerpo ilegible), 401 `invalid_credentials`, 403 `no_panel_access`, 429 `limit_exceeded`, 503 `unavailable` o `audit_unavailable` |
| `POST /api/v1/panel/sesion/renovar` | cookie de renovación + `X-Knock-Panel: 1` + `Origin` del panel | sin cuerpo | 200 como `totp` + `Set-Cookie` nueva; 401 `invalid_session` o `permissions_changed`, 403 `origin_not_allowed`, 429, 503 |
| `POST /api/v1/panel/sesion/cerrar` | token del panel + `X-Knock-Panel: 1` | sin cuerpo | 204 (borra la cookie); 401, 403 `origin_not_allowed` |
| Cualquier endpoint futuro del panel | token del panel con el permiso declarado | según su spec | además de los suyos: 401 `permissions_changed`, 403 `insufficient_permission`, 503 `audit_unavailable` |

**Formas:** `permisos` es un arreglo de cadenas de la lista de R-02, sin repetidos y en el orden de esa lista; `expiraEn` y `venceEn` son fechas UTC ISO 8601; `nombre` tiene de 1 a 60 caracteres.

**Encabezados:** todas las respuestas llevan `Cache-Control: no-store` y `X-Correlation-Id`; las de sesión no se comprimen; 429 y 503 llevan `Retry-After`. La cookie: `__Secure-knock-panel-rt=<token>; HttpOnly; Secure; SameSite=None; Partitioned; Path=/api/v1/panel/sesion; Max-Age=1200`; para borrarla se envía con `Max-Age=0`.

**Idempotencia:** el paso de TOTP no es repetible (desafío de un solo uso); la renovación rota el token y una segunda renovación con el mismo token es un reuso; el cierre repetido responde 401. La app no reintenta solos estos endpoints tras un 2xx.

**Paginación:** no hay listados en esta spec.

**Límites:** cuerpos de 8 KB (celular) y 1 KB (TOTP); desafío de 5 minutos y 5 códigos; 5 fallos por cuenta y 10 por IP en 15 minutos; 10 peticiones por minuto por IP en los endpoints de sesión; 15 minutos de token de acceso, 20 de cookie y 8 horas de sesión; 3 sesiones por cuenta.

**Diferencias con lo que propuso la spec 023 de frontend:** coinciden las cuatro rutas, `idToken`, `desafioId`, `requiereTotp`, `codigo`, la respuesta `{ accessToken, expiraEn, permisos, nombre }`, los cinco permisos, `invalid_credentials`, `no_panel_access` y `permissions_changed`. Esta spec agrega `403 insufficient_permission`, `403 origin_not_allowed`, `401 invalid_session`, `503 unavailable` y `audit_unavailable`, el encabezado obligatorio `X-Knock-Panel: 1` en renovar y cerrar, y fija el límite por IP en 10 fallos (la 023 supuso 5); el panel solo obedece `Retry-After`, así que el cambio no rompe su comportamiento.

## 6. Modelo de datos
**`panel_cuentas` (base `identity`)**

| Campo | Tipo | Regla |
|---|---|---|
| `_id` | ObjectId | — |
| `sub` | string | único; el `usuarios.sub` de la spec 015 |
| `nombre` | string | 1 a 60 caracteres, visible en el panel |
| `roles` | string[] | 1 a 5 de la lista cerrada de R-02 |
| `estado` | string | `activa`, `deshabilitada` |
| `permisosVersion` | int | inicia en 1; aumenta con cada cambio |
| `totpSecretoCifrado` | binario | AES-256-GCM con `panel-totp-key` |
| `totpVersionLlave` | int | versión de la llave usada |
| `totpUltimoPaso` | long | último paso aceptado (anti-repetición) |
| `creadaEn`, `actualizadaEn` | fecha UTC | — |

Índice único en `sub`.

**`panel_sesiones` (base `identity`)**

| Campo | Tipo | Regla |
|---|---|---|
| `_id` | string | el `sid` (UUID v7) |
| `sub` | string | dueño |
| `huellaRenovacion` | string | SHA-256 del token vigente; índice único |
| `huellasUsadas` | string[] | últimas 5; índice multiclave |
| `permisosVersion` | int | la vigente al emitir |
| `creadaEn`, `ultimaActividad` | fecha UTC | — |
| `renovacionVenceEn` | fecha UTC | emisión del token + 20 minutos |
| `venceEn` | fecha UTC | `creadaEn` + 8 horas; índice TTL |
| `revocada` | bool | — |

Índice `{ sub: 1, revocada: 1, creadaEn: 1 }` para contar y revocar.

**`panel_auditoria` (base `identity`)**

| Campo | Tipo | Regla |
|---|---|---|
| `fecha` | fecha UTC | índice TTL de 365 días |
| `tipo` | string | `acceso`, `sesion`, `accion`, `administracion` |
| `evento` | string | por ejemplo `celular_verificado`, `totp_fallido`, `acceso_concedido`, `bloqueo`, `renovacion`, `reuso_detectado`, `cierre`, `accion`, `alta`, `roles_cambiados` |
| `resultado` | string | `permitido`, `denegado`, `fallido` |
| `sub`, `sid`, `permiso`, `ruta`, `recursoId` | string? | según el evento |
| `ipHmac`, `correlationId` | string | — |

Índices `{ sub: 1, fecha: -1 }` y `{ evento: 1, fecha: -1 }` para consultas de revisión por la base.

**Redis:** `panel:desafio:{id}` (hash `sub`, `intentos`; 5 minutos), `panel:fallos:{sub}` y `panel:fallos-ip:{ip}` (contadores de 15 minutos con vencimiento en la misma operación atómica), `panel:cuenta:{sub}` (`roles`, `estado`, `permisosVersion`, `nombre`; 60 segundos) y `sesion-cancelada:{sid}` (spec 009).

**Script de `db/`:** el siguiente número libre crea las tres colecciones con sus índices. No hay semillas con secretos en el repositorio; la cuenta de prueba se crea con la herramienta y `--secreto-de-prueba` en Development.

## 7. Arquitectura y componentes
- Módulo nuevo `src/Knock.Identity.Api/Modules/Panel/` con `Controllers/PanelSesionController`, `Dtos`, `Validators`, `Services` (`AccesoPanelService`, `TotpService`, `SesionPanelService`, `AuditoriaPanel`), `Domain` (`CuentaPanel`, `Rol`, `Permiso`, `MapaDeRoles`, `SesionPanel`), `Data` (almacenes de las tres colecciones y de Redis), `Authorization` (`RequierePermisoAttribute`, `PermisoPanelHandler`) y `Cli` (comandos de la herramienta).
- Reutiliza de la spec 015 el verificador de Firebase (`IFirebaseTokenVerifier`), la derivación del `sub` y la búsqueda por `celularHash`; de la 016, el patrón de rotación condicionada y detección de reuso; de la 009, la cancelación por `sid` y el HMAC de búsqueda.
- `TotpService` envuelve la biblioteca de TOTP y el cifrado del secreto; no conoce HTTP ni MongoDB.
- Esquema de autenticación `PanelBearer` en Identity con la llave y el destinatario del panel, aplicado solo a los controllers del panel; el esquema de la app no se acepta en ellos.
- Gateway: segundo esquema JWT para `/api/v1/panel/**`, rutas en `ReverseProxy`, la ruta de sesión en la política estricta por IP, la política por usuario con el `sub` del panel y la política CORS `panel` con credenciales.
- Herramienta interna: `Program.cs` de Identity detecta el verbo `panel` en los argumentos, construye solo los servicios necesarios (MongoDB, Redis, Key Vault) y termina con código 0 o distinto de 0; nunca levanta Kestrel en ese modo.
- Los futuros endpoints del panel en Mercado o Mensajería necesitarán el mismo `PermisoPanelHandler`; esta spec lo construye en Identity y deja la decisión de compartirlo o duplicarlo para la primera spec que lo necesite (duda abierta).

## 8. Dependencias
- **Base:** specs 002, 003 (MongoDB, Redis, TTL), 009 (JWT, límites, cancelación por `sid`, HMAC y cifrado de campos), 010 (sin caché ni compresión en respuestas con tokens), 011 (registros) y 015 a 018 (verificador de Firebase, `usuarios`, patrón de sesiones y renovación).
- **Frontend:** spec 023 «Panel web interno» consume los cuatro endpoints de §5; la spec 006 de frontend debe vendorizar el contrato de Identity para generar el cliente TypeScript.
- **Consumidoras futuras:** HU-067 (revisar verificaciones, con `IEnlacesPrivados` de la spec 032 y la auditoría de aquí), HU-069 y HU-070.
- **Configuración y secretos nuevos (Key Vault):** `PanelJwt:SigningKey`, `panel-totp-key` (versionada); configuración sin secretos: `Cors:Panel:Origen`, `PanelJwt:Audience = knock-panel`.
- **Dependencia nueva a evaluar y registrar en `Memoria.md`:** `Otp.NET` (licencia MIT), la que nombra HU-066 §7. Firebase en el navegador para el SMS es de frontend.
- **Despliegue:** el origen del panel en Railway (spec 003 de frontend) debe estar fijo antes de configurar CORS.

## 9. Amenazas y controles
| Amenaza | Control | Caso que lo prueba |
|---|---|---|
| Suplantación con el SMS robado (intercambio de SIM) | Segundo factor TOTP obligatorio | CA-01, CA-02 |
| Fuerza bruta del TOTP (10⁶ combinaciones) | 5 códigos por desafío, 5 fallos por cuenta y 10 por IP en 15 minutos, límite del gateway | CA-08, CA-09, CA-22 |
| Bloquear a propósito la cuenta de otra persona del equipo | Los fallos por cuenta solo cuentan después del primer paso, que exige el `idToken` de su celular; un tercero solo puede agotar el límite de su propia IP | CA-08 |
| Repetición de un código TOTP observado | `totpUltimoPaso` con actualización condicionada | CA-06, CA-07 |
| Una cuenta de la app obtiene acceso al panel | Cuenta del panel explícita, sin endpoint de alta; llave y destinatario del token distintos | CA-03, CA-10 |
| Usar el token de la app en el panel o al revés | Llave y destinatario distintos; el gateway separa esquemas por ruta | CA-10 |
| Elevación de privilegios por una acción sin permiso | `RequierePermiso` obligatorio, deniega por defecto, decisión con roles del servidor | CA-04, CA-20 |
| Permisos revocados pero sesión viva | Claim `pv` comparado con la versión vigente en cada petición | CA-05 |
| Robo del token de renovación | Cookie `HttpOnly` fuera del alcance de los scripts, rotación y detección de reuso que revoca todo | CA-11, CA-13 |
| Falsificación de petición entre sitios con la cookie `SameSite=None` | `Origin` exacto y encabezado `X-Knock-Panel` que obliga a una petición preliminar de CORS solo permitida al panel | CA-12, CA-23 |
| Sesión olvidada en un equipo compartido | Token de 15 minutos, cookie de 20, sesión de 8 horas y cierre que cancela el `sid` | CA-14, CA-15 |
| Enumerar cuentas del panel | El 403 `no_panel_access` solo lo ve quien ya demostró tener el celular con Firebase; mismo cuerpo para inexistente y deshabilitada | CA-03, CA-25 |
| Robo de la base | Secreto TOTP cifrado con llave propia; renovación solo como huella | CA-17 |
| Repudio («yo no hice esa acción») | Auditoría de solo inserción con `sub`, `sid`, permiso y recurso; sin auditoría no hay acción | CA-04, CA-18 |
| Fuga de datos por registros o auditoría | Lista de campos prohibidos y prueba que la comprueba | CA-19 |
| Secreto TOTP filtrado al darlo de alta | Se muestra una sola vez en la consola de quien ejecuta, nunca en registros | CA-21 |
| Cuentas de prueba con secreto conocido en producción | `--secreto-de-prueba` solo en Development | CA-21 |
| Denegación por dependencias caídas | 503 sin conceder acceso | CA-24 |

## 10. Plan de pruebas
| Tipo | ¿Aplica? | Motivo |
|---|---|---|
| Unitarias | Sí | Mapa de roles, validadores, TOTP con reloj simulado (ventana, pasos repetidos), estados de la sesión, política de permisos y opciones |
| Integración | Sí | Identity y gateway con MongoDB y Redis reales (`Testcontainers`), emulador o doble de Firebase, dos hosts para límites y simultaneidad |
| Contrato | Sí | Los cuatro endpoints, sus códigos y la cookie en el OpenAPI de Identity; comparación con lo que consume la spec 023 de frontend |
| Seguridad negativa | Sí | Tokens cruzados, CSRF en renovar, reuso, fuerza bruta, cuenta deshabilitada o suspendida, permisos cambiados, alta con datos inválidos |
| Humo | Sí | En el entorno de evaluación, con una cuenta de prueba creada con la herramienta: las rutas del panel sin token responden 401 y `/api/v1/panel/sesion/celular` con cuerpo inválido responde 422; el recorrido completo está en «Aceptación de punta a punta» |
| Regresión | Sí | Suites de las specs 015 a 018 sin cambios: el inicio de sesión de la app no se ve afectado por el esquema del panel |
| Arquitectura | Sí | Todo controller del panel declara permiso; `TotpService` sin dependencias de HTTP ni de MongoDB; el modo de herramienta no registra Kestrel |
| Mutación | Sí | Ventana del TOTP, condición de paso mayor, comparación de `pv`, límites de fallos y mapa de roles (meta de la spec 008) |
| CI/CD | Sí | Gates de la spec 007; el contrato de Identity se regenera y `oasdiff` no marca cambios incompatibles |
| Análisis estático y vulnerabilidades | Sí | Semgrep (sin comparar secretos con `==`; comparación de tiempo constante), Gitleaks (sin secretos TOTP en pruebas fuera de los generados), OSV-Scanner para `Otp.NET` |
| Aceptación de punta a punta | Sí | Cuenta de prueba creada con la herramienta en Development y recorrido del panel (spec 023) contra la API local |
| Rendimiento | No | Herramienta interna con pocas personas usuarias |

## 11. Escenarios
**Positivos**
- La administradora entra con SMS y TOTP y ve las cinco secciones.
- Un moderador entra y solo puede resolver reportes y moderar reseñas.
- El panel renueva en silencio cada 15 minutos mientras se usa, hasta 8 horas.
- El líder retira el rol `verificador` a alguien: su siguiente clic lo saca del panel.

**Negativos**
- Un cliente de la app con su celular verificado intenta entrar al panel.
- Alguien con el SMS robado no tiene el TOTP.
- Un sitio malicioso intenta renovar la sesión con la cookie del navegador.
- Un atacante prueba códigos TOTP desde varias IP.
- Se presenta el token de la app en `/api/v1/panel/...`.

**Errores esperados**
- 400 por cuerpos ilegibles; 422 por cuerpos inválidos; 401 `invalid_credentials`, `invalid_session` y `permissions_changed`; 403 `no_panel_access`, `insufficient_permission` y `origin_not_allowed`; 429 `limit_exceeded`; 503 `unavailable` y `audit_unavailable`.

**Casos límite**
- Código del paso anterior, del actual y del siguiente (aceptados) y de dos pasos atrás (rechazado).
- El quinto y el sexto fallo; el décimo y el undécimo por IP.
- Desafío usado en el segundo 299 y en el 301.
- Renovación en el minuto 19 y en el 21 de la cookie; sesión en la hora 7:59 y 8:01.
- Cambio de roles entre el primer y el segundo paso: el segundo paso lee la cuenta de nuevo y emite con la `permisosVersion` vigente.
- Navegador que bloquea cookies de terceros: la renovación responde 401 y el panel pide entrar otra vez (comportamiento aceptado).
- Reloj del servidor desfasado: la ventana de ±1 paso tolera hasta 30 segundos.

## 12. Definición de terminado
- CA-01 a CA-25 con prueba en verde y la tabla 4.1 igual a los nombres reales; `python scripts/sdd_lint.py --trace 033` sin faltantes.
- OpenAPI de Identity regenerado con los cuatro endpoints, la cookie y los códigos; `docs/endpoints.md` actualizado; el equipo de frontend confirma que la spec 023 coincide con §5.
- Llaves `PanelJwt:SigningKey` y `panel-totp-key` creadas en Key Vault por una persona; `Cors:Panel:Origen` configurado.
- Cuenta de prueba creada con la herramienta en un entorno de desarrollo y recorrido de punta a punta con el panel.
- `Memoria.md` registra `Otp.NET`, el esquema de token del panel y la decisión de la cookie `SameSite=None`.
- Revisión de par y de seguridad sin hallazgos abiertos; PR con «Uso de IA» y etiqueta de puntos.

## 13. Decisiones
- **Sesión del panel separada de la de la app:** otra llave, otro destinatario, otra colección y vidas más cortas; un token de la app nunca abre el panel.
- **Permisos fuera del token:** el servidor decide con los roles vigentes y `pv` detecta el cambio en la siguiente petición; un token no conserva permisos viejos.
- **Cuentas del panel ligadas a la cuenta de la app:** reutiliza la verificación del celular con Firebase y el `sub`, sin un segundo registro de personas.
- **Auditoría que falla cerrada:** en un panel con datos sensibles, una acción sin rastro es peor que una acción denegada.
- **Altas por herramienta interna:** HU-066 lo pide y evita un endpoint de alta que sería el blanco más valioso del sistema.

## 14. Decisiones tomadas sin consulta
Se tomaron de noche, con el criterio de la constitución; quien apruebe puede cambiarlas antes de firmar.

1. **Se adoptan las rutas y nombres que propuso la spec 023 de frontend** (`idToken`, `desafioId`, `codigo`, `permisos`, `nombre`). Así el panel no cambia. Alternativa: usar `firebaseIdToken` como la spec 015, que obligaría a cambiar el borrador de frontend.
2. **Cookie `SameSite=None; Partitioned` con `Origin` exacto y `X-Knock-Panel`.** El panel vive en Railway y la API en Azure: son sitios distintos y una cookie `SameSite=Strict` no viajaría. Alternativa: dominio propio compartido (posible costo) o token de renovación en memoria (cada recarga obliga a repetir SMS y TOTP).
3. **Roles fijos con permisos fijos** (`administrador`, `verificador`, `moderador`, `catalogo`, `analista`). HU-066 solo enumera permisos y menciona «rol de administrador». Alternativa: asignar permisos sueltos a cada cuenta, más flexible y más fácil de equivocar.
4. **Una sola colección `panel_cuentas` con el TOTP dentro**, en lugar de `roles_panel` y `totp` separadas como dice HU-066 §7. Así el cambio de roles, de estado y de TOTP es atómico. Alternativa: dos colecciones, como la ficha.
5. **Vidas: 15 minutos de acceso, 20 de cookie, 8 horas de sesión y 3 sesiones por cuenta.** Coinciden con el cierre por inactividad de 15 minutos del panel y con una jornada. Alternativa: 30 días como la app, excesivo para un panel con datos sensibles.
6. **5 fallos por cuenta y 10 por IP en 15 minutos.** Con 10 por IP, un equipo detrás de la misma red no se bloquea por los errores de una sola persona; la 023 supuso 5 por IP. Alternativa: 5 por IP.
7. **Auditoría con TTL de 365 días e IP como HMAC.** Cubre un año de revisión sin guardar la IP en claro. Alternativa: sin vencimiento (crece sin límite) o 90 días (corto para una revisión).
8. **La auditoría no se consulta desde el panel en esta spec.** No hay permiso para ello en HU-066; se consulta en la base con permisos de la nube. Alternativa: agregar un sexto permiso `audit:read` y un endpoint.
9. **Herramienta interna como modo de línea de comandos del mismo ensamblado de Identity.** No agrega un proyecto ni una imagen. Alternativa: un proyecto `tools/` aparte, con su propio despliegue.
10. **`Otp.NET` como biblioteca de TOTP**, la que nombra HU-066. Alternativa: implementar RFC 6238 a mano, que ahorra una dependencia pero agrega código criptográfico propio.

## 15. Riesgos y dudas abiertas
- **Cookies de terceros:** si el navegador del personal bloquea cookies de terceros aun con `Partitioned`, la renovación no funciona y cada 15 minutos se repiten SMS y TOTP. La solución definitiva es un dominio común para panel y API; queda para el líder.
- **Firebase Phone Auth en web exige reCAPTCHA y tiene cuotas** (KNOCK-146); es un riesgo de frontend que puede afectar el costo cero.
- **`insufficient_permission` y `no_panel_access` en la spec 023:** el panel trata cualquier 403 como «sin permiso» o «sin acceso»; conviene que distinga los dos códigos.
- **Endpoints del panel en otros servicios:** la política de permisos se construye en Identity; Mercado (reseñas, catálogos) necesitará la misma regla. Compartirla por `Knock.Contracts` va contra «contratos sin lógica»; duplicarla requiere una excepción registrada.
- **La auditoría de acciones depende de que cada spec futura pase `recursoId`:** HU-067, 069 y 070 deben declararlo.
- **Responsable:** KNOCK-75 está sin asignar en Jira; la ficha nombra a Alexis.
- **Sprint:** Jira la tiene en Sprint 5; la spec 023 de frontend también. No es requisito de la demo del Sprint 4.

## Aprobación
- [ ] Revisor 1 — nombre: ________ fecha: ________
- [ ] Sin conflicto con la constitución
- **Estados:** Borrador (agente) → Aprobada (persona; habilita el código) → Implementada (tras el merge)
