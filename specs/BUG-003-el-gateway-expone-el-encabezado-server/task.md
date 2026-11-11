# BUG-003 — tareas

> Servicio afectado: Gateway y Mensajería. Spec aprobada el 2026-11-10. El código y las pruebas de T1 están en el PR #78 (sin fusionar); T2 y la verificación en vivo quedan pendientes.

## T1 — Apagar el encabezado
- **Estado:** implementada en el PR #78 (KNOCK-240), todavía sin fusionar. En el gateway y en Mensajería.
- **Verificar:** TC-BUG-003-01 · 02 · 03

## T2 — Documentación
- **Estado:** documento y modelo de amenazas en el PR #78; la verificación en vivo (`curl -D` contra el gateway desplegado) sigue pendiente y deja H-02 abierto hasta entonces.
- **Verificar:** TC-BUG-003-04
