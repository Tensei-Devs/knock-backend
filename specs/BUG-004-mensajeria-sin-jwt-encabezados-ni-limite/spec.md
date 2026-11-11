# BUG-004 — Mensajería sin JWT, encabezados de seguridad ni límite de peticiones propios

> **Jira:** KNOCK-241 · **Épica:** EP-13 · **Módulo:** Messaging · **Responsable:** José Guillermo Rangel Hernández · **Sprint:** S4 · **Puntos:** 3 (propuesto) · **Capas:** Back
> **Repositorio:** knock-backend · **Otra parte en:** —
> **Fuentes:** docs/modelo-de-amenazas.md H-03 · specs/009 · specs/001
> **Skills:** security-core, auth-firebase-jwt, foundation-backend
> **Estado:** Aprobada

## 1. Objetivo
Mensajería no valida JWT, no agrega encabezados de seguridad y no limita peticiones por sí misma; hoy solo la protege el ingreso interno del entorno. No tiene controladores de negocio, así que el riesgo es bajo hoy y crece cuando se agreguen. Se aplican los mismos controles que Identidad y Mercado **antes** de que Mensajería publique su primer endpoint (y de la spec 026, KNOCK-62).

## 2. Historia y casos de prueba
**BUG-004** — Como equipo, queremos que Mensajería aplique los mismos controles de seguridad que Identidad y Mercado, para no depender solo del ingreso interno cuando se agreguen endpoints.

