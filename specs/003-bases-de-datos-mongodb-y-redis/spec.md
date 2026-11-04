# Spec 003 — Bases de datos: MongoDB y Redis

> **Estado:** Borrador · **Repositorio:** knock-backend · **Rector:** `constitution.md`

## 1. Objetivo
Definir qué bases de datos usa el backend, por qué, cómo se conecta cada servicio y cómo se organizan colecciones, índices y scripts, sin crear aún colecciones de negocio.

## 2. Alcance
**Dentro**
- MongoDB como base principal, con una base lógica por servicio.
- Redis como base de datos compartida (tokens cancelados, caché, contadores y mensajes entre copias).
- Patrón de repositorio y límites de uso.
- Convenciones de nombres, índices y scripts versionados en `db/`.
- Colecciones técnicas `outbox` y `tareas_programadas` en cada servicio.
- Respaldo del nivel gratuito y plan B.

**Fuera**
- Colecciones y modelos de negocio.
- Datos de producción.

## 3. Requisitos
- **R-01:** Cada servicio tiene su propia base lógica y ninguno lee la de otro.
- **R-02:** La cadena de conexión viene de configuración o de Key Vault, nunca del código.
- **R-03:** Las colecciones van en `snake_case` plural y los campos en `camelCase`; `_id` es `ObjectId`; las fechas, en UTC; el campo `version` da concurrencia optimista.
- **R-04:** Solo la capa `Data` usa MongoDB y Redis; los servicios usan repositorios.
- **R-05:** Todo campo por el que se filtra u ordena tiene un índice declarado en un script de `db/`.
- **R-06:** Los scripts de `db/` son idempotentes, se aplican en orden y son el script de la base de la entrega.
- **R-07:** Redis corre como contenedor propio de una réplica, con contraseña desde Key Vault y sin persistencia.
- **R-08:** Se programa una copia periódica con `mongodump` a Blob Storage y se prueba restaurarla.
- **R-09:** Los datos se alojan en la región West US de Azure (Estados Unidos), la misma de la spec 005; el aviso de privacidad lo declara y no se cambia de región sin una spec nueva.

## 4. Criterios de aceptación
- **CA-01:** Dada una instancia limpia, cuando se aplican los scripts de `db/`, entonces quedan creados los índices y reaplicarlos no falla.
- **CA-02:** Dada la prueba de arquitectura, cuando un servicio usa el controlador de MongoDB fuera de `Data`, entonces falla.
- **CA-03:** Dada una prueba de integración, cuando corre, entonces usa MongoDB real en contenedor.
- **CA-04:** Dado un respaldo con `mongodump`, cuando se restaura en una base vacía, entonces los datos coinciden.
- **CA-05:** Dado el script de despliegue del servicio de datos, cuando se revisa su configuración, entonces la región es West US.

## 5. Contrato
Define la forma de los datos: colecciones en `snake_case` plural, campos en `camelCase`, `_id` de tipo `ObjectId`, fechas en UTC, campo `version` para concurrencia optimista y las colecciones técnicas `outbox` y `tareas_programadas`.

## 6. Amenazas y controles
| Amenaza | Control |
|---|---|
| Inyección en consultas a MongoDB | Filtros construidos con el controlador tipado y validación previa; nunca concatenación de texto |
| Pérdida de datos por no tener respaldo en el nivel gratuito | Copia periódica con `mongodump` a Blob Storage y restauración probada |
| Exposición de la cadena de conexión | La cadena vive en Key Vault o en variables, nunca en el repositorio |
| Datos de una persona visibles para otra por una clave de caché mal armada | Las claves de caché incluyen el recurso y, si es personal, el identificador de la persona |

## 7. Plan de pruebas
| Tipo | ¿Aplica? | Motivo |
|---|---|---|
| Unitarias | Sí | Repositorios con dobles |
| Integración | Sí | MongoDB y Redis reales en contenedor |
| Base de datos | Sí | Índices, restricciones y scripts idempotentes |
| Arquitectura | Sí | Solo `Data` usa MongoDB |
| Seguridad negativa | Sí | Filtros con entradas maliciosas |
| Rendimiento | No | Se mide en la spec 010 |

## 8. Decisiones
- **MongoDB:** es un requerimiento usar una base NoSQL; el nivel gratuito de DocumentDB existe en Azure; un documento guarda el objeto completo y se lee con una sola consulta; el índice `2dsphere` resuelve búsquedas por ubicación; y permite transacciones para el patrón de bandeja de salida.
- **Una base lógica por servicio:** cada servicio es dueño de sus datos.
- **Plan B, MongoDB Atlas:** externo y gratuito, con límites estrechos; solo si DocumentDB no cumple tras la prueba técnica.
- **Redis como segunda base NoSQL (clave-valor):** es muy rápida y compartirla entre copias evita guardar estado en memoria. Riesgo aceptado: si se reinicia, pierde la lista de tokens cancelados.
- **Sin base de grafos:** las relaciones son listas simples por persona, no recorridos de varios saltos; una tercera base costaría sin aportar.

## 9. Riesgos y dudas abiertas
- Ninguna duda abierta por ahora; se agregan aquí las que surjan al revisar la spec.

## Aprobación
- [ ] Revisor 1 — nombre: ________ fecha: ________
- [ ] Sin conflicto con la constitución
- **Estados:** Borrador → Aprobada (una persona; habilita el código) → Implementada (tras el merge)
