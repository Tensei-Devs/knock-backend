using Knock.Contracts.Conversations;
using Knock.Messaging.Api.Common.Errors;
using Knock.Messaging.Api.Modules.Bloqueos;
using Knock.Messaging.Api.Modules.Conversaciones.Data;
using Knock.Messaging.Api.Modules.Conversaciones.Domain;
using Knock.Messaging.Api.Modules.Conversaciones.Dtos;
using Microsoft.Extensions.Options;

namespace Knock.Messaging.Api.Modules.Conversaciones.Services;

/// <summary>Configuración del módulo (sección <c>Conversaciones</c>). El aviso de seguridad vive aquí y no en cada documento (spec 030, R-07).</summary>
internal sealed class ConversacionesOptions
{
    public const string SectionName = "Conversaciones";

    public string? AvisoSeguridad { get; set; }
}

internal sealed class ConversacionesOptionsValidator : IValidateOptions<ConversacionesOptions>
{
    public ValidateOptionsResult Validate(string? name, ConversacionesOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return string.IsNullOrWhiteSpace(options.AvisoSeguridad)
            ? ValidateOptionsResult.Fail($"{ConversacionesOptions.SectionName}:AvisoSeguridad is not configured.")
            : ValidateOptionsResult.Success;
    }
}

/// <summary>
/// Crear, leer y listar conversaciones (spec 030, R-01 a R-07, R-17 y R-18). El permiso lo decide Mercado (R-02) y Mensajería guarda los participantes para no preguntar en cada lectura: una
/// conversación solo la ve una de sus dos partes, y a cualquier otra persona se le responde 404 sin distinguir si existe (R-05). La contraparte nunca lleva contacto ni <c>sub</c> (R-06).
/// </summary>
internal sealed class ConversacionService(
    ConversacionesStore store,
    IBloqueosApi blocks,
    IPermisoDeConversacion permissions,
    IOptions<ConversacionesOptions> options,
    TimeProvider clock)
{
    /// <summary>
    /// Crea la conversación del par o devuelve la que ya existe (idempotente por el par, R-04). Un técnico siempre habla por sí mismo: el <c>tecnicoId</c> que mande lo ignora Mercado. Una
    /// conversación cerrada no se crea (409), pero una que ya existe se devuelve aunque esté cerrada para poder leerla.
    /// </summary>
    public async Task<ConversacionCreada> CreateAsync(string subject, string bearerToken, CrearConversacionBody body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        var answer = await permissions.ConsultarAsync(body.SolicitudId!, body.TecnicoId, null, subject, bearerToken, cancellationToken);
        var permission = answer switch
        {
            { Estado: EstadoPermisoConversacion.Concedido, Permiso: { } granted } => granted,
            { Estado: EstadoPermisoConversacion.NoEncontrado } => throw ConversacionErrors.NotFound(),
            _ => throw new DomainException(DomainErrorKind.ServiceUnavailable, "The market service did not answer.", ErrorCodes.MarketUnavailableCode),
        };

        // Con un bloqueo entre ambas no se crea ni se reabre: responde como una solicitud o un técnico que no están disponibles (spec 043, R-17, CA-27).
        if (await blocks.AreBlockedAsync(permission.ClienteId, permission.TecnicoUsuarioId, cancellationToken))
        {
            throw ConversacionErrors.NotFound();
        }

        var existing = await store.FindByPairAsync(permission.SolicitudId, permission.TecnicoId, cancellationToken);
        if (existing is null)
        {
            if (!permission.Abierta)
            {
                throw ConversacionErrors.Closed();
            }

            var created = New(permission, clock.GetUtcNow().UtcDateTime);
            if (await store.InsertAsync(created, cancellationToken))
            {
                return new ConversacionCreada(ToView(created, RoleFor(created, subject)), true);
            }

            existing = await store.FindByPairAsync(permission.SolicitudId, permission.TecnicoId, cancellationToken) ?? throw ConversacionErrors.NotFound();
        }

        var role = RoleFor(existing, subject);
        if (Differs(existing, permission))
        {
            await store.RefreshPermissionAsync(existing.Id, permission, clock.GetUtcNow().UtcDateTime, cancellationToken);
            existing = existing with
            {
                Estado = permission.Abierta ? ConversacionEstados.Abierta : ConversacionEstados.Cerrada,
                MotivoCierre = permission.MotivoCierre,
                Elegido = permission.Elegido,
                VisitaId = permission.VisitaId,
            };
        }

        return new ConversacionCreada(ToView(existing, role), false);
    }

    /// <summary>La conversación para una de sus dos partes; a cualquier otra persona, 404 (R-05).</summary>
    public async Task<ConversacionView> GetAsync(string subject, string id, CancellationToken cancellationToken)
    {
        var conversation = await store.FindForParticipantAsync(ConversacionErrors.ParseId(id), subject, cancellationToken) ?? throw ConversacionErrors.NotFound();
        return ToView(conversation, RoleFor(conversation, subject));
    }

    /// <summary>Las conversaciones de la persona, de la más reciente a la más antigua, con el total de no leídos (R-17).</summary>
    public async Task<ConversacionPage> ListAsync(string subject, ConversacionesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var limit = query.Limit ?? ConversacionesQuery.DefaultLimit;
        var cursor = PageCursor.Parse(query.Cursor);
        var found = await store.ListAsync(subject, cursor, limit + 1, cancellationToken);
        var page = found.Take(limit).ToList();
        var next = found.Count > limit ? new PageCursor(page[^1].UltimoMensajeEn, page[^1].Id).Format() : null;
        var total = await store.TotalUnreadAsync(subject, cancellationToken);
        return new ConversacionPage([.. page.Select(c => ToItem(c, RoleFor(c, subject)))], total, next);
    }

    /// <summary>La suma de los no leídos de la persona con los contadores guardados (R-18).</summary>
    public async Task<NoLeidasView> UnreadAsync(string subject, CancellationToken cancellationToken) =>
        new(await store.TotalUnreadAsync(subject, cancellationToken));

    private static string RoleFor(StoredConversation conversation, string subject) =>
        conversation.RoleOf(subject) ?? throw ConversacionErrors.NotFound();

    private static bool Differs(StoredConversation stored, PermisoConversacion permission) =>
        stored.Estado != (permission.Abierta ? ConversacionEstados.Abierta : ConversacionEstados.Cerrada)
        || stored.MotivoCierre != permission.MotivoCierre
        || stored.Elegido != permission.Elegido
        || stored.VisitaId != permission.VisitaId;

    private static StoredConversation New(PermisoConversacion permission, DateTime nowUtc)
    {
        // MongoDB guarda milisegundos: se trunca aquí para que lo devuelto coincida con lo guardado.
        var now = new DateTime(nowUtc.Ticks - (nowUtc.Ticks % TimeSpan.TicksPerMillisecond), DateTimeKind.Utc);
        return new StoredConversation(
            MongoDB.Bson.ObjectId.GenerateNewId(),
            permission.SolicitudId,
            permission.TecnicoId,
            permission.ClienteId,
            permission.TecnicoUsuarioId,
            permission.VisitaId,
            permission.Servicio,
            permission.ColoniaNombre,
            permission.TecnicoPublico.Nombre,
            permission.TecnicoPublico.Foto,
            ConversacionEstados.Abierta,
            null,
            permission.Elegido,
            null,
            now,
            0,
            0,
            null,
            null,
            now);
    }

    /// <summary>Para el cliente, el nombre público y la foto del técnico; para el técnico, «Cliente de {colonia}» sin foto (R-06).</summary>
    private static Contraparte CounterpartOf(StoredConversation conversation, string role) =>
        role == ConversacionRoles.Cliente
            ? new Contraparte(conversation.TecnicoNombre, conversation.TecnicoFotoUrl)
            : new Contraparte($"Cliente de {conversation.ColoniaNombre}", null);

    private ConversacionView ToView(StoredConversation c, string role) => new(
        c.Id.ToString(),
        c.SolicitudId,
        c.VisitaId,
        role,
        CounterpartOf(c, role),
        c.Servicio,
        c.Estado,
        c.MotivoCierre,
        c.Elegido,
        options.Value.AvisoSeguridad!,
        c.UnreadFor(role));

    private static ConversacionItem ToItem(StoredConversation c, string role) => new(
        c.Id.ToString(),
        c.SolicitudId,
        c.VisitaId,
        role,
        CounterpartOf(c, role),
        c.Servicio,
        c.Estado,
        c.UltimoMensaje is { } last ? new UltimoMensajeView(last.Texto, last.EnviadoEn, last.AutorRol) : null,
        c.UnreadFor(role));
}
