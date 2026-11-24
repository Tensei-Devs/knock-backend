using Knock.Hosting.Data;
using Knock.Market.Api.Hosting;
using Knock.Market.Api.Modules.Catalog.Domain;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Knock.Market.Api.Modules.Catalog.Data;

/// <summary>Un oficio tal como lo ve el panel (spec 045, OFI-03 a OFI-05): con sus parámetros de precio y con los inactivos. Los parámetros son opcionales porque los oficios sembrados antes de la spec no los traen.</summary>
internal sealed record AdminTrade(
    string Clave,
    string Nombre,
    string Modalidad,
    IReadOnlyList<string> FrasesSugeridas,
    decimal? TopePorcentaje,
    decimal? TopeFijoPorcentaje,
    int? PisoAprobacion,
    int? VisitaMaxima,
    bool Activo);

/// <summary>Oficio nuevo ya validado (OFI-04). <c>nombreBusqueda</c> y <c>version</c> los calcula el almacén.</summary>
internal sealed record NewTrade(
    string Clave,
    string Nombre,
    string Modalidad,
    IReadOnlyList<string> FrasesSugeridas,
    decimal TopePorcentaje,
    decimal TopeFijoPorcentaje,
    int PisoAprobacion,
    int VisitaMaxima,
    bool Activo);

/// <summary>Cambios de una edición (OFI-05): solo los campos con valor se escriben.</summary>
internal sealed record TradeChanges(
    string? Nombre,
    string? Modalidad,
    IReadOnlyList<string>? FrasesSugeridas,
    decimal? TopePorcentaje,
    decimal? TopeFijoPorcentaje,
    int? PisoAprobacion,
    int? VisitaMaxima,
    bool? Activo);

/// <summary>Lo que el panel necesita de la colección <c>oficios</c>; existe para probar el servicio con dobles sin MongoDB.</summary>
internal interface ITradeAdminStore
{
    /// <summary>Todos los oficios, activos e inactivos, por nombre normalizado y, a igual valor, por clave.</summary>
    Task<IReadOnlyList<AdminTrade>> ListAllAsync(CancellationToken cancellationToken);

    /// <summary>Crea el oficio; <see langword="false"/> si ya existe uno con esa clave (índice único), sin cambiar nada.</summary>
    Task<bool> InsertAsync(NewTrade trade, CancellationToken cancellationToken);

    /// <summary>Aplica los cambios al oficio con esa clave y devuelve cómo quedó, o <see langword="null"/> si no existe. Nunca cambia la clave.</summary>
    Task<AdminTrade?> UpdateAsync(string clave, TradeChanges changes, CancellationToken cancellationToken);
}

