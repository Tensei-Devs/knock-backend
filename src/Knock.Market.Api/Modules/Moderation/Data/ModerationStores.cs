using System.Globalization;
using Knock.Contracts.Moderation;
using MongoDB.Bson;
using MongoDB.Driver;
using StackExchange.Redis;

namespace Knock.Market.Api.Modules.Moderation.Data;

internal enum QuotaResult { Reserved, MonthlyExhausted, PersonExhausted, Unavailable }

/// <summary>Resultado de reservar una llamada; <see cref="MonthlyCount"/> permite avisar al pasar del 80 %.</summary>
internal readonly record struct QuotaReservation(QuotaResult Result, long MonthlyCount);

/// <summary>Decisión guardada en la caché (spec 031, R-10). <c>Unavailable</c> nunca se guarda.</summary>
internal sealed record CachedDecision(string ThresholdVersion, ModerationDecision Decision, IReadOnlyList<string> Categories);

internal enum CacheLookup { Hit, Miss, Unavailable }

/// <summary>Único punto con Redis de la moderación: caché de decisiones y topes atómicos (spec 031, R-10, R-11, R-12).</summary>
internal interface IConsumoModeracionStore
{
    Task<(CacheLookup Lookup, CachedDecision? Decision)> GetCachedAsync(string kind, string fingerprint, CancellationToken cancellationToken);

    Task SetCachedAsync(string kind, string fingerprint, CachedDecision decision, TimeSpan lifetime, CancellationToken cancellationToken);

    Task<QuotaReservation> ReserveAsync(string service, string user, DateOnly day, int monthlyLimit, int personLimit, CancellationToken cancellationToken);

    /// <summary>Marca (una vez al día) que ya se emitió el aviso del 80 %; verdadero solo la primera vez.</summary>
    Task<bool> TryMarkAlertAsync(string service, DateOnly day, CancellationToken cancellationToken);
}

internal sealed class ConsumoModeracionStore(IConnectionMultiplexer redis) : IConsumoModeracionStore
{
    private const string _reserveScript = """
        local m = tonumber(redis.call('GET', KEYS[1]) or '0')
        local u = tonumber(redis.call('GET', KEYS[2]) or '0')
        if m >= tonumber(ARGV[1]) then return {1, m} end
        if u >= tonumber(ARGV[2]) then return {2, m} end
        m = redis.call('INCR', KEYS[1])
        u = redis.call('INCR', KEYS[2])
        if redis.call('TTL', KEYS[1]) == -1 then redis.call('EXPIRE', KEYS[1], ARGV[3]) end
        if redis.call('TTL', KEYS[2]) == -1 then redis.call('EXPIRE', KEYS[2], ARGV[4]) end
        return {0, m}
        """;

    private static readonly int _monthSeconds = (int)TimeSpan.FromDays(40).TotalSeconds;
    private static readonly int _daySeconds = (int)TimeSpan.FromHours(48).TotalSeconds;

