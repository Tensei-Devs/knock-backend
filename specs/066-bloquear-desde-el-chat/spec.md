# Spec 066 — Bloquear a la otra parte desde una conversación, antes de elegir cotización

> **Estado:** Borrador · **Repositorio:** knock-backend · **Rector:** `constitution.md`
> **Jira:** KNOCK-266 (relacionada con KNOCK-96) · **Sprint:** 5 · **Responsable:** por asignar
> **Otra parte en:** knock-frontend: spec 047 §13 (lo deja pendiente) y spec 033 (bloqueos y reportes). La app necesita su propia enmienda o spec después de esta.
> **Servicios:** Identidad (`Safety`) y Mensajería (`Conversaciones`)
> **Fuentes:** specs 030 (R-05, R-06), 043 (R-16 a R-19, R-23, R-24) y 056 (autenticación entre servicios)
> **Skills:** `security-core`, `vertical-slice-api`, `testing-strategy-backend`

## 1. Objetivo
Que el cliente o el técnico puedan bloquear a la otra parte desde el chat aunque todavía no se haya elegido una cotización. La conversación no entrega el identificador de la otra persona (spec 030, R-06), así que la app no puede llamar a `POST /api/v1/usuarios/{id}/bloqueo`. Esta spec permite bloquear **por conversación**: el servidor averigua quién es la otra parte y la app no necesita su identificador.

## 2. Historia y alcance
**2.1 Historia.** Como participante de una conversación, quiero bloquear a la otra parte sin tener que elegirla antes, para dejar de recibir sus mensajes.

**2.2 Dentro**
- `POST /api/v1/me/bloqueos/conversaciones/{conversacionId}`: bloquea a la otra parte.
- `DELETE /api/v1/me/bloqueos/conversaciones/{conversacionId}`: deshace ese bloqueo.
- Una ruta interna de Mensajería que le dice a Identidad quién es la otra parte.
- El bloqueo resultante es el mismo de la spec 043: misma colección, mismos eventos y efectos.

**2.3 Fuera**
- Agregar identificadores de la otra persona a la respuesta de la conversación o de sus mensajes (la spec 030 no cambia).
- Reportar desde el chat antes de elegir (falta decidir qué recibe el panel): otra spec.
- Cambiar BLO-01, BLO-02 o los efectos del bloqueo. BLO-03 solo cambia según D-02.
- La pantalla de la app.

## 3. Requisitos
- **R-01:** Las dos rutas exigen JWT. La persona que bloquea es siempre la del `sub` (spec 043, R-24); no hay cuerpo y cualquier identificador enviado se ignora.
- **R-02:** Identidad pide la otra parte a Mensajería por `GET /api/v1/interno/conversaciones/{conversacionId}/contraparte`, con la credencial de servicio de Identidad (spec 056): un permiso nuevo y exclusivo, `conversacion_contraparte`, y el `sub` de quien pide como reclamo firmado `usuarioId`. La ruta no se publica en la puerta de entrada y no acepta el JWT de una persona.
- **R-03:** Mensajería responde `200 { contraparteUsuarioId }` solo si `usuarioId` es uno de los dos participantes guardados; si la conversación no existe o es ajena, 404. No consulta a Mercado ni devuelve otros datos.
- **R-04:** Identidad nunca entrega `contraparteUsuarioId` en la respuesta de estas rutas, en errores, en registros ni en trazas.
- **R-05:** `POST` crea el bloqueo con las reglas de BLO-01 y responde `201` sin cuerpo. Ya bloqueada: `409 already_blocked`. Se puede bloquear con la conversación abierta o cerrada.
- **R-06:** `DELETE` quita el bloqueo que quien llama tiene sobre la otra parte de esa conversación y responde `204`; sin bloqueo propio, `404 block_not_found`. No quita el bloqueo que la otra persona tenga.
- **R-07:** Conversación inexistente o ajena: `404 conversation_not_found`, igual en ambos casos. `conversacionId` mal formado: `400 invalid_id`.
- **R-08:** Si Mensajería no responde en 3 segundos, falta la configuración o rechaza la credencial, Identidad responde `503 messaging_unavailable` y no crea ni borra nada.
- **R-09:** El bloqueo escribe `BloqueoCreado` o `BloqueoEliminado` en la bandeja de salida en la misma transacción, como en la spec 043; de ahí salen el cierre de la conversación y las proyecciones. No se revela el bloqueo a la otra persona (R-17 de la 043).
- **R-10:** Después de deshacer, la conversación vuelve al estado que Mercado indique para ese par (spec 030, R-03); si ya no está permitida, sigue cerrada. Una prueba de integración comprueba que, tras deshacer y con Mercado dándola por abierta, se puede volver a escribir en 60 segundos como máximo (spec 030, R-09).
- **R-11:** Las dos rutas comparten el límite de la política `bloqueos` (30 por hora por persona, spec 043).
- **R-12:** Las respuestas llevan `Cache-Control: private, no-store` y los registros, solo estados, códigos y latencias.

