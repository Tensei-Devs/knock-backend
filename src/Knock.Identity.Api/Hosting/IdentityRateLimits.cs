using Knock.Hosting.Http;
using Knock.Hosting.RateLimiting;
using Microsoft.IdentityModel.JsonWebTokens;

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

    /// <summary>Spec 039 (R-22): consultar la última exportación y obtener su enlace de 24 horas (USR-06b). Valor propuesto: 30 por hora por persona.</summary>
    public const string ExportLatestPolicy = "exportaciones-ultima";

    /// <summary>Spec 039: programar la eliminación de la cuenta (USR-10). Valor propuesto: la spec no fija límite, solo registra el 429.</summary>
    public const string DeletionPolicy = "eliminacion";

    /// <summary>Spec 043 (R-26): bloquear y desbloquear (BLO-01 y BLO-02). Valor propuesto: la spec no fija límite, solo registra el 429.</summary>
    public const string BlockPolicy = "bloqueos";

    /// <summary>Spec 043 (R-26): crear un reporte y apelar su decisión (REP-01 y REP-03). Valor propuesto: la spec no fija límite, solo registra el 429.</summary>
    public const string ReportPolicy = "reportes";

    /// <summary>Spec 043 (R-26): leer la lista de bloqueos y de reportes propios (BLO-03 y REP-02). Valor propuesto: la spec no fija límite, solo registra el 429.</summary>
    public const string SafetyReadPolicy = "seguridad-leer";

    /// <summary>Spec 025 (R-19): crear una verificación, 3 por hora por persona.</summary>
    public const string VerificationCreatePolicy = "verificacion-crear";

    /// <summary>Spec 025 (R-19): confirmar una foto de verificación, 10 por hora por persona.</summary>
    public const string VerificationConfirmPolicy = "verificacion-confirmar";

    /// <summary>Spec 060 (R-07): apelación de una persona suspendida, 20 por hora por IP; el límite de 5 por hora por número lo aplica el servicio.</summary>
    public const string SuspensionAppealPolicy = "suspension-apelacion";

    /// <summary>Spec 053 (R-25, CA-39): lecturas del panel (VER-03 y REP-04), 120 por minuto por sesión del panel.</summary>
    public const string PanelReadPolicy = "panel-lectura";

    /// <summary>Spec 053 (R-25, CA-39): abrir una verificación y decidir (VER-04, VER-05 y REP-05), 30 por minuto por sesión del panel.</summary>
    public const string PanelDecisionPolicy = "panel-decision";

    private const int _maxSidLength = 64;
    private const int _maxBearerLength = 4096;
    private const string _bearerPrefix = "Bearer ";

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
                .PerClient(SuspensionAppealPolicy, "suspension-apelacion", 20, TimeSpan.FromHours(1))
                .PerSubject(ExportPolicy, 3, TimeSpan.FromHours(1))
                .PerSubject(ExportLatestPolicy, 30, TimeSpan.FromHours(1))
                .PerSubject(DeletionPolicy, 5, TimeSpan.FromHours(1))
                .PerSubject(BlockPolicy, 30, TimeSpan.FromHours(1))
                .PerSubject(ReportPolicy, 10, TimeSpan.FromHours(1))
                .PerSubject(SafetyReadPolicy, 120, TimeSpan.FromMinutes(1))
                .PerSubject(VerificationCreatePolicy, 3, TimeSpan.FromHours(1))
                .PerSubject(VerificationConfirmPolicy, 10, TimeSpan.FromHours(1))
                .PerKey(PanelReadPolicy, PanelSessionKey, 120, TimeSpan.FromMinutes(1))
                .PerKey(PanelDecisionPolicy, PanelSessionKey, 30, TimeSpan.FromMinutes(1)),
        });

    /// <summary>Spec 033, R-27: <c>/api/v{n}/panel/sesion/*</c> y <c>/api/v{n}/panel/activacion</c> llevan el mismo límite estricto por IP que <c>/auth</c>.</summary>
    private static bool IsPanelSessionSegments(string[] segments) =>
        segments.Length >= 4
        && segments[2].Equals("panel", StringComparison.OrdinalIgnoreCase)
        && (segments[3].Equals("sesion", StringComparison.OrdinalIgnoreCase) || segments[3].Equals("activacion", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Clave del contador del panel: el <c>sid</c> del token leído SIN validar la firma (el limitador corre antes de que el esquema del panel autentique) junto con la IP del cliente. La firma la exige
    /// después el controlador (sin ella la petición responde 401), así que cualquiera puede escribir un token con el <c>sid</c> de otra sesión; unir la IP evita que desde otra red se agote el cupo de esa
    /// sesión (negación de servicio dirigida) y obliga a quien quiera repartir peticiones a usar muchas IP, que ya frena el límite global por IP. Sin token legible devuelve <see langword="null"/> y cuenta por IP.
    /// </summary>
    internal static string? PanelSessionKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var header = context.Request.Headers.Authorization.ToString();
        if (header.Length is 0 or > _maxBearerLength || !header.StartsWith(_bearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var handler = new JsonWebTokenHandler();
        var token = header[_bearerPrefix.Length..].Trim();
        if (!handler.CanReadToken(token))
        {
            return null;
        }

        var sid = handler.ReadJsonWebToken(token).TryGetClaim("sid", out var claim) ? claim.Value : null;
        return sid is { Length: > 0 and <= _maxSidLength } && sid.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_') ? $"{sid}|{RequestKeys.ClientKey(context)}" : null;
    }
}
