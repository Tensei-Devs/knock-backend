using AwesomeAssertions;
using Knock.Identity.Api.Modules.Moderation.Services;
using Knock.Identity.Api.Modules.Panel;
using Knock.Identity.Api.Modules.Safety;
using Knock.Identity.Api.Modules.Safety.Data;
using Knock.Identity.Api.Modules.Safety.Domain;
using Knock.Identity.Api.Modules.Safety.Services;
using Knock.Identity.Api.Modules.Sesiones;
using Microsoft.Extensions.Options;
using Xunit;

namespace Knock.UnitTest.Safety;

/// <summary>Spec 059, R-16 y R-17: la regla gradual al decidir una advertencia (`media`), con dobles en memoria. Sin umbral configurado la regla está apagada.</summary>
public sealed class Spec059GradualTests
{
    private const string _report = "507f1f77bcf86cd799439011";
    private const string _reported = "7b484abb-e9e0-8104-a08d-d30ce15e8148";
    private const string _moderator = "11111111-e9e0-8104-a08d-d30ce15e8148";

    private static (ReportDecisionEffects Effects, Spec059SuspensionTests.World World, FakeWarnings Warnings) Build(int validWarnings, int? threshold)
    {
        var world = new Spec059SuspensionTests.World();
        world.Accounts.Account = new AccountForSuspension("activa", true);
        var warnings = new FakeWarnings(validWarnings);
        var safety = new SafetyApi(world.Service, world.Store, warnings, Options.Create(new SuspensionOptions { UmbralAdvertencias = threshold }), new Spec059SuspensionTests.FakeClock());
        return (new ReportDecisionEffects(safety), world, warnings);
    }

    private static Task<ReportActionResult> Warn(ReportDecisionEffects effects) =>
        effects.ApplyAsync(new StoreTransaction(new object()), new ReportDecisionApplied(_report, "media", "advertencia", "reporter-sub", _reported, _moderator), CancellationToken.None);

    [Fact]
    public async Task Alcanzar_El_Umbral_Suspende_Con_Tipo_Gradual_Y_Avisa_CA_059_13()
    {
        var (effects, world, _) = Build(validWarnings: 3, threshold: 3);

        await Warn(effects);

        world.Accounts.SuspendedWith.Should().Be(("gradual", _report));
        world.Store.Events.Select(e => e.Tipo).Should().Contain(["CuentaSuspendida", "ReporteDecidido"]);
    }

    [Fact]
    public async Task Por_Debajo_Del_Umbral_No_Suspende_CA_059_13()
    {
        var (effects, world, _) = Build(validWarnings: 2, threshold: 3);

        await Warn(effects);

        world.Accounts.SuspendedWith.Should().BeNull();
        world.Store.Events.Should().OnlyContain(e => e.Tipo == "ReporteDecidido");
    }

    [Fact]
    public async Task Sin_Umbral_La_Regla_Esta_Apagada_Y_Ni_Cuenta_CA_059_13()
    {
        var (effects, world, warnings) = Build(validWarnings: 50, threshold: null);

        await Warn(effects);

        world.Accounts.SuspendedWith.Should().BeNull();
        warnings.Calls.Should().Be(0);
    }

    [Theory]
    [InlineData("critica", "suspension_preventiva")]
    [InlineData("alta", "ocultar")]
    [InlineData("baja", "mediacion")]
    public async Task Solo_Una_Advertencia_Cuenta_Para_La_Regla_Gradual_CA_059_13(string severity, string response)
    {
        var (effects, _, warnings) = Build(validWarnings: 9, threshold: 1);

        await effects.ApplyAsync(new StoreTransaction(new object()), new ReportDecisionApplied(_report, severity, response, "reporter-sub", _reported, _moderator), CancellationToken.None);

        warnings.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Una_Advertencia_Fuera_De_La_Ventana_No_Cuenta_CA_059_20()
    {
        var (effects, _, warnings) = Build(validWarnings: 2, threshold: 3);

        await Warn(effects);

        warnings.Since.Should().Be(new DateTime(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc).AddDays(-90), "solo cuentan las advertencias decididas en los últimos 90 días");
    }

    internal sealed class FakeWarnings(int count) : IWarningCounter
    {
        public int Calls { get; private set; }

        public DateTime? Since { get; private set; }

        public Task<int> CountDecidedWarningsAsync(StoreTransaction transaction, string reportedId, DateTime since, CancellationToken cancellationToken)
        {
            Calls++;
            Since = since;
            return Task.FromResult(count);
        }
    }
}
