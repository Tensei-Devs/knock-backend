# Spec 063 — Lectura de la cotización final y de los ajustes de precio pendientes

> **Estado:** Aprobada · **Repositorio:** knock-backend · **Rector:** `constitution.md`
> **Jira:** KNOCK-266 (Seguimiento de la spec 049) · **Responsable:** Guillermo Rangel
> **Fuentes:** spec 049 (rutas de **escritura** COT-06, COT-07, AJU-01 y AJU-02, que ya existen), spec 050 (la visita y su cierre) y la app (pantallas de la visita y de «Pedir ajuste»), que hoy no puede leer lo que escribe.
> **Servicios:** Mercado (`Knock.Market.Api`, módulo `Requests`; gateway: `/api/v1/visitas/{**resto}` ya enruta) · **Colecciones:** `visitas` (campo `cotizacionFinal`) y `ajustes_precio`, sin cambios
> **Skills:** `security-core`, `vertical-slice-api`, `api-contract-openapi`

## 1. Objetivo
Que el cliente dueño y el técnico de una visita puedan **leer** la cotización final y los ajustes de precio pendientes de esa visita. La spec 049 solo define rutas que escriben: la app recibe el resultado de cada POST pero, al reabrir la visita o recibir el aviso, no tiene de dónde leer el precio propuesto, su descripción ni su vencimiento.

## 2. Historia y alcance
**2.1 Historia.** Como cliente, quiero ver el precio y la descripción que el técnico propone tras la visita, y los ajustes pendientes, para decidir con la información a la vista. Como técnico, quiero ver el estado de lo que envié.

**2.2 Dentro**
- `GET /api/v1/visitas/{id}/cotizacion-final`: la cotización final de la visita.
- `GET /api/v1/visitas/{id}/ajustes-pendientes`: los ajustes en estado `pendiente` y vigentes de las cotizaciones de esa visita.

**2.3 Fuera**
- Cambiar las rutas de escritura de la spec 049 o sus estados, vigencias y eventos.
- Historial de ajustes decididos o vencidos (solo pendientes).
- Las notificaciones (specs 044 y 054).

## 3. Requisitos
- **R-01:** `GET /api/v1/visitas/{id}/cotizacion-final` exige JWT. Lo puede leer el **cliente dueño** de la visita y el **técnico** de la visita (`tecnicoUsuarioId`); cualquier otra persona recibe 404 uniforme, igual que una visita inexistente o sin cotización final.
- **R-02:** Responde 200 `{ visitaId, precio, descripcion, estado, venceEn, decididaEn? }`. `estado` es el de la spec 049 (`enviada`, `aprobada`, `rechazada` o `vencida`). Una cotización `enviada` cuyo `venceEn` ya pasó se informa como `vencida` aunque el ejecutor aún no la haya marcado (se calcula al leer, sin escribir).
- **R-03:** `descripcion` es el texto que escribió el técnico: nunca se repite en registros (spec 011) y la respuesta no lleva identificadores de personas.
- **R-04:** `GET /api/v1/visitas/{id}/ajustes-pendientes` exige JWT y las mismas reglas de acceso de R-01. Responde 200 `{ items: [{ id, cotizacionId, montoNuevo, motivo, estado: "pendiente", venceEn }] }` con a lo más un ajuste por cotización (spec 049 R-15) y ordenado por `venceEn` ascendente; sin ajustes, `items` vacío (no 404).
- **R-05:** Un ajuste `pendiente` con `venceEn` pasado no se lista (se trata como vencido, sin escribir).
- **R-06:** Un `id` que no es un `ObjectId` responde 422 con `errors.id` sin repetir el valor.
- **R-07:** Ambas respuestas llevan `Cache-Control: private, no-store`, sin `ETag`, y cuentan con el límite general de lectura de visitas (el mismo de `GET /api/v1/visitas/{id}`).
- **R-08:** Las dos rutas declaran en OpenAPI 200, 401, 404, 422 y 429 con ProblemDetails, y `code` en los errores de negocio.

## 4. Criterios de aceptación
- **CA-01:** Dada una visita con cotización final `enviada`, cuando el cliente dueño la lee, entonces responde 200 con `precio`, `descripcion`, `estado = enviada` y `venceEn`.
- **CA-02:** Dada la misma visita, cuando la lee el técnico de la visita, entonces responde 200 con el mismo contenido.
- **CA-03:** Dada una persona ajena a la visita, una visita inexistente o una visita sin cotización final, cuando la lee, entonces responde 404 uniforme (mismo cuerpo en los tres casos).
- **CA-04:** Dada una cotización final `enviada` con `venceEn` pasado, cuando se lee, entonces `estado = vencida` y no se escribe nada.
- **CA-05:** Dada una cotización final ya decidida, cuando se lee, entonces `estado` es `aprobada` o `rechazada` y trae `decididaEn`.
- **CA-06:** Dada una visita con un ajuste pendiente vigente, cuando el cliente o el técnico lee `ajustes-pendientes`, entonces el ajuste aparece con `montoNuevo`, `motivo` y `venceEn`.
- **CA-07:** Dada una visita sin ajustes pendientes o con uno vencido, cuando se lee, entonces responde 200 con `items` vacío.
- **CA-08:** Dado un `id` mal formado, cuando se llama cualquiera de las dos rutas, entonces responde 422 con `errors.id` y no repite el valor.
- **CA-09:** Dada una petición sin token o con token vencido, cuando se llama, entonces responde 401.
- **CA-10:** Dadas las dos respuestas, cuando se inspeccionan los encabezados, entonces `Cache-Control` es `private, no-store` y no hay `ETag`.
- **CA-11:** Dado el contrato publicado, cuando se lee `docs/openapi.v1.json`, entonces las dos rutas declaran 200, 401, 404, 422 y 429.

