// Tipo de prueba para la regla de acceso a Redis (Spec003DataAccessTests): un servicio de módulo que usa Redis fuera de Data.
// Es estático y nunca se llama: la regla solo lee sus dependencias.
using StackExchange.Redis;

namespace Knock.UnitTest.Architecture.Fixtures.Redis.Violating.Modules.Alpha.Services;

internal static class AlphaServiceReadsRedis
{
    public static RedisValue Read(IDatabase database) => database.StringGet("alpha:ficticio");
}
