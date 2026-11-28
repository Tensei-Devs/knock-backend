using Knock.Market.Api.Modules.Tecnicos.Domain;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Driver;

namespace Knock.Market.Api.Modules.Tecnicos.Data;

/// <summary>Perfil del técnico y sus fotos de trabajos para la cuenta de la persona (spec 039, R-22 y R-25). Cada consulta se filtra por el identificador de la persona del token de servicio.</summary>
internal sealed class TechnicianAccountStore([FromKeyedServices(TecnicosModuleExtensions.ModuleKey)] IMongoDatabase database)
{
    private static readonly string[] _profileFields = ["nombre", "nombreNegocio", "descripcion", "oficios", "anios", "estado", "creadoEn"];

    private IMongoCollection<BsonDocument> Technicians => database.GetCollection<BsonDocument>(TechnicianProfileStore.CollectionName);

    public async Task<string?> ExportProfileJsonAsync(string sub, CancellationToken cancellationToken)
    {
        var profile = await Technicians.Find(Builders<BsonDocument>.Filter.Eq("usuarioId", sub)).FirstOrDefaultAsync(cancellationToken);
        if (profile is null)
        {
            return null;
        }

        var picked = new BsonDocument();
        foreach (var name in _profileFields.Where(profile.Contains))
        {
            picked[name] = profile[name];
        }

        return picked.ToJson(new JsonWriterSettings { OutputMode = JsonOutputMode.RelaxedExtendedJson });
    }

    public async Task AnonymizeProfileAsync(string sub, CancellationToken cancellationToken)
    {
        var f = Builders<BsonDocument>.Filter;
        var profile = await Technicians.Find(f.Eq("usuarioId", sub)).Project(Builders<BsonDocument>.Projection.Include("_id")).FirstOrDefaultAsync(cancellationToken);
        if (profile is null)
        {
            return;
        }

        await Technicians.UpdateOneAsync(
            f.Eq("_id", profile["_id"]),
            Builders<BsonDocument>.Update.Set("estado", "eliminado").Set("oculto", true).Set("nombre", "Técnico eliminado")
                .Unset("nombreNegocio").Unset("descripcion").Unset("fotoUrl").Unset("tallerDireccion").Unset("ubicacionBase").Unset("otroOficio")
                .Unset(TechnicianSearchFields.PublicName).Unset(TechnicianSearchFields.Words),
            cancellationToken: cancellationToken);
        await database.GetCollection<BsonDocument>(WorkPhotoStore.CollectionName).DeleteManyAsync(f.Eq("tecnicoId", profile["_id"]), cancellationToken);
    }
}
