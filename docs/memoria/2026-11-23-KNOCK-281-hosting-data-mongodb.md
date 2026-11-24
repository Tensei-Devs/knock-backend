# 2026-11-23 · KNOCK-281 · MongoDB pasa a Knock.Hosting.Data (spec 055, T-16 y T-20 parcial)

## Qué cambió
Nuevo proyecto `src/Knock.Hosting.Data` (`net11.0`, referencia a `MongoDB.Driver` 3.14.0, la misma versión que los servicios). Contiene `MongoExtensions`, `KnockMongoOptions`, su validador y la comprobación de readiness de MongoDB; se borraron las tres copias de Identidad, Mensajería y Mercado. Lo referencian solo esos tres servicios: el gateway y `Knock.Hosting` no cargan MongoDB (R-07a, CA-13). Los `Dockerfile` de los tres servicios copian el proyecto y su `packages.lock.json`, `Knock.slnx` lo incluye y `stryker-config.json` suma `**/Knock.Hosting.Data/**/*.cs` (R-18). `DataExtensions` de cada servicio se queda porque solo contiene la clave de su base de datos. Se retira EXC-008 (ya no queda ninguna copia). Nuevas pruebas de arquitectura `Sin_MongoDB_En_Hosting_Ni_Gateway_CA_055_13` y `Hosting_Data_No_Referencia_Servicios_Ni_Contratos_CA_055_01`.

## Por qué
Etapa 3 de la spec 055: una sola implementación del acceso técnico a MongoDB sin que el gateway cargue el controlador.

## A evitar
- Referenciar `Knock.Hosting.Data` desde el gateway.
- Agregar reglas de negocio o colecciones a la biblioteca de datos: solo la parte técnica del cliente.
- Recordatorio abierto: `SHARED` de `ci-pr.yml` y `ci-deploy.yml` aún no incluye `^src/Knock\.Hosting` ni `^src/Knock\.Hosting\.Data/` (T-08 de la spec 055, pendiente de decisión).
