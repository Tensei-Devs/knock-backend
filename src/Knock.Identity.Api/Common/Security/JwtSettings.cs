// jscpd:ignore-start -- EXC-015
using Microsoft.IdentityModel.Tokens;

namespace Knock.Identity.Api.Common.Security;

/// <summary>Parámetros del JWT propio: los comparten el emisor (Identity) y la validación (Hosting).</summary>
internal sealed record JwtSettings(string Issuer, string Audience, SymmetricSecurityKey SigningKey);
// jscpd:ignore-end
