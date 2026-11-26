# Tareas 019 — MCP del proyecto (backend)

> **Estado:** T-01, T-02 y T-06 hechas (conciliado el 2026-11-23, con evidencia por fila); T-03, T-04 y T-05 siguen pendientes de verificar.

| Tarea | Qué se hace | Cubre | Estado |
|---|---|---|---|
| T-01 | Escribir `docs/mcp.md` con la lista de servidores aprobados (Jira con Atlassian, GitHub con `gh`, MongoDB opcional y local) | R-02, R-03, R-05 · CA-04 | hecha: `docs/mcp.md` |
| T-02 | Agregar la configuración de MCP de cada agente, sin secretos | R-01, R-04 · CA-01 | hecha: `.mcp.json` en la raíz |
| T-03 | Dejar las herramientas de escritura fuera de lo permitido por defecto | R-06, R-07 · CA-03 | pendiente |
| T-04 | Probar la consulta a Jira con y sin sesión | R-01 · CA-02, CA-05 | pendiente |
| T-05 | Correr la búsqueda de secretos sobre la configuración | R-04 · CA-01 | pendiente |
| T-06 | Registrar la decisión en `Memoria.md` | — | hecha: entradas «Spec 019: MCP del proyecto» en `Memoria.md` (2026-11-04 y 2026-11-05, KNOCK-214) |

## Definición de terminado
- Los criterios de aceptación de `spec.md` están cubiertos por pruebas o comandos en verde.
- El CI está en verde, un par revisó el cambio y `Memoria.md` está al día.
