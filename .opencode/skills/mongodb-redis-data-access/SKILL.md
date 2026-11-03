---
name: mongodb-redis-data-access
description: Convenciones de acceso a datos con MongoDB y Redis: repositorios, índices, nombres, caché y concurrencia; úsala al especificar colecciones o scripts de `db/`.
---

# mongodb-redis-data-access

- Una base lógica por servicio; colecciones en `snake_case` plural y campos en `camelCase`.
- `_id` es `ObjectId`, fechas en UTC y campo `version` para concurrencia optimista.
- Solo la capa `Data` toca MongoDB y Redis; los servicios usan repositorios.
- Todo campo por el que se filtra u ordena lleva un índice declarado en un script de `db/`.
- Cada servicio tiene sus colecciones `outbox` y `tareas_programadas`.
