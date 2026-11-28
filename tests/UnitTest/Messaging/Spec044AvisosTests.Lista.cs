extern alias messaging;

using AwesomeAssertions;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using Xunit;
using Domain = messaging::Knock.Messaging.Api.Modules.Notices.Domain;
using Dtos = messaging::Knock.Messaging.Api.Modules.Notices.Dtos;
using Validators = messaging::Knock.Messaging.Api.Modules.Notices.Validators;

namespace Knock.UnitTest.Messaging;

/// <summary>Spec 044, T-02: <c>GET /api/v1/avisos</c> (AVI-01), criterios CA-01 a CA-04.</summary>
public sealed partial class Spec044AvisosTests
{
    [Fact]
    public async Task List_ReturnsOnlyTheCallersNoticesNewestFirst_CA_044_01()
    {
        var store = StoreWith(3, others: 2);

        var list = ListOf(await Avisos(store).List(new Dtos.ListNoticesQuery(), CancellationToken.None));

        list.Items.Should().HaveCount(3);
        list.Items.Select(n => n.Id).Should().Equal(Id(3), Id(2), Id(1));
        list.SiguienteCursor.Should().BeNull();
    }

    [Fact]
    public async Task List_WithoutSub_ReturnsUnauthorizedAndTouchesNothing_CA_044_02()
    {
        var store = StoreWith(2);

        var result = await Avisos(store, sub: null).List(new Dtos.ListNoticesQuery(), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
        store.LastUser.Should().BeNull();
    }

    [Theory]
    [InlineData("limite", "00")]
    [InlineData("limite", "51")]
    [InlineData("limite", "abc")]
    [InlineData("limite", "-77")]
    [InlineData("leido", "quizas")]
    [InlineData("comoRol", "admin")]
    [InlineData("cursor", "no-es-cursor")]
    public async Task ListQuery_WithAnInvalidValue_FailsWithoutRepeatingIt_CA_044_03(string field, string value)
    {
        var query = new Dtos.ListNoticesQuery
        {
            Limite = field == "limite" ? value : null,
            Leido = field == "leido" ? value : null,
            ComoRol = field == "comoRol" ? value : null,
            Cursor = field == "cursor" ? value : null,
        };

        var result = await new Validators.ListNoticesQueryValidator().ValidateAsync(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.PropertyName.Should().Be(char.ToUpperInvariant(field[0]) + field[1..]);
        result.Errors[0].ErrorMessage.Should().NotContain(value);
    }

    [Fact]
    public async Task List_WithoutLimit_ReturnsTwentyAndTheNextCursor_AndLimit51IsRejected_CA_044_04()
    {
        var store = StoreWith(25);

        var first = ListOf(await Avisos(store).List(new Dtos.ListNoticesQuery(), CancellationToken.None));
        var second = ListOf(await Avisos(store).List(new Dtos.ListNoticesQuery { Limite = "50" }, CancellationToken.None));
        var rejected = await new Validators.ListNoticesQueryValidator().ValidateAsync(new Dtos.ListNoticesQuery { Limite = "51" });

        first.Items.Should().HaveCount(20);
        first.SiguienteCursor.Should().NotBeNullOrEmpty();
        Domain.NoticeCursor.TryDecode(first.SiguienteCursor, out var cursor).Should().BeTrue();
        cursor.Id.Should().Be(first.Items[^1].Id);
        second.Items.Should().HaveCount(25);
        second.SiguienteCursor.Should().BeNull();
        rejected.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Cursor_RoundTrips_AndRejectsGarbage_CA_044_03()
    {
        var cursor = new Domain.NoticeCursor(1_780_000_000_000, "507f1f77bcf86cd799439011");

        Domain.NoticeCursor.TryDecode(cursor.Encode(), out var decoded).Should().BeTrue();
        decoded.Should().Be(cursor);
        Domain.NoticeCursor.TryDecode("###", out _).Should().BeFalse();
        Domain.NoticeCursor.TryDecode(Convert.ToBase64String("1.no-es-id"u8.ToArray()), out _).Should().BeFalse();
        Domain.NoticeCursor.TryDecode(new string('a', 200), out _).Should().BeFalse();
    }

    [Fact]
    public void Notice_ResponseCarriesTheTargetAsAPlainObject_CA_044_01()
    {
        var target = new BsonDocument { ["ruta"] = "/cotizaciones", ["solicitudId"] = "507f1f77bcf86cd799439011" };

        var mapped = BsonTypeMapper.MapToDotNetValue(target);

        mapped.Should().BeAssignableTo<IDictionary<string, object>>();
    }
}
