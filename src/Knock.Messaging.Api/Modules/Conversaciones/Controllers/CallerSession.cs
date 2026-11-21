using System.Net.Http.Headers;
using System.Security.Claims;

namespace Knock.Messaging.Api.Modules.Conversaciones.Controllers;

/// <summary>La sesión de quien llama, tomada del token ya validado: el <c>sub</c> y el token que se reenvía a Mercado para el permiso (spec 030, R-02). Nunca se registra.</summary>
internal static class CallerSession
{
    public static string Subject(ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return user.FindFirst("sub")?.Value ?? string.Empty;
    }

    public static string BearerToken(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return AuthenticationHeaderValue.TryParse(request.Headers.Authorization.ToString(), out var header) && header.Parameter is { Length: > 0 } token
            ? token
            : string.Empty;
    }
}
