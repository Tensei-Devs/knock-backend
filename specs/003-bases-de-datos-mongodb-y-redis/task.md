# Tareas 003 — Bases de datos: MongoDB y Redis

> **Estado:** conciliado con el código el 2026-11-23: T-01 a T-06 y T-08 hechas, T-07 parcial (falta programar el respaldo). `sdd_lint --trace 003` sin casos faltantes.

| Tarea | Qué se hace | Cubre | Estado |
|---|---|---|---|
| T-01 | Ejecutar la prueba técnica sobre el clúster gratuito y registrar el resultado | R-01, R-08 · CA-04 | hecha: evidencia del spike en `docs/evidencia/KNOCK-147-spike-documentdb.md` (Azure DocumentDB nivel gratuito, West US, ADR-002) |
| T-02 | Agregar la configuración de conexión y el cliente de cada servicio | R-01, R-02 | hecha: opciones y cliente de MongoDB y Redis en `Knock.Hosting` y los hosts; pruebas `Spec003DataClientsTests` (CA_003_02, CA_003_03), `KnockMongoOptionsTests`, `KnockMongoDatabasesTests` |
| T-03 | Definir la interfaz y la implementación base de repositorio | R-03, R-04 · CA-02 | hecha con otro diseño: cada módulo tiene su `Store` en `Data` (p. ej. `UsuariosStore`) en lugar de una base genérica de repositorio; la regla R-04 la hacen cumplir `MongoAccessTests` y `Spec003DataAccessTests` (CA_003_02) |
| T-04 | Crear `db/` con el primer script de índices | R-05, R-06 · CA-01 | hecha: `db/001_indices.js` y los scripts numerados de `db/`; prueba `Spec003IndexScriptTests` (CA_003_01, idempotente) |
| T-05 | Configurar Redis y su acceso desde la capa de datos | R-07 | hecha: prueba `Spec003RedisTests` (CA_003_03) con `RedisFixture` (Redis real); Redis como contenedor propio en `deploy/` |
| T-06 | Preparar el fixture de pruebas con MongoDB y Redis reales | CA-03 | hecha: `tests/DatabaseTest/Support` (`DocumentDbFixture`, `RedisFixture`, `MongoToolsContainer`) |
| T-07 | Programar el respaldo con `mongodump` y probar la restauración | R-08 · CA-04 | parcial: `deploy/backup/mongo-backup.sh` (dump y restore) y prueba `Spec003BackupRestoreTests` (CA_003_04); falta programar la copia periódica a Blob Storage (no hay trabajo programado ni Terraform) |
| T-08 | Fijar y comprobar la región West US en la configuración de datos | R-09 · CA-05 | hecha: prueba `Spec003RegionTests` (CA_003_05, región West US) |

## Definición de terminado
- Los criterios de aceptación de `spec.md` están cubiertos por pruebas en verde.
- El CI está en verde, un par revisó el cambio y `Memoria.md` está al día.
