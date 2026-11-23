# Spec 064 — El técnico no se presentó

> **Estado:** Aprobada · **Repositorio:** knock-backend · **Rector:** `constitution.md`
> **Jira:** KNOCK-266 (Seguimiento de las specs 021 y 050) · **Responsable:** por asignar
> **Fuentes:** spec 021 (§ fuera de alcance: «Reprogramar, cancelar y "no se presentó" (HU-034, HU-041)»; estado `no_presentado` ya existe), spec 050 (R-13 y R-27: «cliente ausente», la ruta simétrica que sí existe) y la app (acción «El técnico no llegó»).
> **Servicios:** Mercado (`Knock.Market.Api`, módulo `Requests`; gateway: `/api/v1/visitas/{**resto}` ya enruta) · **Colecciones:** `visitas`, buzón de eventos, sin colecciones nuevas
> **Skills:** `security-core`, `vertical-slice-api`, `events-outbox`

## 1. Objetivo
Que el cliente pueda reportar que el técnico no llegó a la cita. Hoy solo el técnico puede marcar `no_presentado` (cliente ausente, spec 050); el cliente que espera en vano no tiene ruta y la visita queda `confirmada` hasta vencer.

## 2. Historia y alcance
**2.1 Historia.** Como cliente, quiero avisar que el técnico no llegó, para cerrar la cita y que Knock lo sepa.

**2.2 Dentro**
- `POST /api/v1/visitas/{id}/tecnico-ausente`: el cliente dueño marca la visita `no_presentado` con `quienFalto = tecnico`.
- Evento `TecnicoNoSePresento` en el buzón (solo identificadores) y aviso fijo al técnico.

**2.3 Fuera**
- Penalización automática, suspensión o cambio de reputación del técnico (ver D-01, decisión de José).
- Reabrir una cotización o reasignar la solicitud.

## 3. Requisitos
- **R-01:** `POST /api/v1/visitas/{id}/tecnico-ausente` exige JWT y que la persona sea el **cliente dueño** de la visita; cualquier otra recibe 404 uniforme (inexistente o ajena).
- **R-02:** Solo dentro de la ventana de la cita igual que «cliente ausente» (spec 050 R-27): desde 15 minutos después de la hora de la cita y hasta 2 horas después; la visita debe estar `confirmada` (sin llegada del técnico). Fuera de la ventana o con la visita ya `en_curso`, `terminada`, `cancelada` o `no_presentado`: 409 `outside_arrival_window`.
- **R-03:** Cambio atómico y condicionado: `estado = no_presentado`, `quienFalto = tecnico`, `tardia` sin cambio, `actualizadoEn`; escribe el evento `TecnicoNoSePresento` (`visitaId`, `solicitudId`, `clienteId`, `tecnicoUsuarioId`) en el buzón en la misma transacción. Repetir la llamada con la visita ya `no_presentado` por el técnico responde 200 idempotente sin duplicar el evento.
- **R-04:** Responde 200 `{ visitaId, estado, quienFalto }` con `quienFalto = tecnico`.
- **R-05:** El código de la visita deja de ser válido (la visita deja de ser `confirmada`), así que `POST /visitas/{id}/llegada` responde su 409 habitual.
- **R-06:** Cuerpo vacío; cualquier cuerpo con campos se ignora. `id` inválido: 422 `errors.id`; cuerpo ilegible: 400.
- **R-07:** Límite: la política `visitas-cambios` (20 por minuto) de las demás acciones de visita.
- **R-08:** El aviso al técnico usa texto fijo (spec 037/054), sin nombre ni datos del cliente.
- **R-09:** OpenAPI declara 200, 401, 404, 409 `outside_arrival_window`, 422 y 429 con ProblemDetails y `code`.

## 4. Criterios de aceptación
- **CA-01:** Dada una visita `confirmada` y pasados 20 minutos de la hora de la cita, cuando el cliente dueño llama la ruta, entonces responde 200 con `estado = no_presentado` y `quienFalto = tecnico`.
- **CA-02:** Dada la visita anterior, cuando se inspecciona el buzón, entonces hay un solo `TecnicoNoSePresento` con solo identificadores.
- **CA-03:** Dado un llamado antes de los 15 minutos, después de las 2 horas o con la visita en otro estado, cuando se llama, entonces 409 `outside_arrival_window` y no cambia nada.
- **CA-04:** Dado el técnico de la visita o una persona ajena, cuando llama la ruta, entonces 404 uniforme y no cambia nada.
- **CA-05:** Dada la visita ya marcada `no_presentado` por el técnico ausente, cuando el cliente repite, entonces 200 sin segundo evento.
- **CA-06:** Dado un `id` mal formado, entonces 422 `errors.id` sin repetir el valor; sin token, 401.
- **CA-07:** Dada la visita marcada, cuando el técnico intenta `POST /visitas/{id}/llegada`, entonces responde el 409 de siempre y no cambia el estado.
- **CA-08:** Dadas 21 llamadas en un minuto, entonces la 21 responde 429 con `Retry-After`.
- **CA-09:** Dado el contrato publicado, cuando se lee `docs/openapi.v1.json`, entonces la ruta declara 200, 401, 404, 409, 422 y 429.

