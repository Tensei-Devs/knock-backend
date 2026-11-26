# Tareas 038 — Semillas de demo de clientes

> **Jira:** KNOCK-40 · **Estado:** spec aprobada; implementación parcial.

| Tarea | Qué se hace | Cubre | Estado |
|---|---|---|---|
| T-01 | Aplicar las decisiones de §9 (nombre en `usuarios`, upsert por `_id`, sin caché que invalidar, sin códigos de error) y resolver el acceso a datos. | R-03 a R-06, R-09 · CA-04 a CA-06, CA-08, CA-11 | parcial: se usa `UsuariosStore`; nueva decisión: invalidar `perfil:{sub}` de los diez en cada carga y avisar si Redis falla para reintentar en la siguiente. Discrepa de §9 y de la decisión anterior; pendiente revisión del Tech Lead |
| T-02 | Preparar pruebas de archivo y JSON de diez clientes con nombres claramente ficticios; verificar estado, perfil y consentimientos vigentes. | R-01, R-02, R-10 · CA-01 a CA-03, CA-13 | hecha para el archivo; CA-13 tras carga queda en T-03 |
| T-03 | Extender el acceso a datos de Identidad e implementar cargador con derivación, cifrado y hash, upsert idempotente, validación previa, índices únicos y pruebas de carreras y recuperación de fallos parciales. | R-03 a R-06, CA-04 a CA-06, CA-08, CA-11 | parcial: dos cargas concurrentes con cada índice y recuperación parcial verificadas; falta provocar de forma determinista una colisión tras la preconsulta y verificar su rechazo por cada índice (necesita un punto de inyección en el almacén; no hay prueba aún, se decide con el Tech Lead junto con T-01) |
| T-04 | Conectar el arranque y probar las guardas de configuración, producción, todos los hosts de `MongoUrl.Servers`, credenciales, rechazo de SRV y documentos no demo, sin escrituras ante rechazo. | R-07 · CA-07, CA-09, CA-12 | hecha para conexión y guardas; evidencia de dos arranques CA-09 pendiente en T-06 |
| T-05 | Documentar contenido, carga local, conteo y borrado de demo sin secretos, y carga separada y manual a Azure con `db/entrega.js`. | R-08 · CA-09 | hecha; `entrega.js` no carga clientes demo en Azure, aclarado en README |
| T-06 | Ejecutar pruebas por caso, dos arranques con conteos y build/formato; revisar convención de errores y actualizar memoria. | R-01 a R-10 · CA-01 a CA-13 | parcial: CA-09 verificado en documentdb-local aislado, sin documentos previos: readiness 200 y conteos 10 / 10; CA-10 build Release sin restore, 0 errores/advertencias, y formato sin cambios. Carreras de CA-11: `DemoSeedLoaderTests.FiveConcurrentLoads_WithAllUniqueIndexes_KeepTenUsersAndVersions_CA_038_11` (cinco cargas simultáneas con los índices únicos) escrita y fusionada en el PR #311; solo corre en el CI, sin corrida verde registrada aún. Sigue parcial: queda la revisión de errores y `sdd_lint --trace 038` marca sin prueba CA-09 y CA-10 (se verificaron con comandos locales: conteos 10 / 10 y build Release; falta marcarlos «(comando)» en la spec) |

## Definición de terminado
Casos y comandos verificados con evidencia sin secretos, CI en verde, revisión de par y `Memoria.md` actualizado.
