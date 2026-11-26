using System.Security.Cryptography;

namespace Knock.Messaging.Api.Modules.Avisos.Domain;

internal static class DispositivoId
{
    public static string Create() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
}
