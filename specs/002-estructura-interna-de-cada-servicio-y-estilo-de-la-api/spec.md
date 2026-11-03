# Spec 002 — Estructura interna de cada servicio y estilo de la API

> **Estado:** Borrador · **Repositorio:** knock-backend · **Rector:** `constitution.md`

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

## 4. Criterios de aceptación
- **CA-01:** Dada una ruta inexistente, cuando se pide, entonces responde 404 en formato ProblemDetails.
- **CA-02:** Dado un controller con acceso directo a MongoDB, cuando corre la prueba de arquitectura, entonces falla.
- **CA-03:** Dado `/health/ready` con MongoDB caído, cuando se pide, entonces responde 503.
- **CA-04:** Dado un cambio en un endpoint, cuando se regenera, entonces `docs/openapi.v1.json` cambia.

## 5. Contrato
Fija el formato común de la API: rutas bajo `/api/v1`, errores en ProblemDetails (`type`, `title`, `status`, `traceId`), paginación por cursor (`cursor`, `limit`, `nextCursor`) y el OpenAPI 3.1 publicado en `docs/openapi.v1.json`.

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

## 9. Riesgos y dudas abiertas
- Ninguna duda abierta por ahora; se agregan aquí las que surjan al revisar la spec.

## Aprobación
- [ ] Revisor 1 — nombre: ________ fecha: ________
- [ ] Sin conflicto con la constitución
- **Estados:** Borrador → Aprobada (una persona; habilita el código) → Implementada (tras el merge)
