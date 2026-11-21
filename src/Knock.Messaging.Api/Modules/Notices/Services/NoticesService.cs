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

    private static NoticeResponse ToResponse(StoredNotice notice) => new(
        notice.Id, notice.Type, notice.Role, notice.Title, notice.Text, notice.Target is null ? null : BsonTypeMapper.MapToDotNetValue(notice.Target), notice.Read, notice.CreatedAt);
}
