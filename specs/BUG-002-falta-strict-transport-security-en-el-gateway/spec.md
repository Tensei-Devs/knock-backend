# BUG-002 — Falta el encabezado Strict-Transport-Security en el gateway

> **Jira:** KNOCK-239 · **Épica:** EP-13 · **Módulo:** Gateway · **Responsable:** José Guillermo Rangel Hernández · **Sprint:** S4 · **Puntos:** 2 (propuesto) · **Capas:** Back
> **Repositorio:** knock-backend · **Otra parte en:** —
> **Fuentes:** docs/modelo-de-amenazas.md H-01 · docs/security-headers.md · docs/evidencia/KNOCK-78-resumen.md · specs/009
> **Skills:** security-core, foundation-backend
> **Estado:** Borrador

## 1. Objetivo
Ninguna respuesta del gateway desplegado incluye `Strict-Transport-Security`, aunque `Program.cs` llama a `UseHsts()` fuera de Development. Se comprobó con `curl -D` en `/health`, `/sitemap.xml` y `/api/v1/me/x`. Se diagnostica la causa y se corrige para que las respuestas HTTPS lo incluyan.

## 2. Historia y casos de prueba
**BUG-002** — Como persona que usa la app, quiero que el gateway pida HSTS en toda respuesta HTTPS, para que el navegador no acepte un primer acceso por HTTP.

| Caso | Criterio (Dado / Cuando / Entonces) | Verificación |
|---|---|---|
| TC-BUG-002-01 | Dado una petición que llega al gateway como HTTP interno con `X-Forwarded-Proto: https` de una red de confianza, cuando se responde cualquier ruta (`/health`, anónima, 401, 404, 429) → la respuesta lleva `Strict-Transport-Security: max-age=…; includeSubDomains` | SecurityTest/Gateway/HstsTests.ForwardedHttps_ResponseCarriesHsts_TC_BUG_002_01 |
| TC-BUG-002-02 | Dado el mismo caso sin `X-Forwarded-Proto` o desde una red que no es de confianza → el encabezado **también** se envía (es inofensivo por HTTP, los navegadores lo ignoran) y no se confía en el encabezado para nada más | SecurityTest/Gateway/HstsTests.UntrustedSource_StillCarriesHsts_TC_BUG_002_02 |
| TC-BUG-002-03 | Dado el gateway sin `Security:Hsts` → el encabezado sale con `max-age=31536000; includeSubDomains` (los valores por defecto documentados en `docs/security-headers.md`) y nunca con `preload`; con `Security:Hsts:MaxAge` configurado sale el valor configurado, y un valor ≤ 0 impide arrancar (igual que Identidad y Mercado) | UnitTest/Gateway/HstsOptionsTests.MaxAge_IsConfigurable_TC_BUG_002_03 |
| TC-BUG-002-04 | Dado el entorno Development → no se envía (para no fijar HSTS en `localhost`) | SecurityTest/Gateway/HstsTests.Development_DoesNotSendHsts_TC_BUG_002_04 |
| TC-BUG-002-05 | Dado el gateway desplegado → `curl -D` en `/health`, `/sitemap.xml` y `/api/v1/me/x` muestra el encabezado, y el modelo de amenazas marca H-01 como cerrado | (comando) curl -D contra el gateway tras el despliegue · docs/modelo-de-amenazas.md |

## 3. Alcance / No-alcance
- **In (este repositorio):**
- **Diagnóstico** (antes de escribir el arreglo): reproducir con el host de pruebas por qué falta. Hipótesis: `UseHsts()` solo emite el encabezado cuando `Request.IsHttps` es verdadero y no es `localhost`; detrás del ingreso la petición llega como HTTP y `ForwardedHeaders` solo aplica `X-Forwarded-Proto` si el origen está en `ForwardedHeaders:KnownNetworks` (`deploy/apps/main.tf` lo fija con `var.container_apps_subnet_cidr`); si la IP del proxy del ingreso queda fuera de esa red, el esquema sigue siendo `http` y el encabezado no sale.
- **Corrección propuesta:** fijar `Strict-Transport-Security` en `SecurityHeadersMiddleware` (donde ya se fijan los demás encabezados en `OnStarting`, así vale en 401, 429 y 503) fuera de Development, sin depender del esquema que vea Kestrel; quitar `UseHsts()` para no duplicar.
- `Security:Hsts` en el gateway con los valores por defecto documentados y validación al arrancar.
- Pruebas de seguridad y actualización de `docs/security-headers.md` y del modelo de amenazas.
- **Out:**
- Cambiar la red de confianza (`ForwardedHeaders__KnownNetworks__0`) o el Terraform; si el diagnóstico muestra que está mal, se reporta al líder.
- HSTS en Identidad y Mercado (no tienen salida pública).
- Precarga en la lista HSTS preload del navegador.

## 4. Contratos API y eventos
- Ninguno (solo un encabezado de respuesta).

## 5. Modelo de datos
- Ninguno.

## 6. Seguridad, privacidad y amenazas
| Amenaza | Control | Caso que lo prueba |
|---|---|---|
| Alteración / intermediario: primer acceso por HTTP sin HSTS | Encabezado en toda respuesta del gateway, con `max-age` y `includeSubDomains` | TC-BUG-002-01, 02 |
| Confianza indebida en `X-Forwarded-Proto` suplantado | El encabezado de respuesta no depende de ese valor; la red de confianza sigue limitada al ingreso | TC-BUG-002-02 |
| Bloqueo accidental de `localhost` en desarrollo | No se envía en Development | TC-BUG-002-04 |

## 7. Pantallas
—

## 8. Pruebas, gates y límites
**Plan de pruebas** (Sí/No con motivo; meta de mutación):

| Tipo | ¿Aplica? | Motivo / herramienta |
|---|---|---|
| Unitarias | Sí | Opciones de `max-age` y valores por defecto |
| Integración / Seguridad | Sí | Host real del gateway (rutas anónimas, 401, 404, 429) |
| Contrato / Base de datos | No | Sin endpoints ni colecciones |
| Verificación en vivo | Sí (comando) | `curl -D` contra el gateway desplegado, a cargo de quien opera Azure |

**Gates:** ver `AGENTS.md §5` (Definición de terminado).
**Límites y dudas abiertas** (las resuelve una persona antes de aprobar; no se inventan):
1. **Causa real en Azure.** La hipótesis anterior no se puede comprobar sin acceso al entorno. Propuesta: si el diagnóstico local no basta, el líder confirma con `curl -D` y los registros del ingreso cuál IP ve el gateway.
2. **Configuración de HSTS en el gateway.** Hoy solo Identidad y Mercado tienen `Security:Hsts` (`SecuritySettings`); el gateway no. Propuesta: agregar la misma sección al gateway con los valores por defecto de `docs/security-headers.md` (365 días, `includeSubDomains`, sin `preload`) y la validación de que sea mayor que cero.
3. **Despliegue.** El cierre del bug exige un despliegue del gateway; lo hace el líder.

## Aprobación
- [ ] Revisor 1 — nombre: ________ fecha: ________
- [ ] Sin conflicto con la constitución
- **Estados:** Borrador (agente) → Aprobada (persona; habilita el código) → Firmada (tras el merge)
