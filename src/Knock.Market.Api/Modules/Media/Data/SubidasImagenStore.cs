using Knock.Market.Api.Hosting;
using Knock.Market.Api.Modules.Media.Domain;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Knock.Market.Api.Modules.Media.Data;

/// <summary>Acceso a la colección <c>subidas_imagen</c> (spec 032, §6). Las transiciones de estado son condicionales a <c>pendiente</c>.</summary>
internal interface ISubidasImagenStore
{
    Task InsertAsync(SubidaImagen subida, CancellationToken cancellationToken);

    /// <summary>La subida solo si existe y es de <paramref name="sub"/>; ajena e inexistente son lo mismo (404 uniforme, R-06).</summary>
    Task<SubidaImagen?> FindOwnedAsync(string id, string sub, CancellationToken cancellationToken);

    /// <summary>Cuántas subidas <c>pendiente</c> de la persona siguen vigentes (su <c>confirmarAntesDe</c> no ha pasado), para el tope de R-14.</summary>
    Task<int> CountPendientesVigentesAsync(string sub, DateTime ahora, CancellationToken cancellationToken);

    /// <summary>Pasa de <c>pendiente</c> a <c>publicada</c> y fija la fecha de vencimiento lejana; <see langword="false"/> si otro ya la resolvió.</summary>
    Task<bool> MarcarPublicadaAsync(string id, string url, int ancho, int alto, int bytes, CancellationToken cancellationToken);

    /// <summary>Pasa de <c>pendiente</c> a <c>rechazada</c> o <c>invalida</c>; <see langword="false"/> si ya no estaba pendiente.</summary>
    Task<bool> MarcarFinalAsync(string id, EstadoSubida estado, CancellationToken cancellationToken);
}

/// <summary>Implementación sobre MongoDB (base <c>market</c>). Los índices los crea el script de <c>db/</c> (spec 032, T-03).</summary>
internal sealed class SubidasImagenStore(
    [FromKeyedServices(MediaMongo.ModuleKey)] IMongoDatabase database,
    IOptions<KnockMongoOptions> options) : ISubidasImagenStore
{
    public const string Coleccion = "subidas_imagen";

    private IMongoCollection<BsonDocument> Subidas => database.GetCollection<BsonDocument>(Coleccion);

    private FindOptions ConsultaCorta => new() { MaxTime = options.Value.QueryMaxTime };

    public Task InsertAsync(SubidaImagen subida, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(subida);
        var documento = new BsonDocument
        {
            ["_id"] = subida.Id,
            ["claveBlob"] = subida.ClaveBlob,
            ["sub"] = subida.Sub,
            ["proposito"] = subida.Proposito.ToContractValue(),
            ["tipoDeclarado"] = subida.TipoDeclarado,
            ["tamanoDeclarado"] = subida.TamanoDeclarado,
            ["estado"] = subida.Estado.ToDb(),
            ["creadaEn"] = subida.CreadaEn,
            ["confirmarAntesDe"] = subida.ConfirmarAntesDe,
            ["expiraEn"] = subida.ExpiraEn,
        };
        return Subidas.InsertOneAsync(documento, cancellationToken: cancellationToken);
    }

    public async Task<SubidaImagen?> FindOwnedAsync(string id, string sub, CancellationToken cancellationToken)
    {
        var f = Builders<BsonDocument>.Filter;
        var documento = await Subidas.Find(f.Eq("_id", id) & f.Eq("sub", sub), ConsultaCorta).FirstOrDefaultAsync(cancellationToken);
        return documento is null ? null : Leer(documento);
    }

    public async Task<int> CountPendientesVigentesAsync(string sub, DateTime ahora, CancellationToken cancellationToken)
    {
        var f = Builders<BsonDocument>.Filter;
        var filtro = f.Eq("sub", sub) & f.Eq("estado", EstadoSubida.Pendiente.ToDb()) & f.Gt("confirmarAntesDe", ahora);
        return (int)await Subidas.CountDocumentsAsync(filtro, new CountOptions { MaxTime = options.Value.QueryMaxTime }, cancellationToken);
    }

    public async Task<bool> MarcarPublicadaAsync(string id, string url, int ancho, int alto, int bytes, CancellationToken cancellationToken)
    {
        var f = Builders<BsonDocument>.Filter;
        var cambio = Builders<BsonDocument>.Update
            .Set("estado", EstadoSubida.Publicada.ToDb())
            .Set("url", url)
            .Set("ancho", ancho)
            .Set("alto", alto)
            .Set("bytes", bytes)
            .Set("expiraEn", SubidaImagen.SinVencimiento);
        var resultado = await Subidas.UpdateOneAsync(f.Eq("_id", id) & f.Eq("estado", EstadoSubida.Pendiente.ToDb()), cambio, cancellationToken: cancellationToken);
        return resultado.ModifiedCount == 1;
    }

    public async Task<bool> MarcarFinalAsync(string id, EstadoSubida estado, CancellationToken cancellationToken)
    {
        if (estado is EstadoSubida.Pendiente or EstadoSubida.Publicada)
        {
            throw new ArgumentOutOfRangeException(nameof(estado));
        }

        var f = Builders<BsonDocument>.Filter;
        var resultado = await Subidas.UpdateOneAsync(
            f.Eq("_id", id) & f.Eq("estado", EstadoSubida.Pendiente.ToDb()),
            Builders<BsonDocument>.Update.Set("estado", estado.ToDb()),
            cancellationToken: cancellationToken);
        return resultado.ModifiedCount == 1;
    }

    private static SubidaImagen Leer(BsonDocument d) => new(
        d["_id"].AsString,
        d["claveBlob"].AsString,
        d["sub"].AsString,
        PropositoImagenExtensions.Parse(d["proposito"].AsString) ?? PropositoImagen.Trabajo,
        d["tipoDeclarado"].AsString,
        d["tamanoDeclarado"].ToInt32(),
        EstadoSubidaExtensions.ParseDb(d["estado"].AsString),
        d.TryGetValue("url", out var url) && url.IsString ? url.AsString : null,
        Entero(d, "ancho"),
        Entero(d, "alto"),
        Entero(d, "bytes"),
        d["creadaEn"].ToUniversalTime(),
        d["confirmarAntesDe"].ToUniversalTime(),
        d["expiraEn"].ToUniversalTime());

    private static int? Entero(BsonDocument d, string campo) => d.TryGetValue(campo, out var v) && v.IsNumeric ? v.ToInt32() : null;
}

