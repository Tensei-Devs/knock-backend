using Knock.Contracts.Pagination;
using Knock.Messaging.Api.Modules.Conversaciones.Data;
using Knock.Messaging.Api.Modules.Conversaciones.Domain;
using Knock.Messaging.Api.Modules.Conversaciones.Dtos;

namespace Knock.Messaging.Api.Modules.Conversaciones.Services;

/// <summary>
/// Lectura de una conversación por sus participantes (spec 030): el historial de mensajes paginado por cursor (R-16) y «marcar como leído» (R-19). Se resuelve contra la conversación
/// guardada, sin llamar a Mercado, y a quien no participa se le responde 404 como si no existiera (R-05).
/// </summary>
internal sealed class LecturaService(ConversacionesStore conversations, MensajesStore messages, INotificadorEnVivo live)
{
    /// <summary>Intentos de recontar si llega un mensaje mientras se marca como leído; después, gana el mensaje nuevo y la persona vuelve a marcar.</summary>
    private const int _attempts = 3;

    /// <summary>Los mensajes del más reciente al más antiguo. Sin <c>sub</c>, nombre ni teléfono de quien escribió (R-16).</summary>
    public async Task<CursorPage<MensajeView>> MessagesAsync(string subject, string id, MensajesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var conversationId = ConversacionErrors.ParseId(id);
        var limit = query.Limit ?? MensajesQuery.DefaultLimit;
        var cursor = PageCursor.Parse(query.Cursor);
        _ = await conversations.FindForParticipantAsync(conversationId, subject, cancellationToken) ?? throw ConversacionErrors.NotFound();

        var found = await messages.ListAsync(conversationId, cursor, limit + 1, cancellationToken);
        var page = found.Take(limit).ToList();
        var next = found.Count > limit ? new PageCursor(page[^1].CreadoEn, page[^1].Id).Format() : null;
        return new CursorPage<MensajeView>([.. page.Select(m => new MensajeView(m.MensajeId, m.AutorRol, m.Texto, m.ContactoOculto, m.CreadoEn))], next);
    }

    /// <summary>
    /// Mueve la marca de lectura de quien pide al máximo entre la guardada y <c>min(hasta, ultimoMensajeEn)</c>, recalcula su contador con los mensajes de la otra parte posteriores a esa
    /// marca y avisa a sus otros dispositivos (R-19). Repetirlo con el mismo valor o con uno anterior no cambia nada. Una fecha futura se acota al último mensaje: la app no puede inventar lecturas.
    /// </summary>
    public async Task MarkReadAsync(string subject, string id, DateTimeOffset until, CancellationToken cancellationToken)
    {
        var conversationId = ConversacionErrors.ParseId(id);
        for (var attempt = 0; attempt < _attempts; attempt++)
        {
            var conversation = await conversations.FindForParticipantAsync(conversationId, subject, cancellationToken) ?? throw ConversacionErrors.NotFound();
            var role = conversation.RoleOf(subject) ?? throw ConversacionErrors.NotFound();
            var target = Min(Truncate(until.UtcDateTime), conversation.UltimoMensajeEn);
            if (conversation.ReadUntilFor(role) is { } current && target <= current)
            {
                return;
            }

            var unread = await messages.CountUnreadAsync(conversationId, role, target, cancellationToken);
            if (!await conversations.ApplyReadAsync(conversationId, role, target, unread, conversation.UltimoMensajeEn, cancellationToken))
            {
                continue;
            }

            var total = await conversations.TotalUnreadAsync(subject, cancellationToken);
            await live.UnreadChangedAsync(subject, conversationId.ToString(), unread, total, cancellationToken);
            return;
        }
    }

    private static DateTime Truncate(DateTime value) => new(value.Ticks - (value.Ticks % TimeSpan.TicksPerMillisecond), DateTimeKind.Utc);

    private static DateTime Min(DateTime a, DateTime b) => a <= b ? a : b;
}
