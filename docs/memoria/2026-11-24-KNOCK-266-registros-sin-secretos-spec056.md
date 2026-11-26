# Spec 056 T-06 y T-09: los registros de una llamada de servicio no traen secretos

## Qué cambió
- `tests/UnitTest/Hosting/Spec056RegistrosTests.cs` (CA_056_08): registra el esquema real de la credencial de servicio (`AddKnockServiceCredential`) con un proveedor de registros que captura todo en nivel Trace, autentica una llamada aceptada y cuatro rechazadas (firma de otra clave, vencida, mal formada, sin credencial), las desafía, y comprueba que ningún registro ni cuerpo de error contiene la clave vigente, la anterior, la ajena (Base64, hexadecimal ni sin relleno), ni el token ni sus partes. Claves y tokens se generan al azar en la ejecución.
- `specs/056.../task.md`: T-06 y T-09 pasan a hechas con la evidencia y con la revisión por par y `security-reviewer` anotadas como omitidas por decisión de José.

## Por qué
- CA-08 pedía revisar los registros de una llamada rechazada y aceptada; solo estaba probada la configuración y el repositorio.

## A evitar
- Reusar el proveedor raíz como `RequestServices` en varias llamadas: el manejador de autenticación guarda su resultado dentro del ámbito y todas las llamadas devuelven la primera respuesta (la prueba parecía mostrar que una clave ajena se aceptaba). Hay que crear un ámbito por petición.
- No capturar con `ILoggerProvider` el registro de Serilog en consola: esta prueba cubre los registros del esquema de autenticación, no el formateador de consola (eso lo cubren las pruebas de redacción de las specs 011 y 035).
