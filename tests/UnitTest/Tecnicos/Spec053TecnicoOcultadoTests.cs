using AwesomeAssertions;
using Knock.Market.Api.Modules.Tecnicos.Data;
using Knock.Market.Api.Modules.Tecnicos.Dtos;
using Knock.Market.Api.Modules.Tecnicos.Services;
using Knock.Market.Api.Modules.Tecnicos.Validators;
using Xunit;

namespace Knock.UnitTest.Tecnicos;

/// <summary>Spec 053 R-20: el consumidor de <c>TecnicoOcultado</c> en Mercado (servicio y validador del cuerpo), sin MongoDB.</summary>
public sealed class Spec053TecnicoOcultadoTests
{
    private sealed class FakeStore : ITechnicianModerationStore
    {
        public List<(string Usuario, string Reporte, DateTime Now)> Calls { get; } = [];

        public bool Result { get; set; } = true;

        public Task<bool> HideAsync(string usuarioId, string reporteId, DateTime nowUtc, CancellationToken cancellationToken)
        {
            Calls.Add((usuarioId, reporteId, nowUtc));
            return Task.FromResult(Result);
        }
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    [Fact]
    public async Task Hide_Passes_The_Identifiers_And_The_Clock_To_The_Store_R_20()
    {
        var store = new FakeStore();
        var now = new DateTimeOffset(2026, 11, 22, 10, 0, 0, TimeSpan.Zero);

        var changed = await new TechnicianHideService(store, new FixedClock(now)).HideAsync("sub-1", "rep-1", CancellationToken.None);

        changed.Should().BeTrue();
        store.Calls.Should().ContainSingle().Which.Should().Be(("sub-1", "rep-1", now.UtcDateTime));
    }

    [Fact]
    public async Task Hide_Reports_No_Change_When_The_Store_Finds_It_Already_Hidden_R_20()
    {
        var store = new FakeStore { Result = false };

        (await new TechnicianHideService(store, TimeProvider.System).HideAsync("sub-1", "rep-1", CancellationToken.None)).Should().BeFalse();
    }

    [Theory]
    [InlineData("", "sub-1", "rep-1")]
    [InlineData("evt-1", null, "rep-1")]
    [InlineData("evt-1", "sub 1", "rep-1")]
    [InlineData("evt-1", "sub-1", "rep\n1")]
    [InlineData("evt-1", "sub-1", "")]
    public void The_Validator_Rejects_Missing_Or_Malformed_Identifiers_R_20(string? eventId, string? user, string? report)
    {
        var result = new TechnicianHiddenEventBodyValidator().Validate(new TechnicianHiddenEventBody { EventoId = eventId, UsuarioId = user, ReporteId = report });

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void The_Validator_Rejects_An_Identifier_Over_The_Limit_And_Accepts_A_Normal_Body_R_20()
    {
        var validator = new TechnicianHiddenEventBodyValidator();

        validator.Validate(new TechnicianHiddenEventBody { EventoId = "ReporteX:1", UsuarioId = new string('a', 129), ReporteId = "rep-1" }).IsValid.Should().BeFalse();
        validator.Validate(new TechnicianHiddenEventBody { EventoId = "TecnicoOcultado:rep-1:1", UsuarioId = "7b484abb-e9e0-8104-a08d-d30ce15e8148", ReporteId = "507f1f77bcf86cd799439011" }).IsValid.Should().BeTrue();
    }
}
