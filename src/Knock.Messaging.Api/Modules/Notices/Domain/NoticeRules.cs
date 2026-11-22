using System.Globalization;
using System.Text;

namespace Knock.Messaging.Api.Modules.Notices.Domain;

/// <summary>Reglas puras de los avisos dentro de la app (spec 044): tipos, roles, límites, cursor y preferencias por omisión.</summary>
internal static class NoticeRules
{
    public const int DefaultLimit = 20;
    public const int MaxLimit = 50;

    /// <summary>Máximo que muestra el contador (R-11, D-05); más allá la app dice «99+».</summary>
    public const int MaxUnreadShown = 99;

    /// <summary>Conservación de los avisos en días (D-04): el valor del índice de vencimiento, no una configuración.</summary>
    public const int RetentionDays = 30;

    public const string ClientRole = "cliente";
    public const string TechnicianRole = "tecnico";
    public const string SecurityType = "seguridad";
    public const string PromotionsType = "promociones";

    /// <summary>Roles válidos de <c>comoRol</c>.</summary>
    public static readonly IReadOnlyList<string> Roles = [ClientRole, TechnicianRole];

    /// <summary>
    /// Lista cerrada de tipos (decisión provisional): cotizaciones, visitas, solicitudes aceptadas, opiniones y resultados de verificación (spec 044 §6), más mensajes (frontend 024),
    /// seguridad y promociones (R-13).
    /// </summary>
    public static readonly IReadOnlyList<string> Types =
        ["cotizaciones", "mensajes", "visitas", "solicitudes", "opiniones", "verificaciones", SecurityType, PromotionsType];

    /// <summary>Fecha de vencimiento (<c>venceEn</c>) de un aviso creado en <paramref name="createdAt"/>: 30 días después (D-04). La usa quien crea avisos (despachador de la spec 037); el TTL de la base lo borra.</summary>
    public static DateTime ExpiresAt(DateTime createdAt) => createdAt.AddDays(RetentionDays);

    public static bool IsKnownRole(string? role) => role is not null && Roles.Contains(role, StringComparer.Ordinal);

    /// <summary>Los avisos de seguridad siempre llegan y no se pueden apagar (D-06).</summary>
    public static bool CanBeTurnedOff(string type) => !string.Equals(type, SecurityType, StringComparison.Ordinal);

    /// <summary>Si la persona nunca cambió nada, cada tipo está activo salvo las promociones (R-13, D-06).</summary>
    public static bool DefaultEnabled(string type) => !string.Equals(type, PromotionsType, StringComparison.Ordinal);

    /// <summary>Valor efectivo de un tipo: seguridad siempre activa; si no, lo guardado o el valor por omisión.</summary>
    public static bool Effective(string type, IReadOnlyDictionary<string, bool>? stored) =>
        !CanBeTurnedOff(type) || (stored is not null && stored.TryGetValue(type, out var value) ? value : DefaultEnabled(type));

    /// <summary>Límite pedido: vacío es 20; solo enteros de 1 a 50. <see langword="null"/> si no vale (422).</summary>
    public static int? ParseLimit(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return DefaultLimit;
        }

        return int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var limit) && limit is >= 1 and <= MaxLimit ? limit : null;
    }

    /// <summary>Filtro <c>leido</c>: vacío no filtra; solo <c>true</c> o <c>false</c>. <c>Valid</c> en falso indica que no vale (422).</summary>
    public static (bool Valid, bool? Value) ParseRead(string? raw) => raw switch
    {
        null or "" => (true, null),
        "true" => (true, true),
        "false" => (true, false),
        _ => (false, null),
    };
}

/// <summary>Posición de la página siguiente: fecha (milisegundos UTC) y <c>_id</c> del último aviso entregado. Opaco para la app.</summary>
internal readonly record struct NoticeCursor(long CreatedAtMs, string Id)
{
    private const char _separator = '.';

    public string Encode() =>
        Convert.ToBase64String(Encoding.ASCII.GetBytes($"{CreatedAtMs.ToString(CultureInfo.InvariantCulture)}{_separator}{Id}"))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static bool TryDecode(string? raw, out NoticeCursor cursor)
    {
        cursor = default;
        if (string.IsNullOrEmpty(raw) || raw.Length > 80)
        {
            return false;
        }

        try
        {
            var padded = raw.Replace('-', '+').Replace('_', '/');
            padded = padded.PadRight(padded.Length + ((4 - (padded.Length % 4)) % 4), '=');
            var parts = Encoding.ASCII.GetString(Convert.FromBase64String(padded)).Split(_separator);
            if (parts.Length != 2
                || !long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var ms)
                || !IsObjectId(parts[1]))
            {
                return false;
            }

            cursor = new NoticeCursor(ms, parts[1]);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>Un <c>ObjectId</c> son 24 caracteres hexadecimales.</summary>
    public static bool IsObjectId(string? value) =>
        value is { Length: 24 } && value.All(char.IsAsciiHexDigit);
}
