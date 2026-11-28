using System.Globalization;
using Knock.Hosting.Data;
using Knock.Market.Api.Hosting;
using Knock.Market.Api.Modules.Media.Domain;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Knock.Market.Api.Modules.Media.Data;

/// <summary>Nombre del módulo como clave de su base (<c>Mongo:Databases:Media</c>).</summary>
internal static class ArchivosMongo
{
    public const string ModuleKey = "Media";
}

/// <summary>
/// <see cref="IMediaFileStore"/> sobre la colección <c>archivos</c> de MongoDB (spec 058, §6). Los índices los crea <c>db/049-market-archivos.js</c>. <c>_id</c> es un
/// <see cref="ObjectId"/> que no sale del almacén; <c>archivoId</c> (UUID v7 en 32 hexadecimales) es el identificador de negocio y su índice único hace fallar un alta repetida.
/// Todas las claves van en camelCase y <c>version</c> da la concurrencia optimista (R-02, R-04). El documento no lleva datos personales (R-13): <c>propietarioId</c> es el <c>sub</c> interno.
/// </summary>
internal sealed class MongoMediaFileStore(
    [FromKeyedServices(ArchivosMongo.ModuleKey)] IMongoDatabase database,
    IOptions<KnockMongoOptions> options) : IMediaFileStore
{
    public const string CollectionName = "archivos";
    private const int _duplicateKey = 11000;

    private IMongoCollection<BsonDocument> Files => database.GetCollection<BsonDocument>(CollectionName);

    public async Task AddAsync(MediaFile file, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(file);
        try
        {
            await Files.InsertOneAsync(ToDocument(file), cancellationToken: cancellationToken);
        }
        catch (MongoWriteException ex) when (IsDuplicateKey(ex))
        {
            throw new InvalidOperationException("A media file with the same id already exists.", ex);
        }
    }

    public async Task<PendingAddResult> AddPendingWithinLimitAsync(MediaFile file, int limit, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(file);
        var document = ToDocument(file);
        try
        {
            await Files.InsertOneAsync(document, cancellationToken: cancellationToken);
        }
        catch (MongoWriteException ex) when (IsDuplicateKey(ex))
        {
            throw new InvalidOperationException("A media file with the same id already exists.", ex);
        }

        var f = Builders<BsonDocument>.Filter;
        var valid = f.Eq("propietarioId", file.OwnerId) & f.Eq("estado", "pendiente") & f.Gt("confirmarAntesDe", now.UtcDateTime);
        var count = await Files.CountDocumentsAsync(valid, new CountOptions { MaxTime = options.Value.QueryMaxTime }, cancellationToken);
        if (count <= limit)
        {
            return new PendingAddResult(true, null);
        }

        // Exceeded: withdraw only the document inserted here, and only while it is still pending (R-10).
        await Files.DeleteOneAsync(f.Eq("_id", document["_id"]) & f.Eq("estado", "pendiente"), cancellationToken);
        var oldest = await Files.Find(valid, new FindOptions { MaxTime = options.Value.QueryMaxTime })
            .Sort(Builders<BsonDocument>.Sort.Ascending("confirmarAntesDe"))
            .Limit(1)
            .FirstOrDefaultAsync(cancellationToken);
        return new PendingAddResult(false, oldest is null ? null : new DateTimeOffset(oldest["confirmarAntesDe"].ToUniversalTime(), TimeSpan.Zero));
    }

    public async Task<StoredMediaFile?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        var document = await Files.Find(Builders<BsonDocument>.Filter.Eq("archivoId", FileId(id)), new FindOptions { MaxTime = options.Value.QueryMaxTime })
            .FirstOrDefaultAsync(cancellationToken);
        return document is null ? null : new StoredMediaFile(ToDomain(document), document["version"].ToInt32().ToString(CultureInfo.InvariantCulture));
    }

    public async Task<bool> TryReplaceAsync(MediaFile file, string expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (!int.TryParse(expectedVersion, NumberStyles.None, CultureInfo.InvariantCulture, out var version))
        {
            return false;
        }

        var f = Builders<BsonDocument>.Filter;
        var update = Builders<BsonDocument>.Update
            .Set("estado", file.State.ToContractValue())
            .Set("blobOriginal", Nullable(file.OriginalBlob))
            .Set("blobReducido", Nullable(file.ReducedBlob))
            .Set("ancho", Nullable(file.Width))
            .Set("alto", Nullable(file.Height))
            .Set("confirmadoEn", Nullable(file.ConfirmedAt?.UtcDateTime))
            .Set("expiraEn", file.ExpiresAt.UtcDateTime)
            .Inc("version", 1);
        var result = await Files.UpdateOneAsync(f.Eq("archivoId", FileId(file.Id)) & f.Eq("version", version), update, cancellationToken: cancellationToken);
        return result.ModifiedCount == 1;
    }

    /// <summary>El identificador de negocio: el <see cref="Guid"/> en 32 hexadecimales minúsculas, sin guiones.</summary>
    internal static string FileId(Guid id) => id.ToString("N", CultureInfo.InvariantCulture);

    internal static BsonDocument ToDocument(MediaFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        return new BsonDocument
        {
            ["_id"] = ObjectId.GenerateNewId(),
            ["archivoId"] = FileId(file.Id),
            ["blobKey"] = file.BlobKey,
            ["propietarioId"] = file.OwnerId,
            ["uso"] = file.Usage.ToContractValue(),
            ["estado"] = file.State.ToContractValue(),
            ["contentType"] = file.DeclaredContentType,
            ["tamanoBytes"] = file.DeclaredSizeBytes,
            ["blobOriginal"] = Nullable(file.OriginalBlob),
            ["blobReducido"] = Nullable(file.ReducedBlob),
            ["ancho"] = Nullable(file.Width),
            ["alto"] = Nullable(file.Height),
            ["creadoEn"] = file.CreatedAt.UtcDateTime,
            ["confirmarAntesDe"] = file.ConfirmBefore.UtcDateTime,
            ["confirmadoEn"] = Nullable(file.ConfirmedAt?.UtcDateTime),
            ["expiraEn"] = file.ExpiresAt.UtcDateTime,
            ["version"] = 1,
        };
    }

    internal static MediaFile ToDomain(BsonDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return new MediaFile(
            Guid.ParseExact(document["archivoId"].AsString, "N"),
            document["blobKey"].AsString,
            document["propietarioId"].AsString,
            MediaRules.ParseUsage(document["uso"].AsString) ?? throw new InvalidOperationException("Unknown media usage in the stored record."),
            ParseState(document["estado"].AsString),
            document["contentType"].AsString,
            document["tamanoBytes"].ToInt64(),
            new DateTimeOffset(document["creadoEn"].ToUniversalTime(), TimeSpan.Zero))
        {
            OriginalBlob = Text(document, "blobOriginal"),
            ReducedBlob = Text(document, "blobReducido"),
            Width = Number(document, "ancho"),
            Height = Number(document, "alto"),
            ConfirmedAt = document.TryGetValue("confirmadoEn", out var confirmed) && confirmed.IsValidDateTime
                ? new DateTimeOffset(confirmed.ToUniversalTime(), TimeSpan.Zero)
                : null,
        };
    }

    internal static MediaFileState ParseState(string value) => value switch
    {
        "pendiente" => MediaFileState.Pending,
        "listo" => MediaFileState.Ready,
        "rechazado" => MediaFileState.Rejected,
        _ => throw new InvalidOperationException("Unknown media state in the stored record."),
    };

    private static BsonValue Nullable(string? value) => value is null ? BsonNull.Value : value;

    private static BsonValue Nullable(int? value) => value is null ? BsonNull.Value : value.Value;

    private static BsonValue Nullable(DateTime? value) => value is null ? BsonNull.Value : value.Value;

    private static bool IsDuplicateKey(MongoWriteException ex) => ex.WriteError.Category == ServerErrorCategory.DuplicateKey || ex.WriteError.Code == _duplicateKey;

    private static string? Text(BsonDocument document, string name) => document.TryGetValue(name, out var value) && value.IsString ? value.AsString : null;

    private static int? Number(BsonDocument document, string name) => document.TryGetValue(name, out var value) && value.IsNumeric ? value.ToInt32() : null;
}
