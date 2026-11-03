# Spec 015 — Registro e inicio de sesión

> **Estado:** Borrador · **Repositorio:** knock-backend · **Rector:** `constitution.md`
> **Servicio:** Identidad y confianza · **Endpoints del Excel:** SES-01 (`POST /api/v1/auth/firebase`) · **Colecciones:** `usuarios`, `sesiones`
> **Alineada con:** HU-001 (KNOCK-43) y TT-API-12 (KNOCK-187) del repositorio histórico del equipo, ya aprobadas. Donde difieren, aquí manda lo decidido en ellas (ver sección 8).

## 1. Objetivo
Permitir que una persona entre a Knock con su celular: el teléfono verifica el número con Firebase (código SMS) y el backend cambia esa prueba por una sesión propia. La primera vez que entra, la misma operación crea su cuenta. Es el endpoint de **registro y login** que pide la evaluación de la segunda unidad.

## 2. Alcance
**Dentro**
- Validar el token de Firebase que manda la app.
- Crear la cuenta si el celular es nuevo (con la aceptación del aviso de privacidad y los términos) o reconocerla si ya existe.
- Crear la sesión del dispositivo: token de acceso (JWT de 15 minutos) y token de renovación (30 días, guardado solo como huella).
- Límite de 10 sesiones abiertas por cuenta.
- Comportamiento según el estado de la cuenta y los conflictos de identidad.

**Fuera**
- Renovar el token y cerrar sesión (spec 016).
- Listar y cerrar sesiones de otros dispositivos (spec 018).
- Completar el perfil con el nombre (spec 017), cambio de número y recuperación de acceso.
- Envío del SMS: lo hace Firebase en el teléfono, no el backend.
- Registro del técnico y verificación de identidad.

## 3. Requisitos
- **R-01:** `POST /api/v1/auth/firebase` es una ruta pública: el gateway la deja pasar sin token y le aplica el límite de peticiones por IP más estricto de la API (valor en configuración).
- **R-02:** El servicio valida el `firebaseIdToken` con las claves públicas de Google: firma, emisor y destinatario del proyecto Firebase de Knock, vencimiento, y que traiga el número de celular verificado. Sin eso responde 401 uniforme. El celular y el `uid` salen solo del token verificado; el cliente nunca los manda.
- **R-03:** El cuerpo lleva `firebaseIdToken` (1 a 4096 caracteres), `deviceId` (UUID de 36 caracteres que genera la app), `aceptaAviso` (debe ser `true`, en cada canje) y `dispositivo` (nombre del teléfono, máximo 80 caracteres). FluentValidation valida todo en la frontera; un cuerpo inválido responde 422 con `errors` por campo, sin repetir el valor enviado y sin llamar a Firebase ni a la base.
- **R-04:** El `sub` de la persona es un UUID v8 derivado con SHA-256 del `uid` de Firebase (TT-API-12); el `uid` no se guarda. Si no existe un `usuarios` con ese `sub`, se crea la cuenta en una sola transacción: `sub`, `celularCifrado` (AES-256-GCM) y `celularHash` (HMAC-SHA256, único), `estado = activa`, `esTecnico = false`, `perfilCompleto = false` y dos consentimientos (`aviso_privacidad` y `terminos`) con su versión vigente, fecha y `deviceId`. Responde **201** con `esNuevo = true`. Si ya existe, responde **200** con `esNuevo = false` y su `perfilCompleto`, sin tocar sus datos.
- **R-05:** La unicidad la garantizan los índices únicos de `usuarios.sub` y `usuarios.celularHash`: si varias peticiones crean la misma cuenta a la vez, una gana (201) y las demás se tratan como cuenta existente (200).
- **R-06:** Si el mismo celular llega con otro `uid` (otro `sub`), o el mismo `uid` con otro celular, se responde **409** con `code = account_conflict`, sin tokens, sin crear nada y sin revelar nada de la otra cuenta.
- **R-07:** Una cuenta `suspendida` recibe **403** con `code = account_suspended` y no se crea sesión. Una cuenta en `eliminacion_programada` sí inicia sesión; la app consulta su estado con `GET /me` (spec 017) para ofrecer cancelar la eliminación.
- **R-08:** Se crea un documento en `sesiones` por dispositivo con la huella (SHA-256) del token de renovación, `venceEn` a 30 días y `revocada = false`. El token de renovación es aleatorio de 256 bits y nunca se guarda en claro.
- **R-09:** El JWT de acceso dura 15 minutos, usa HMAC-SHA256 con la clave de Key Vault (spec 009) y lleva `sub` (el `usuarios.sub`), `sid` (id de la sesión) y `jti`.
- **R-10:** Una cuenta tiene como máximo 10 sesiones abiertas; al abrir la 11.ª se revoca la más antigua.
- **R-11:** La respuesta exitosa es `{ accessToken, refreshToken, esNuevo, perfilCompleto }`. Lleva `Cache-Control: no-store` y no se comprime (spec 010, R-09). Nunca incluye el celular, el `uid`, el hash ni la huella.
- **R-12:** Los registros no incluyen el celular, el `uid`, el token de Firebase, el JWT ni el token de renovación. Errores en ProblemDetails.
- **R-13:** El acceso al verificador de Firebase está detrás de una interfaz, para sustituirlo por un doble en las pruebas.

