namespace Knock.Market.Api.Modules.Requests.Dtos;

/// <summary>Lo que Mercado reporta como abierto de una persona (spec 039, R-24; ruta interna de Identidad): solo conteos e identificadores propios, hasta 50 por grupo.</summary>
internal sealed record AccountPendingResponse(int AppointmentTotal, IReadOnlyList<string> AppointmentIds, int QuoteTotal, IReadOnlyList<string> QuoteIds, bool VisitInProgress);
