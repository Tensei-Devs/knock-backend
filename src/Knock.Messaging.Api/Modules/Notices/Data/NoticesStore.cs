using Knock.Messaging.Api.Hosting;
using Knock.Messaging.Api.Modules.Notices.Domain;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Knock.Messaging.Api.Modules.Notices.Data;

/// <summary>Un aviso guardado (spec 044 §6). <c>UserId</c> es el <c>sub</c> del token: Mensajería no lee la colección <c>usuarios</c> de Identidad.</summary>
internal sealed record StoredNotice(string Id, string Type, string Role, string Title, string Text, BsonDocument? Target, bool Read, DateTime CreatedAt);

internal sealed record NoticePage(IReadOnlyList<StoredNotice> Items, NoticeCursor? Next);

/// <summary>Lo que los avisos necesitan de MongoDB; existe para probar el servicio con dobles sin base de datos.</summary>
internal interface INoticesStore
{
    Task<NoticePage> ListAsync(string userId, bool? read, string? role, NoticeCursor? after, int limit, CancellationToken cancellationToken);

    /// <summary>Cantidad de avisos sin leer de la persona, contada hasta <paramref name="cap"/>.</summary>
    Task<int> CountUnreadAsync(string userId, int cap, CancellationToken cancellationToken);

    /// <summary><see langword="false"/> si el aviso no existe o no es de la persona (no se distingue).</summary>
    Task<bool> MarkReadAsync(string userId, string noticeId, CancellationToken cancellationToken);

    Task MarkAllReadAsync(string userId, CancellationToken cancellationToken);

    /// <summary>Lo que la persona guardó por tipo, o <see langword="null"/> si nunca cambió nada.</summary>
    Task<IReadOnlyDictionary<string, bool>?> GetPreferencesAsync(string userId, CancellationToken cancellationToken);
}

