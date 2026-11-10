# BUG-004 — tareas

> Servicio afectado: Messaging. Borrador: nada implementado.

## T1 — Autenticación JWT y sesión cancelada
- **Estado:** pendiente de aprobación de la spec. Esquema, `[Authorize]` por defecto, comprobación en Redis y arranque con falla rápida.
- **Verificar:** TC-BUG-004-01 · 02 · 05 · 06

## T2 — Encabezados de seguridad y sin Server
- **Estado:** pendiente de aprobación de la spec. Middleware de encabezados y `AddServerHeader = false`.
- **Verificar:** TC-BUG-004-03

## T3 — Límite de peticiones
- **Estado:** pendiente de aprobación de la spec. Limitador global por IP con `Retry-After`.
- **Verificar:** TC-BUG-004-04

## T4 — Documentación
- **Estado:** pendiente de aprobación de la spec. Matriz de límites, encabezados y modelo de amenazas.
- **Verificar:** TC-BUG-004-07
