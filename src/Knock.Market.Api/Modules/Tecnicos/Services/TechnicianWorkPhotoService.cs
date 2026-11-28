using System.Globalization;
using Knock.Market.Api.Modules.Tecnicos.Dtos;
using Microsoft.Extensions.Caching.Memory;
using MongoDB.Bson;

namespace Knock.Market.Api.Modules.Tecnicos.Services;

internal interface IWorkPhotoReader
{
    /// <summary>Fotos publicadas del técnico (con permiso del cliente y moderadas), más recientes primero, hasta <paramref name="limit"/> + 1 para saber si hay más.</summary>
    Task<IReadOnlyList<WorkPhotoResponse>> ListAsync(ObjectId technicianId, ObjectId? after, int limit, CancellationToken cancellationToken);
}

/// <summary>
/// Fotos de trabajos (spec 046, TEC-07, R-44): solo las de trabajos con permiso del cliente (spec 050, VIS-11) que pasaron la moderación. Un técnico que no existe o está
/// oculto responde como no encontrado (<see langword="null"/>). Cada página se guarda 1 minuto en la caché de la réplica con una clave que solo lleva el técnico, el cursor
/// y el límite (R-26); quien publique o retire fotos llama a <see cref="Forget"/>.
/// </summary>
internal sealed class TechnicianWorkPhotoService(TechnicianProfileService profiles, IWorkPhotoReader reader, IMemoryCache cache)
{
    public static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(1);

    private readonly HashSet<string> _keys = [];
    private readonly object _keysLock = new();

    public static string CacheKey(ObjectId id, string? cursor, int limit) => $"tecnicos:fotos:{id}:{cursor ?? string.Empty}:{limit}";

    public async Task<WorkPhotoListResponse?> ListAsync(ObjectId id, WorkPhotoQuery query, CancellationToken cancellationToken)
    {
        if (await profiles.GetAsync(id, withDetail: false, cancellationToken) is null)
        {
            return null;
        }

        var limit = int.TryParse(query.Limite, NumberStyles.Integer, CultureInfo.InvariantCulture, out var requested) ? requested : WorkPhotoQuery.DefaultLimit;
        var key = CacheKey(id, query.Cursor, limit);
        if (cache.TryGetValue(key, out WorkPhotoListResponse? cached) && cached is not null)
        {
            return cached;
        }

        var found = await reader.ListAsync(id, query.Cursor is null ? null : ObjectId.Parse(query.Cursor), limit, cancellationToken);
        var page = found.Take(limit).ToList();
        var response = new WorkPhotoListResponse(page, found.Count > limit ? page[^1].Id : null);
        cache.Set(key, response, CacheLifetime);
        lock (_keysLock)
        {
            _keys.Add(key);
        }

        return response;
    }

    /// <summary>Quita de la caché de esta réplica las páginas de fotos del técnico (R-26: se invalida al escribir).</summary>
    public void Forget(ObjectId id)
    {
        var prefix = $"tecnicos:fotos:{id}:";
        lock (_keysLock)
        {
            foreach (var key in _keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList())
            {
                cache.Remove(key);
                _keys.Remove(key);
            }
        }
    }
}
