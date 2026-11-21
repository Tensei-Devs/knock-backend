using Knock.Hosting.RateLimiting;

namespace Knock.Identity.Api.Hosting;

/// <summary>Políticas por nombre de Identidad. El limitador (Redis compartido, respaldo por copia, 429) vive en <c>Knock.Hosting</c> (spec 055, etapa 2).</summary>
internal static class IdentityRateLimits
{
    /// <summary>Spec 039: comprobar disponibilidad del número nuevo (USR-03), 3 por hora por persona (R-20).</summary>
    public const string PhoneChangeCheckPolicy = "cambio-numero-comprobar";

    /// <summary>Spec 039: confirmar el cambio de número (USR-04). Valor propuesto: la spec no fija límite, solo registra el 429.</summary>
    public const string PhoneChangeConfirmPolicy = "cambio-numero-confirmar";

    /// <summary>Spec 039: recuperación de acceso (USR-05), 3 por hora por IP (R-21); el límite por número lo aplica el servicio.</summary>
    public const string AccessRecoveryPolicy = "recuperacion-acceso";

    /// <summary>Spec 039: pedir una exportación (USR-06). Valor propuesto: la spec no fija límite, solo registra el 429.</summary>
    public const string ExportPolicy = "exportaciones";

    /// <summary>Spec 039: programar la eliminación de la cuenta (USR-10). Valor propuesto: la spec no fija límite, solo registra el 429.</summary>
    public const string DeletionPolicy = "eliminacion";

    /// <summary>Spec 025 (R-19): crear una verificación, 3 por hora por persona.</summary>
    public const string VerificationCreatePolicy = "verificacion-crear";

    /// <summary>
    /// Límite por IP: política global, estricta para <c>/api/v{n}/auth/*</c> y la sesión del panel (spec 033, R-27) y propia para <c>/health/ready</c>.
    /// </summary>
    public static IServiceCollection AddKnockRateLimiting(this IServiceCollection services) =>
        services.AddKnockRateLimiting(new KnockRateLimitingOptions
        {
            ServiceName = "identity",
            StrictAuth = true,
            IsExtraStrictPath = IsPanelSessionSegments,
            WriteTooManyRequestsAsync = context => ProblemResponses.WriteAsync(context, StatusCodes.Status429TooManyRequests, "Too Many Requests"),
            ConfigurePolicies = policies => policies
                .PerSubject(PhoneChangeCheckPolicy, 3, TimeSpan.FromHours(1))
                .PerSubject(PhoneChangeConfirmPolicy, 5, TimeSpan.FromHours(1))
                .PerClient(AccessRecoveryPolicy, "recuperacion", 3, TimeSpan.FromHours(1))
                .PerSubject(ExportPolicy, 3, TimeSpan.FromHours(1))
                .PerSubject(DeletionPolicy, 5, TimeSpan.FromHours(1))
                .PerSubject(VerificationCreatePolicy, 3, TimeSpan.FromHours(1)),
        });

    /// <summary>Spec 033, R-27: <c>/api/v{n}/panel/sesion/*</c> y <c>/api/v{n}/panel/activacion</c> llevan el mismo límite estricto por IP que <c>/auth</c>.</summary>
    private static bool IsPanelSessionSegments(string[] segments) =>
        segments.Length >= 4
        && segments[2].Equals("panel", StringComparison.OrdinalIgnoreCase)
        && (segments[3].Equals("sesion", StringComparison.OrdinalIgnoreCase) || segments[3].Equals("activacion", StringComparison.OrdinalIgnoreCase));
}
