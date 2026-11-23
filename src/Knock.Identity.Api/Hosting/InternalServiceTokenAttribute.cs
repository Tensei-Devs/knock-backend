using System.Globalization;
using Knock.Identity.Api.Modules.HomeAndCar.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Knock.Identity.Api.Hosting;

/// <summary>
/// Autoriza las rutas internas que otros servicios llaman a Identidad (spec 037, R-11): token HS256 firmado con la clave de Mercado (<c>Security:InternalTokens:MarketKey</c>, secreto <c>interno-key-market</c>),
/// <c>iss</c> <c>knock-market</c>, <c>aud</c> <c>knock-identity</c>, <c>jti</c> e <c>iat</c>, vida de 5 minutos como máximo y el claim <c>permisos</c> con el permiso de la ruta. Sin clave válida falla cerrado: 401
/// uniforme; con token válido pero sin el permiso, 403. No es un token de persona y no sustituye <c>[Authorize]</c>: las rutas internas no lo usan. El gateway no las enruta.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
internal sealed class InternalServiceTokenAttribute(string permission) : Attribute, IAsyncAuthorizationFilter
{
    public string Permission { get; } = permission;

    public const string KeyConfigurationKey = "Security:InternalTokens:MarketKey";
    public const string Issuer = "knock-market";
    public const string Audience = "knock-identity";
    public const string ServiceNotAllowedCode = "internal_token_not_allowed";
    public const int MaxLifetimeSeconds = 300;

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var http = context.HttpContext;
        var header = http.Request.Headers.Authorization.ToString();
        var key = MarketHistoryClient.ReadKey(http.RequestServices.GetRequiredService<IConfiguration>()[KeyConfigurationKey]);
        if (key is null || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            context.Result = await Problem(http, StatusCodes.Status401Unauthorized);
            return;
        }

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(header["Bearer ".Length..].Trim(), new TokenValidationParameters
        {
            ValidIssuer = Issuer,
            ValidAudience = Audience,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            RequireSignedTokens = true,
            RequireExpirationTime = true,
            ClockSkew = TimeSpan.Zero,
        });
        if (!result.IsValid || !IsBounded(result.Claims))
        {
            context.Result = await Problem(http, StatusCodes.Status401Unauthorized);
            return;
        }

        if (!result.ClaimsIdentity.FindAll("permisos").Any(claim => string.Equals(claim.Value, Permission, StringComparison.Ordinal)))
        {
            context.Result = await Problem(http, StatusCodes.Status403Forbidden);
        }
    }

    private static bool IsBounded(IDictionary<string, object> claims) =>
        claims.TryGetValue("jti", out var jti) && jti is string { Length: > 0 }
        && long.TryParse(Text(claims, "iat"), NumberStyles.None, CultureInfo.InvariantCulture, out var issued)
        && long.TryParse(Text(claims, "exp"), NumberStyles.None, CultureInfo.InvariantCulture, out var expires)
        && expires > issued && expires - issued <= MaxLifetimeSeconds;

    private static string? Text(IDictionary<string, object> claims, string name) => claims.TryGetValue(name, out var value) ? Convert.ToString(value, CultureInfo.InvariantCulture) : null;

    private static async Task<IActionResult> Problem(HttpContext http, int status)
    {
        await ProblemResponses.WriteAsync(http, status, status == StatusCodes.Status401Unauthorized ? "Unauthorized" : "Forbidden", status == StatusCodes.Status403Forbidden ? ServiceNotAllowedCode : null);
        return new EmptyResult();
    }
}
