namespace Knock.Identity.Api.Modules.Trust;

/// <summary>Lo que el módulo de confianza ofrece a los demás módulos de Identidad sin que lean su dominio (spec 025).</summary>
public interface ITrustApi
{
    /// <summary>Si <paramref name="reason"/> es un motivo del catálogo de rechazos (spec 025, R-15), el que trae consejos para la app.</summary>
    bool IsCatalogReason(string? reason);

    /// <summary>Baja de cuenta (spec 039, R-25; spec 057): purga las fotos y la llave de cada verificación de la persona. Idempotente; si el borrado de blobs falla lanza y quien llama reintenta.</summary>
    Task PurgeAccountAsync(string accountSub, CancellationToken cancellationToken);
}

internal sealed class TrustApi(IServiceScopeFactory scopes) : ITrustApi
{
    public bool IsCatalogReason(string? reason) => Domain.VerificationReasons.TipsFor(reason).Count > 0;

    public async Task PurgeAccountAsync(string accountSub, CancellationToken cancellationToken)
    {
        // La purga es scoped (usa el almacén y el panel por petición); esta fachada es singleton.
        await using var scope = scopes.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<Services.PurgaDeVerificacion>().PurgeAccountAsync(accountSub, cancellationToken);
    }
}