## 4. Criterios de aceptación
- **CA-01:** Dado un token válido de un celular nuevo y `aceptaAviso = true`, cuando se pide, entonces responde 201 con `esNuevo = true` y `perfilCompleto = false`, y existen la cuenta, sus dos consentimientos y la sesión.
- **CA-02:** Dado un token válido de un celular que ya tiene cuenta, cuando se pide, entonces responde 200 con `esNuevo = false` y no se crea otra cuenta ni otros consentimientos.
- **CA-03:** Dado un cuerpo sin `firebaseIdToken`, con `deviceId` que no es UUID o con `aceptaAviso` distinto de `true`, cuando se pide, entonces responde 422 con `errors` y no se llama a Firebase ni se crea nada.
- **CA-04:** Dado un token vencido, de otro proyecto, con otra firma o sin número, cuando se pide, entonces responde 401.
- **CA-05:** Dada una cuenta suspendida, cuando se pide, entonces responde 403 `account_suspended` sin tokens.
- **CA-06:** Dada una cuenta en eliminación programada, cuando se pide, entonces responde 200 y `GET /me` muestra `estado = eliminacion_programada`.
- **CA-07:** Dada una cuenta con 10 sesiones abiertas, cuando abre otra, entonces la más antigua queda revocada y siguen 10 abiertas.
- **CA-08:** Dadas ocho peticiones simultáneas con el mismo celular nuevo, cuando terminan, entonces hay exactamente un 201, siete 200 y una sola cuenta.
- **CA-09:** Dado el mismo celular con otro `uid`, o el mismo `uid` con otro celular, cuando se pide, entonces responde 409 `account_conflict` sin tokens ni documentos nuevos.
- **CA-10:** Dadas las respuestas y la base, cuando se inspeccionan, entonces `Cache-Control: no-store`, sin compresión, ningún celular en claro (en la base solo `celularCifrado` y `celularHash`) y solo la huella del token de renovación.
- **CA-11:** Dadas más peticiones que el límite desde una IP, cuando se pide, entonces responde 429.
- **CA-12:** Dado un registro de la operación, cuando se escribe, entonces no contiene celular, `uid` ni tokens.

## 5. Contrato
`POST /api/v1/auth/firebase` · público · cuerpo `{ firebaseIdToken, deviceId, aceptaAviso, dispositivo }` · respuestas 200, 201, 400, 401, 403, 409, 422, 429 (ProblemDetails con `code` en 403 y 409). Respuesta exitosa: `{ accessToken, refreshToken, esNuevo, perfilCompleto }`. Se publica en `docs/openapi.v1.json`; la interfaz del verificador de Firebase vive en el servicio, no en `Knock.Contracts`.

