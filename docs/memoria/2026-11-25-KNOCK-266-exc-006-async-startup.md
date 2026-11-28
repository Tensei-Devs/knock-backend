# EXC-006 retirada: las pruebas de arranque usan IAsyncStartupValidator

## Qué cambió
- Las cuatro clases de prueba que validaban la configuración al arrancar pasan de `IStartupValidator.Validate()` (obsoleto en .NET 11, SYSLIB0066) a `IAsyncStartupValidator.ValidateAsync(CancellationToken.None)`: `KnockMongoOptionsTests`, `KnockMongoDatabasesTests`, `Spec003DataClientsTests` y `Spec003RegionTests` (12 llamadas, 8 métodos convertidos a `async Task`).
- Los casos que esperan un fallo de arranque usan `Func<Task>` con `ThrowAsync<OptionsValidationException>`; el mensaje que se inspecciona es el mismo, sin cambios en las aserciones.
- Se quita el `NoWarn` de `SYSLIB0066` en `tests/UnitTest/Knock.UnitTest.csproj` y se retira la excepción EXC-006 de `security/exceptions.yaml` con el plan ya cumplido.

## Por qué
- EXC-006 vencía el 2027-01-15; su plan era exactamente portar estas cuatro clases y quitar el `NoWarn`. El código de producción no cambia: la validación al arrancar la sigue ejecutando el host (`ValidateOnStart()`); solo cambia la API con la que las pruebas la invocan.

## A evitar
- Volver a registrar `IStartupValidator` en pruebas nuevas: el host ya no lo necesita y el SDK lo marca obsoleto.
- Suponer que la migración cambia el comportamiento de producción: el registro de opciones, los validadores y el pipeline del host son los mismos.
