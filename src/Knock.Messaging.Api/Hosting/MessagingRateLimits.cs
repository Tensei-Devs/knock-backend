using Knock.Hosting.RateLimiting;
using Knock.Messaging.Api.Common.Errors;

namespace Knock.Messaging.Api.Hosting;

/// <summary>Políticas por nombre de Mensajería. El limitador (Redis compartido, respaldo por copia, 429) vive en <c>Knock.Hosting</c> (spec 055, etapa 2).</summary>
internal static class MessagingRateLimits
{
    /// <summary>Crear conversaciones: 20 por hora por persona (spec 030, R-05), además del límite general por IP. Contador compartido en Redis.</summary>
    public const string CreateConversationPolicy = "conversaciones-crear";

    /// <summary>
    /// Límite por IP: política global y propia para <c>/health/ready</c>, en memoria por copia (como antes de mover el limitador; PARA VALIDACIÓN DE JOSÉ si pasa a Redis).
    /// </summary>
    public static IServiceCollection AddKnockRateLimiting(this IServiceCollection services) =>
        services.AddKnockRateLimiting(new KnockRateLimitingOptions
        {
            ServiceName = "messaging",
            SharedGlobalCounters = false,
            WriteTooManyRequestsAsync = context => ProblemResponses.WriteAsync(context, StatusCodes.Status429TooManyRequests, "Too Many Requests", ErrorCodes.RateLimitedCode),
            ConfigurePolicies = policies => policies.PerSubject(CreateConversationPolicy, 20, TimeSpan.FromHours(1)),
        });
}