## 6. Amenazas y controles
| Amenaza | Control |
|---|---|
| Token de Firebase falsificado, reutilizado o de otro proyecto | Validación de firma, emisor, destinatario y vencimiento con las claves públicas de Google |
| Fuerza bruta o abuso de la ruta pública | Límite de peticiones por IP en el gateway, más estricto que el resto, y 429 |
| Crear muchas cuentas o sesiones | Límite por IP y máximo de 10 sesiones por cuenta |
| Robo de la base de datos | El celular se guarda cifrado y solo se busca por su hash; del token de renovación solo la huella |
| Suplantar o duplicar una cuenta con el mismo celular y otro `uid` | 409 `account_conflict` por los índices únicos de `sub` y `celularHash` |
| Enumerar celulares registrados | El 401 es genérico y el 409 no revela nada de la otra cuenta |
| Fuga de datos por los registros | Filtro de datos personales (spec 009) y no se registran tokens, celular ni `uid` |
| Clave de firma y llaves de cifrado expuestas | Viven en Key Vault, nunca en el repositorio |

## 7. Plan de pruebas
| Tipo | ¿Aplica? | Motivo |
|---|---|---|
| Unitarias | Sí | Derivación del `sub`, validadores del cuerpo, estados de la cuenta, emisión y huella de tokens |
| Integración | Sí | MongoDB y Redis reales en contenedor, con un doble del verificador de Firebase |
| Seguridad negativa | Sí | Token inválido, cuenta suspendida, conflicto de cuenta, límite de peticiones y registros sin datos personales |
| Contrato | Sí | Respuestas y errores en el OpenAPI |
| Arquitectura | Sí | El controller no toca MongoDB (spec 002) |

## 8. Decisiones
- **Se hereda lo ya aprobado en HU-001 y TT-API-12:** `sub` derivado del `uid`, celular cifrado más hash, `aceptaAviso` y `deviceId` obligatorios, 409 por conflicto y respuesta de cuatro campos. Motivo: es lo que el líder ya decidió y lo que el código del repositorio histórico implementa; esta spec nace del Excel de endpoints, que se ajustó a ello.
- **Registro y login en un solo endpoint:** el número ya viene verificado por Firebase, así que no hay contraseña ni paso aparte de registro; 201 distingue la cuenta nueva.
- **El nombre se pide después:** la cuenta se crea con `perfilCompleto = false` y la persona completa su perfil con `PUT /me` (spec 017).
- **Token de renovación opaco y con huella:** permite revocarlo y detectar su reuso (spec 016).
- **Sin SMS propio:** lo envía Firebase; el backend nunca ve el código.

## 9. Riesgos y dudas abiertas
- HU-001 guarda la huella del token en una colección `refresh_tokens` con `familiaId` y `dispositivoId`; el Excel usa `sesiones`. ¿Son la misma colección? Propuesta: `sesiones` con los campos de ambas (agregar `deviceId` y la familia de rotación). Decide el equipo antes de implementar.
- Los SMS reales de Firebase requieren el plan de pago por uso (Blaze). Para la evaluación del 3 y 4 de noviembre hay que decidir si se usan números de prueba de Firebase.
- Versiones iniciales de `aviso_privacidad` y `terminos` (en HU-001 son una constante por confirmar).
- ¿Qué librería verifica el token de Firebase? En el repositorio histórico ya se eligió una: reutilizarla si pasa la evaluación de dependencias (`Memoria.md`).
- Valor concreto del límite de peticiones de autenticación por IP.

## Aprobación
- [ ] Revisor 1 — nombre: ________ fecha: ________
- [ ] Sin conflicto con la constitución
- **Estados:** Borrador → Aprobada (una persona; habilita el código) → Implementada (tras el merge)
