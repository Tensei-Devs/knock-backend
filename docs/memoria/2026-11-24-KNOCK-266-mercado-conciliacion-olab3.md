# Mercado: conciliación de task.md (046, 047, 048, 045, 020) y prueba 429 de Mapa y Explorar

## Qué cambió
- `specs/046`, `047`, `048`, `045` y `020`: filas de `task.md` conciliadas con el código real. Pasan a hechas 046 T-00, T-16 y T-17; 047 T-00 y T-08; 048 T-00 y T-07; 045 T-11; 020 T-12. 045 T-00 queda parcial con la causa vigente (falta la auditoría real del panel).
- Nueva prueba de integración `Spec046MapaExplorarLimitesHttpTests` (CA_046_68): la petición 61 por minuto y por IP a `GET /api/v1/mapa/tecnicos` y `GET /api/v1/explorar` responde 429 con `Retry-After`, y cada ruta tiene su propio contador. Corre solo en el CI.
- - Nueva prueba `Spec046FotosReputacionHttpTests` (CA_046_25, 26, 48, 49 y 60: fotos de trabajos y reputación propia por HTTP); con ella `sdd_lint --trace 046` queda sin casos faltantes (antes 5).
- Sin cambios de código de producción ni de contrato: `export_openapi.py --service market --no-build` no produjo diferencias.

## Por qué
- Las filas estaban desactualizadas: los prerrequisitos (027, 028, 046, evento `CotizacionElegida`) y la documentación ya estaban en la rama. Faltaba la prueba HTTP del 429 de las dos rutas públicas nuevas.
- Revisión por par y revisión de seguridad por agente omitidas por decisión de José (2026-11-24); no se marcan como hechas.

## A evitar
- No marcar 413 para Mapa y Explorar: son GET sin cuerpo.
- No cerrar 045 T-00 hasta que exista la auditoría real del panel; mientras tanto alta y edición de oficios responden 503 por diseño (falla cerrado).
