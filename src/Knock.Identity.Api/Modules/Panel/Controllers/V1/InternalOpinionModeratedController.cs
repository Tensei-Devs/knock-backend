using Asp.Versioning;
using Knock.Identity.Api.Hosting;
using Knock.Identity.Api.Modules.Panel.Services;
using Microsoft.AspNetCore.Mvc;

namespace Knock.Identity.Api.Modules.Panel.Controllers.V1;

/// <summary>
/// Entrega interna de <c>OpinionModerada</c> de Mercado (spec 061, R-06): solo la llama Mercado con su token de servicio y el permiso <c>opinion-moderada</c> (<see cref="InternalServiceTokenAttribute"/>);
/// el gateway no la enruta. Deja la auditoría del panel y responde 202, también si el evento ya estaba auditado. 503 <c>audit_unavailable</c> si no se pudo escribir, para que quien entrega reintente.
/// Nunca se cachea.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/interno/opiniones-moderadas")]
[InternalServiceToken(Permission)]
internal sealed class InternalOpinionModeratedController(OpinionModeradaConsumer consumer) : InternalEventDeliveryController<OpinionModeratedEventBody>
{
    public const string Permission = "opinion-moderada";

    protected override async Task ConsumeAsync(OpinionModeratedEventBody body, CancellationToken cancellationToken) => _ = await consumer.ConsumeAsync(body, cancellationToken);
}
