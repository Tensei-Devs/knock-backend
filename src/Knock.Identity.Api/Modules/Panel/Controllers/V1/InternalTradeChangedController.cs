using Asp.Versioning;
using Knock.Identity.Api.Hosting;
using Knock.Identity.Api.Modules.Panel.Services;
using Microsoft.AspNetCore.Mvc;

namespace Knock.Identity.Api.Modules.Panel.Controllers.V1;

/// <summary>
/// Entrega interna de <c>CambioDeOficio</c> de Mercado (spec 045, R-20): solo la llama Mercado con su token de servicio y el permiso <c>cambio-de-oficio</c> (<see cref="InternalServiceTokenAttribute"/>); el gateway
/// no la enruta. Deja la auditoría del panel y responde 202, también si el evento ya estaba auditado. 503 <c>audit_unavailable</c> si no se pudo escribir, para que quien entrega reintente. Nunca se cachea.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/interno/cambios-de-oficio")]
[InternalServiceToken(Permission)]
internal sealed class InternalTradeChangedController(CambioDeOficioConsumer consumer) : InternalEventDeliveryController<TradeChangedEventBody>
{
    public const string Permission = "cambio-de-oficio";

    protected override async Task ConsumeAsync(TradeChangedEventBody body, CancellationToken cancellationToken) => _ = await consumer.ConsumeAsync(body, cancellationToken);
}
