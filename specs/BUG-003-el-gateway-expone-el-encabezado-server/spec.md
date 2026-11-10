# BUG-003 — El gateway y Mensajería exponen el encabezado Server: Kestrel

> **Jira:** KNOCK-240 · **Épica:** EP-13 · **Módulo:** Gateway y Mensajería · **Responsable:** José Guillermo Rangel Hernández · **Sprint:** S4 · **Puntos:** 1 (propuesto) · **Capas:** Back
> **Repositorio:** knock-backend · **Otra parte en:** —
> **Fuentes:** docs/modelo-de-amenazas.md H-02 · docs/security-headers.md
> **Skills:** security-core, foundation-backend
> **Estado:** Aprobada

## 1. Objetivo
Las respuestas del gateway desplegado incluyen `server: Kestrel`, y `docs/security-headers.md` dice que ese encabezado no sale. Identidad y Mercado fijan `AddServerHeader = false` (`Hosting/SecurityExtensions.cs`); el gateway y Mensajería no.

## 2. Historia y casos de prueba
**BUG-003** — Como equipo de seguridad, queremos que ningún servicio revele la tecnología del servidor, para no facilitar ataques dirigidos.

| Caso | Criterio (Dado / Cuando / Entonces) | Verificación |
|---|---|---|
| TC-BUG-003-01 | Dado el gateway, cuando responde cualquier ruta (`/health`, anónima, 401, 404, 429, proxy) → no incluye el encabezado `Server` | SecurityTest/Gateway/ServerHeaderTests.Gateway_NeverSendsServer_TC_BUG_003_01 |
| TC-BUG-003-02 | Dado Mensajería, cuando responde `/health` o una ruta inexistente → no incluye el encabezado `Server` | SecurityTest/Messaging/ServerHeaderTests.Messaging_NeverSendsServer_TC_BUG_003_02 |
| TC-BUG-003-03 | Dado el gateway → tampoco salen `X-Powered-By` ni `X-AspNet-Version` | SecurityTest/Gateway/ServerHeaderTests.Gateway_NeverSendsOtherLeakyHeaders_TC_BUG_003_03 |
| TC-BUG-003-04 | Dado `docs/security-headers.md` → coincide con lo desplegado (el encabezado ausente en los cuatro servicios) y el modelo de amenazas marca H-02 como cerrado | (comando) curl -D tras el despliegue · docs |

## 3. Alcance / No-alcance
- **In (este repositorio):**
- `builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false)` en el gateway y en Mensajería, con el mismo patrón que Identidad y Mercado.
- Pruebas de seguridad en el host real de cada servicio.
- Documentación y modelo de amenazas.
- **Out:**
- Cambios en Identidad y Mercado; el resto de los encabezados de seguridad de Mensajería (BUG-004).

## 4. Contratos API y eventos
- Ninguno.

## 5. Modelo de datos
- Ninguno.

## 6. Seguridad, privacidad y amenazas
| Amenaza | Control | Caso que lo prueba |
|---|---|---|
| Fuga de información: versión y tecnología del servidor | `AddServerHeader = false` en los cuatro servicios; el gateway ya quita `Server` en `OnStarting` pero Kestrel lo agrega después | TC-BUG-003-01, 02, 03 |

## 7. Pantallas
—

## 8. Pruebas, gates y límites
**Plan de pruebas** (Sí/No con motivo; meta de mutación):

| Tipo | ¿Aplica? | Motivo / herramienta |
|---|---|---|
| Unitarias | No | Una línea de configuración |
| Integración / Seguridad | Sí | Host real de gateway y de Mensajería |
| Contrato / Base de datos | No | — |
| Verificación en vivo | Sí (comando) | `curl -D` tras el despliegue |

**Gates:** ver `AGENTS.md §5` (Definición de terminado).
**Límites y dudas abiertas** (las resuelve una persona antes de aprobar; no se inventan):
1. **Pruebas de Mensajería sin fábrica.** Mensajería no tiene fábrica de pruebas de seguridad; se crea una mínima (`SecurityMessagingFactory`) que también servirá a BUG-004 y la spec 026 (KNOCK-62). Si BUG-004 se hace primero, se reutiliza.

## Aprobación
- [x] Revisor 1 — nombre: José Julián Hernández Hernández fecha: 2026-11-10
- [x] Sin conflicto con la constitución
- **Estados:** Borrador (agente) → Aprobada (persona; habilita el código) → Firmada (tras el merge)
