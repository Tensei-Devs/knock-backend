using AwesomeAssertions;
using Knock.Market.Api.Modules.Requests.Data;
using Knock.Market.Api.Modules.Requests.Services;
using MongoDB.Bson;
using Xunit;

namespace Knock.UnitTest.Requests;

/// <summary>Spec 050 (campos derivados de la visita): reprogramación pendiente, estado de la cotización final y permiso de fotos se leen del documento sin cambiar nada. Datos ficticios.</summary>
public sealed class Spec050VisitFieldsTests
{
    private static readonly DateTime _now = new(2026, 11, 23, 12, 0, 0, DateTimeKind.Utc);

    private static VisitRecord Visit(RescheduleProposal? proposal) =>
        new(ObjectId.GenerateNewId(), ObjectId.GenerateNewId(), ObjectId.GenerateNewId(), "client", "technician", ObjectId.GenerateNewId(), "domicilio", "2026-11-24", "10:00",
            500, 600, "confirmada", null, null, false, 0, false, null, Proposal: proposal);

    [Fact]
    public void PendingReschedule_IsTrueOnlyForAProposalThatHasNotExpired_CA_050_46()
    {
        VisitService.PendingReschedule(Visit(null), _now).Should().BeFalse();
        VisitService.PendingReschedule(Visit(new RescheduleProposal(_now.AddDays(1), "cliente", _now.AddHours(5))), _now).Should().BeTrue();
        VisitService.PendingReschedule(Visit(new RescheduleProposal(_now.AddDays(1), "cliente", _now.AddSeconds(-1))), _now).Should().BeFalse();
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
