# Tareas 031 — Moderación de contenido con Azure

> **Estado:** pendientes. En esta etapa **no se ejecutan**: se activan cuando la spec esté Aprobada y una persona lo indique.
> **Responsable sugerido:** José Julián (KNOCK-171). Revisión de par: Alexis (seguridad) y Guillermo (llamadores del chat).

| Tarea | Qué se hace | Cubre | Depende de | En paralelo con | Responsable sugerido | Estado |
|---|---|---|---|---|---|---|
| T-01 | Interfaz `IContentModerator` y tipos en `Knock.Contracts/Moderation/` | R-01 | — | T-02 | José | hecha: `Knock.Contracts/Moderation/ContentModeration.cs` (`IContentModeration`, tipos y códigos); el `IContentModerator` de la 013 se conserva y un adaptador lo cubre |
| T-02 | Normalizador, lista local (con validación) y comparación con umbrales; unitarias y mutación | R-03, R-05, R-18 · CA-02, CA-18 | — | T-01, T-03 | Alexis | hecha (unitarias; mutación pendiente de CI): `NormalizadorDeModeracion`, `ListaLocal`, umbrales en `ContentSafetyModerator.Evaluate` |
| T-03 | Plantillas y `ModerationProblemMapper` | R-13, R-14 · CA-19 | T-01 | T-02 | Alexis | hecha: `PlantillasDeModeracion` y `ModerationProblemMapper` (falta el uso en las rutas, T-09) |
| T-04 | `ContentSafetyClient` (texto e imagen, identidad administrada, 5 s, reintento, cortacircuito) y servidor simulado | R-05, R-07, R-08, R-09, R-16 · CA-05, CA-06, CA-11, CA-15 | T-01 | T-02, T-03 | José | hecha (unitarias con servidor simulado): `ContentSafetyClient` por REST; interruptor `Moderacion:Habilitado` apagado; identidad administrada pendiente de registrar `TokenCredential` (parche de despliegue) |
| T-05 | Caché de decisiones y topes atómicos en Redis; sin Redis no se llama | R-10, R-11, R-12 · CA-07, CA-08, CA-09, CA-10 | T-01 | T-04 | José | parcial: `ConsumoModeracionStore` (caché y topes con script Lua) probado con doble; pruebas con Redis real `Spec031CuotaRedisTests` (CA-08, conteo atómico mensual y por persona) escritas y compiladas, solo corren en CI (2026-11-24); CA-07/09/10 con Redis real siguen sin prueba propia |
| T-06 | `ContentSafetyModerator`, filtro de privacidad, límite de longitud, `AddKnockModeration()` y opciones (modos, umbrales versionados) | R-02, R-04, R-06, R-15, R-19 · CA-01, CA-03, CA-04, CA-12, CA-13, CA-14 | T-02, T-04, T-05 | — | José | hecha: `ContentSafetyModerator`, filtro de privacidad, longitud, `AddModerationModule()` y opciones validadas |
| T-07 | `uso_moderacion` (script de `db/` en dos bases) y registros sin contenido | R-17 · CA-16 | T-06 | T-08 | Alexis | parcial: `db/031-moderacion-uso-moderacion.js` + `Spec031ModerationScriptTests` (solo CI) y `UsoModeracionStore`; registros de la aplicación sin contenido por construcción |
| T-08 | Copia en Mensajería con excepción de duplicación (la registra el líder) y prueba de listas idénticas; pruebas de arquitectura | R-02, R-18 · CA-17, CA-18 | T-06 | T-07 | Guillermo | pendiente: copia en Mensajería y prueba de listas idénticas; la prueba de arquitectura de punto único ya existe |
| T-09 | Conectar llamadores (020, 030, 032) con el traductor y correr su regresión de falla cerrada | R-13 · CA-01, CA-05, CA-07 | T-06, T-08 | — | José | parcial: solicitudes (020) e imágenes (032) responden 429 `moderation_limit` con `Retry-After` del traductor único (`ModerationOutcome.LimitReached` en el adaptador); 503 de solicitudes ahora lleva `Retry-After`; falta mensajería (030) y notas de cotización, que dependen de T-08 |
| T-10 | Recurso de Content Safety y rol en Terraform; prueba manual con contenido sintético; `Memoria.md` | R-16 | T-06 | T-07 a T-09 | José | pendiente: recurso de Content Safety y rol en Terraform (decisión de gasto de José) |

**Qué bloquea:** T-01 desbloquea a las specs 020, 030 y 032 (pueden usar un doble); T-06 bloquea T-07 a T-10. **Qué va en paralelo:** T-02, T-03, T-04 y T-05 con tres personas una vez lista T-01 (media hora de trabajo).

## Definición de terminado
- Los criterios de aceptación de `spec.md` están cubiertos por pruebas en verde.
- El CI está en verde, un par revisó el cambio y `Memoria.md` está al día.
