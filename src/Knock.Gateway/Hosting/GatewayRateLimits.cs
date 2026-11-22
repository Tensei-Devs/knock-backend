using System.Globalization;
using Knock.Hosting.RateLimiting;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Knock.Gateway.Hosting;

/// <summary>Una política de límite: peticiones por ventana fija.</summary>
internal sealed class RateLimitPolicy
{
    public int PermitLimit { get; set; }

    public TimeSpan Window { get; set; }

    public bool IsValid => PermitLimit > 0 && Window > TimeSpan.Zero;
}

/// <summary>Valores de <c>RateLimiting</c> (sin secretos): por IP antes de autenticar, estricto para <c>/auth</c>, por usuario después (spec 009, R-04) y propio de la búsqueda (spec 027, R-14).</summary>
internal sealed class RateLimitSettings
{
    public const string SectionName = "RateLimiting";

    public RateLimitPolicy Ip { get; set; } = new();

    public RateLimitPolicy Auth { get; set; } = new();

    public RateLimitPolicy User { get; set; } = new();

    public RateLimitPolicy Search { get; set; } = new();

    public RateLimitPolicy Discard { get; set; } = new() { PermitLimit = 30, Window = TimeSpan.FromHours(1) };
}

internal static class GatewayRateLimits
{
    public static IServiceCollection AddKnockGatewayRateLimiting(this IServiceCollection services)
    {
        services.AddOptions<RateLimitSettings>()
            .BindConfiguration(RateLimitSettings.SectionName)
            .Validate(
                s => s.Ip.IsValid && s.Auth.IsValid && s.User.IsValid && s.Search.IsValid && s.Discard.IsValid,
                "RateLimiting:Ip, RateLimiting:Auth, RateLimiting:User and RateLimiting:Search need a PermitLimit > 0 and a Window > 0.")
            .ValidateOnStart();
        services.AddKnockRequestCounter();
        return services;
    }
}

/// <summary>
/// Límite por IP, antes de autenticar: política general para toda ruta salvo <c>/health</c> y la estricta, además, para <c>/api/v{n}/auth/*</c>.
/// Responde 429 con <c>Retry-After</c> en segundos (spec 009, R-09). Si Redis no responde, responde 503: sin contadores no se puede limitar.
/// </summary>
internal sealed class IpRateLimitMiddleware(RequestDelegate next, IRequestCounter counter, IOptions<RateLimitSettings> options)
{
    private static readonly PathString _health = new("/health");

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Request.Path.Equals(_health, StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        var settings = options.Value;
        var client = RequestKeys.ClientKey(context);
        if (await RateLimitCheck.RejectedAsync(context, counter, $"ratelimit:ip:{client}", settings.Ip)
            || (IsAuthPath(context.Request.Path) && await RateLimitCheck.RejectedAsync(context, counter, $"ratelimit:auth:{client}", settings.Auth))
            || (RequestKeys.IsPanelSessionPath(context.Request.Path) && await RateLimitCheck.RejectedAsync(context, counter, $"ratelimit:panel-sesion:{client}", settings.Auth)))
        {
            return;
        }

        await next(context);
    }

    /// <summary>
    /// <c>/api/v{version}/auth</c> y lo que cuelga de ahí; sin distinguir mayúsculas porque el enrutador tampoco las distingue.
    /// </summary>
    internal static bool IsAuthPath(PathString path) => RequestKeys.IsApiPath(path, "auth");
}

/// <summary>
/// Límite por usuario (<c>sub</c> del JWT), después de autenticar; las peticiones sin sesión ya las limitó la IP (spec 009, R-04). La búsqueda de expertos
/// (<c>/api/v{n}/busqueda</c>) suma su propio contador por <c>sub</c> (spec 027, R-14).
/// </summary>
internal sealed class UserRateLimitMiddleware(RequestDelegate next, IRequestCounter counter, IOptions<RateLimitSettings> options)
{
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var sub = context.User.Identity?.IsAuthenticated == true ? context.User.FindFirst("sub")?.Value : null;
        if (sub is not null
            && (await RateLimitCheck.RejectedAsync(context, counter, RequestKeys.IsPanelApiPath(context.Request.Path) ? $"ratelimit:panel-user:{sub}" : $"ratelimit:user:{sub}", options.Value.User)
                || (RequestKeys.IsApiPath(context.Request.Path, "busqueda") && await RateLimitCheck.RejectedAsync(context, counter, $"ratelimit:busqueda:{sub}", options.Value.Search))
                || (IsDiscard(context.Request) && await RateLimitCheck.RejectedAsync(context, counter, $"ratelimit:descarte:{sub}", options.Value.Discard))))
        {
            return;
        }

        await next(context);
    }

    private static bool IsDiscard(HttpRequest request)
    {
        var segments = request.Path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries) ?? [];
        return HttpMethods.IsPost(request.Method) && segments.Length == 5
            && segments[0].Equals("api", StringComparison.OrdinalIgnoreCase)
            && segments[1].Equals("v1", StringComparison.OrdinalIgnoreCase)
            && segments[2].Equals("solicitudes", StringComparison.OrdinalIgnoreCase)
            && segments[4].Equals("descartar", StringComparison.OrdinalIgnoreCase);
    }
}

internal static class RateLimitCheck
{
    /// <summary>Cuenta la petición; si supera el límite escribe el 429 (o el 503 si Redis no responde) y devuelve <c>true</c>. El cuerpo no revela IP, clave, límite ni contador.</summary>
    public static async Task<bool> RejectedAsync(HttpContext context, IRequestCounter counter, string key, RateLimitPolicy policy)
    {
        (long Count, TimeSpan Remaining) hit;
        try
        {
            hit = await counter.HitAsync(key, policy.Window, context.RequestAborted);
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException)
        {
            context.Response.Headers.RetryAfter = "5";
            await ProblemResponses.WriteAsync(context, StatusCodes.Status503ServiceUnavailable, "Service Unavailable");
            return true;
        }

        if (hit.Count <= policy.PermitLimit)
        {
            return false;
        }

        var seconds = Math.Max(1, (int)Math.Ceiling(hit.Remaining.TotalSeconds));
        context.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
        await ProblemResponses.WriteAsync(context, StatusCodes.Status429TooManyRequests, "Too Many Requests");
        return true;
    }
}
