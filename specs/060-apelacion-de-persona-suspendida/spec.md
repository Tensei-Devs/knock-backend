# Spec 060 — Apelación de una persona suspendida

> **Estado:** Borrador (agente) — **sin firmar: la prueba y la firma son de José** · **Repositorio:** knock-backend · **Rector:** `constitution.md`
> **Jira:** por crear (KNOCK-266 paraguas) · **Responsable:** por asignar
> **Fuentes:** spec 059 (§15, decisión 3 de José del 2026-11-23: «la apelación de una persona suspendida se resuelve en una spec propia: ruta pública que prueba el control del número con el código SMS»), spec 043 (REP-03, R-13 a R-15 y R-21), spec 016 (R-02 y R-04 a R-08), spec 015, spec 033 (panel, doble factor), spec 053 (R-20, REP-05), `docs/referencia/DECISIONES.md` §5.
> **Servicios:** Identidad (`Knock.Identity.Api`, módulos `Safety`, `Sesiones` y `Panel`) · **Colecciones:** `usuarios`, `reportes`, `panel_auditoria`, `outbox` (sin colecciones nuevas)
> **Skills:** `security-core`, `auth-firebase-jwt`, `vertical-slice-api`, `events-outbox`

## 1. Objetivo
Que una persona suspendida (spec 059) pueda apelar de verdad. Hoy REP-03 (spec 043) exige sesión y la suspensión revoca todas las sesiones y bloquea el inicio de sesión (403 `account_suspended`), así que el debido proceso de `DECISIONES.md` §5 no es alcanzable. Esta spec define una ruta pública que **prueba el control del número con el código SMS (Firebase) sin abrir sesión**, registra la apelación sobre la decisión que causó la suspensión y la deja en la cola del panel; y define la resolución por el panel que reactiva la cuenta (`Reactivar` de la spec 059, R-15).

## 2. Historia y alcance
**2.1 Historia.** Como persona suspendida, quiero apelar la decisión desde la pantalla de «cuenta suspendida» sin poder iniciar sesión, para que una persona distinta de la que decidió la revise y, si procede, recuperar mi cuenta.

**2.2 Dentro**
- `POST /api/v1/suspension/apelacion` (pública: sin JWT de Knock; con el `idToken` de Firebase de un inicio de sesión por SMS): crea o reutiliza la apelación de la decisión vigente.
- `GET /api/v1/suspension/apelacion` (mismas credenciales): estado de su apelación (`pendiente`, `aceptada`, `rechazada`).
- Resolución desde el panel (spec 033): aceptar (llama `Reactivar`, spec 059 R-15) o rechazar, con permiso `reports:resolve` y doble factor, por una persona distinta de la que decidió (con la marca «misma persona» si no hay otra, spec 043 R-21).
- Avisar a la persona del resultado cuando vuelva a iniciar sesión (no hay push: no tiene dispositivos).

**2.3 Fuera**
- Crear reportes, bloqueos y la apelación autenticada de una decisión que no suspende (spec 043).
- La regla gradual y sus valores (spec 059 R-16; valores decididos: 3 advertencias vigentes en 90 días, suspensión de 7 días).
- Texto de las pantallas (frontend).

## 3. Requisitos
- **R-01:** `POST /api/v1/suspension/apelacion` no usa el JWT de Knock. Exige `Authorization: Bearer <idToken de Firebase>` de un inicio de sesión por SMS reciente (`auth_time` de hasta 10 minutos) y se valida igual que `POST /api/v1/auth/firebase` (firma, proyecto, expiración y número verificado). **No emite sesión ni tokens de Knock.**
- **R-02:** La persona se identifica por la huella HMAC de su número (spec 015). Si no hay cuenta con ese número, o la cuenta no está `suspendida`, o no hay una apelación posible, la respuesta es **la misma 404 `not_found` en los tres casos** (no revela si el número existe ni su estado).
- **R-03:** Cuerpo `{ texto }` con 8 a 500 caracteres (igual que spec 043 R-21). Un valor inválido responde 422 con `errors.texto` sin repetirlo; un cuerpo ilegible, 400.
- **R-04:** La apelación es **una sola vez por decisión** (spec 043). Repetirla con el mismo `texto` responde 200 con la misma apelación (idempotente); con otro `texto`, 409 `appeal_not_allowed`.
- **R-05:** La apelación se asigna con prioridad a una persona del panel distinta de la que decidió; si no hay otra con el permiso, la revisa quien lo tenga y queda marcada «misma persona» (spec 043 R-21). Mientras está pendiente (`en_apelacion`) **la suspensión se mantiene** (spec 059 R-14).
- **R-06:** Resolver la apelación desde el panel exige `reports:resolve` y doble factor y deja un registro en `panel_auditoria` (`apelacion_aceptada` o `apelacion_rechazada`, con quién resolvió y el reporte; nunca el texto). **Aceptar** ejecuta `Reactivar(usuarioId, reporteId)` (spec 059 R-15) en la misma transacción; **rechazar** deja la cuenta suspendida y cierra la apelación.
- **R-07:** Límites: 5 intentos por hora por huella de número y 20 por hora por IP (política `suspension-apelacion` en `IdentityRateLimits`); 429 `rate_limited` con `Retry-After`. Cuerpo máximo de 2 KiB (413 `payload_too_large`).
- **R-08:** Ningún dato de quien reportó ni el texto del reporte aparece en las respuestas, registros ni auditoría (spec 043 R-22). El `idToken`, el número y el texto de la apelación nunca se registran.
- **R-09:** `GET /api/v1/suspension/apelacion` devuelve `{ estado, presentadaEn, resueltaEn? }` y nunca el texto del moderador ni la identidad de quien resolvió.
- **R-10:** Al resolver, se escribe `ApelacionResuelta` en la bandeja de salida (spec 054) solo para auditoría; **no** se envía push (la persona no tiene dispositivos). La persona ve el resultado con `GET /api/v1/suspension/apelacion` o al volver a iniciar sesión tras `Reactivar`.

