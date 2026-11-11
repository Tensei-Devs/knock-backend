# BUG-004 — tareas

> Servicio afectado: Messaging. Spec aprobada el 2026-11-10; nada implementado todavía.

## T1 — Autenticación JWT y sesión cancelada
- **Estado:** por hacer (spec aprobada). Esquema, `[Authorize]` por defecto, comprobación en Redis y arranque con falla rápida.
- **Verificar:** TC-BUG-004-01 · 02 · 05 · 06

## T2 — Encabezados de seguridad y sin Server
- **Estado:** por hacer (spec aprobada). Middleware de encabezados y `AddServerHeader = false`.
- **Verificar:** TC-BUG-004-03

## T3 — Límite de peticiones
- **Estado:** por hacer (spec aprobada). Limitador global por IP con `Retry-After`.
- **Verificar:** TC-BUG-004-04

## T4 — Documentación
- **Estado:** por hacer (spec aprobada). Matriz de límites, encabezados y modelo de amenazas.
- **Verificar:** TC-BUG-004-07
