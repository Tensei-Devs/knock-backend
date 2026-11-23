using Asp.Versioning;
using Knock.Identity.Api.Hosting;
using Knock.Identity.Api.Modules.Safety.Dtos;
using Knock.Identity.Api.Modules.Safety.Services;
using Microsoft.AspNetCore.Mvc;

namespace Knock.Identity.Api.Modules.Safety.Controllers.V1;

/// <summary>
/// Entrega interna de <c>ReporteNoTerminadoCreado</c> de Mercado (spec 053; spec 050, R-32): solo la llama Mercado con su token de servicio y el permiso <c>reporte-no-terminado-creado</c>
/// (<see cref="InternalServiceTokenAttribute"/>); el gateway no la enruta. El evento lleva solo identificadores: <see cref="NotFinishedReportConsumer"/> lee el texto en Mercado y abre el reporte en la cola
/// del panel. 202 si lo abrió o ya existía; 503 si Mercado no responde, para que quien entrega reintente. Nunca se cachea.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/interno/reportes-no-terminados")]
[InternalServiceToken(Permission)]
internal sealed class InternalNotFinishedReportsController(NotFinishedReportConsumer consumer) : InternalEventDeliveryController<NotFinishedReportEventBody>
{
    public const string Permission = "reporte-no-terminado-creado";

    protected override async Task ConsumeAsync(NotFinishedReportEventBody body, CancellationToken cancellationToken) => _ = await consumer.ConsumeAsync(body, cancellationToken);
}
