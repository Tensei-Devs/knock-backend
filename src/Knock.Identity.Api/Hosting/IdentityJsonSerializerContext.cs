using System.Text.Json.Serialization;
using Knock.Identity.Api.Modules.Account.Dtos;
using Knock.Identity.Api.Modules.Panel.Dtos;
using Knock.Identity.Api.Modules.Sesiones.Dtos;
using Knock.Identity.Api.Modules.Usuarios.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Knock.Identity.Api.Hosting;

/// <summary>Contexto de serialización JSON generado en compilación (spec 010, T-06): DTO de sesión, perfil y problemas.</summary>
[JsonSerializable(typeof(ProblemDetails))]
[JsonSerializable(typeof(SessionResponse))]
[JsonSerializable(typeof(RefreshResponse))]
[JsonSerializable(typeof(ProfileResponse))]
[JsonSerializable(typeof(SessionListItemResponse))]
[JsonSerializable(typeof(List<SessionListItemResponse>))]
[JsonSerializable(typeof(FirebaseExchangeRequest))]
[JsonSerializable(typeof(RefreshRequest))]
[JsonSerializable(typeof(UpdateProfileRequest))]
[JsonSerializable(typeof(ConsentResponse))]
[JsonSerializable(typeof(ConsentResponse[]))]
[JsonSerializable(typeof(UpdateConsentRequest))]
[JsonSerializable(typeof(PendingResponse))]
[JsonSerializable(typeof(DeletionScheduledResponse))]
[JsonSerializable(typeof(LatestExportResponse))]
[JsonSerializable(typeof(PanelCelularRequest))]
[JsonSerializable(typeof(PanelCelularResponse))]
[JsonSerializable(typeof(PanelTotpRequest))]
[JsonSerializable(typeof(PanelAccesoResponse))]
[JsonSerializable(typeof(PanelActivacionRequest))]
[JsonSerializable(typeof(PanelActivacionResponse))]
internal sealed partial class IdentityJsonSerializerContext : JsonSerializerContext
{
}
