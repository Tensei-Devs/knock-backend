# BUG-005 — Market escribe registros de consola sin redacción

> **Jira:** KNOCK-255 · **Área:** Observabilidad y seguridad · **Servicio:** Mercado · **Capas:** Back, pruebas
> **Repositorio:** knock-backend · **Otra parte en:** —
> **Encontrado en:** spec 011, CA-02 y R-04 (observabilidad y registro).
> **Relacionadas:** KNOCK-251 (extraerá después el redactor y `ForwardedHeadersExtensions` a una biblioteca compartida); spec 011.
> **Fuentes:** `specs/011-observabilidad-y-registro/spec.md` · `src/Knock.Market.Api/Hosting/LoggingExtensions.cs` · `src/Knock.Market.Api/Hosting/TelemetryRedactor.cs` · equivalentes de Identity, Gateway y Messaging.
> **Skills:** spec-first-writing, story-workflow, security-core, testing-strategy-backend, reuse-before-create
> **Estado:** Aprobada

## 1. Objetivo
Evitar que datos sensibles aparezcan en los registros de consola de Market y recuperar el cumplimiento de CA-02 de la spec 011.

**Qué pasa hoy (diagnóstico confirmado por una persona el 2026-11-10).** Market escribe con `.WriteTo.Console(new RenderedCompactJsonFormatter())` sin `RedactingJsonFormatter` (`src/Knock.Market.Api/Hosting/LoggingExtensions.cs`), y su `TelemetryRedactor` no incluye esa clase. Identity, Gateway y Messaging sí envuelven el formateador. Además, Market reporta `service.name = knock-api` en lugar de `knock-market` (`ObservabilityExtensions.ServiceName` y `appsettings.json`, sección de observabilidad), lo que mezcla su telemetría con un nombre genérico. La spec 011 prohíbe datos personales en los registros y CA-02 exige que un celular no aparezca.

**Reproducción propuesta:** en un entorno local o de pruebas, configurar el registro real de consola de Market y emitir por Serilog un mensaje con un celular sintético con prefijo internacional; capturar la salida de consola y buscar el valor original. El diagnóstico predice que aparece sin redactar. Repetir con correo, token y coordenadas sintéticos, en mensaje y propiedades; no usar datos reales ni publicar valores sensibles. La reproducción aún no se ejecutó.

## 2. Defecto y casos de prueba
**BUG-005** — Como equipo, queremos que Market redacte la consola para no divulgar datos sensibles al registrar eventos.

| Caso | Criterio (Dado / Cuando / Entonces) | Verificación |
|---|---|---|
| TC-BUG-005-01 | Dado un celular sintético con prefijo internacional, cuando Market lo registra en consola, entonces el valor original no aparece. | `tests/UnitTest/Hosting/MarketConsoleRedactionTests.cs` → `Phone_IsRedacted_TC_BUG_005_01` |
| TC-BUG-005-02 | Dado un correo sintético, cuando Market lo registra en consola, entonces el valor original no aparece. | `tests/UnitTest/Hosting/MarketConsoleRedactionTests.cs` → `Email_IsRedacted_TC_BUG_005_02` |
| TC-BUG-005-03 | Dado un token sintético, cuando Market lo registra en consola, entonces el valor original no aparece. | `tests/UnitTest/Hosting/MarketConsoleRedactionTests.cs` → `Token_IsRedacted_TC_BUG_005_03` |
| TC-BUG-005-04 | Dadas coordenadas sintéticas, cuando Market las registra en consola, entonces sus valores originales no aparecen. | `tests/UnitTest/Hosting/MarketConsoleRedactionTests.cs` → `Coordinates_AreRedacted_TC_BUG_005_04` |
| TC-BUG-005-05 | Dado un registro estructurado de Market con valores sensibles y otros inocuos, cuando se escribe en consola, entonces la salida sigue siendo JSON válido y conserva los datos inocuos. | `tests/UnitTest/Hosting/MarketConsoleRedactionTests.cs` → `Output_RemainsValidJson_TC_BUG_005_05` |
| TC-BUG-005-06 | Dada la corrección, cuando se revisa la duplicación, entonces jscpd no reporta un clon nuevo, o el clon del redactor queda cubierto por una excepción temporal ligada a KNOCK-251. | (comando) `npx --yes jscpd@4 --config .jscpd.json .`; salida en el PR |
| TC-BUG-005-07 | Dada la configuración de Market, cuando arranca, entonces su telemetría reporta `service.name = knock-market` y no `knock-api`; el `Issuer` del JWT no cambia. | `tests/UnitTest/Hosting/MarketConsoleRedactionTests.cs` → `ServiceName_IsKnockMarket_TC_BUG_005_07` |