    public async Task<(CacheLookup Lookup, CachedDecision? Decision)> GetCachedAsync(string kind, string fingerprint, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var value = await redis.GetDatabase().StringGetAsync($"mod:{kind}:{fingerprint}");
            return value.IsNullOrEmpty ? (CacheLookup.Miss, null) : Parse(value.ToString());
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            return (CacheLookup.Unavailable, null);
        }
    }

    public async Task SetCachedAsync(string kind, string fingerprint, CachedDecision decision, TimeSpan lifetime, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(decision);
        cancellationToken.ThrowIfCancellationRequested();
        if (decision.Decision == ModerationDecision.Unavailable)
        {
            return;
        }

        try
        {
            var value = string.Create(CultureInfo.InvariantCulture, $"{decision.ThresholdVersion}|{(int)decision.Decision}|{string.Join(',', decision.Categories)}");
            await redis.GetDatabase().StringSetAsync($"mod:{kind}:{fingerprint}", value, lifetime);
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            // La caché solo ahorra cuota: si falla, la decisión sigue siendo válida.
        }
    }

    public async Task<QuotaReservation> ReserveAsync(string service, string user, DateOnly day, int monthlyLimit, int personLimit, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(service);
        ArgumentException.ThrowIfNullOrWhiteSpace(user);
        cancellationToken.ThrowIfCancellationRequested();
        var keys = new RedisKey[]
        {
            $"mod:consumo:{service}:{day.ToString("yyyyMM", CultureInfo.InvariantCulture)}",
            $"mod:usuario:{user}:{day.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}",
        };
        try
        {
            var reply = (RedisResult[]?)await redis.GetDatabase().ScriptEvaluateAsync(_reserveScript, keys, [monthlyLimit, personLimit, _monthSeconds, _daySeconds]);
            var status = (int)reply![0];
            var count = (long)reply[1];
            return status switch
            {
                0 => new QuotaReservation(QuotaResult.Reserved, count),
                1 => new QuotaReservation(QuotaResult.MonthlyExhausted, count),
                _ => new QuotaReservation(QuotaResult.PersonExhausted, count),
            };
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException or InvalidCastException or NullReferenceException)
        {
            return new QuotaReservation(QuotaResult.Unavailable, 0);
        }
    }

    public async Task<bool> TryMarkAlertAsync(string service, DateOnly day, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return await redis.GetDatabase().StringSetAsync($"mod:alerta80:{service}:{day.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}", "1", TimeSpan.FromHours(48), When.NotExists);
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            return false;
        }
    }

    private static (CacheLookup, CachedDecision?) Parse(string value)
    {
        var parts = value.Split('|');
        if (parts.Length != 3 || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var decision) || decision is not (1 or 2))
        {
            return (CacheLookup.Miss, null);
        }

        var categories = parts[2].Length == 0 ? [] : parts[2].Split(',');
        return (CacheLookup.Hit, new CachedDecision(parts[0], (ModerationDecision)decision, categories));
    }
}

/// <summary>Una fila de <c>uso_moderacion</c>: sin texto, sin imagen, sin huella y sin <c>sub</c> (spec 031, R-17).</summary>
internal sealed record ModerationUsage(string Service, ModerationContext Context, string Type, ModerationDecision Decision, ModerationSource? Source, IReadOnlyList<string> Categories, string? ReasonCode, int LatencyMs, DateTime CreatedAt);

internal interface IUsoModeracionStore
{
    Task SaveAsync(ModerationUsage usage, CancellationToken cancellationToken);
}

internal sealed class UsoModeracionStore(IMongoDatabase database) : IUsoModeracionStore
{
    public Task SaveAsync(ModerationUsage usage, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(usage);
        var doc = new BsonDocument
        {
            ["servicio"] = usage.Service,
            ["contexto"] = ModerationNames.Context(usage.Context),
            ["tipo"] = usage.Type,
            ["decision"] = ModerationNames.Decision(usage.Decision),
            ["origen"] = ModerationNames.Source(usage.Source),
            ["categorias"] = new BsonArray(usage.Categories),
            ["latenciaMs"] = usage.LatencyMs,
            ["creadoEn"] = usage.CreatedAt,
        };
        if (usage.ReasonCode is not null)
        {
            doc["reasonCode"] = usage.ReasonCode;
        }

        return database.GetCollection<BsonDocument>("uso_moderacion").InsertOneAsync(doc, cancellationToken: cancellationToken);
    }
}

/// <summary>Nombres estables (spec 031, §6) de contexto, decisión y origen para registros y métricas.</summary>
internal static class ModerationNames
{
    public static string Context(ModerationContext context) => context switch
    {
        ModerationContext.SolicitudDescripcion => "solicitud_descripcion",
        ModerationContext.CotizacionNota => "cotizacion_nota",
        ModerationContext.MensajeChat => "mensaje_chat",
        _ => "imagen_publica",
    };

    public static string Decision(ModerationDecision decision) => decision switch
    {
        ModerationDecision.Approved => "approved",
        ModerationDecision.Rejected => "rejected",
        _ => "unavailable",
    };

    public static string Source(ModerationSource? source) => source switch
    {
        ModerationSource.Azure => "azure",
        ModerationSource.LocalList => "lista_local",
        ModerationSource.Cache => "cache",
        _ => "ninguno",
    };
}