| Caso | Criterio (Dado / Cuando / Entonces) | Verificación |
|---|---|---|
| TC-BUG-004-01 | Dado Mensajería, cuando llega una petición a una ruta protegida sin token, con token expirado, con firma inválida, `alg=none`, otra audiencia o emisor → 401 `ProblemDetails` sin repetir datos | SecurityTest/Messaging/AuthenticationTests.NoValidToken_Returns401_TC_BUG_004_01 |
| TC-BUG-004-02 | Dado un token válido de Knock → pasa la autenticación (la identidad sale de `sub`) y las rutas anónimas (`/health`) siguen respondiendo | SecurityTest/Messaging/AuthenticationTests.ValidToken_Passes_TC_BUG_004_02 |
| TC-BUG-004-03 | Dado cualquier respuesta de Mensajería → lleva `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, `Content-Security-Policy` y `Cache-Control: no-store`, sin `Server` | SecurityTest/Messaging/SecurityHeadersTests.EveryResponse_CarriesTheHeaders_TC_BUG_004_03 |
| TC-BUG-004-04 | Dado más peticiones que el límite configurado en la ventana → 429 con `Retry-After` y `ProblemDetails`; otra IP no se ve afectada | SecurityTest/Messaging/RateLimitTests.OverTheLimit_Returns429_TC_BUG_004_04 |
| TC-BUG-004-05 | Dado una sesión cancelada (`sesion-cancelada:{sid}` en Redis) → 401 igual que en Identidad y Mercado | SecurityTest/Messaging/AuthenticationTests.RevokedSession_Returns401_TC_BUG_004_05 |
| TC-BUG-004-06 | Dado Mensajería sin llave de firma o con la configuración inválida → no arranca (falla rápido), igual que los otros servicios | UnitTest/Messaging/AuthenticationStartupTests.InvalidConfiguration_FailsFast_TC_BUG_004_06 |
| TC-BUG-004-08 | Dado Redis que no responde (caído o con tiempo agotado) al revisar `sesion-cancelada:{sid}` → 503 `ProblemDetails` y la petición no pasa, igual que en el gateway (no se deja pasar sin saber si la sesión sigue viva) | SecurityTest/Messaging/AuthenticationTests.RedisUnavailable_Returns503_TC_BUG_004_08 |
| TC-BUG-004-07 | Dado el modelo de amenazas y `docs/rate-limit-matrix.md` → H-03 cerrado y la matriz incluye Mensajería | (comando) sdd_lint + revisión de docs |

## 3. Alcance / No-alcance
- **In (este repositorio):**
- Validación del JWT de Knock (mismo emisor, audiencia y llave que los otros servicios; sin `alg=none`), `[Authorize]` por defecto con `/health` y `/health/ready` anónimos.
- Encabezados de seguridad y `AddServerHeader = false`.
- Límite de peticiones global por IP con `Retry-After` y `ProblemDetails` (mismo mecanismo que Mercado), configurable.
- Comprobación de sesión cancelada en Redis (si Mensajería no tiene Redis hoy, se agrega solo lectura).
- `ForwardedHeaders` solo de la red del gateway.
- Pruebas de seguridad con un host real y una fábrica de pruebas de Mensajería.
- **Out:**
- Endpoints de negocio y el hub (spec 026, KNOCK-62, y HU-029).
- Ruta pública en el gateway.
- Cambios en Identidad, Mercado o el gateway, salvo la lectura de la misma lista de sesiones canceladas.

## 4. Contratos API y eventos
- Ninguno nuevo; las respuestas 401/429 usan `ProblemDetails` como los demás servicios.

## 5. Modelo de datos
- Redis: solo lectura de `sesion-cancelada:{sid}`.

## 6. Seguridad, privacidad y amenazas
| Amenaza | Control | Caso que lo prueba |
|---|---|---|
| Suplantación: llamar a Mensajería sin sesión si alguien llega a la red interna | JWT obligatorio; mismos emisor, audiencia y llave | TC-BUG-004-01, 02 |
| Fuga de información por encabezados | Encabezados de seguridad y sin `Server` | TC-BUG-004-03 |
| Denegación por exceso de peticiones | Límite global por IP con 429 y `Retry-After` | TC-BUG-004-04 |
| Sesión cancelada sigue siendo válida | Comprobación de `sesion-cancelada:{sid}` | TC-BUG-004-05 |
| Redis caído y la sesión se acepta sin comprobarla (fallar abierto) | Si Redis no responde al revisar la sesión cancelada, responde 503 y cierra el paso, como el gateway | TC-BUG-004-08 |
| Configuración débil (llave corta o ausente) | Arranque con falla rápida | TC-BUG-004-06 |

## 7. Pantallas
—

## 8. Pruebas, gates y límites
**Plan de pruebas** (Sí/No con motivo; meta de mutación):

| Tipo | ¿Aplica? | Motivo / herramienta |
|---|---|---|
| Unitarias | Sí | Arranque y configuración |
| Integración / Seguridad | Sí | Host real de Mensajería (`WebApplicationFactory`, Redis y MongoDB de Testcontainers) |
| Contrato | No | Sin endpoints |
| Base de datos | No | Sin colecciones |
| Mutación | Meta ≥ 60 % del módulo en el nightly | Stryker |

**Gates:** ver `AGENTS.md §5` (Definición de terminado).
**Límites y dudas abiertas** (las resuelve una persona antes de aprobar; no se inventan):
1. **Reutilización.** Identidad y Mercado tienen copias de `AuthenticationExtensions`, `SecurityExtensions` y `RateLimitingExtensions`. EXC-007 solo cubre logging, observabilidad y `TelemetryRedactor`, así que NO ampara estas clases. Propuesta: copiar el patrón a Mensajería con `jscpd:ignore` bajo una **excepción propia y temporal** (nuevo `EXC-NNN`, herramienta jscpd, alcance los tres archivos copiados, vence ligada a KNOCK-251, plan: moverlas a la biblioteca técnica compartida y retirar la excepción). Alternativa: llevar esas clases directo a la biblioteca compartida, a costa de ampliar esta tarea. El líder elige; la excepción la aprueba el líder antes de fusionar (no se copia nada sin ella).
2. **Orden con la spec 026 (KNOCK-62).** Su R-03 exige que Mensajería valide el token por su cuenta en el hub. Propuesta: este bug aporta primero el esquema de autenticación compartido (JWT, sesión cancelada, encabezados y límite) y la spec 026 lo reutiliza.
3. **Redis en Mensajería.** Confirmar que el servicio ya tiene cadena de conexión a Redis en el entorno (`deploy/apps/main.tf`); si no, se agrega solo la variable (la aplica el líder).
4. **Alternativa aceptada por la ficha:** si no se implementa ahora, la spec de Mensajería debe exigir estos controles antes de su primer endpoint. Propuesta: implementarlo (es corto y desbloquea la spec 026 (KNOCK-62)).

## Aprobación
- [x] Revisor 1 — nombre: José Julián Hernández Hernández fecha: 2026-11-10
- [x] Sin conflicto con la constitución
- **Estados:** Borrador (agente) → Aprobada (persona; habilita el código) → Firmada (tras el merge)