## 4. Criterios de aceptación
- **CA-01:** Dada una persona suspendida con un `idToken` válido y reciente, cuando apela con un texto válido, entonces responde 200 con `{ estado: "pendiente" }`, la cuenta sigue `suspendida` y no se creó ninguna sesión.
- **CA-02:** Dado un `idToken` inválido, vencido, de otro proyecto o con `auth_time` de más de 10 minutos, cuando se llama la ruta, entonces responde 401 y no cambia nada.
- **CA-03:** Dado un número sin cuenta, una cuenta activa o una cuenta suspendida sin decisión apelable, cuando se llama la ruta, entonces las tres respuestas son idénticas (404 `not_found`, mismo cuerpo y misma forma).
- **CA-04:** Dado un texto de 7 o de 501 caracteres, cuando se llama, entonces responde 422 con `errors.texto` sin repetir el valor.
- **CA-05:** Dada una apelación ya presentada, cuando se repite con el mismo texto, entonces 200 con la misma apelación; con otro texto, 409 `appeal_not_allowed`.
- **CA-06:** Dada una apelación pendiente, cuando se consulta la cuenta, entonces sigue `suspendida` (spec 059 CA-19).
- **CA-07:** Dada una apelación aceptada desde el panel con permiso y doble factor, cuando se resuelve, entonces la cuenta queda `activa` (sin sesiones restauradas), se emite `CuentaReactivada`, hay registros `apelacion_aceptada` y `cuenta_reactivada` y la persona puede iniciar sesión de nuevo.
- **CA-08:** Dada una apelación rechazada, cuando se resuelve, entonces la cuenta sigue `suspendida`, la apelación queda `rechazada` y no se puede apelar de nuevo la misma decisión.
- **CA-09:** Dada una persona del panel sin `reports:resolve` o sin doble factor, cuando intenta resolver, entonces 403 y no cambia nada.
- **CA-10:** Dado un solo moderador con permiso, que decidió el reporte, cuando llega la apelación, entonces la revisa él mismo y queda marcada «misma persona».
- **CA-11:** Dadas 6 apelaciones en una hora con el mismo número, cuando llega la sexta, entonces 429 con `Retry-After`; dadas 21 desde la misma IP, 429.
- **CA-12:** Dado cualquier registro, auditoría o respuesta de error, cuando se inspeccionan, entonces no contienen el `idToken`, el número, el texto de la apelación ni el texto del reporte.
- **CA-13:** Dada una apelación resuelta, cuando se consulta `GET /api/v1/suspension/apelacion`, entonces devuelve el estado y las fechas sin la identidad de quien resolvió.

**4.1 Criterio → prueba prevista**

| Criterio | Prueba prevista |
|---|---|
| CA-01, CA-02, CA-03, CA-04, CA-05 | `IntegrationTest/Spec060ApelacionSuspensionTests` (con el emulador de Firebase, como las specs 015 y 016) |
| CA-06, CA-07, CA-08, CA-10 | `IntegrationTest/Spec060ResolucionTests` |
| CA-09 | `SecurityTest/Spec060ResolucionSeguridadTests` |
| CA-11, CA-12, CA-13 | `SecurityTest/Spec060ApelacionSeguridadTests` y `UnitTest/Spec060RegistrosTests` |

## 5. Contrato
| Ruta | Quién | Entrada | Éxito | Errores |
|---|---|---|---|---|
| `POST /api/v1/suspension/apelacion` | persona suspendida con `idToken` de Firebase | `{ texto }` | `200 { estado, presentadaEn }` | `400`, `401`, `404 not_found`, `409 appeal_not_allowed`, `413`, `422`, `429` |
| `GET /api/v1/suspension/apelacion` | igual | — | `200 { estado, presentadaEn, resueltaEn? }` | `401`, `404 not_found`, `429` |
| `POST /api/v1/admin/apelaciones/{id}/resolucion` | persona del panel (`reports:resolve`, doble factor) | `{ decision: "aceptar" \| "rechazar" }` | `200` | `400`, `401`, `403`, `404`, `409 appeal_already_resolved`, `422` |