Las pruebas 01–04 deben ejercitar la configuración real de consola de Market, no solo llamar directamente al redactor, y cubrir mensaje y propiedades estructuradas según corresponda. Las rutas y nombres son propuestos y se ajustan a las pruebas existentes sin perder trazabilidad.

## 3. Alcance / No-alcance
- **In (este repositorio):** corregir la salida de consola de Market; reutilizar o extraer el redactor existente sin introducir clones; pruebas por caso, regresión de los servicios afectados y documentación de la decisión.
- **Out:** cambiar contratos HTTP o eventos, modificar producción, revisar otros sumideros o ampliar el catálogo de datos redactados más allá de los casos anteriores sin una decisión explícita.

## 4. Contratos API y eventos
No agrega ni cambia endpoints ni eventos. La consola conserva JSON estructurado válido; no se define un esquema nuevo.

## 5. Modelo de datos
No aplica: no se crean colecciones ni se modifican datos persistidos.

## 6. Seguridad, privacidad y amenazas
| Amenaza | Control | Caso que lo prueba |
|---|---|---|
| Divulgación de celular o correo en registros | Redacción en el camino real a consola | TC-BUG-005-01 y 02 |
| Exposición de credenciales y ubicación | Redacción de token y coordenadas sintéticos | TC-BUG-005-03 y 04 |
| La redacción rompe la observabilidad o JSON | Comprobar salida estructurada y datos inocuos | TC-BUG-005-05 |
| Un clon nuevo diverge y deja de proteger datos | Reutilizar o extraer la implementación; verificar jscpd | TC-BUG-005-06 |

## 7. Pantallas
—

## 8. Pruebas, gates y límites
| Tipo | ¿Aplica? | Motivo / herramienta |
|---|---|---|
| Unitarias | Sí | Capturar consola configurada por Market y comprobar TC-01 a 05. |
| Integración | Por decidir | Confirmar si la configuración real se verifica suficientemente sin arrancar el host. |
| Seguridad negativa | Sí | Datos sintéticos no aparecen en salida real de consola. |
| Regresión | Sí | Mantener comportamiento del redactor y servicios afectados. |
| Arquitectura | Sí | Si se extrae una biblioteca, comprobar fronteras entre servicios. |
| Contrato API / base de datos | No | No cambian. |
| Mutación | Informativa | Sin umbral nuevo; comprobar que las pruebas fallan si se omite la redacción. |
| CI y análisis | Sí | Build, formato, pruebas y jscpd sin clon nuevo. |

**Gates:** ver `checklists/gates.md`; la implementación espera la aprobación de Julián y la resolución de las decisiones relevantes.

**Decisiones (Julián, 2026-11-10):**
- Se corrige ya en Market con el mismo patrón que los otros tres servicios; no se espera a KNOCK-251, que después extraerá el redactor y `ForwardedHeadersExtensions` juntos a una biblioteca compartida. Si jscpd marca el clon del redactor, se cubre con una excepción temporal ligada a KNOCK-251 (la registra el Tech Lead).
- `service.name` de Market pasa a `knock-market` en la misma corrección, porque es la misma configuración de registro.

**Datos confirmados:** el `TelemetryRedactor` actual redacta JWT (`eyJ…`), `Bearer …`, pares clave=valor con llaves sensibles, `cookie`/`set-cookie`, celulares con `+` o `%2B`, CURP y pares de coordenadas. jscpd se corre con `npx --yes jscpd@4 --config .jscpd.json .`.

**Riesgos y dudas abiertas:**
- Un celular nacional sin `+` en texto libre no se redacta; se mantiene como riesgo aceptado de la spec 011 salvo decisión contraria.
- ¿Basta una prueba de la configuración real sin host o se requiere además integración con el host de Market?

## Aprobación
- [x] Revisor 1 — nombre: José Julián Hernández Hernández fecha: 2026-11-10
- [x] Sin conflicto con la constitución
- **Estados:** Borrador (agente) → Aprobada (persona; habilita el código) → Firmada (tras el merge)
