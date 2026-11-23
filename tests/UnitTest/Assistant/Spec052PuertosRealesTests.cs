using AwesomeAssertions;
using Knock.Market.Api.Modules.Assistant.Services;
using Knock.Market.Api.Modules.Requests;
using Knock.Market.Api.Modules.Tecnicos;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Knock.UnitTest.Assistant;

/// <summary>Spec 052, §8: los puertos reales de cuentas y solicitudes usan los contratos públicos de Tecnicos y Requests y fallan cerrado si faltan.</summary>
public sealed class Spec052PuertosRealesTests
{
    private sealed class FakeTecnicosApi(bool has) : ITecnicosApi
    {
        public string? Asked { get; private set; }

        public Task<bool> HasProfileAsync(string userId, CancellationToken cancellationToken)
        {
            Asked = userId;
            return Task.FromResult(has);
        }

        public string ModuleName => "Tecnicos";

        public Task<string?> ExportProfileJsonAsync(string accountSub, CancellationToken cancellationToken) => Task.FromResult<string?>(null);

        public Task AnonymizeProfileAsync(string accountSub, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeRequestsApi((bool Participates, DateTime? ClosedAtUtc) access) : IRequestsApi
    {
        public string ModuleName => "Requests";

        public bool CanArrive(string state, string modality, string day, bool blocked, bool codeUsed, DateTime? expiresAtUtc, DateTime nowUtc) => false;

        public Task<(bool Participates, DateTime? ClosedAtUtc)> GetTechnicianAccessAsync(string solicitudId, string userId, CancellationToken cancellationToken) => Task.FromResult(access);
    }

    private static IServiceScopeFactory Scopes(Action<IServiceCollection>? register = null)
    {
        var services = new ServiceCollection();
        register?.Invoke(services);
        return services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Cuentas_Responde_Lo_Que_Dice_Tecnicos_CA_052_20(bool has)
    {
        var api = new FakeTecnicosApi(has);
        var port = new AsistenteCuentasReal(Scopes(s => s.AddScoped<ITecnicosApi>(_ => api)));

        (await port.IsTechnicianAsync("user-1", CancellationToken.None)).Should().Be(has);
        api.Asked.Should().Be("user-1");
    }

    [Fact]
    public async Task Sin_Contrato_De_Tecnicos_Nadie_Es_Tecnico()
    {
        (await new AsistenteCuentasReal(Scopes()).IsTechnicianAsync("user-1", CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task Solicitudes_Devuelve_Participacion_Y_Cierre_CA_052_34()
    {
        var closed = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
        var port = new AsistenteSolicitudesReal(Scopes(s => s.AddScoped<IRequestsApi>(_ => new FakeRequestsApi((true, closed)))));

        var access = await port.GetAccessAsync("665f1e7a2b3c4d5e6f708192", "user-1", CancellationToken.None);

        access.Should().Be(new RequestAccess(true, closed));
    }

    [Fact]
    public async Task Sin_Contrato_De_Requests_No_Participa_403_CA_052_20()
    {
        var access = await new AsistenteSolicitudesReal(Scopes()).GetAccessAsync("665f1e7a2b3c4d5e6f708192", "user-1", CancellationToken.None);

        access.Should().Be(new RequestAccess(false, null));
    }
}
