using System.Security.Cryptography;
using System.Text.Json;
using Knock.Market.Api.Modules.Catalog.Data;
using Knock.Market.Api.Modules.Catalog.Domain;

namespace Knock.Market.Api.Modules.Catalog.Services;

internal sealed record SearchContext(bool ColoniaExists, double? Latitude, double? Longitude, int Version,
    IReadOnlyList<SearchTrade> Trades, IReadOnlyList<SearchSynonym> Synonyms);
internal sealed record SearchInput(string Query, string ColoniaId, string? Trade, bool VerifiedOnly, int Limit, string? Cursor);
internal sealed record PublicSearchItem(string Id, string Name, IReadOnlyList<SearchTrade> Trades, decimal Rating, int Reviews,
    bool Verified, string Verification, string? BaseColonia, int? DistanceKm, bool ServesColonia, string? PhotoUrl);
internal sealed record SearchResponse(SearchInterpretation Interpretation, IReadOnlyList<PublicSearchItem> Items, string? NextCursor);
internal sealed record SearchServiceResult(SearchResponse? Response, string? Error);

internal interface IBusquedaSource
{
    Task<SearchContext?> GetContextAsync(string colonia, CancellationToken token);
    Task<IReadOnlyList<RankedCandidate>?> FindPageAsync(SearchPageRequest query, SearchRank? after, int limit, CancellationToken token);
    Task<bool> CountAsync(string term, DateTimeOffset now, CancellationToken token);
}

internal sealed class BusquedaService(IBusquedaSource source, ISearchResponseCache cache, IFiltroDatosPersonales filter, TimeProvider clock, byte[] cursorKey)
{
    public async Task<SearchServiceResult> SearchAsync(SearchInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        var context = await source.GetContextAsync(input.ColoniaId, cancellationToken);
        if (context is null) return new(null, "search_unavailable");
        if (!context.ColoniaExists) return new(null, "colonia_not_found");
        var selected = input.Trade is null ? null : context.Trades.FirstOrDefault(t => t.Key == input.Trade);
        if (input.Trade is not null && selected is null) return new(null, "trade_invalid");
        var normalized = SearchNameNormalizer.NormalizeQuery(input.Query);
        var query = new SearchQuery(normalized, input.ColoniaId, input.Trade, input.VerifiedOnly, context.Version);
        SearchCursorPayload? state;
        try { state = input.Cursor is null ? null : CursorDeBusqueda.Read(input.Cursor, query, cursorKey); }
        catch (FormatException) { return new(null, "invalid_cursor"); }
        if (input.Limit is < 1 or > 20) return new(null, "invalid_limit");
        var hash = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { query, input.Cursor, input.Limit }));
        var cached = await cache.GetSearchAsync(hash, cancellationToken);
        if (cached is not null)
        {
            try
            {
                if (JsonSerializer.Deserialize<SearchResponse>(cached) is { } response) return new(response, null);
            }
            catch (JsonException) { /* Treat malformed cache entries as misses. */ }
        }

        return await SearchUncachedAsync(input, context, selected, normalized, query, state, hash, cancellationToken);
    }

    private async Task<SearchServiceResult> SearchUncachedAsync(SearchInput input, SearchContext context, SearchTrade? selected,
        string normalized, SearchQuery query, SearchCursorPayload? state, byte[] hash, CancellationToken cancellationToken)
    {
        var interpretation = InterpretadorDeConsulta.Interpret(normalized, context.Trades, context.Synonyms, selected);
        var activeTrade = interpretation.Detected?.Key;
        var request = new SearchPageRequest(input.ColoniaId, context.Latitude, context.Longitude, normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries),
            activeTrade, context.Trades.Select(t => t.Key).ToArray(), input.VerifiedOnly);
        var available = Math.Min(input.Limit, 100 - (state?.Count ?? 0));
        var ranked = await source.FindPageAsync(request, state?.Last, available + 1, cancellationToken);
        if (ranked is null) return new(null, "search_unavailable");
        var page = ranked.Take(available).ToArray();
        var count = (state?.Count ?? 0) + page.Length;
        var next = ranked.Count > available && count < 100 ? CursorDeBusqueda.CreateNext(query, page[^1].Rank, count, cursorKey) : null;
        if (page.Length == 0 && state is null && interpretation.State == "no_entendido" && !HasSevenDigits(input.Query) && filter.CanCount(input.Query) &&
            !await source.CountAsync(normalized, clock.GetUtcNow(), cancellationToken)) return new(null, "search_unavailable");
        var items = page.Select(item => Map(item.Candidate, context.Trades)).ToArray();
        var response = new SearchResponse(interpretation, items, next);
        await cache.SetSearchAsync(hash, JsonSerializer.Serialize(response), cancellationToken);
        return new(response, null);
    }

    private static PublicSearchItem Map(SearchCandidate item, IReadOnlyList<SearchTrade> trades) => new(
        item.Id, item.Name, trades.Where(t => item.Trades.Contains(t.Key, StringComparer.Ordinal)).ToArray(), item.Rating,
        item.Reviews, item.Verified, item.Verification, item.BaseColonia,
        item.DistanceKm is null ? null : Math.Max(1, (int)Math.Round(item.DistanceKm.Value, MidpointRounding.AwayFromZero)),
        item.ServesColonia, item.PhotoUrl);

    private static bool HasSevenDigits(string input)
    {
        var count = 0;
        foreach (var c in input)
        {
            count = char.IsAsciiDigit(c) ? count + 1 : 0;
            if (count >= 7) return true;
        }
        return false;
    }
}
