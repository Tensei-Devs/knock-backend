using Knock.Hosting.RateLimiting;
using Knock.Market.Api.Common.Errors;

namespace Knock.Market.Api.Hosting;

/// <summary>Políticas por nombre de Mercado. El limitador (Redis compartido, respaldo por copia, 429) vive en <c>Knock.Hosting</c> (spec 055, etapa 2).</summary>
internal static class MarketRateLimits
{
    public const string WaitlistPolicy = "waitlist";

    /// <summary>Crear solicitudes: 10 por hora por persona (spec 020, §5), además del límite general.</summary>
    public const string CreateRequestPolicy = "solicitudes-crear";
    /// <summary>Cancelar solicitudes: 10 por hora por persona (spec 029, R-14).</summary>
    public const string CancelRequestPolicy = "solicitudes-cancelar";
    public const string QuotePolicy = "quotes";
    public const string CompareQuotesPolicy = "cotizaciones-comparar";
    public const string ChooseQuotePolicy = "cotizaciones-elegir";
    public const string ArrivalPolicy = "visitas-llegada";
    public const string TrustPolicy = "tecnicos-confianza";

    /// <summary>Servicios del técnico: 30 por minuto por persona (spec 047, R-20).</summary>
    public const string ServicesPolicy = "tecnicos-servicios";

    /// <summary>Rutas de administración del panel (spec 045): 60 por minuto por IP; el token del panel no pasa por el esquema de la app, así que el sub aún no está disponible en el limitador.</summary>
    public const string AdminPolicy = "admin-catalogo";

    /// <summary>Registro como técnico: 5 por hora por persona (spec 046, R-52).</summary>
    public const string RegisterTechnicianPolicy = "tecnicos-registro";

    /// <summary>Límite por IP: política global, estricta para <c>/api/v{n}/auth/*</c> y propia para <c>/health/ready</c>.</summary>
    public static IServiceCollection AddKnockRateLimiting(this IServiceCollection services) =>
        services.AddKnockRateLimiting(new KnockRateLimitingOptions
        {
            ServiceName = "market",
            StrictAuth = true,
            WriteTooManyRequestsAsync = context => ProblemResponses.WriteAsync(context, StatusCodes.Status429TooManyRequests, "Too Many Requests", ErrorCodes.RateLimitedCode),
            ConfigurePolicies = policies => policies
                .PerSubject(WaitlistPolicy, 5, TimeSpan.FromHours(1))
                .PerSubject(CreateRequestPolicy, 10, TimeSpan.FromHours(1))
                .PerSubject(CancelRequestPolicy, 10, TimeSpan.FromHours(1))
                .PerSubject(QuotePolicy, 3, TimeSpan.FromMinutes(1))
                .PerSubject(CompareQuotesPolicy, 60, TimeSpan.FromMinutes(1))
                .PerSubject(ChooseQuotePolicy, 10, TimeSpan.FromMinutes(1))
                .PerSubject(ArrivalPolicy, 5, TimeSpan.FromMinutes(1))
                .PerSubject(TrustPolicy, 30, TimeSpan.FromMinutes(1))
                .PerSubject(ServicesPolicy, 30, TimeSpan.FromMinutes(1))
                .PerClient(AdminPolicy, AdminPolicy, 60, TimeSpan.FromMinutes(1))
                .PerSubject(RegisterTechnicianPolicy, 5, TimeSpan.FromHours(1)),
        });
}
