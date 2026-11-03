# Spec 018 — Sesiones por dispositivo

> **Estado:** Borrador · **Repositorio:** knock-backend · **Rector:** `constitution.md`
> **Servicio:** Identidad y confianza · **Endpoints del Excel:** SES-04 (`GET /api/v1/me/sesiones`) y SES-05 (`DELETE /api/v1/me/sesiones/{id}`) · **Colección:** `sesiones`

## 1. Objetivo
Que la persona vea en qué dispositivos tiene la sesión abierta y pueda cerrar la de otro dispositivo (por ejemplo, un teléfono perdido) sin tener que cerrar la del que está usando.

## 2. Alcance
**Dentro**
- Listar las sesiones abiertas de la persona.
- Cerrar la sesión de otro dispositivo: revocarla y cancelar sus JWT.

**Fuera**
- Crear, renovar y cerrar la sesión propia (specs 015 y 016).
- Cerrar todas las sesiones a la vez.
- Mostrar ubicación o dirección IP de las sesiones.

## 3. Requisitos
- **R-01:** `GET /api/v1/me/sesiones` exige JWT válido y devuelve las sesiones de la persona que no están revocadas ni vencidas, con `id`, `dispositivo`, `ultimaActividad` y `actual` (verdadero para la sesión del token). Nunca devuelve huellas ni tokens.
- **R-02:** Una cuenta tiene como máximo 10 sesiones abiertas (spec 015), así que el listado no se pagina; es la excepción documentada a la paginación por cursor.
- **R-03:** El listado va de la sesión con actividad más reciente a la más antigua y la respuesta lleva `Cache-Control: private, no-store`: es personal y cambia con cada inicio o cierre de sesión.
- **R-04:** `DELETE /api/v1/me/sesiones/{id}` marca la sesión como `revocada = true`, escribe `sesion-cancelada:{sid}` en Redis con el resto de vigencia del JWT (máximo 15 minutos) y responde 204.
- **R-05:** Solo la dueña de la sesión puede cerrarla: si la sesión es de otra persona responde 403 y no cambia nada; si no existe, 404.
- **R-06:** El identificador de la ruta debe ser un `ObjectId` válido; si no, 400.
- **R-07:** La sesión cerrada ya no se puede renovar (spec 016) y sus JWT son rechazados por el gateway.
- **R-08:** Los registros no incluyen tokens ni nombres de dispositivo. Errores en ProblemDetails.

## 4. Criterios de aceptación
- **CA-01:** Dada una persona con tres sesiones abiertas, cuando pide el listado, entonces recibe tres, una marcada como `actual`, ordenadas por actividad.
- **CA-02:** Dada una sesión revocada o vencida, cuando se pide el listado, entonces no aparece.
- **CA-03:** Dado el listado, cuando se inspecciona, entonces no contiene huellas ni tokens.
- **CA-04:** Dada una sesión propia de otro dispositivo, cuando se cierra, entonces responde 204, queda revocada y su JWT recibe 401 en el gateway.
- **CA-05:** Dada la sesión de otra persona, cuando se intenta cerrar, entonces responde 403 y sigue abierta.
- **CA-06:** Dado un identificador que no existe, cuando se intenta cerrar, entonces responde 404.
- **CA-07:** Dado un identificador mal formado, cuando se pide, entonces responde 400.
- **CA-08:** Dada una petición sin token, cuando se pide cualquiera de los dos endpoints, entonces responde 401.
- **CA-09:** Dada la sesión cerrada, cuando su dispositivo intenta renovar, entonces responde 401.

## 5. Contrato
- `GET /api/v1/me/sesiones` · persona · 200 `[{ id, dispositivo, ultimaActividad, actual }]`, 401.
- `DELETE /api/v1/me/sesiones/{id}` · persona (dueña) · sin cuerpo · 204, 400, 401, 403, 404.

## 6. Amenazas y controles
| Amenaza | Control |
|---|---|
| Cerrar o ver las sesiones de otra persona | El listado se filtra por el `sub` del token (convertido al `_id` interno de la cuenta) y el cierre comprueba que la sesión sea de quien lo pide |
| Enumerar identificadores de sesiones ajenas | Identificadores `ObjectId` no secuenciales y 403 solo después de autenticar; registro del intento |
| Sesión robada que sigue activa | Cierre desde otro dispositivo con cancelación inmediata del JWT en Redis |
| Fuga de tokens | El listado nunca incluye huellas ni tokens |

## 7. Plan de pruebas
| Tipo | ¿Aplica? | Motivo |
|---|---|---|
| Unitarias | Sí | Reglas del listado y de propiedad de la sesión |
| Integración | Sí | MongoDB y Redis reales: cerrar una sesión y comprobar el rechazo del JWT |
| Seguridad negativa | Sí | Sesión ajena, identificador inválido y sin token |
| Contrato | Sí | Respuestas y errores en el OpenAPI |
| Arquitectura | No | Se cubre en la spec 002 |

## 8. Decisiones
- **Sin paginación:** el máximo de 10 sesiones por cuenta acota el listado; paginar solo agregaría complejidad.
- **Mismo mecanismo de cancelación que el logout:** una sola clave por `sid` en Redis, definida en la spec 016.

## 9. Riesgos y dudas abiertas
- ¿Qué debe hacer `DELETE /me/sesiones/{id}` si el `id` es la sesión actual? Propuesta: 409 con el mensaje de usar el cierre de sesión normal (spec 016).
- El Excel pide 403 para la sesión ajena. Un 404 evitaría confirmar que el identificador existe; con `ObjectId` el riesgo es bajo. ¿Se mantiene el 403?
- ¿El nombre del dispositivo (modelo y sistema) es un dato personal que deba protegerse más? Se asume que no.

## Aprobación
- [ ] Revisor 1 — nombre: ________ fecha: ________
- [ ] Sin conflicto con la constitución
- **Estados:** Borrador → Aprobada (una persona; habilita el código) → Implementada (tras el merge)
