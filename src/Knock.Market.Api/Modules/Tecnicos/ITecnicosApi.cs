namespace Knock.Market.Api.Modules.Tecnicos;

/// <summary>Contrato público del módulo Tecnicos hacia el resto del servicio (spec 052, §8: el asistente solo atiende cuentas con perfil de técnico).</summary>
public interface ITecnicosApi
{
    /// <summary>Verdadero si la cuenta del <paramref name="userId"/> tiene perfil de técnico.</summary>
    Task<bool> HasProfileAsync(string userId, CancellationToken cancellationToken);
}
