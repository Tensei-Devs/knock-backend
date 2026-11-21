namespace Knock.Hosting.RateLimiting;

/// <summary>Una política de ventana fija: peticiones permitidas y duración de la ventana (sección <c>RateLimiting:*</c> de la configuración).</summary>
public sealed class RateLimitPolicySettings
{
    public int PermitLimit { get; set; }

    public TimeSpan Window { get; set; }

    public bool IsValid => PermitLimit > 0 && Window > TimeSpan.Zero;
}

/// <summary>
/// Valores del limitador por IP de los servicios (sección <c>RateLimiting</c>); sin secretos. No hay valores por defecto en código: sin configuración válida el servicio no arranca.
/// <see cref="Auth"/> solo se exige si el servicio declara la política estricta de <c>/auth</c>.
/// </summary>
public sealed class RateLimitSettings
{
    public const string SectionName = "RateLimiting";

    public RateLimitPolicySettings Global { get; set; } = new();

    public RateLimitPolicySettings Auth { get; set; } = new();

    /// <summary>Política propia de <c>/health/ready</c> (hace pings a las bases por petición anónima, así que no puede quedar sin límite).</summary>
    public RateLimitPolicySettings Ready { get; set; } = new();
}
