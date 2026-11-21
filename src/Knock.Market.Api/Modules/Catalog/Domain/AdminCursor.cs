using System.Globalization;
using System.Text;
using Knock.Market.Api.Common.Errors;
using Microsoft.AspNetCore.WebUtilities;

namespace Knock.Market.Api.Modules.Catalog.Domain;

/// <summary>Código de negocio del cursor de las listas de administración del catálogo (spec 045, R-15 y R-17).</summary>
internal static class AdminErrorCodes
{
    public const string InvalidCursorCode = "invalid_admin_cursor";
}

/// <summary>
/// Cursor opaco de las listas agrupadas del panel (spec 045): la cantidad y la clave del último grupo devuelto, en texto base64url. Un valor ilegible es un 400, igual que los demás cursores del
/// servicio. La lista de espera no ordena por cantidad y guarda cantidad 0.
/// </summary>
internal static class AdminCursor
{
    public static string Encode(long count, string key) =>
        WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(string.Create(CultureInfo.InvariantCulture, $"{count};{key}")));

    public static (long Count, string Key) Decode(string cursor)
    {
        ArgumentException.ThrowIfNullOrEmpty(cursor);
        try
        {
            var text = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(cursor));
            var separator = text.IndexOf(';', StringComparison.Ordinal);
            if (separator > 0
                && separator < text.Length - 1
                && long.TryParse(text.AsSpan(0, separator), NumberStyles.None, CultureInfo.InvariantCulture, out var count))
            {
                return (count, text[(separator + 1)..]);
            }
        }
        catch (FormatException)
        {
            // Se responde igual que con cualquier otro cursor ilegible.
        }
        catch (DecoderFallbackException)
        {
            // Bytes que no son texto: mismo caso.
        }

        throw new DomainException(DomainErrorKind.BadRequest, "The cursor is not valid.", AdminErrorCodes.InvalidCursorCode);
    }
}