/// <summary>
/// Escrituras y lecturas del panel sobre <c>oficios</c> (spec 045, OFI-03 a OFI-05). Los filtros son tipados del driver y toda consulta lleva <c>maxTimeMS</c>. Las escrituras invalidan la caché de
/// oficios activos desde el servicio, después de escribir. El catálogo es acotado (decisión D9 de la spec TT-PRU-01): se lista completo, con un tope de seguridad.
/// </summary>
internal sealed class TradeAdminStore(
    [FromKeyedServices(CatalogModuleExtensions.ModuleKey)] IMongoDatabase database,
    IOptions<KnockMongoOptions> options) : ITradeAdminStore
{
    /// <summary>Tope de seguridad de la lista: el catálogo real tiene unas decenas de oficios.</summary>
    public const int MaxListed = 500;

    private const string _percentageField = "topePorcentaje";
    private const string _fixedPercentageField = "topeFijoPorcentaje";
    private const string _approvalFloorField = "pisoAprobacion";
    private const string _maxVisitField = "visitaMaxima";

    private static readonly ProjectionDefinition<BsonDocument> _projection = Builders<BsonDocument>.Projection.Exclude(CatalogStore.IdField).Exclude(CatalogStore.NombreBusquedaField);

    private IMongoCollection<BsonDocument> Oficios => database.GetCollection<BsonDocument>(CatalogStore.OficiosCollection);

    private FindOptions Find => new() { MaxTime = options.Value.QueryMaxTime };

    public async Task<IReadOnlyList<AdminTrade>> ListAllAsync(CancellationToken cancellationToken)
    {
        var documents = await Oficios.Find(FilterDefinition<BsonDocument>.Empty, Find)
            .Project(_projection)
            .Sort(Builders<BsonDocument>.Sort.Ascending(CatalogStore.NombreBusquedaField).Ascending(CatalogStore.ClaveField))
            .Limit(MaxListed)
            .ToListAsync(cancellationToken);
        return [.. documents.Select(ToAdminTrade)];
    }

    public async Task<bool> InsertAsync(NewTrade trade, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(trade);
        var document = new BsonDocument
        {
            { CatalogStore.ClaveField, trade.Clave },
            { CatalogStore.NombreField, trade.Nombre },
            { CatalogStore.NombreBusquedaField, SearchNameNormalizer.Normalize(trade.Nombre) },
            { CatalogStore.ModalidadField, trade.Modalidad },
            { CatalogStore.FrasesSugeridasField, new BsonArray(trade.FrasesSugeridas) },
            { _percentageField, new BsonDecimal128(trade.TopePorcentaje) },
            { _fixedPercentageField, new BsonDecimal128(trade.TopeFijoPorcentaje) },
            { _approvalFloorField, trade.PisoAprobacion },
            { _maxVisitField, trade.VisitaMaxima },
            { CatalogStore.ActivoField, trade.Activo },
            { CatalogStore.VersionField, 1 },
        };
        try
        {
            await Oficios.InsertOneAsync(document, cancellationToken: cancellationToken);
            return true;
        }
        catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
    }

    public async Task<AdminTrade?> UpdateAsync(string clave, TradeChanges changes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(changes);
        var update = Builders<BsonDocument>.Update.Inc(CatalogStore.VersionField, 1);
        if (changes.Nombre is not null)
        {
            update = update.Set(CatalogStore.NombreField, changes.Nombre).Set(CatalogStore.NombreBusquedaField, SearchNameNormalizer.Normalize(changes.Nombre));
        }

        if (changes.Modalidad is not null) { update = update.Set(CatalogStore.ModalidadField, changes.Modalidad); }
        if (changes.FrasesSugeridas is not null) { update = update.Set(CatalogStore.FrasesSugeridasField, new BsonArray(changes.FrasesSugeridas)); }
        if (changes.TopePorcentaje is { } percentage) { update = update.Set(_percentageField, new BsonDecimal128(percentage)); }
        if (changes.TopeFijoPorcentaje is { } fixedPercentage) { update = update.Set(_fixedPercentageField, new BsonDecimal128(fixedPercentage)); }
        if (changes.PisoAprobacion is { } floor) { update = update.Set(_approvalFloorField, floor); }
        if (changes.VisitaMaxima is { } maxVisit) { update = update.Set(_maxVisitField, maxVisit); }
        if (changes.Activo is { } active) { update = update.Set(CatalogStore.ActivoField, active); }

        var updated = await Oficios.FindOneAndUpdateAsync(
            Builders<BsonDocument>.Filter.Eq(CatalogStore.ClaveField, clave),
            update,
            new FindOneAndUpdateOptions<BsonDocument> { ReturnDocument = ReturnDocument.After, Projection = _projection },
            cancellationToken);
        return updated is null ? null : ToAdminTrade(updated);
    }

    internal static AdminTrade ToAdminTrade(BsonDocument document) => new(
        document[CatalogStore.ClaveField].AsString,
        document[CatalogStore.NombreField].AsString,
        document[CatalogStore.ModalidadField].AsString,
        document.TryGetValue(CatalogStore.FrasesSugeridasField, out var phrases) && phrases.IsBsonArray
            ? [.. phrases.AsBsonArray.Where(phrase => phrase.IsString).Select(phrase => phrase.AsString)]
            : [],
        Decimal(document, _percentageField),
        Decimal(document, _fixedPercentageField),
        Integer(document, _approvalFloorField),
        Integer(document, _maxVisitField),
        document.GetValue(CatalogStore.ActivoField, false).ToBoolean());

    private static decimal? Decimal(BsonDocument document, string name) => document.TryGetValue(name, out var value) && value.IsNumeric ? value.ToDecimal() : null;

    private static int? Integer(BsonDocument document, string name) => document.TryGetValue(name, out var value) && value.IsNumeric ? value.ToInt32() : null;
}
