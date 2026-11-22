using Knock.Market.Api.Modules.Requests;
using Knock.Market.Api.Modules.Tecnicos;

namespace Knock.Market.Api.Modules.Assistant.Services;

/// <summary>
/// Puerto real de cuentas (spec 052, §8): consulta el contrato público de Tecnicos. Los servicios del asistente son singleton y los contratos de los otros módulos son por petición, así que cada
/// llamada abre su alcance. Si el contrato no está registrado se falla cerrado: nadie es técnico.
/// </summary>
internal sealed class AsistenteCuentasReal(IServiceScopeFactory scopes) : IAsistenteCuentas
{
    public async Task<bool> IsTechnicianAsync(string sub, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var api = scope.ServiceProvider.GetService<ITecnicosApi>();
        return api is not null && await api.HasProfileAsync(sub, cancellationToken);
    }
}

/// <summary>Puerto real de solicitudes (spec 052, R-21): participación y cierre desde el contrato público de Requests; sin él, el técnico no participa (403).</summary>
internal sealed class AsistenteSolicitudesReal(IServiceScopeFactory scopes) : ISolicitudesAsistente
{
    public async Task<RequestAccess> GetAccessAsync(string solicitudId, string sub, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var api = scope.ServiceProvider.GetService<IRequestsApi>();
        if (api is null)
        {
            return new RequestAccess(false, null);
        }

        var (participates, closedAtUtc) = await api.GetTechnicianAccessAsync(solicitudId, sub, cancellationToken);
        return new RequestAccess(participates, closedAtUtc);
    }
}
