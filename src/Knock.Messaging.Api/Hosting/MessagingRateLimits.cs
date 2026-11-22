using Knock.Hosting.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Knock.Messaging.Api.Common.Errors;

namespace Knock.Messaging.Api.Hosting;

/// <summary>Políticas por nombre de Mensajería. El limitador (Redis compartido, respaldo por copia, 429) vive en <c>Knock.Hosting</c> (spec 055, etapa 2).</summary>
internal static class MessagingRateLimits
{
    /// <summary>Crear conversaciones: 20 por hora por persona (spec 030, R-05), además del límite general por IP. Contador compartido en Redis.</summary>
    public const string CreateConversationPolicy = "conversaciones-crear";

    /// <summary>Enviar mensajes: 30 por minuto por persona (spec 030, R-11 y CA-05), con contador compartido en Redis entre copias.</summary>
    public const string SendMessagePolicy = "conversaciones-mensajes";

    private const int _messagesPerMinute = 30;
    private const string _messageLimitCode = "message_limit";

    /// <summary>
    /// Límite por IP: política global y propia para <c>/health/ready</c>, en memoria por copia (como antes de mover el limitador; PARA VALIDACIÓN DE JOSÉ si pasa a Redis).
    /// </summary>
    public static IServiceCollection AddKnockRateLimiting(this IServiceCollection services) =>
        services.AddKnockRateLimiting(new KnockRateLimitingOptions
        {
            ServiceName = "messaging",
            SharedGlobalCounters = false,
            WriteTooManyRequestsAsync = context => ProblemResponses.WriteAsync(context, StatusCodes.Status429TooManyRequests, "Too Many Requests", CodeFor(context)),
            ConfigurePolicies = policies => policies
                .PerSubject(CreateConversationPolicy, 20, TimeSpan.FromHours(1))
                .PerSubject(SendMessagePolicy, _messagesPerMinute, TimeSpan.FromMinutes(1)),
        });

    /// <summary>El límite de enviar mensajes tiene su propio <c>code</c> (<c>message_limit</c>, spec 030, R-11); los demás, el genérico.</summary>
    private static string CodeFor(HttpContext context) =>
        context.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName == SendMessagePolicy
            ? _messageLimitCode
            : ErrorCodes.RateLimitedCode;
}