## 4. Criterios de aceptación
- **CA-01:** Dado el cliente de una conversación sin cotización elegida, cuando llama `POST`, entonces responde 201, queda un solo bloqueo del cliente hacia el técnico y un solo `BloqueoCreado`.
- **CA-02:** Dado el técnico de esa conversación, cuando llama `POST`, entonces ocurre lo mismo en su dirección.
- **CA-03:** Dado un bloqueo ya creado, cuando se repite `POST`, entonces responde 409 `already_blocked` sin otro evento.
- **CA-04:** Dada una persona ajena o un identificador inexistente, cuando llama `POST` o `DELETE`, entonces responde 404 `conversation_not_found` con el mismo cuerpo y no cambia nada.
- **CA-05:** Dadas las respuestas, errores, registros y trazas de estas dos rutas, cuando se inspeccionan, entonces no contienen el identificador, nombre ni teléfono de la otra parte.
- **CA-06:** Dada la ruta interna, cuando se llama sin credencial, con el JWT de una persona, con otro emisor, sin el permiso `conversacion_contraparte` o por la puerta de entrada, entonces no entrega nada (401, 403 o 404 según el caso).
- **CA-07:** Dado un `usuarioId` firmado que no participa en la conversación, cuando Identidad pide la contraparte, entonces Mensajería responde 404.
- **CA-08:** Dada Mensajería caída o sin configuración, cuando se llama, entonces 503 `messaging_unavailable` y no cambia nada.
- **CA-09:** Dado un bloqueo propio creado por conversación, cuando se llama `DELETE`, entonces 204 y un solo `BloqueoEliminado`; repetido, 404 `block_not_found`; el bloqueo de la otra persona no cambia.
- **CA-10:** Dado el bloqueo ya entregado a Mensajería, cuando cualquiera intenta escribir, entonces no puede; y tras deshacerlo, si Mercado da la conversación por abierta, vuelve a poder en 60 segundos como máximo.
- **CA-11:** Dada una petición sin token, entonces 401; con identificador mal formado, 400 `invalid_id`; por encima de 30 por hora, 429 con `Retry-After`.
- **CA-12:** Dado el contrato publicado, cuando se lee `docs/openapi.v1.json`, entonces están las dos rutas públicas con sus respuestas y no está la ruta interna.

**4.1 Criterio → prueba prevista**

| Criterio | Prueba prevista |
|---|---|
| CA-01 a CA-03, CA-08 a CA-10 | `IntegrationTest/Safety/Spec066BloqueoPorConversacionTests` (MongoDB real) |
| CA-04 a CA-07, CA-11 | `SecurityTest/Safety/Spec066SeguridadTests` |
| CA-12 | `ContractTest/Spec066ContractTests` |

## 5. Contrato
| Ruta | Quién | Éxito | Errores |
|---|---|---|---|
| `POST /api/v1/me/bloqueos/conversaciones/{conversacionId}` | participante | `201` | `400`, `401`, `404`, `409 already_blocked`, `429`, `503` |
| `DELETE /api/v1/me/bloqueos/conversaciones/{conversacionId}` | participante | `204` | `400`, `401`, `404` (`conversation_not_found`, `block_not_found`), `429`, `503` |
| `GET /api/v1/interno/conversaciones/{conversacionId}/contraparte` (Mensajería, red interna) | Identidad | `200 { contraparteUsuarioId }` | `401`, `403`, `404` |

