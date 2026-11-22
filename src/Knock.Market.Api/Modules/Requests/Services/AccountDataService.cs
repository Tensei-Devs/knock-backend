using Knock.Market.Api.Modules.Requests.Data;
using Knock.Market.Api.Modules.Requests.Domain;
using Knock.Market.Api.Modules.Requests.Dtos;
using Knock.Market.Api.Modules.Tecnicos;
using MongoDB.Bson;
using MongoDB.Bson.IO;

namespace Knock.Market.Api.Modules.Requests.Services;

/// <summary>
/// Puerto de la anonimización de las opiniones de una persona (spec 039, R-25; spec 051). Mientras la spec 051 no esté en esta rama la implementación registrada no hace nada;
/// la de la spec 051 sustituye al nombre y borra el texto de las opiniones de la persona.
/// </summary>
internal interface IOpinionAnonymizer
{
    Task AnonymizeAsync(string sub, CancellationToken cancellationToken);
}

/// <summary>Sustituto mientras no existen las opiniones (spec 051): no hay nada que anonimizar.</summary>
internal sealed class NoOpinionsToAnonymize : IOpinionAnonymizer
{
    public Task AnonymizeAsync(string sub, CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>
/// Operaciones de Mercado sobre la cuenta de una persona (spec 039): pendientes (R-24), cancelación al programar la baja (R-25), exportación (R-22) y el consumo de
/// <c>CuentaEliminada</c>. Todas son idempotentes. Los registros no llevan identificadores de la persona ni texto de sus datos.
/// </summary>
internal sealed class AccountDataService(
    AccountDataStore store,
    CierreDeSolicitudStore requests,
    VisitScheduleStore visits,
    ITecnicosApi technicians,
    IOpinionAnonymizer opinions,
    TimeProvider clock)
{
    public Task<AccountPendingResponse> PendingAsync(string sub, CancellationToken cancellationToken) => store.PendingAsync(sub, cancellationToken);

    /// <summary>Cancela las solicitudes abiertas y retira las cotizaciones pendientes de la persona; cada cierre avisa a la otra parte por el buzón de salida (R-25).</summary>
    public async Task CancelOpenAsync(string sub, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        foreach (var id in await store.OpenRequestIdsAsync(sub, cancellationToken))
        {
            await requests.CloseAsync(id, sub, AccountDataStore.AccountDeletedReason, false, now, cancellationToken);
        }

        await store.WithdrawQuotesAsync(sub, now, cancellationToken);
    }

    /// <summary>Datos de la persona en Mercado como texto JSON (un archivo por servicio en el ZIP, R-22).</summary>
    public async Task<string> ExportJsonAsync(string sub, CancellationToken cancellationToken)
    {
        var document = await store.ExportAsync(sub, cancellationToken);
        if (await technicians.ExportProfileJsonAsync(sub, cancellationToken) is { } profile)
        {
            document.Add("perfilTecnico", BsonDocument.Parse(profile));
        }

        return document.ToJson(new JsonWriterSettings { OutputMode = JsonOutputMode.RelaxedExtendedJson });
    }

    /// <summary>
    /// Consume <c>CuentaEliminada</c>: cancela como tardías las visitas <c>confirmada</c> que queden, cierra lo abierto, borra lo personal de Mercado y anonimiza las opiniones (R-25).
    /// </summary>
    public async Task AccountDeletedAsync(string sub, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        foreach (var visit in await store.ConfirmedVisitsAsync(sub, cancellationToken))
        {
            var by = string.Equals(visit.ClientId, sub, StringComparison.Ordinal) ? VisitScheduleRules.ProposedByClient : VisitScheduleRules.ProposedByTechnician;
            await visits.CancelAsync(visit, by, AccountDataStore.AccountDeletedReason, true, now, cancellationToken);
        }

        await CancelOpenAsync(sub, cancellationToken);
        await store.AnonymizeAsync(sub, now, cancellationToken);
        await technicians.AnonymizeProfileAsync(sub, cancellationToken);
        await opinions.AnonymizeAsync(sub, cancellationToken);
    }
}
