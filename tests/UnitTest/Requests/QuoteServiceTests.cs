using AwesomeAssertions;
using Knock.Market.Api.Modules.Requests.Data;
using Knock.Market.Api.Modules.Requests.Domain;
using Knock.Market.Api.Modules.Requests.Services;
using Knock.Market.Api.Modules.Requests.Validators;
using Microsoft.Extensions.Options;
using Xunit;

namespace Knock.UnitTest.Requests;

public class QuoteServiceTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 13, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly _day = new(2026, 11, 13);
    private static readonly QuoteValidationInput _body = new("fijo", 100, null, null, _day, "12:00", null, _day);

    [Fact]
    public async Task MissingTechnicianIsForbidden()
    {
        var service = Create(null, Request());
        (await service.SendAsync("sub", "request", _body)).Should().Be(QuoteCheckResult.NotATechnician);
    }

    [Fact]
    public async Task ExistingButFilteredTechnicianIsNotFound()
    {
        var directory = new FakeDirectory(null, true);
        var service = new QuoteService(directory, new FakeRequests(Request()), new QuoteValidator(Options.Create(new QuotePricingOptions())), new FixedTimeProvider(), new FakeModerator(), new FakeWriter(), new FakeBlocks());
        (await service.SendAsync("sub", "request", _body)).Should().Be(QuoteCheckResult.NotFound);
    }

    [Theory]
    [InlineData("suspendido", false, "plo", true)]
    [InlineData("activo", true, "plo", true)]
    [InlineData("activo", false, "ele", true)]
    [InlineData("activo", false, "plo", false)]
    public async Task IneligibleTechnicianIsNotFound(string state, bool hidden, string trade, bool covered)
    {
        var service = Create(new Technician("sub", state, hidden, [trade], covered ? "colonia" : "other", null, null, 5), Request());
        (await service.SendAsync("sub", "request", _body)).Should().Be(QuoteCheckResult.NotFound);
    }

    [Fact]
    public async Task MissingRequestIsNotFound()
    {
        var service = Create(Technician(), null);
        (await service.SendAsync("sub", "request", _body)).Should().Be(QuoteCheckResult.NotFound);
    }

    [Theory]
    [InlineData("cancelada", false)]
    [InlineData("cita_confirmada", false)]
    [InlineData("enviada", true)]
    public async Task ClosedOrExpiredRequestIsNotQuotable(string state, bool expired)
    {
        var request = Request() with { State = RequestStates.ParseRequest(state), ExpiresAt = expired ? _now : _now.AddHours(1) };
        var service = Create(Technician(), request);
        (await service.SendAsync("sub", "request", _body)).Should().Be(QuoteCheckResult.RequestNotQuotable);
    }

    [Fact]
    public async Task EligibleValidQuotePassesPreliminaryChecks()
    {
        var service = Create(Technician(), Request());
        (await service.SendAsync("sub", "request", _body)).Should().Be(QuoteCheckResult.Created);
    }

    [Fact]
    public async Task InvalidBodyIsRejectedUsingStoredRequestDate()
    {
        var service = Create(Technician(), Request());
        (await service.SendAsync("sub", "request", _body with { Dia = _day.AddDays(8) })).Should().Be(QuoteCheckResult.Invalid);
    }

    [Fact]
    public async Task NoteWithUnavailableModeratorFailsClosed()
    {
        var moderator = new FakeModerator();
        var writer = new FakeWriter();
        var service = Create(Technician(), Request(), moderator, writer);
        (await service.SendAsync("sub", "request", _body with { Nota = "nota" })).Should().Be(QuoteCheckResult.ModerationUnavailable);
        moderator.Calls.Should().Be(1);
        writer.Calls.Should().Be((0));
    }

    [Fact]
    public async Task NoNoteDoesNotCallModerator()
    {
        var moderator = new FakeModerator();
        var service = Create(Technician(), Request(), moderator);
        (await service.SendAsync("sub", "request", _body)).Should().Be(QuoteCheckResult.Created);
        moderator.Calls.Should().Be(0);
    }

    [Fact]
    public async Task DefaultModeratorIsUnavailable()
    {
        (await new UnavailableNoteModerator().ModerateAsync("note", CancellationToken.None)).Should().Be(NoteModerationResult.Unavailable);
    }

    [Theory]
    [InlineData(19.0, 5, true)]
    [InlineData(19.04, 5, true)]
    [InlineData(19.1, 5, false)]
    public async Task CoverageUsesDistanceToNeighborhoodCenter(double latitude, int radius, bool covered)
    {
        var technician = Technician() with { ColoniaBaseId = "other", Latitude = 19, Longitude = -99, RadioAtencionKm = radius };
        var request = Request() with { Latitude = latitude, Longitude = -99 };
        (await Create(technician, request).SendAsync("sub", "request", _body)).Should().Be(covered ? QuoteCheckResult.Created : QuoteCheckResult.NotFound);
    }

    private static Technician Technician() => new("sub", "activo", false, ["plo"], "colonia", null, null, 5);
    private static QuoteRequestSnapshot Request() => new("plo", "colonia", RequestState.Enviada, _now.AddHours(1), _day, null, null);
    private static QuoteService Create(Technician? technician, QuoteRequestSnapshot? request, INoteModerator? moderator = null, IQuoteWriter? writer = null, IBlockLookup? blocks = null) =>
        new(new FakeDirectory(technician), new FakeRequests(request), new QuoteValidator(Options.Create(new QuotePricingOptions())), new FixedTimeProvider(), moderator ?? new FakeModerator(), writer ?? new FakeWriter(), blocks ?? new FakeBlocks());

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task BlockedPairGetsTheSameAnswerAsAnUnavailableTechnician(bool blocked)
    {
        var writer = new FakeWriter();
        var service = Create(Technician(), Request() with { ClientId = "client" }, writer: writer, blocks: new FakeBlocks(blocked));
        (await service.SendAsync("sub", "request", _body)).Should().Be(blocked ? QuoteCheckResult.NotFound : QuoteCheckResult.Created);
        writer.Calls.Should().Be(blocked ? 0 : 1);
    }

    private sealed class FakeBlocks(bool blocked = false) : IBlockLookup
    {
        public Task<bool> AreBlockedAsync(string personA, string personB, CancellationToken token) => Task.FromResult(blocked);

        public Task<IReadOnlySet<string>> BlockedWithAsync(string person, IEnumerable<string> others, CancellationToken token) =>
            Task.FromResult<IReadOnlySet<string>>(blocked ? others.ToHashSet() : new HashSet<string>());
    }

    private sealed class FakeWriter : IQuoteWriter
    {
        public int Calls { get; private set; }
        public Task<QuoteCheckResult> SaveAsync(string requestId, string technicianId, string sub, string trade, QuoteValidationInput body, DateTimeOffset now, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(QuoteCheckResult.Created);
        }
    }

    private sealed class FakeModerator : INoteModerator
    {
        public int Calls { get; private set; }
        public Task<NoteModerationResult> ModerateAsync(string note, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(NoteModerationResult.Unavailable);
        }
    }

    private sealed class FakeDirectory(Technician? technician, bool exists = false) : ITechnicianDirectory
    {
        public Task<Technician?> FindBySubAsync(string sub, CancellationToken cancellationToken) => Task.FromResult(technician);
        public Task<Technician?> FindEligibleAsync(string sub, QuoteRequestSnapshot request, CancellationToken cancellationToken) => Task.FromResult(technician);
        public Task<bool> ExistsAsync(string sub, CancellationToken cancellationToken) => Task.FromResult(exists || technician is not null);
    }

    private sealed class FakeRequests(QuoteRequestSnapshot? request) : IQuoteRequestReader
    {
        public Task<QuoteRequestSnapshot?> FindAsync(string id, CancellationToken cancellationToken) => Task.FromResult(request);
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => _now;
    }
}
