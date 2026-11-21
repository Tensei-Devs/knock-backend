using Knock.Messaging.Api.Hosting;
using Knock.Messaging.Api.Modules.Conversaciones.Domain;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Knock.Messaging.Api.Modules.Conversaciones.Data;

/// <summary>Un mensaje por guardar (spec 030, R-14). <c>Texto</c> ya viene ocultado y moderado; <c>AutorId</c> es el <c>sub</c> y nunca sale de la base.</summary>
internal sealed record NuevoMensaje(
    string ConversacionId,
    string MensajeId,
    string AutorRol,
    string AutorId,
    string Texto,
    bool ContactoOculto,
    DateTime CreadoEnUtc);

/// <summary>Contadores de la otra parte tras guardar: los del evento <c>noLeidos</c> (R-14). <c>DestinatarioUsuarioId</c> es su <c>sub</c>.</summary>
internal sealed record MensajeGuardado(string DestinatarioUsuarioId, int NoLeidos, int TotalNoLeidos);

/// <summary>Acceso a MongoDB para enviar un mensaje (spec 030, R-09 y R-14): leer los participantes, guardar el permiso vigente y la transacción de guardado.</summary>
internal interface IMensajesStore
{
    /// <summary>La conversación con ese identificador, o <see langword="null"/> si no existe o el identificador no es un <c>ObjectId</c>.</summary>
    Task<ConversacionParaEnvio?> ObtenerAsync(string conversacionId, CancellationToken cancellationToken);

    /// <summary>Actualiza <c>estado</c>, <c>motivoCierre</c>, <c>elegido</c> y <c>visitaId</c> con el permiso recién consultado (R-09).</summary>
    Task GuardarPermisoAsync(string conversacionId, PermisoVigente permiso, DateTime ahoraUtc, CancellationToken cancellationToken);

    /// <summary>Inserta el mensaje, actualiza la conversación y escribe el buzón en una sola transacción (R-14).</summary>
    Task<MensajeGuardado> GuardarAsync(NuevoMensaje mensaje, CancellationToken cancellationToken);
}