El gateway publica las dos rutas `suspension/apelacion` como públicas (sin autorización de Knock; la política de límites de Identidad las cubre) y la de panel dentro de `/api/v1/admin/**`.

## 6. Modelo de datos
Sin colecciones nuevas. `reportes.apelacion` (spec 043) gana `origen` (`sesion` o `suspension`) y `resuelta` (`{ decision, en, por }`); `usuarios.suspension` (spec 059) no cambia. Índice nuevo: ninguno (se busca por `reportes._id` y por la huella del número, ya indexada).

## 7. Arquitectura y componentes
`SuspensionAppealController` y `SuspensionAppealService` en `Safety`; el servicio valida el `idToken` con el verificador ya usado por `AuthController` (no se duplica), llama a `ReportesStore` (apelación una sola vez, con relectura y `KeyedGate`) y a `AccountSuspension.ReactivateAsync` (spec 059). `PanelAppealResolutionController` en `Panel`. Sin dependencias nuevas.

## 8. Dependencias
Specs 043 (REP-03), 059 (R-14, R-15, `Reactivar`), 033 (panel y doble factor), 015/016 (huella de número y verificación de Firebase), 054 (bandeja). Pruebas con el emulador de Firebase (como las specs 015 y 016).

## 9. Amenazas y controles
| Amenaza | Control | Caso |
|---|---|---|
| Enumerar qué números tienen cuenta o están suspendidos | Respuesta idéntica en los tres casos de R-02 | CA-03 |
| Apelar la cuenta de otra persona | Se exige el código SMS del número (Firebase) reciente, sin sesión de Knock | CA-02 |
| Reutilizar un `idToken` viejo | `auth_time` de hasta 10 minutos | CA-02 |
| Inundar la cola del panel | Una sola vez por decisión, límites por número y por IP | CA-05, CA-11 |
| El moderador que decidió revisa su propia decisión | Prioridad a otra persona; marca «misma persona» si no hay otra | CA-10 |
| Filtrar datos personales en registros | Redacción y prueba de registros | CA-12 |

## 10. Plan de pruebas
Unitarias (validadores y reglas), integración con emulador de Firebase y MongoDB reales, seguridad negativa (tokens inválidos, enumeración, límites), arquitectura (sin referencias nuevas entre módulos), contrato (OpenAPI de Identidad) y registros sin datos personales.

## 11. Escenarios
**Positivo:** una persona suspendida por una decisión `critica` inicia el código SMS, apela y un moderador distinto acepta: la cuenta vuelve `activa` y puede iniciar sesión. **Negativo:** el mismo número apela dos veces con otro texto (409). **Error esperado:** `idToken` de hace una hora (401). **Límite:** el único moderador con permiso decidió el reporte: revisa él y queda marcada «misma persona».

## 12. Definición de terminado
CA-01 a CA-13 con prueba; `python scripts/sdd_lint.py --trace 060` en OK; OpenAPI de Identidad regenerado; `docs/endpoints.md` y `docs/rate-limit-matrix.md` al día; gates de `AGENTS.md §5` en verde; `Memoria.md` con la entrada.

## 13. Decisiones
- **D-01:** la apelación pública vive en una ruta nueva (`/suspension/apelacion`) y no relaja `REP-03`, para que ninguna ruta autenticada acepte a una persona suspendida.
- **D-02:** la prueba de control del número es el `idToken` de Firebase reciente y **no emite sesión** (decisión de José del 2026-11-23: «prueba el control del número con el código SMS»).
- **D-03:** valores de la regla gradual de la spec 059: 3 advertencias vigentes en 90 días y suspensión de 7 días; «se levanta solo por apelación en el panel» (decisión de José del 2026-11-23).

## 14. Decisiones tomadas sin consulta
1. **`auth_time` de 10 minutos.** Alternativa: 5 o 30 minutos.
2. **404 uniforme en lugar de 403 para «no suspendida».** Evita enumerar números a costa de un mensaje menos claro para quien se equivoca de número.
3. **Sin push de resultado** (la persona no tiene dispositivos): se consulta con `GET` o al iniciar sesión.
4. **Interpretación de «suspensión de 7 días… se levanta solo por apelación»** (D-03): la suspensión gradual dura 7 días y **antes de ese plazo solo se levanta por apelación aceptada**; al cumplirse el plazo vence sola. Alternativa: no vence nunca y solo la levanta una apelación.

## 15. Riesgos y dudas abiertas
- **Duda 1 (para José):** la interpretación 4 de §14 (¿la suspensión gradual vence sola a los 7 días?).
- **Duda 2:** pruebas de integración con el emulador de Firebase solo corren en el CI (hoy dos pruebas de Identidad lo necesitan y fallan en local).
- **Duda 3:** el número máximo de apelaciones simultáneas en la cola del panel para una sola persona (hoy una por decisión).

## Aprobación
- [ ] Revisor 1 — nombre: ______ fecha: ______
- [ ] Sin conflicto con la constitución
- **Estados:** Borrador (agente) → Aprobada (persona; habilita el código) → Implementada (tras el merge)
