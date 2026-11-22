using System.Reflection;
using System.Security.Claims;
using AwesomeAssertions;
using Knock.Market.Api.Common.Errors;
using Knock.Market.Api.Modules.Catalog.Controllers.V1;
using Knock.Market.Api.Modules.Catalog.Data;
using Knock.Market.Api.Modules.Catalog.Domain;
using Knock.Market.Api.Modules.Catalog.Dtos;
using Knock.Market.Api.Modules.Catalog.Services;
using Knock.Market.Api.Modules.Catalog.Validators;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Knock.UnitTest.Catalog;

/// <summary>Spec 045, T-06: <c>POST /api/v1/oficios-pedidos</c> (OFP-01), criterios CA-20 a CA-24.</summary>
public sealed partial class Spec045OficiosTests
{
    private const string _technician = "tecnico-1";

    private sealed class FakeRequestStore(params string[] technicians) : ITradeRequestStore
    {
        public List<(string User, string Text, string Normalized, string Id)> Saved { get; } = [];

        public Task<bool> TechnicianExistsAsync(string subject, CancellationToken cancellationToken) => Task.FromResult(technicians.Contains(subject));

        public Task<string> SaveAsync(string subject, string text, string normalizedText, DateTime createdAtUtc, CancellationToken cancellationToken)
        {
            var existing = Saved.FirstOrDefault(s => s.User == subject && s.Normalized == normalizedText);
            if (existing.Id is not null)
            {
                return Task.FromResult(existing.Id);
            }

            var id = (Saved.Count + 1).ToString("x24", System.Globalization.CultureInfo.InvariantCulture);
            Saved.Add((subject, text, normalizedText, id));
            return Task.FromResult(id);
        }
    }

    private static TradeRequestsController Requests(FakeRequestStore store, string? sub = _technician)
    {
        var identity = sub is null ? new ClaimsIdentity() : new ClaimsIdentity([new Claim("sub", sub)], "test");
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        return new TradeRequestsController(new TradeRequestService(store, new FixedClock())) { ControllerContext = Context(http) };
    }

    private static TradeRequestResponse Created(IActionResult result) =>
        (TradeRequestResponse)result.Should().BeOfType<ObjectResult>().Which.Value!;

    [Fact]
    public async Task TradeRequest_OfATechnician_Returns201AndSavesTheNormalizedForm_CA_045_20()
    {
        var store = new FakeRequestStore(_technician);

        var result = await Requests(store).Create(new TradeRequestBody("  Técnico   en A/C "), CancellationToken.None);

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(201);
        Created(result).Texto.Should().Be("Técnico en A/C");
        var saved = store.Saved.Should().ContainSingle().Subject;
        saved.User.Should().Be(_technician);
        saved.Normalized.Should().Be("tecnico en a c");
    }

    [Fact]
    public async Task TradeRequest_WithoutSub_Returns401AndSavesNothing_CA_045_21()
    {
        var store = new FakeRequestStore(_technician);

        var result = await Requests(store, sub: null).Create(new TradeRequestBody("Plomero"), CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
        store.Saved.Should().BeEmpty();
    }

    [Fact]
    public async Task TradeRequest_OfAnAccountWithoutTechnicianProfile_Throws404WithAUniformCode_CA_045_22()
    {
        var store = new FakeRequestStore(_technician);

        var act = async () => await Requests(store, sub: "cliente-1").Create(new TradeRequestBody("Plomero"), CancellationToken.None);

        var thrown = (await act.Should().ThrowAsync<DomainException>()).Which;
        thrown.Kind.Should().Be(DomainErrorKind.NotFound);
        thrown.Code.Should().Be(TradeErrorCodes.NotFoundCode, "el código no dice que el recurso exista");
        thrown.Message.Should().NotContain("cliente-1");
        store.Saved.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("   ")]
    [InlineData("Llámame al 7711234567")]
    [InlineData("plomero@correo.mx")]
    [InlineData("ver www.ejemplo.mx")]
    [InlineData("<script>alert(1)</script>")]
    public async Task TradeRequestBody_WithAnInvalidText_FailsWithoutRepeatingIt_CA_045_23(string? text)
    {
        var result = await new TradeRequestBodyValidator().ValidateAsync(new TradeRequestBody(text));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.PropertyName.Should().Be("Texto");
        result.Errors[0].ErrorMessage.Should().Be(CreateTradeBodyValidator.Reason);
    }

    [Fact]
    public async Task TradeRequestBody_WithMoreThanSixtyCharacters_IsRejected_AndSixtyIsAccepted_CA_045_23()
    {
        var validator = new TradeRequestBodyValidator();

        (await validator.ValidateAsync(new TradeRequestBody(new string('a', 61)))).IsValid.Should().BeFalse();
        (await validator.ValidateAsync(new TradeRequestBody(new string('a', 60)))).IsValid.Should().BeTrue();
        (await validator.ValidateAsync(new TradeRequestBody("ab"))).IsValid.Should().BeTrue();
    }

    [Fact]
    public void TheTradeRequestRoute_RequiresASession_HasABodyLimit_AndTheUnreadableBodyIs400ByTheFramework_CA_045_24()
    {
        // Un cuerpo ilegible lo rechaza el enlace de datos de [ApiController] con 400; la prueba HTTP real está en Spec045OficiosPedidosHttpTests.
        var create = typeof(TradeRequestsController).GetMethod("Create")!;

        typeof(TradeRequestsController).GetCustomAttribute<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>().Should().NotBeNull();
        create.GetCustomAttributes<RequestSizeLimitAttribute>().Should().ContainSingle();
        TradeRequestBodies.MaxBodyBytes.Should().Be(1024);
    }
}