Las rutas públicas caen en `/api/v1/me/{**resto}`, que la puerta de entrada ya envía a Identidad con sesión. Se confirma con una prueba.

## 6. Modelo de datos
Sin colecciones ni índices nuevos. Con la opción 1 de D-02, el documento de `bloqueos` suma el texto opcional `etiqueta`.

## 7. Arquitectura y componentes
Identidad: dos acciones en `Safety` y un cliente HTTP hacia Mensajería, con el esquema de firma que Identidad ya usa en sus llamadas internas. Mensajería: un controlador interno de lectura. Ningún servicio lee la base de otro (spec 001, R-03).

## 8. Dependencias
Specs 030, 043, 054 y 056. En el despliegue, Mensajería tiene que admitir el permiso nuevo del emisor Identidad; no se prevé un secreto nuevo (confirmarlo al implementar).

## 9. Amenazas y controles
| Amenaza | Control | Caso |
|---|---|---|
| Obtener el identificador de la otra persona por el chat | No sale en la conversación ni en estas respuestas (ver D-02 sobre la lista) | CA-05 |
| Bloquear en nombre de otra persona | `sub` del token y participación comprobada | CA-04, CA-07 |
| Usar la ruta interna desde fuera o con otra credencial | Credencial de servicio con permiso exclusivo; sin ruta en la puerta de entrada | CA-06 |
| Averiguar si una conversación ajena existe | 404 uniforme | CA-04 |
| Quedar a medias por una caída | Falla cerrada | CA-08 |

## 10. Plan de pruebas
Integración con MongoDB real y los dos servicios, seguridad negativa y contrato.

## 11. Escenarios
**Positivo:** el cliente pregunta, recibe un mensaje molesto y bloquea antes de elegir. **Negativo:** una persona ajena prueba identificadores. **Error esperado:** Mensajería no responde (503). **Límite:** bloquear en una conversación ya cerrada.

## 12. Definición de terminado
CA-01 a CA-12 con prueba, `python scripts/sdd_lint.py --trace 066` en OK, OpenAPI regenerado, `docs/endpoints.md` y `docs/rate-limit-matrix.md` al día, enmienda propuesta a la spec 043 (la firma una persona), gates de `AGENTS.md §5` y memoria.

## 13. Decisiones pendientes de quien aprueba
- **D-01:** Cómo resolver el identificador. **A:** agregar `contraparte.usuarioId` a la conversación: menos trabajo, pero contradice la spec 030 (R-06 y CA-14) y entrega el identificador de cada parte a la otra. **B (propuesta):** bloquear por conversación, como dice esta spec.
- **D-02:** `GET /api/v1/me/bloqueos` (BLO-03) devuelve `usuarioId` y nombre público. Un bloqueo hecho desde el chat aparecería allí, así que el técnico podría ver el nombre público del cliente, que el chat no le muestra. **Opción 1 (propuesta):** guardar en el bloqueo la `etiqueta` visible en el chat («Cliente de {colonia}» o el nombre público del técnico) y mostrarla como `nombre` en los bloqueos creados por conversación; `usuarioId` se sigue devolviendo porque «Deshacer» desde Ajustes lo usa. La ruta interna tiene que devolver también esa etiqueta. **Opción 2:** dejar la lista como está y aceptarlo. **Opción 3:** ofrecer el bloqueo por conversación solo al cliente.
- **D-03:** ¿Se permite bloquear desde una conversación cerrada? *Propuesta: sí.*

## 14. Decisiones tomadas sin consulta
1. **Identidad pregunta a Mensajería** y no al revés: los bloqueos son de Identidad y ya existen llamadas internas de Identidad a Mensajería.
2. **409 al repetir**, igual que BLO-01. Alternativa: 201 idempotente.

## 15. Riesgos y dudas abiertas
- El efecto en el chat llega por evento: puede tardar unos segundos.
- Con la opción 1 de D-02, antes de firmar se ajustan R-03, R-04, §5 y los criterios, y se agrega el criterio que prueba la etiqueta en la lista. Con la opción 3, cambian CA-02 y R-05.

## Aprobación
- [ ] Revisor 1 — nombre: ________ fecha: ________
- [ ] Sin conflicto con la constitución
- **Estados:** Borrador → Aprobada (una persona; habilita el código) → Implementada (tras el merge)
