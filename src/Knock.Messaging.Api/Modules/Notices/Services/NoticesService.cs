using Knock.Messaging.Api.Modules.Notices.Data;
using Knock.Messaging.Api.Modules.Notices.Domain;
using Knock.Messaging.Api.Modules.Notices.Dtos;
using MongoDB.Bson;

namespace Knock.Messaging.Api.Modules.Notices.Services;

/// <summary>
/// Casos de uso de los avisos (spec 044). La persona es siempre la del <c>sub</c> del token (R-15): ningún método recibe el identificador de otra persona desde la petición. Las entradas ya
/// llegan validadas (<c>ValidationFilter</c>).
/// </summary>
internal sealed class NoticesService(INoticesStore store)
{
    public async Task<NoticeListResponse> ListAsync(string sub, ListNoticesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var limit = NoticeRules.ParseLimit(query.Limite) ?? NoticeRules.DefaultLimit;
        var read = NoticeRules.ParseRead(query.Leido).Value;
        NoticeCursor? after = NoticeCursor.TryDecode(query.Cursor, out var cursor) ? cursor : null;
        var page = await store.ListAsync(sub, read, string.IsNullOrEmpty(query.ComoRol) ? null : query.ComoRol, after, limit, cancellationToken);
        return new NoticeListResponse([.. page.Items.Select(ToResponse)], page.Next?.Encode());
    }

    public async Task<UnreadCountResponse> CountUnreadAsync(string sub, CancellationToken cancellationToken)
    {
        // Se cuenta uno más del máximo mostrado para saber si hay «99+».
        var count = await store.CountUnreadAsync(sub, NoticeRules.MaxUnreadShown + 1, cancellationToken);
        return new UnreadCountResponse(Math.Min(count, NoticeRules.MaxUnreadShown), count > NoticeRules.MaxUnreadShown);
    }

    /// <summary><see langword="false"/> si no existe o no es de la persona (404 uniforme, R-12).</summary>
    public Task<bool> MarkReadAsync(string sub, string noticeId, CancellationToken cancellationToken) =>
        store.MarkReadAsync(sub, noticeId, cancellationToken);

    public Task MarkAllReadAsync(string sub, CancellationToken cancellationToken) => store.MarkAllReadAsync(sub, cancellationToken);

    private static NoticeResponse ToResponse(StoredNotice notice) => new(
        notice.Id, notice.Type, notice.Role, notice.Title, notice.Text, notice.Target is null ? null : BsonTypeMapper.MapToDotNetValue(notice.Target), notice.Read, notice.CreatedAt);
}
