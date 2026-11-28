using AwesomeAssertions;
using Knock.Market.Api.Common.Errors;
using Knock.Market.Api.Modules.Catalog.Controllers.V1;
using Knock.Market.Api.Modules.Catalog.Domain;
using Knock.Market.Api.Modules.Catalog.Dtos;
using Knock.Market.Api.Modules.Catalog.Services;
using Knock.Market.Api.Modules.Catalog.Validators;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Knock.UnitTest.Catalog;

/// <summary>Spec 045, T-02: <c>GET /api/v1/oficios/{clave}</c> (OFI-02), criterios CA-01 a CA-04.</summary>
public sealed partial class Spec045OficiosTests
{
    private static OficiosController Public(FakeActiveOficios source) => new(new OficioQueries(source)) { ControllerContext = Context() };

    [Fact]
    public async Task Detail_OfAnActiveTrade_ReturnsItsFields_AndNoPriceParametersOrFlags_CA_045_01()
    {
        var controller = Public(new FakeActiveOficios(Mecanica(), Plomeria()));

        var detail = Ok<TradeDetailResponse>(await controller.Detail(new TradeKeyRoute { Clave = "plomeria" }, null, CancellationToken.None));

        detail.Should().BeEquivalentTo(new TradeDetailResponse("plomeria", "Plomería", "domicilio", ["Fuga en el baño", "Se tapó el drenaje"]));
        typeof(TradeDetailResponse).GetProperties().Select(p => p.Name).Should().BeEquivalentTo("Clave", "Nombre", "Modalidad", "FrasesSugeridas");
        controller.Response.Headers.CacheControl.ToString().Should().Be("public, max-age=0, must-revalidate");
    }

    [Fact]
    public async Task Detail_OfATradeWithoutSuggestedPhrases_ReturnsAnEmptyList_CA_045_01()
    {
        var detail = Ok<TradeDetailResponse>(await Public(new FakeActiveOficios(Mecanica())).Detail(new TradeKeyRoute { Clave = "mecanica" }, null, CancellationToken.None));

        detail.FrasesSugeridas.Should().BeEmpty();
    }

    [Fact]
    public async Task Detail_WithAMatchingETag_Returns304()
    {
        var controller = Public(new FakeActiveOficios(Plomeria()));
        await controller.Detail(new TradeKeyRoute { Clave = "plomeria" }, null, CancellationToken.None);
        var etag = controller.Response.Headers.ETag.ToString();

        var second = Public(new FakeActiveOficios(Plomeria()));
        var result = await second.Detail(new TradeKeyRoute { Clave = "plomeria" }, etag, CancellationToken.None);

        result.Should().BeOfType<StatusCodeResult>().Which.StatusCode.Should().Be(StatusCodes.Status304NotModified);
    }

    [Theory]
    [InlineData("Plomeria")]
    [InlineData("con espacios")]
    [InlineData("")]
    [InlineData("clave-demasiado-larga-xx")]
    [InlineData("a;b")]
    [InlineData("ñandu")]
    public async Task KeyRoute_WithAnInvalidKey_FailsWithoutRepeatingTheValue_CA_045_02(string key)
    {
        var result = await new TradeKeyRouteValidator().ValidateAsync(new TradeKeyRoute { Clave = key });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.PropertyName.Should().Be("Clave");
        result.Errors[0].ErrorMessage.Should().NotContain(key.Length == 0 ? "\0" : key);
    }

    [Theory]
    [InlineData("plomeria")]
    [InlineData("a")]
    [InlineData("aire_acond-1")]
    public async Task KeyRoute_WithAValidKey_Passes_CA_045_02(string key) =>
        (await new TradeKeyRouteValidator().ValidateAsync(new TradeKeyRoute { Clave = key })).IsValid.Should().BeTrue();

    [Fact]
    public async Task Detail_OfAnUnknownOrInactiveTrade_Throws404WithTheBusinessCode_CA_045_03()
    {
        // El almacén solo trae oficios activos: uno inactivo y uno inexistente son lo mismo para esta ruta.
        var controller = Public(new FakeActiveOficios(Plomeria()));

        var act = async () => await controller.Detail(new TradeKeyRoute { Clave = "electricidad" }, null, CancellationToken.None);

        var thrown = (await act.Should().ThrowAsync<DomainException>()).Which;
        thrown.Kind.Should().Be(DomainErrorKind.NotFound);
        thrown.Code.Should().Be(TradeErrorCodes.TradeNotFoundCode);
        thrown.Message.Should().NotContain("electricidad");
    }

    [Fact]
    public async Task Detail_ReadsTheCachedActiveList_SoOneLookupPerRequestAndNoOtherSource_CA_045_04()
    {
        // La caché de 24 horas es la de la lista de oficios activos (CatalogStore); la prueba de base de datos CA_045_04 verifica que la segunda lectura no llega a MongoDB.
        var source = new FakeActiveOficios(Plomeria());
        var controller = Public(source);

        await controller.Detail(new TradeKeyRoute { Clave = "plomeria" }, null, CancellationToken.None);

        source.Reads.Should().Be(1);
    }
}
