using AwesomeAssertions;
using Knock.Identity.Api.Hosting.Procesos.Outbox;
using Knock.Identity.Api.Modules.Moderation.Services;
using Knock.Identity.Api.Modules.Panel;
using Knock.Identity.Api.Modules.Safety.Data;
using Knock.Identity.Api.Modules.Safety.Domain;
using Knock.Identity.Api.Modules.Safety.Services;
using Knock.Identity.Api.Modules.Sesiones;
using Knock.UnitTest.Safety;
using Microsoft.Extensions.Options;
using Xunit;

namespace Knock.UnitTest.Moderation;

/// <summary>Spec 053 R-20 (KNOCK-86): la respuesta real de cada gravedad, con los dobles de la spec 059 (sin MongoDB ni Redis).</summary>
public sealed class Spec053EfectosTests
{
    private const string _report = "507f1f77bcf86cd799439011";
    private const string _reporter = "reporter-sub";
    private const string _reported = "7b484abb-e9e0-8104-a08d-d30ce15e8148";
    private const string _moderator = "11111111-e9e0-8104-a08d-d30ce15e8148";

    private static (ReportDecisionEffects Effects, Spec059SuspensionTests.World World) Build()
    {
        var world = new Spec059SuspensionTests.World();
        return (new ReportDecisionEffects(new SafetyApi(world.Service, world.Store, new Spec059GradualTests.FakeWarnings(0), Options.Create(new SuspensionOptions()), new Spec059SuspensionTests.FakeClock())), world);
    }

    private static ReportDecisionApplied Decision(string severity, string response) => new(_report, severity, response, _reporter, _reported, _moderator);

    private static StoreTransaction Session() => new(new object());

    [Fact]
    public async Task Critica_Sobre_Un_Tecnico_Suspende_Y_Avisa_A_Las_Dos_Partes_CA_053_34()
    {
        var (effects, world) = Build();
        world.Accounts.Account = new AccountForSuspension("activa", true);
        world.Accounts.Sessions = ["sid-1"];

        var sessions = await effects.ApplyAsync(Session(), Decision("critica", "suspension_preventiva"), CancellationToken.None);
        await effects.AfterCommitAsync(sessions, CancellationToken.None);

        world.Accounts.SuspendedWith.Should().Be(("preventiva", _report));
        world.Accounts.Cancelled.Should().ContainSingle().Which.Should().Be("sid-1");
        world.Store.Events.Select(e => e.Tipo).Should().Contain(["CuentaSuspendida", "sesion_revocada", "ReporteDecidido"]);
        world.Store.Events.Count(e => e.Tipo == "ReporteDecidido").Should().Be(2);
    }

    [Fact]
    public async Task Critica_Sobre_Un_Cliente_No_Suspende_Pero_Avisa_CA_053_34()
    {
        var (effects, world) = Build();
        world.Accounts.Account = new AccountForSuspension("activa", false);

        var sessions = await effects.ApplyAsync(Session(), Decision("critica", "suspension_preventiva"), CancellationToken.None);

        sessions.Should().BeEmpty();
        world.Accounts.SuspendedWith.Should().BeNull();
        world.Store.Events.Should().OnlyContain(e => e.Tipo == "ReporteDecidido");
    }

    [Fact]
    public async Task Alta_Oculta_Al_Tecnico_Para_Market_CA_053_34()
    {
        var (effects, world) = Build();

        await effects.ApplyAsync(Session(), Decision("alta", "ocultar"), CancellationToken.None);

        world.Store.Events.Should().Contain(e => e.Tipo == "TecnicoOcultado" && e.Destino == "market" && e.Carga["usuarioId"] == _reported);
        world.Accounts.SuspendedWith.Should().BeNull();
    }

    [Theory]
    [InlineData("media", "advertencia")]
    [InlineData("baja", "mediacion")]
    public async Task Media_Y_Baja_Solo_Avisan_Sin_Datos_De_La_Otra_Parte_CA_053_34(string severity, string response)
    {
        var (effects, world) = Build();

        await effects.ApplyAsync(Session(), Decision(severity, response), CancellationToken.None);

        var notices = world.Store.Events;
        notices.Should().HaveCount(2).And.OnlyContain(e => e.Tipo == "ReporteDecidido");
        notices.Should().AllSatisfy(e => OutboxReglas.Validate(e));
        var toReporter = notices.Single(e => e.Carga["rol"] == "reportante");
        toReporter.Carga["usuarioId"].AsString.Should().Be(_reporter);
        toReporter.Carga.ToString().Should().NotContain(_reported);
        var toReported = notices.Single(e => e.Carga["rol"] == "reportado");
        toReported.Carga.ToString().Should().NotContain(_reporter);
    }
}
