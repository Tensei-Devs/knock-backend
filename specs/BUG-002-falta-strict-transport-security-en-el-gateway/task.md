# BUG-002 — tareas

> Servicio afectado: Gateway. Spec aprobada el 2026-11-10; nada implementado todavía.

## T1 — Reproducir y diagnosticar
- **Estado:** por hacer (spec aprobada). Prueba que falla hoy con el host de pruebas y `X-Forwarded-Proto`.
- **Verificar:** TC-BUG-002-01 · 02

## T2 — Corregir y probar
- **Estado:** por hacer (spec aprobada). Encabezado en `SecurityHeadersMiddleware`, opciones y Development.
- **Verificar:** TC-BUG-002-03 · 04

## T3 — Documentar y verificar en vivo
- **Estado:** por hacer (spec aprobada). `docs/security-headers.md`, modelo de amenazas y `curl -D` tras el despliegue.
- **Verificar:** TC-BUG-002-05
- **Dependencia:** el humo de BUG-001 (spec 004 del CI, job `dast`) solo podrá comprobar HSTS cuando BUG-002 esté desplegado; contra `http://localhost` el encabezado no sale a propósito (TC-BUG-002-04).