**4.1 Criterio → prueba prevista**

| Criterio | Prueba prevista |
|---|---|
| CA-01, CA-02, CA-03, CA-05, CA-07 | `IntegrationTest/Requests/Spec064TecnicoAusenteTests` (MongoDB real) |
| CA-04, CA-06, CA-08 | `SecurityTest/Requests/Spec064TecnicoAusenteSeguridadTests` |
| ventana de R-02 | `UnitTest/Requests/Spec064VentanaTests` |
| CA-09 | `ContractTest/Spec064ContractTests` |

## 5. Contrato
| Ruta | Quién | Entrada | Éxito | Errores |
|---|---|---|---|---|
| `POST /api/v1/visitas/{id}/tecnico-ausente` | cliente dueño de la visita | ruta `id`, sin cuerpo | `200 { visitaId, estado, quienFalto }` | `400`, `401`, `404`, `409 outside_arrival_window`, `422`, `429` |

## 6. Modelo de datos
Sin colecciones ni índices nuevos. `quienFalto` ya existe en `visitas` (spec 050 §6) y admite ahora `tecnico` además de `cliente`; evento nuevo `TecnicoNoSePresento` en el buzón.

## 7. Arquitectura y componentes
Una acción en el controlador de agenda/cierre de visita y un método simétrico a «cliente ausente» en `VisitScheduleService` y su almacén (actualización condicionada + buzón en la misma transacción). Sin gateway nuevo.

## 8. Dependencias
Specs 021, 022, 050 (estados, ventana y `quienFalto`), 037 y 054 (aviso y buzón), 009 y 011. Código ya en `sprint-05`: `VisitScheduleService`, `VisitScheduleStore`.

## 9. Amenazas y controles
| Amenaza | Control | Caso |
|---|---|---|
| El cliente marca ausente a un técnico que sí llegó | Solo con visita `confirmada` (sin llegada) y dentro de la ventana | CA-03, CA-07 |
| Una persona ajena cierra la visita | 404 uniforme por dueño | CA-04 |
| Repetir para inflar eventos | Idempotente, un solo evento | CA-05 |
| Abuso de la ruta | Límite `visitas-cambios` | CA-08 |

## 10. Plan de pruebas
Unitarias de la ventana, integración con MongoDB real, seguridad negativa y contrato.

## 11. Escenarios
**Positivo:** pasan 25 minutos, el cliente reporta y la cita se cierra. **Negativo:** el técnico llama la ruta (404). **Error esperado:** a los 10 minutos (409). **Límite:** exactamente a los 15 minutos (permitido).

## 12. Definición de terminado
CA-01 a CA-09 con prueba; `python scripts/sdd_lint.py --trace 064` en OK; OpenAPI regenerado; `docs/endpoints.md`, `docs/events.md` y `docs/rate-limit-matrix.md` al día; gates de `AGENTS.md §5`; memoria.

## 13. Decisiones
- **D-01 (DECIDIDO, José, 2026-11-23):** el hecho solo se registra y se avisa al técnico; no hay penalización automática ni cambio de reputación. Motivo: el reporte lo hace una sola parte y una sanción automática permitiría abusos. El evento `TecnicoNoSePresento` queda disponible para que una spec futura (053/059) lo cuente en el panel de seguridad.
- **D-02 (DECIDIDO, José, 2026-11-23):** misma ventana que «cliente ausente» (15 minutos a 2 horas), por simetría.

## 14. Decisiones tomadas sin consulta
1. **Ruta propia `tecnico-ausente`**, simétrica a `cliente-ausente`. Alternativa: un solo `POST /ausente` con cuerpo.
2. **Idempotente (200)** al repetir. Alternativa: 409.

## 15. Riesgos y dudas abiertas
- La spec 021 lo dejó fuera de alcance sin spec dueña: esta la crea; falta confirmar que no choca con HU-034/HU-041.

## Aprobación
- [x] Revisor 1 — nombre: José Julián Hernández Hernández fecha: 2026-11-23
- [x] Sin conflicto con la constitución
- **Estados:** Aprobada
