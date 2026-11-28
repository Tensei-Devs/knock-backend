using System.Net;
using AwesomeAssertions;
using Knock.Contracts.Moderation;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.IntegrationTest.Requests;

/// <summary>
/// Spec 049, R-08 (motivo «Moderado»), KNOCK-266 (A1): el ajuste de precio usa el moderador del Market y no uno que siempre falla. A diferencia de <see cref="Spec049Host.StartAsync"/>,
/// aquí NO se reemplaza el moderador de notas: la decisión sale del moderador de contenido del host. MongoDB real y solo datos ficticios. Las corre el CI.
/// </summary>
public sealed class Spec049MotivoModeradoTests
{
    [Fact]
    public async Task Con_La_Moderacion_Disponible_El_Ajuste_Responde_201_Y_No_503()
    {
        await using var host = await Spec020Host.StartAsync();
        var visit = await host.InProgressAsync("fijo");
        host.Moderator.Outcome = ModerationOutcome.Allowed;

        using var response = await host.ProposeAdjustmentAsync(visit.QuoteId, visit.Technician.Sub);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await host.CountAsync("ajustes_precio", FilterDefinition<BsonDocument>.Empty)).Should().Be(1);
    }

    [Fact]
    public async Task Con_El_Motivo_Rechazado_Responde_422_Y_No_Guarda()
    {
        await using var host = await Spec020Host.StartAsync();
        var visit = await host.InProgressAsync("fijo");
        host.Moderator.Outcome = ModerationOutcome.Rejected;

        using var response = await host.ProposeAdjustmentAsync(visit.QuoteId, visit.Technician.Sub);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await Spec020Host.ReadAsync(response)).GetProperty("code").GetString().Should().Be("content_rejected");
        (await host.CountAsync("ajustes_precio", FilterDefinition<BsonDocument>.Empty)).Should().Be(0);
    }

    [Fact]
    public async Task Con_La_Moderacion_Caida_Sigue_Fallando_Cerrado_Con_503()
    {
        await using var host = await Spec020Host.StartAsync();
        var visit = await host.InProgressAsync("fijo");
        host.Moderator.Outcome = ModerationOutcome.Unavailable;

        using var response = await host.ProposeAdjustmentAsync(visit.QuoteId, visit.Technician.Sub);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await Spec020Host.ReadAsync(response)).GetProperty("code").GetString().Should().Be("moderation_unavailable");
        (await host.CountAsync("ajustes_precio", FilterDefinition<BsonDocument>.Empty)).Should().Be(0);
    }
}
