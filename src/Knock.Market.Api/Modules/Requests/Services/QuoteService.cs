using Knock.Market.Api.Modules.Requests.Domain;
using Knock.Market.Api.Modules.Requests.Validators;

namespace Knock.Market.Api.Modules.Requests.Services;

internal sealed record Technician(string UsuarioId, string Estado, bool Oculto, IReadOnlyList<string> Oficios,
    string? ColoniaBaseId, double? Latitude, double? Longitude, int RadioAtencionKm, string Id = "");

internal sealed record QuoteRequestSnapshot(string Trade, string ColoniaId, RequestState State,
    DateTimeOffset ExpiresAt, DateOnly Day, double? Latitude, double? Longitude);

internal interface ITechnicianDirectory
{
    Task<Technician?> FindBySubAsync(string sub, CancellationToken cancellationToken);
    Task<bool> ExistsAsync(string sub, CancellationToken cancellationToken);
}

internal interface IQuoteRequestReader
{
    Task<QuoteRequestSnapshot?> FindAsync(string id, CancellationToken cancellationToken);
}

internal enum QuoteCheckResult { NotATechnician, NotFound, RequestNotQuotable, Invalid, ModerationUnavailable, ContentRejected, QuoteDuplicate, QuoteQuotaFull, Created }

internal interface IQuoteWriter
{
    Task<QuoteCheckResult> SaveAsync(string requestId, string technicianId, string sub, string trade, QuoteValidationInput body, DateTimeOffset now, CancellationToken cancellationToken);
}

internal enum NoteModerationResult { Approved, Rejected, Unavailable }

internal interface INoteModerator
{
    Task<NoteModerationResult> ModerateAsync(string note, CancellationToken cancellationToken);
}

internal sealed class UnavailableNoteModerator : INoteModerator
{
    public Task<NoteModerationResult> ModerateAsync(string note, CancellationToken cancellationToken) => Task.FromResult(NoteModerationResult.Unavailable);
}

internal sealed class QuoteService(ITechnicianDirectory technicians, IQuoteRequestReader requests, QuoteValidator validator, TimeProvider clock, INoteModerator moderator, IQuoteWriter writer)
{
    public async Task<QuoteCheckResult> SendAsync(string sub, string solicitudId, QuoteValidationInput body, CancellationToken cancellationToken = default)
    {
        var technician = await technicians.FindBySubAsync(sub, cancellationToken);
        if (technician is null)
            return await technicians.ExistsAsync(sub, cancellationToken) ? QuoteCheckResult.NotFound : QuoteCheckResult.NotATechnician;
        var request = await requests.FindAsync(solicitudId, cancellationToken);
        if (request is null || technician.Estado != "activo" || technician.Oculto ||
            !technician.Oficios.Contains(request.Trade, StringComparer.Ordinal) || !Covers(technician, request))
            return QuoteCheckResult.NotFound;
        if (!RequestRules.AcceptsQuotes(request.State) || request.ExpiresAt <= clock.GetUtcNow())
            return QuoteCheckResult.RequestNotQuotable;
        if (!(await validator.ValidateAsync(body with { RequestDate = request.Day }, cancellationToken)).IsValid)
            return QuoteCheckResult.Invalid;
        if (body.Nota is not null)
        {
            var moderation = await moderator.ModerateAsync(body.Nota, cancellationToken);
            if (moderation == NoteModerationResult.Unavailable) return QuoteCheckResult.ModerationUnavailable;
            if (moderation != NoteModerationResult.Approved) return QuoteCheckResult.ContentRejected;
        }
        return await writer.SaveAsync(solicitudId, technician.Id, sub, request.Trade, body, clock.GetUtcNow(), cancellationToken);
    }

    private static bool Covers(Technician technician, QuoteRequestSnapshot request)
    {
        if (request.Latitude is null || request.Longitude is null)
            return technician.ColoniaBaseId == request.ColoniaId;
        if (technician.Latitude is null || technician.Longitude is null || technician.RadioAtencionKm is < 1 or > 30)
            return false;
        const double radians = Math.PI / 180;
        var a = Math.Pow(Math.Sin((request.Latitude.Value - technician.Latitude.Value) * radians / 2), 2)
            + Math.Cos(request.Latitude.Value * radians) * Math.Cos(technician.Latitude.Value * radians)
            * Math.Pow(Math.Sin((request.Longitude.Value - technician.Longitude.Value) * radians / 2), 2);
        return 12742 * Math.Asin(Math.Sqrt(Math.Clamp(a, 0, 1))) <= technician.RadioAtencionKm;
    }
}
