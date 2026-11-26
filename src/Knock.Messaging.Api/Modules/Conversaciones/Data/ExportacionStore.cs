using Knock.Hosting.Data;
using Knock.Messaging.Api.Hosting;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Knock.Messaging.Api.Modules.Conversaciones.Data;

/// <summary>Un mensaje propio para la exportación (spec 039, R-22): solo texto y fecha. Nunca el identificador de la otra parte ni sus mensajes.</summary>
internal sealed record MensajePropioExportado(string MensajeId, string Texto, bool ContactoOculto, DateTime CreadoEn);

/// <summary>Una conversación de la persona con únicamente los mensajes que ella escribió.</summary>
internal sealed record ConversacionExportada(string ConversacionId, string Rol, IReadOnlyList<MensajePropioExportado> Mensajes);

/// <summary>Lo que la exportación necesita de MongoDB; existe para probar el controlador con dobles.</summary>
internal interface IExportacionStore
{
    /// <summary>Las conversaciones de <paramref name="sub"/> (como cliente o como técnico) con solo los mensajes que escribió esa persona.</summary>
    Task<IReadOnlyList<ConversacionExportada>> ListOwnMessagesAsync(string sub, CancellationToken cancellationToken);
}

/// <summary>
/// Lectura de la exportación de datos personales (spec 039, R-22; spec 054, T-09; decisión de José del 2026-11-24: <c>mensajeria.json</c> lleva solo los mensajes propios). Las conversaciones se
/// buscan con los índices de cada rol (<c>clienteId</c> y <c>tecnicoUsuarioId</c>) y los mensajes con el índice por conversación, filtrando por <c>autorId</c> igual al <c>sub</c> de la
/// persona: no hace falta índice nuevo. Los filtros son tipados y el <c>sub</c> viene del token de servicio ya validado contra la ruta.
/// </summary>
internal sealed class ExportacionStore(
    [FromKeyedServices(DataExtensions.DatabaseKey)] IMongoDatabase database,
    IOptions<KnockMongoOptions> mongoOptions) : IExportacionStore
{
    /// <summary>Tope de conversaciones por rol en una exportación (acota memoria y tiempo; la retención de 180 días ya limita lo que existe).</summary>
    public const int MaxConversations = 1000;

    /// <summary>Tope de mensajes propios por conversación.</summary>
    public const int MaxMessagesPerConversation = 5000;

    private const string _authorField = "autorId";
    private const string _conversationField = "conversacionId";
    private const string _createdField = "creadoEn";
    private const string _idField = "_id";

    private readonly FindOptions _findOptions = new() { MaxTime = mongoOptions.Value.QueryMaxTime };

    public async Task<IReadOnlyList<ConversacionExportada>> ListOwnMessagesAsync(string sub, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sub);
        var result = new List<ConversacionExportada>();
        foreach (var (field, role) in new[] { ("clienteId", "cliente"), ("tecnicoUsuarioId", "tecnico") })
        {
            var conversations = await database.GetCollection<BsonDocument>(MensajesStore.ConversacionesCollection)
                .Find(Builders<BsonDocument>.Filter.Eq(field, sub), _findOptions)
                .Project(Builders<BsonDocument>.Projection.Include(_idField))
                .Sort(Builders<BsonDocument>.Sort.Ascending(_idField))
                .Limit(MaxConversations)
                .ToListAsync(cancellationToken);
            foreach (var id in conversations.Select(c => c[_idField].AsObjectId))
            {
                result.Add(new ConversacionExportada(id.ToString(), role, await OwnMessagesAsync(id, sub, cancellationToken)));
            }
        }

        return result;
    }

    private async Task<IReadOnlyList<MensajePropioExportado>> OwnMessagesAsync(ObjectId conversationId, string sub, CancellationToken cancellationToken)
    {
        var filters = Builders<BsonDocument>.Filter;
        var documents = await database.GetCollection<BsonDocument>(MensajesStore.MessagesCollection)
            .Find(filters.Eq(_conversationField, conversationId) & filters.Eq(_authorField, sub), _findOptions)
            .Project(Builders<BsonDocument>.Projection.Exclude(_authorField))
            .Sort(Builders<BsonDocument>.Sort.Ascending(_createdField).Ascending(_idField))
            .Limit(MaxMessagesPerConversation)
            .ToListAsync(cancellationToken);
        return [.. documents.Select(d => new MensajePropioExportado(
            d["mensajeId"].AsString,
            d["texto"].AsString,
            d.GetValue("contactoOculto", false).AsBoolean,
            d[_createdField].ToUniversalTime()))];
    }
}
