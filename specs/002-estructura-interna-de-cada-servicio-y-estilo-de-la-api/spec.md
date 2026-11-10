# Spec 002 — Estructura interna de cada servicio y estilo de la API

> **Estado:** Aprobada · **Repositorio:** knock-backend · **Rector:** `constitution.md`

## 1. Objetivo
Definir cómo está organizado cada servicio por dentro y qué estilo de API expone, para que todos se construyan igual.

## 2. Alcance
**Dentro**
- Capas `Controllers`, `Services`, `Domain` y `Data` y su regla de dependencia.
- Módulos con fronteras y rebanadas verticales.
- Estilo REST con OpenAPI, versionado `/api/v1`, errores en ProblemDetails y paginación por cursor.
- Rutas de salud `/health` y `/health/ready`.

**Fuera**
- Funciones de negocio concretas.
- Acceso a datos (spec 003).

## 3. Requisitos
- **R-01:** Cada servicio se organiza en `Modules/<Modulo>/{Controllers,Dtos,Validators,Services,Domain,Data,Events}` con `Common/`, `Hosting/` y `Middleware/`.
- **R-02:** Los controllers validan, delegan y responden; no contienen lógica de negocio ni manejo de errores propio.
- **R-03:** Los errores los atrapa una pieza común y se devuelven como ProblemDetails con `type`, `title`, `status` y `traceId`.
- **R-04:** Toda ruta pública empieza por `/api/v1`.
- **R-05:** Los listados se paginan por cursor, 20 por página y máximo 50.
- **R-06:** `/health` dice si el proceso vive y `/health/ready` si puede hablar con MongoDB y Redis.
- **R-07:** El contrato OpenAPI se genera en compilación y se publica en `docs/openapi.v1.json`.
- **R-08:** Todo error lleva en el ProblemDetails un campo `code` estable, en inglés y en `snake_case` (por ejemplo `account_conflict`, `colonia_not_found`, `moderation_unavailable`). El `code` no incluye datos de la persona ni del recurso; el `title` y el `detail` no son parte del contrato.
- **R-09:** **422** significa que la petición se entendió pero sus datos no son válidos: un campo faltante o con tipo, longitud, rango o formato incorrectos, o una regla de contenido o de negocio que no se cumple (una colonia que no existe, una imagen que no se puede procesar). Lleva `errors` por campo cuando aplica. Es el código de toda validación de campos, la haga el filtro de validación o el controller.
- **R-10:** **400** significa que la petición no se puede interpretar: cuerpo ilegible (JSON mal formado o tipo de contenido incorrecto), identificador de ruta mal formado (por ejemplo un `ObjectId` inválido) o cursor mal formado.
- **R-11:** Los demás estados siguen su significado estándar: 401 sin sesión válida, 403 sin permiso, 404 recurso inexistente o ajeno (sin distinguirlos), 409 conflicto de estado, 429 con `Retry-After` y 503 con `Retry-After` cuando una dependencia no responde.

## 4. Criterios de aceptación
- **CA-01:** Dada una ruta inexistente, cuando se pide, entonces responde 404 en formato ProblemDetails.
- **CA-02:** Dado un controller con acceso directo a MongoDB, cuando corre la prueba de arquitectura, entonces falla.
- **CA-03:** Dado `/health/ready` con MongoDB caído, cuando se pide, entonces responde 503.
- **CA-04:** Dado un cambio en un endpoint, cuando se regenera, entonces `docs/openapi.v1.json` cambia.
- **CA-05:** Dado un cuerpo con un campo faltante o con longitud o formato incorrectos, cuando se pide cualquier endpoint, entonces responde 422 con `errors` por campo y no cambia nada.
- **CA-06:** Dado un cuerpo ilegible o un identificador de ruta o un cursor mal formados, cuando se pide, entonces responde 400 y no cambia nada.
- **CA-07:** Dados los `code` de error de todos los servicios, cuando se revisan, entonces están en inglés y en `snake_case` y ninguno se repite con otro significado.

## 5. Contrato
Fija el formato común de la API: rutas bajo `/api/v1`, errores en ProblemDetails (`type`, `title`, `status`, `code`, `traceId`) con la convención de R-08 a R-11, paginación por cursor (`cursor`, `limit`, `nextCursor`) y el OpenAPI 3.1 publicado en `docs/openapi.v1.json`.

## 6. Amenazas y controles
| Amenaza | Control |
|---|---|
| Un error revela detalles internos (trazas, consultas) | El manejo común de excepciones devuelve un ProblemDetails genérico con `traceId` y registra el detalle solo en el servidor |
| Un controller con lógica o acceso a datos salta las validaciones | Prueba de arquitectura de capas y validación con FluentValidation en la frontera |
| Un listado sin límite se usa para extraer datos o tirar el servicio | Paginación por cursor con máximo de 50 por página |

## 7. Plan de pruebas
| Tipo | ¿Aplica? | Motivo |
|---|---|---|
| Unitarias | Sí | Manejo de excepciones y validadores |
| Integración | Sí | Rutas reales con el servidor de pruebas |
| Arquitectura | Sí | Capas y fronteras de módulo |
| Seguridad negativa | Sí | Entradas inválidas y rutas inexistentes |
| Contrato | Sí | El OpenAPI regenerado no rompe al anterior |
| Humo / extremo a extremo | No | Se cubre en la spec 001 |

## 8. Decisiones
- **REST y no GraphQL ni gRPC:** hay un solo tipo de cliente (app y panel); REST es más fácil de probar, de cachear con HTTP y de documentar, y OpenAPI permite generar el cliente.
- **Capas y rebanadas verticales:** cada función atraviesa las cuatro capas con sus pruebas en lugar de construir capa por capa.
- **ProblemDetails en toda la API:** un solo formato de error para clientes y pruebas.
- **422 para toda validación de campos y 400 solo para lo que no se puede interpretar (decisión del 2026-11-10):** es lo que ya hace el filtro de validación de Identity y Market y lo que dice la spec 015; el 400 queda para el cuerpo ilegible y los identificadores o cursores mal formados (specs 016 y 018). `PUT /me` valida a mano y hoy responde 400 en lugar de 422: se corrige en una tarea de código aparte.
- **Códigos en inglés y `snake_case`:** la app maneja un solo nombre por error y el código ya implementado casi siempre lo cumple.

## 9. Riesgos y dudas abiertas
- `PUT /me` responde 400 a campos inválidos y debe pasar a 422 (spec 017, CA-06): hace falta un cambio de código y de pruebas, y regenerar `docs/openapi.v1.json`.
- La constante `eliminacion_programada` de Identity convive con `deletion_scheduled` para el mismo caso: hay que unificarla en `deletion_scheduled`.
- Las specs 020 a 037 (borradores) usan códigos en español; deben renombrarse a inglés antes de aprobarse.
- Ninguna otra duda abierta por ahora; se agregan aquí las que surjan al revisar la spec.

## Aprobación
- [x] Revisor 1 — nombre: José Julián Hernández Hernández fecha: 2026-11-04
- [x] Sin conflicto con la constitución
- **Estados:** Borrador → Aprobada (una persona; habilita el código) → Implementada (tras el merge)
