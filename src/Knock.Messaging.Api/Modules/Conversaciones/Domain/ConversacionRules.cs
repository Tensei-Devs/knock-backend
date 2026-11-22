using System.Buffers.Text;
using System.Globalization;
using System.Text;
using MongoDB.Bson;

namespace Knock.Messaging.Api.Modules.Conversaciones.Domain;

/// <summary>Quién es cada parte de una conversación (spec 030, R-01): el cliente dueño de la solicitud o el técnico del par.</summary>
internal static class ConversacionRoles
{
    public const string Cliente = "cliente";
    public const string Tecnico = "tecnico";
}

/// <summary>Estado de una conversación guardado (spec 030, R-03). El motivo de cierre lo calcula Mercado.</summary>
internal static class ConversacionEstados
{
    public const string Abierta = "abierta";
    public const string Cerrada = "cerrada";
}

/// <summary>
/// <c>code</c> de los errores de negocio del módulo (spec 030, §5), en inglés y <c>snake_case</c>, sin datos de la persona ni del recurso. El 404 es uniforme: una conversación ajena y una
/// que no existe se ven igual.
/// </summary>
internal static class ConversacionErrors
{
    public const string NotFoundCode = "not_found";
    public const string ConversationClosedCode = "conversation_closed";
    public const string InvalidIdCode = "invalid_id";
    public const string InvalidCursorCode = "invalid_cursor";

    public static DomainException NotFound() => new(DomainErrorKind.NotFound, "The conversation was not found.", NotFoundCode);

    public static DomainException Closed() => new(DomainErrorKind.Conflict, "The conversation is closed.", ConversationClosedCode);

    /// <summary>El <c>id</c> de la ruta no tiene forma de identificador: 400 (spec 030, §5), sin repetir el valor.</summary>
    public static ObjectId ParseId(string? id) =>
        id is { Length: 24 } && ObjectId.TryParse(id, out var parsed)
            ? parsed
            : throw new DomainException(DomainErrorKind.BadRequest, "The identifier is not valid.", InvalidIdCode);
}

/// <summary>
/// Cursor de las listas del módulo (conversaciones por <c>ultimoMensajeEn</c> y mensajes por <c>creadoEn</c>): la fecha del último elemento devuelto, en milisegundos UTC (la precisión
/// de MongoDB), y su <c>_id</c> para desempatar. Es opaco para la app (base64 de URL). Siempre se combina con el filtro de la persona o de la conversación, así que un cursor fabricado solo
/// mueve la posición dentro de lo que ya puede ver. Un valor que no se puede leer es un 400 <c>invalid_cursor</c>.
/// </summary>
internal readonly record struct PageCursor(DateTime Date, ObjectId Id)
{
    private const char _separator = '.';

    public string Format() =>
        Base64Url.EncodeToString(Encoding.ASCII.GetBytes(
            string.Create(CultureInfo.InvariantCulture, $"{new DateTimeOffset(Date, TimeSpan.Zero).ToUnixTimeMilliseconds()}{_separator}{Id}")));

    public static PageCursor? Parse(string? cursor)
    {
        if (string.IsNullOrEmpty(cursor))
        {
            return null;
        }

        if (cursor.Length <= 80 && TryRead(cursor, out var parsed))
        {
            return parsed;
        }

        throw new DomainException(DomainErrorKind.BadRequest, "The cursor is not valid.", ConversacionErrors.InvalidCursorCode);
    }

    private static bool TryRead(string cursor, out PageCursor parsed)
    {
        parsed = default;
        if (!Base64Url.IsValid(cursor))
        {
            return false;
        }

        var parts = Encoding.ASCII.GetString(Base64Url.DecodeFromChars(cursor)).Split(_separator);
        if (parts is not [var millis, { Length: 24 } id]
            || !long.TryParse(millis, NumberStyles.None, CultureInfo.InvariantCulture, out var ms)
            || ms > DateTimeOffset.MaxValue.ToUnixTimeMilliseconds()
            || !ObjectId.TryParse(id, out var objectId))
        {
            return false;
        }

        parsed = new PageCursor(DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime, objectId);
        return true;
    }
}
