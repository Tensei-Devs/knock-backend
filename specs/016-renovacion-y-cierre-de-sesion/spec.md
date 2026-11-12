# Spec 016 — Renovación y cierre de sesión

> **Estado:** Aprobada · **Repositorio:** knock-backend · **Rector:** `constitution.md`
> **Servicio:** Identidad y confianza · **Endpoints del Excel:** SES-02 (`POST /api/v1/auth/refresh`) y SES-03 (`POST /api/v1/auth/logout`) · **Colección:** `sesiones`

## 1. Objetivo
Mantener la sesión viva mientras la persona use la app, sin pedirle otro código, y cerrarla de verdad en el backend cuando haga logout. Cubre los requerimientos «la sesión siempre viva, excepto si el usuario realiza logout» y «logout a nivel de backend y móvil», que incluye cancelar el JWT y no solo borrarlo en el teléfono.

## 2. Alcance
**Dentro**
- Cambiar un token de renovación por un par nuevo (rotación) y detectar el uso de un token ya usado.
- Cerrar la sesión del dispositivo: cancelar el JWT en Redis y revocar el token de renovación.
- Rechazo de los JWT cancelados en el gateway.

**Fuera**
- Crear la sesión (spec 015) y gestionar las sesiones de otros dispositivos (spec 018).
- Quitar el identificador de notificaciones del teléfono (endpoint `DIS-02` del servicio de Mensajería; la app lo llama antes de cerrar sesión).
- Borrar la caché y la sesión local de la app (frontend).

## 3. Requisitos
- **R-01:** `POST /api/v1/auth/refresh` es público y recibe `refreshToken`; el gateway le aplica el límite de peticiones por IP de las rutas de autenticación.
- **R-02:** El servicio busca la sesión por la huella del token. Si no existe, está revocada, vencida o la cuenta está suspendida, responde 401.
- **R-03:** Al renovar con éxito se genera un token de renovación nuevo, su huella reemplaza a la anterior, la anterior pasa a `huellasUsadas` (máximo las últimas 5), `ultimaActividad` se actualiza y `venceEn` se mueve a 30 días. La respuesta trae un JWT de acceso nuevo (15 minutos, mismo `sid`) y el token de renovación nuevo.
- **R-04:** La rotación es atómica: se hace con una sola operación condicionada a la huella vigente, para que de dos peticiones simultáneas con el mismo token (o en dos copias del servicio) solo una gane.
- **R-05:** Si el token presentado coincide con una huella de `huellasUsadas`, se considera robado: se revocan todas las sesiones de la cuenta, se cancelan sus JWT en Redis y se responde 401.
- **R-06:** `POST /api/v1/auth/logout` exige JWT válido y no lleva cuerpo. Marca la sesión del token (`sid`) como `revocada = true` y escribe en Redis la clave `sesion-cancelada:{sid}` con tiempo de vida igual al resto de vigencia del JWT (máximo 15 minutos). Responde 204.
- **R-07:** El gateway rechaza con 401 todo JWT cuyo `sid` esté cancelado en Redis.
- **R-08:** Una sesión revocada no se puede renovar aunque el token de renovación sea correcto.
- **R-09:** Las respuestas de ambos endpoints llevan `Cache-Control: no-store` y no se comprimen (spec 010, R-09). Los registros no incluyen tokens.
- **R-10:** Errores en ProblemDetails; el 401 no dice si el token estaba vencido, revocado o reutilizado.

## 4. Criterios de aceptación
- **CA-01:** Dado un token de renovación vigente, cuando se renueva, entonces responde 200 con un JWT y un token de renovación nuevos y el token anterior ya no sirve.
- **CA-02:** Dados dos intentos simultáneos con el mismo token, cuando terminan, entonces uno responde 200 y el otro 401.
- **CA-03:** Dado un token de renovación ya usado, cuando se presenta, entonces responde 401 y todas las sesiones de la cuenta quedan revocadas.
- **CA-04:** Dada una sesión revocada o vencida, cuando se renueva, entonces responde 401.
- **CA-05:** Dado un logout con un JWT válido, cuando termina, entonces responde 204, la sesión queda revocada y existe la clave de cancelación en Redis.
- **CA-06:** Dado un JWT de una sesión cerrada, cuando se usa en cualquier ruta protegida, entonces el gateway responde 401 aunque no haya vencido.
- **CA-07:** Dado un logout sin token o con token inválido, cuando se pide, entonces responde 401.
- **CA-08:** Dado un token de renovación de una sesión cerrada con logout, cuando se renueva, entonces responde 401.
- **CA-09:** Dadas las respuestas, cuando se inspeccionan, entonces traen `Cache-Control: no-store` y no están comprimidas.
- **CA-10:** Dadas más peticiones que el límite desde una IP, cuando se renueva, entonces responde 429.

