using AwesomeAssertions;
using Knock.Market.Api.Modules.Requests.Data;
using Knock.Market.Api.Modules.Requests.Dtos;
using Knock.Market.Api.Modules.Requests.Services;
using MongoDB.Bson;
using Xunit;

namespace Knock.UnitTest.Requests;

/// <summary>Spec 050 (campos derivados de la visita): reprogramación pendiente, estado de la cotización final y permiso de fotos se leen del documento sin cambiar nada. Datos ficticios.</summary>
public sealed class Spec050VisitFieldsTests
{
    private static readonly DateTime _now = new(2026, 11, 23, 12, 0, 0, DateTimeKind.Utc);

    private static VisitRecord Visit(RescheduleProposal? proposal, string state = "confirmada", bool locked = false) =>
        new(ObjectId.GenerateNewId(), ObjectId.GenerateNewId(), ObjectId.GenerateNewId(), "client", "technician", ObjectId.GenerateNewId(), "domicilio", "2026-11-24", "10:00",
            500, 600, state, null, null, false, 0, locked, null, Proposal: proposal);

    private static RescheduleProposal Proposal(TimeSpan expiresIn, string by = "cliente") => new(_now.AddDays(1), by, _now + expiresIn);

    [Theory]
    [InlineData("cliente", false)]
    [InlineData("tecnico", false)]
    [InlineData("cliente", true)]
    public void CurrentProposal_OfAConfirmedVisit_SaysWhenWhoAndUntilWhen_CA_050_46_CA_050_56(string by, bool locked)
    {
        var view = VisitService.CurrentProposal(Visit(Proposal(TimeSpan.FromHours(5), by), locked: locked), _now);

        view.Should().Be(new VisitRescheduleProposal(new DateTimeOffset(_now.AddDays(1), TimeSpan.Zero), by, new DateTimeOffset(_now.AddHours(5), TimeSpan.Zero)));
    }

    [Fact]
    public void CurrentProposal_WithoutAProposalOrExpired_IsNull_CA_050_46_CA_050_57()
    {
        VisitService.CurrentProposal(Visit(null), _now).Should().BeNull();
        VisitService.CurrentProposal(Visit(Proposal(TimeSpan.FromSeconds(-1))), _now).Should().BeNull();
        VisitService.CurrentProposal(Visit(Proposal(TimeSpan.Zero)), _now).Should().BeNull();
    }

    [Theory]
    [InlineData("en_curso")]
    [InlineData("terminada")]
    [InlineData("no_terminada")]
    [InlineData("cancelada")]
    [InlineData("no_presentado")]
    public void CurrentProposal_OutsideConfirmed_IsNullAndKeepsTheStoredOne_CA_050_58(string state)
    {
        var visit = Visit(Proposal(TimeSpan.FromHours(5)), state);

        VisitService.CurrentProposal(visit, _now).Should().BeNull();
        visit.Proposal.Should().NotBeNull();
    }

    [Fact]
    public void ToRecord_ReadsFinalQuoteStateAndPhotoPermission_CA_050_41()
    {
        var document = new BsonDocument
        {
            { "_id", ObjectId.GenerateNewId() }, { "solicitudId", ObjectId.GenerateNewId() }, { "cotizacionId", ObjectId.GenerateNewId() },
            { "clienteId", "client" }, { "tecnicoUsuarioId", "technician" }, { "tecnicoId", ObjectId.GenerateNewId() },
            { "modalidad", "taller" }, { "dia", "2026-11-24" }, { "hora", "10:00" }, { "tope", 600 }, { "estado", "terminada" },
            { "cotizacionFinal", new BsonDocument { { "estado", "enviada" } } }, { "permisoPublicarFotos", true },
        };

        var record = VisitStore.ToRecord(document);

        record.FinalQuoteState.Should().Be("enviada");
        record.PhotoPermission.Should().BeTrue();
        var bare = document.DeepClone().AsBsonDocument;
        bare.Remove("cotizacionFinal");
        bare.Remove("permisoPublicarFotos");
        var without = VisitStore.ToRecord(bare);
        without.FinalQuoteState.Should().BeNull();
        without.PhotoPermission.Should().BeNull();
    }
}