/// <summary>
/// Implementación sobre MongoDB (base <c>messaging</c>). Para el envío, <c>conversaciones</c>, <c>mensajes</c> y <c>outbox</c> se tocan solo aquí. El buzón lleva solo
/// <c>conversacionId</c> y <c>destinatarioUsuarioId</c>: ningún texto (R-14, CA-01).
/// </summary>
internal sealed class MensajesStore(
    [FromKeyedServices(DataExtensions.DatabaseKey)] IMongoDatabase database,
    IOptions<KnockMongoOptions> mongoOptions) : IMensajesStore
{
    public const string ConversacionesCollection = "conversaciones";
    public const string MensajesCollection = "mensajes";
    public const string OutboxCollection = "outbox";
    public const string MensajeNuevoType = "MensajeNuevo";

    private IMongoCollection<BsonDocument> Conversaciones => database.GetCollection<BsonDocument>(ConversacionesCollection);

    public async Task<ConversacionParaEnvio?> ObtenerAsync(string conversacionId, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(conversacionId, out var id))
        {
            return null;
        }

        var document = await Conversaciones.Find(Builders<BsonDocument>.Filter.Eq("_id", id), new FindOptions { MaxTime = mongoOptions.Value.QueryMaxTime })
            .FirstOrDefaultAsync(cancellationToken);
        return document is null
            ? null
            : new ConversacionParaEnvio(
                conversacionId,
                document["solicitudId"].AsString,
                document["tecnicoId"].AsString,
                document["clienteId"].AsString,
                document["tecnicoUsuarioId"].AsString);
    }

    public async Task GuardarPermisoAsync(string conversacionId, PermisoVigente permiso, DateTime ahoraUtc, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(permiso);
        var update = Builders<BsonDocument>.Update
            .Set("estado", permiso.Abierta ? "abierta" : "cerrada")
            .Set("motivoCierre", OrNull(permiso.MotivoCierre))
            .Set("elegido", permiso.Elegido)
            .Set("visitaId", OrNull(permiso.VisitaId))
            .Set("actualizadoEn", ahoraUtc);
        await Conversaciones.UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", ObjectId.Parse(conversacionId)), update, cancellationToken: cancellationToken);
    }

    public async Task<MensajeGuardado> GuardarAsync(NuevoMensaje mensaje, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mensaje);
        var conversationId = ObjectId.Parse(mensaje.ConversacionId);
        var fromClient = string.Equals(mensaje.AutorRol, ReglasDeMensaje.RolCliente, StringComparison.Ordinal);
        var unreadField = fromClient ? "noLeidosTecnico" : "noLeidosCliente";
        var recipientField = fromClient ? "tecnicoUsuarioId" : "clienteId";

        using var session = await database.Client.StartSessionAsync(cancellationToken: cancellationToken);
        return await session.WithTransactionAsync(async (transaction, token) =>
        {
            await database.GetCollection<BsonDocument>(MensajesCollection).InsertOneAsync(
                transaction,
                new BsonDocument
                {
                    { "mensajeId", mensaje.MensajeId },
                    { "conversacionId", conversationId },
                    { "autorRol", mensaje.AutorRol },
                    { "autorId", mensaje.AutorId },
                    { "texto", mensaje.Texto },
                    { "contactoOculto", mensaje.ContactoOculto },
                    { "creadoEn", mensaje.CreadoEnUtc },
                },
                cancellationToken: token);

            var update = Builders<BsonDocument>.Update
                .Set("ultimoMensaje", new BsonDocument { { "texto", ReglasDeMensaje.Resumir(mensaje.Texto) }, { "enviadoEn", mensaje.CreadoEnUtc }, { "autorRol", mensaje.AutorRol } })
                .Set("ultimoMensajeEn", mensaje.CreadoEnUtc)
                .Set("actualizadoEn", mensaje.CreadoEnUtc)
                .Inc(unreadField, 1);
            var conversation = await Conversaciones.FindOneAndUpdateAsync(
                transaction,
                Builders<BsonDocument>.Filter.Eq("_id", conversationId),
                update,
                new FindOneAndUpdateOptions<BsonDocument> { ReturnDocument = ReturnDocument.After },
                token);
            var recipient = conversation[recipientField].AsString;

            await database.GetCollection<BsonDocument>(OutboxCollection).InsertOneAsync(
                transaction,
                new BsonDocument
                {
                    { "_id", $"{MensajeNuevoType}:{mensaje.MensajeId}" },
                    { "tipo", MensajeNuevoType },
                    { "agregadoId", mensaje.ConversacionId },
                    { "datos", new BsonDocument { { "conversacionId", mensaje.ConversacionId }, { "destinatarioUsuarioId", recipient } } },
                    { "estado", "pendiente" },
                    { "intentos", 0 },
                    { "creadoEn", mensaje.CreadoEnUtc },
                },
                cancellationToken: token);

            return new MensajeGuardado(recipient, conversation[unreadField].ToInt32(), await TotalAsync(transaction, recipient, token));
        }, cancellationToken: cancellationToken);
    }

    private static BsonValue OrNull(string? value) => value is null ? BsonNull.Value : new BsonString(value);

    /// <summary>La suma de los contadores guardados de la persona en todas sus conversaciones (como cliente y como técnico), sin contar mensajes.</summary>
    private async Task<int> TotalAsync(IClientSessionHandle session, string sub, CancellationToken cancellationToken)
    {
        var asClient = await SumAsync(session, "clienteId", "noLeidosCliente", sub, cancellationToken);
        var asTechnician = await SumAsync(session, "tecnicoUsuarioId", "noLeidosTecnico", sub, cancellationToken);
        return asClient + asTechnician;
    }

    private async Task<int> SumAsync(IClientSessionHandle session, string participantField, string unreadField, string sub, CancellationToken cancellationToken)
    {
        var rows = await Conversaciones.Aggregate(session)
            .Match(Builders<BsonDocument>.Filter.Eq(participantField, sub))
            .Group(new BsonDocument { { "_id", BsonNull.Value }, { "total", new BsonDocument("$sum", "$" + unreadField) } })
            .ToListAsync(cancellationToken);
        return rows.Count == 0 ? 0 : rows[0]["total"].ToInt32();
    }
}