## 5. Contrato
- `POST /api/v1/auth/refresh` · público · cuerpo `{ refreshToken }` · 200 `{ accessToken, expiresIn, refreshToken }`, 400 (cuerpo ilegible), 401, 422 (`errors.refreshToken`: ausente o con formato inválido), 429.
- `POST /api/v1/auth/logout` · persona · sin cuerpo · 204, 401.
- Clave de Redis `sesion-cancelada:{sid}` que el gateway consulta en cada petición con token.

## 6. Amenazas y controles
| Amenaza | Control |
|---|---|
| Robo del token de renovación | Rotación en cada uso y detección de reuso con revocación de todas las sesiones |
| Carrera entre dos copias con el mismo token | Rotación atómica condicionada a la huella vigente |
| JWT sigue sirviendo después del logout | Cancelación por `sid` en Redis, que el gateway consulta, hasta que el JWT venza |
| Pérdida de la lista de cancelados si Redis se reinicia | Riesgo aceptado: un JWT cerrado podría servir hasta 15 minutos más; el token de renovación sí queda revocado en MongoDB |
| Fuerza bruta de tokens de renovación | Token aleatorio de 256 bits, solo se guarda su huella y límite de peticiones por IP |
| Fuga de tokens por los registros | No se registran tokens (spec 009) |

## 7. Plan de pruebas
| Tipo | ¿Aplica? | Motivo |
|---|---|---|
| Unitarias | Sí | Reglas de rotación, detección de reuso y cálculo de vigencia |
| Integración | Sí | MongoDB y Redis reales; rotación concurrente y logout seguido de una petición protegida |
| Seguridad negativa | Sí | Token reutilizado, sesión revocada, JWT cancelado y token manipulado |
| Contrato | Sí | Respuestas y errores en el OpenAPI |
| Arquitectura | No | Se cubre en la spec 002 |

## 8. Decisiones
- **La identidad sale del `sub`:** el servicio busca la cuenta por `usuarios.sub` y trabaja con su `_id` interno; el `_id` no sale de la base.
- **Cancelar por `sid` y no por `jti`:** una sola clave cancela todos los JWT de la sesión, incluidos los que ya se emitieron al renovar.
- **Rotación con detección de reuso:** si un atacante y la persona usan el mismo token, se descubre y se cierran las sesiones.
- **La cancelación vive en Redis:** el JWT dura solo 15 minutos, así que la lista nunca crece más de lo que se emite en ese tiempo.

## 9. Decisiones de la revisión, riesgos y dudas
- **Nota (2026-11-12, spec 037 R-13):** la 037 pide que Identidad avise `sesion_revocada { sid }` al cerrar una sesión (R-06) y al revocarlas por reuso del token de renovación (R-04), para borrar los dispositivos de avisos ligados a ese `sid`. Esta spec no cambia su contrato HTTP; el evento es un requisito nuevo que se concreta al implementar la 037.
Decisiones tomadas en la revisión del 2026-11-04. Quien aprueba la spec las acepta; si alguna no convence, se cambia aquí antes de aprobar.

- **Renovación perdida por la señal (decidido): se acepta el riesgo y se mide.** Si el servidor ya rotó el token y la respuesta no llegó, el reintento cuenta como reuso y cierra las sesiones. Es raro y seguro; una tolerancia de segundos se agrega solo si las pruebas muestran que ocurre.
- **Cerrar en todos los dispositivos (decidido): fuera de esta spec.** No está en el Excel; se escribe como spec aparte si se pide.
- **Riesgo:** el gateway consulta Redis en cada petición con token; el costo se confirma con las pruebas de carga.

## Aprobación
- [x] Revisor 1 — nombre: José Julián Hernández Hernández fecha: 2026-11-11
- [x] Sin conflicto con la constitución
- **Estados:** Borrador → Aprobada (una persona; habilita el código) → Implementada (tras el merge)