/// <summary>
/// Colecciones <c>avisos</c> y <c>preferencias_avisos</c> (spec 044 §6). Único punto del módulo con <c>IMongoCollection</c>: los filtros son tipados y toda lectura o escritura lleva el
/// <c>usuarioId</c> del token, así un aviso ajeno no existe para quien lo pide (R-12, R-15). Nada se cachea. Este módulo nunca crea avisos (R-14): solo el despachador de la spec 037.
/// </summary>
internal sealed class NoticesStore(
    [FromKeyedServices(DataExtensions.DatabaseKey)] IMongoDatabase database,
    IOptions<KnockMongoOptions> mongoOptions) : INoticesStore
{
    public const string NoticesCollection = "avisos";
    public const string PreferencesCollection = "preferencias_avisos";
    private const string _idField = "_id";
    private const string _userField = "usuarioId";
    private const string _roleField = "comoRol";
    private const string _readField = "leido";
    private const string _createdField = "creadoEn";
    private static readonly FilterDefinitionBuilder<BsonDocument> _filter = Builders<BsonDocument>.Filter;

    private readonly FindOptions _findOptions = new() { MaxTime = mongoOptions.Value.QueryMaxTime };

    private IMongoCollection<BsonDocument> Notices { get; } = database.GetCollection<BsonDocument>(NoticesCollection);

    private IMongoCollection<BsonDocument> Preferences { get; } = database.GetCollection<BsonDocument>(PreferencesCollection);

    public async Task<NoticePage> ListAsync(string userId, bool? read, string? role, NoticeCursor? after, int limit, CancellationToken cancellationToken)
    {
        var filters = new List<FilterDefinition<BsonDocument>> { _filter.Eq(_userField, userId) };
        if (read is { } value)
        {
            filters.Add(_filter.Eq(_readField, value));
        }

        if (!string.IsNullOrEmpty(role))
        {
            filters.Add(_filter.Eq(_roleField, role));
        }

        if (after is { } cursor)
        {
            var date = DateTimeOffset.FromUnixTimeMilliseconds(cursor.CreatedAtMs).UtcDateTime;
            filters.Add(_filter.Or(
                _filter.Lt(_createdField, date),
                _filter.And(_filter.Eq(_createdField, date), _filter.Lt(_idField, ObjectId.Parse(cursor.Id)))));
        }

        var documents = await Notices.Find(_filter.And(filters), _findOptions)
            .Sort(Builders<BsonDocument>.Sort.Descending(_createdField).Descending(_idField))
            .Limit(limit + 1)
            .ToListAsync(cancellationToken);
        var items = documents.Take(limit).Select(ToStored).ToList();
        NoticeCursor? next = documents.Count > limit && items.Count > 0
            ? new NoticeCursor(new DateTimeOffset(items[^1].CreatedAt, TimeSpan.Zero).ToUnixTimeMilliseconds(), items[^1].Id)
            : null;
        return new NoticePage(items, next);
    }

    public async Task<int> CountUnreadAsync(string userId, int cap, CancellationToken cancellationToken) =>
        (int)await Notices.CountDocumentsAsync(
            _filter.And(_filter.Eq(_userField, userId), _filter.Eq(_readField, false)),
            new CountOptions { Limit = cap, MaxTime = mongoOptions.Value.QueryMaxTime },
            cancellationToken);

    public async Task<bool> MarkReadAsync(string userId, string noticeId, CancellationToken cancellationToken)
    {
        var filter = _filter.And(_filter.Eq(_idField, ObjectId.Parse(noticeId)), _filter.Eq(_userField, userId));
        var result = await Notices.UpdateOneAsync(filter, Builders<BsonDocument>.Update.Set(_readField, true), cancellationToken: cancellationToken);

        // Ya leído (idempotente, R-12): existe aunque no haya cambiado nada. Con el mismo filtro, ajeno e inexistente no se distinguen.
        return result.MatchedCount > 0;
    }

    public Task MarkAllReadAsync(string userId, CancellationToken cancellationToken) =>
        Notices.UpdateManyAsync(
            _filter.And(_filter.Eq(_userField, userId), _filter.Eq(_readField, false)),
            Builders<BsonDocument>.Update.Set(_readField, true),
            cancellationToken: cancellationToken);

    public async Task<IReadOnlyDictionary<string, bool>?> GetPreferencesAsync(string userId, CancellationToken cancellationToken)
    {
        var document = await Preferences.Find(_filter.Eq(_userField, userId), _findOptions).Limit(1).FirstOrDefaultAsync(cancellationToken);
        if (document is null || !document.TryGetValue("tipos", out var types) || !types.IsBsonDocument)
        {
            return null;
        }

        return types.AsBsonDocument.Where(e => e.Value.IsBoolean).ToDictionary(e => e.Name, e => e.Value.AsBoolean, StringComparer.Ordinal);
    }

    /// <summary>Índices de la spec 044 (§6). Idempotente; el script <c>db/044-avisos-dentro-de-la-app.js</c> hace lo mismo.</summary>
    public async Task EnsureIndexesAsync(CancellationToken cancellationToken)
    {
        await Notices.Indexes.CreateOneAsync(
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending(_userField).Ascending(_readField).Descending(_createdField)),
            cancellationToken: cancellationToken);
        await Notices.Indexes.CreateOneAsync(
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("venceEn"), new CreateIndexOptions { ExpireAfter = TimeSpan.Zero }),
            cancellationToken: cancellationToken);
        await Preferences.Indexes.CreateOneAsync(
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending(_userField), new CreateIndexOptions { Unique = true }),
            cancellationToken: cancellationToken);
    }

    private static StoredNotice ToStored(BsonDocument document) => new(
        document[_idField].AsObjectId.ToString(),
        document.GetValue("tipo", string.Empty).AsString,
        document.GetValue(_roleField, string.Empty).AsString,
        document.GetValue("titulo", string.Empty).AsString,
        document.GetValue("texto", string.Empty).AsString,
        document.TryGetValue("destino", out var target) && target.IsBsonDocument ? target.AsBsonDocument : null,
        document.GetValue(_readField, false).AsBoolean,
        document[_createdField].ToUniversalTime());
}
