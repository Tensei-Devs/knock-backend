# Tareas 017 — Perfil de la persona

> **Estado:** T-01 a T-08 hechas (conciliado el 2026-11-23 contra `sprint-05`; cada fila cita su evidencia). Revisión de par pendiente.

| Tarea | Qué se hace | Cubre | Estado |
|---|---|---|---|
| T-01 | Agregar al repositorio de `usuarios` la lectura con caché y la actualización con invalidación | R-02, R-07 · CA-02, CA-05 | hecha: `Modules/Usuarios/Data/UsuariosStore.cs` y `ProfileCache.cs` (caché `perfil:{sub}`, invalidación al escribir). Pruebas: `ProfileServiceTests`, `MeEndpointTests` |
| T-02 | Crear el validador del `PUT` con las reglas de nombre, código postal y foto | R-04, R-05, R-08 · CA-06, CA-08 | hecha: `Services/ProfileRules.cs`. Pruebas: `ProfileRulesTests` |
| T-03 | Definir la interfaz de colonias en `Knock.Contracts` y usarla en la validación | R-06 · CA-07 | hecha: `Services/MarketColoniaCatalog.cs`. Pruebas: `MarketColoniaCatalogTests`, `ColoniaExistsControllerTests` |
| T-04 | Crear el servicio de perfil (búsqueda por `sub`, `perfilCompleto`) | R-01, R-09 · CA-01, CA-09, CA-11 | hecha: `Services/ProfileService.cs`. Pruebas: `ProfileServiceTests` |
| T-05 | Crear el controller con `ETag`, `Cache-Control` y 304 | R-03 · CA-03, CA-04 | hecha: controller en `Modules/Usuarios/Controllers/V1` (ETag, Cache-Control, 304). Pruebas: `MeEndpointTests` |
| T-06 | Filtrar el celular y el nombre en los registros | R-10 · CA-10 | hecha: registros sin celular ni nombre. Pruebas: `ProfileSecurityTests` |
| T-07 | Escribir las pruebas unitarias, de integración y de seguridad negativa | R-01 a R-12 · CA-01 a CA-13 | hecha: `ProfileRulesTests`, `ProfileServiceTests`, `MeEndpointTests`, `ProfileSecurityTests`; `sdd_lint.py --trace 017` cubre CA-01 a CA-13 |
| T-08 | Publicar el OpenAPI actualizado y registrar la decisión en `Memoria.md` | R-03 | hecha: decisión en `Memoria.md` (2026-11-05, spec 017) y contrato en `docs/openapi.identity.v1.json` |

## Definición de terminado
- Los criterios de aceptación de `spec.md` están cubiertos por pruebas en verde.
- El CI está en verde, un par revisó el cambio y `Memoria.md` está al día.
