using Knock.Market.Api.Common.Errors;
using Knock.Market.Api.Modules.Catalog.Data;
using Knock.Market.Api.Modules.Catalog.Domain;
using Knock.Market.Api.Modules.Catalog.Dtos;

namespace Knock.Market.Api.Modules.Catalog.Services;

/// <summary>
/// «Otro oficio» de un técnico en registro (spec 045, OFP-01). La persona es siempre la del <c>sub</c> del token (R-23). Solo guarda el texto de quien ya tiene perfil de técnico; para cualquier otra
/// cuenta responde 404 uniforme (<see cref="TradeErrorCodes.NotFoundCode"/>) sin revelar que el recurso existe. No toca MongoDB: pasa por <see cref="ITradeRequestStore"/>.
/// </summary>
internal sealed class TradeRequestService(ITradeRequestStore store, TimeProvider clock)
{
    public async Task<TradeRequestResponse> CreateAsync(string subject, TradeRequestBody body, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(subject);
        ArgumentNullException.ThrowIfNull(body);
        if (!await store.TechnicianExistsAsync(subject, cancellationToken))
        {
            throw new DomainException(DomainErrorKind.NotFound, "The account has no technician profile.", TradeErrorCodes.NotFoundCode);
        }

        var text = string.Join(' ', body.Texto!.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        var id = await store.SaveAsync(subject, text, TradeRules.NormalizeRequestText(text), clock.GetUtcNow().UtcDateTime, cancellationToken);
        return new TradeRequestResponse(id, text);
    }
}
