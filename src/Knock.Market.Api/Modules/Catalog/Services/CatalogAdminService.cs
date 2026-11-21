using Knock.Market.Api.Modules.Catalog.Data;
using Knock.Market.Api.Modules.Catalog.Domain;
using Knock.Market.Api.Modules.Catalog.Dtos;

namespace Knock.Market.Api.Modules.Catalog.Services;

/// <summary>
/// Consultas del panel sobre oficios pedidos y lista de espera (spec 045, OFP-02 y LIS-02; HU-070). Solo lectura y solo conteos. Un cursor ilegible lanza 400 (<c>invalid_admin_cursor</c>). No toca MongoDB:
/// pasa por <see cref="CatalogAdminStore"/>.
/// </summary>
internal sealed class CatalogAdminService(CatalogAdminStore store)
{
    public async Task<TradeRequestPage> ListTradeRequestsAsync(AdminPageQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        (long, string)? after = string.IsNullOrEmpty(query.Cursor) ? null : AdminCursor.Decode(query.Cursor);
        var rows = await store.ListTradeRequestsAsync(after, query.Limite + 1, cancellationToken);
        var page = rows.Take(query.Limite).ToList();
        var next = rows.Count > query.Limite ? AdminCursor.Encode(page[^1].Count, page[^1].Text) : null;
        return new TradeRequestPage([.. page.Select(row => new TradeRequestGroup(row.Text, row.Count))], next);
    }

    public async Task<WaitlistZonePage> ListWaitlistZonesAsync(AdminPageQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var after = string.IsNullOrEmpty(query.Cursor) ? null : AdminCursor.Decode(query.Cursor).Key;
        var rows = await store.ListWaitlistZonesAsync(after, query.Limite + 1, cancellationToken);
        var page = rows.Take(query.Limite).ToList();
        var next = rows.Count > query.Limite ? AdminCursor.Encode(0, page[^1].PostalCode) : null;
        return new WaitlistZonePage([.. page.Select(row => new WaitlistZone(row.PostalCode, row.Count))], next);
    }
}