**4.1 Criterio → prueba prevista**

| Criterio | Prueba prevista |
|---|---|
| CA-01 a CA-07 | `IntegrationTest/Requests/Spec063LecturaTests` (MongoDB real) |
| CA-08 a CA-10 | `SecurityTest/Requests/Spec063LecturaSeguridadTests` |
| CA-04, CA-05, R-02, R-05 | `UnitTest/Requests/Spec063VigenciaTests` (cálculo de estado al leer) |
| CA-11 | `ContractTest/Spec063ContractTests` |

## 5. Contrato
| Ruta | Quién | Entrada | Éxito | Errores |
|---|---|---|---|---|
| `GET /api/v1/visitas/{id}/cotizacion-final` | cliente dueño o técnico de la visita | ruta `id` | `200 { visitaId, precio, descripcion, estado, venceEn, decididaEn? }` | `401`, `404`, `422`, `429` |
| `GET /api/v1/visitas/{id}/ajustes-pendientes` | cliente dueño o técnico de la visita | ruta `id` | `200 { items: [{ id, cotizacionId, montoNuevo, motivo, estado, venceEn }] }` | `401`, `404`, `422`, `429` |

## 6. Modelo de datos
Sin colecciones, campos ni índices nuevos: se lee `visitas.cotizacionFinal` y `ajustes_precio` (índice por `visitaId` y `estado` de la spec 049, §6).

## 7. Arquitectura y componentes
Dos acciones de solo lectura en `FinalQuoteController` (o un controlador de lectura hermano) que delegan en `FinalQuoteService`; lecturas nuevas en `FinalQuoteStore` con filtros por `_id` de la visita. El acceso reutiliza la comprobación de dueño/técnico de `VisitService`. No toca el gateway: `/api/v1/visitas/{**resto}` ya enruta a Mercado.

## 8. Dependencias
Specs 049 (modelo y estados), 050 (visita), 021 y 022 (acceso a la visita), 009 y 011 (límites y registros). Código ya en `sprint-05`: `FinalQuoteStore`, `FinalQuoteService`, `VisitService`.

## 9. Amenazas y controles
| Amenaza | Control | Caso |
|---|---|---|
| Leer la cotización de una visita ajena | 404 uniforme para ajena, inexistente y sin cotización | CA-03 |
| Revelar que existe una visita ajena | Mismo cuerpo y mismo código en los tres casos de 404 | CA-03 |
| Datos de personas en la respuesta | Solo precio, texto, estados y fechas | CA-01 |
| Respuestas viejas en caché de un intermediario | `Cache-Control: private, no-store`, sin `ETag` | CA-10 |
| Texto del técnico en registros | Redacción (spec 011) | R-03 |

## 10. Plan de pruebas
Unitarias del cálculo de vigencia, integración con MongoDB real, seguridad negativa (acceso, 404 uniforme, 401, 422) y contrato (OpenAPI de Mercado).

## 11. Escenarios
**Positivo:** el cliente reabre la visita y lee el precio final y su descripción. **Negativo:** otra persona intenta leerla (404). **Error esperado:** `id` mal formado (422). **Límite:** la cotización venció hace un segundo (se lee `vencida`).

## 12. Definición de terminado
CA-01 a CA-11 con prueba; `python scripts/sdd_lint.py --trace 063` en OK; OpenAPI de Mercado regenerado con `scripts/export_openapi.py`; `docs/endpoints.md` al día; gates de `AGENTS.md §5` en verde; `Memoria.md` y `docs/memoria/` actualizados.

## 13. Decisiones
- **D-01 (DECIDIDO, José, 2026-11-23):** los ajustes se leen por visita (`/visitas/{id}/ajustes-pendientes`) y no por cotización, porque la app abre la visita y no la cotización.

## 14. Decisiones tomadas sin consulta
1. **Estado `vencida` calculado al leer**, sin esperar al ejecutor de vencimientos de la spec 054. Alternativa: devolver el estado guardado.
2. **Solo ajustes pendientes**, sin historial. Alternativa: lista completa con `estado` como filtro.
3. **Mismo límite que `GET /visitas/{id}`**, sin política nueva.

## 15. Riesgos y dudas abiertas
- Si la spec 049 se enmienda para mover la cotización final a otra colección, esta lectura cambia con ella.

## Aprobación
- [x] Revisor 1 — nombre: José Julián Hernández Hernández fecha: 2026-11-23
- [x] Sin conflicto con la constitución
- **Estados:** Aprobada

## Enmiendas
- **2026-11-23 (nota, sin cambio de aprobación ni decisiones):** el encabezado y la sección de implementación decían que el gateway ya enruta `/api/v1/visitas/{**resto}`. Es falso: las rutas de `visitas` se declaran una por una, con método y política, en `src/Knock.Gateway/appsettings.json`. Se agregan `mercado-visita-cotizacion-final-lectura` (`GET /api/v1/visitas/{id}/cotizacion-final`) y `mercado-visita-ajustes-pendientes` (`GET /api/v1/visitas/{id}/ajustes-pendientes`), con la política `default`, el CORS `knock` y las mismas reglas que sus vecinas.
