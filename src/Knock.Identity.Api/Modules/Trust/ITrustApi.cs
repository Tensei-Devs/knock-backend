namespace Knock.Identity.Api.Modules.Trust;

/// <summary>Lo que el módulo de confianza ofrece a los demás módulos de Identidad sin que lean su dominio (spec 025).</summary>
public interface ITrustApi
{
    /// <summary>Si <paramref name="reason"/> es un motivo del catálogo de rechazos (spec 025, R-15), el que trae consejos para la app.</summary>
    bool IsCatalogReason(string? reason);
}

internal sealed class TrustApi : ITrustApi
{
    public bool IsCatalogReason(string? reason) => Domain.VerificationReasons.TipsFor(reason).Count > 0;
}