/// <summary>Clave de la base de datos del módulo de imágenes (<c>Mongo:Databases:Media</c>).</summary>
internal static class MediaMongo
{
    public const string ModuleKey = "Media";
}

/// <summary>Almacén en memoria para pruebas unitarias y para entornos sin MongoDB.</summary>
internal sealed class SubidasImagenMemoriaStore : ISubidasImagenStore
{
    private readonly Dictionary<string, SubidaImagen> _subidas = [];
    private readonly Lock _bloqueo = new();

    public Task InsertAsync(SubidaImagen subida, CancellationToken cancellationToken)
    {
        lock (_bloqueo)
        {
            _subidas[subida.Id] = subida;
        }

        return Task.CompletedTask;
    }

    public Task<SubidaImagen?> FindOwnedAsync(string id, string sub, CancellationToken cancellationToken)
    {
        lock (_bloqueo)
        {
            return Task.FromResult(_subidas.TryGetValue(id, out var s) && s.Sub == sub ? s : null);
        }
    }

    public Task<int> CountPendientesVigentesAsync(string sub, DateTime ahora, CancellationToken cancellationToken)
    {
        lock (_bloqueo)
        {
            return Task.FromResult(_subidas.Values.Count(s => s.Sub == sub && s.Estado == EstadoSubida.Pendiente && s.ConfirmarAntesDe > ahora));
        }
    }

    public Task<bool> MarcarPublicadaAsync(string id, string url, int ancho, int alto, int bytes, CancellationToken cancellationToken)
    {
        lock (_bloqueo)
        {
            if (!_subidas.TryGetValue(id, out var s) || s.Estado != EstadoSubida.Pendiente)
            {
                return Task.FromResult(false);
            }

            _subidas[id] = s with { Estado = EstadoSubida.Publicada, Url = url, Ancho = ancho, Alto = alto, Bytes = bytes, ExpiraEn = SubidaImagen.SinVencimiento };
            return Task.FromResult(true);
        }
    }

    public Task<bool> MarcarFinalAsync(string id, EstadoSubida estado, CancellationToken cancellationToken)
    {
        lock (_bloqueo)
        {
            if (!_subidas.TryGetValue(id, out var s) || s.Estado != EstadoSubida.Pendiente)
            {
                return Task.FromResult(false);
            }

            _subidas[id] = s with { Estado = estado };
            return Task.FromResult(true);
        }
    }
}
