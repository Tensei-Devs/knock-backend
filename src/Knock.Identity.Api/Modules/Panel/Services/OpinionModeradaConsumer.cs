using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using Knock.Hosting.Concurrency;
using Knock.Identity.Api.Modules.Panel.Data;
using MongoDB.Bson;

namespace Knock.Identity.Api.Modules.Panel.Services;

/// <summary>
/// Cuerpo de <c>POST /api/v1/interno/opiniones-moderadas</c> (spec 061): <c>OpinionModerada</c> que Mercado entrega desde su bandeja de salida. Solo identificadores: nunca el texto de la opinión ni el
/// motivo. Cualquier otro campo queda en <see cref="Extra"/> y el validador lo rechaza con 422.
/// </summary>
internal sealed record OpinionModeratedEventBody(string? EventoId, string? Tipo, string? OpinionId, string? TecnicoId, string? Accion, string? PorId)
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; init; }
}

/// <summary>Formato de <c>POST /api/v1/interno/opiniones-moderadas</c> (spec 061, R-06): tipo y acción del catálogo, opinión y técnico de 24 hexadecimales, persona del panel de caracteres seguros y sin campos de más; si no, 422 sin repetir el valor.</summary>
internal sealed class OpinionModeratedEventBodyValidator : AbstractValidator<OpinionModeratedEventBody>
{
    public const string FormatReason = "The value is not valid.";

    public OpinionModeratedEventBodyValidator()
    {
        RuleFor(x => x.EventoId).Must(value => AuditoriaPanel.IsSafeResourceId(value)).WithMessage(FormatReason);
        RuleFor(x => x.Tipo).Equal(OpinionModeradaConsumer.EventType).WithMessage(FormatReason);
        RuleFor(x => x.OpinionId).Must(IsObjectId).WithMessage(FormatReason);
        RuleFor(x => x.TecnicoId).Must(IsObjectId).WithMessage(FormatReason);
        RuleFor(x => x.Accion).Must(value => value is OpinionModeradaConsumer.HiddenAction or OpinionModeradaConsumer.ShownAction).WithMessage(FormatReason);
        RuleFor(x => x.PorId).Must(value => AuditoriaPanel.IsSafeResourceId(value)).WithMessage(FormatReason);
        RuleFor(x => x.Extra).Must(extra => extra is null || extra.Count == 0).WithMessage(FormatReason);
    }

    private static bool IsObjectId(string? value) => value is { Length: 24 } && ObjectId.TryParse(value, out _);
}

/// <summary>
/// Base de los consumidores de eventos de Mercado que dejan una sola auditoría del panel por <c>eventoId</c> (specs 061 y 045): el identificador del evento es el <c>_id</c> del registro, una entrega repetida
/// no escribe otro y, dentro de la instancia, las entregas del mismo evento se turnan (<see cref="KeyedGate"/>). Siempre con resultado <c>permitido</c> y de tipo <c>accion</c>.
/// </summary>
internal abstract class PanelEventConsumer(AuditoriaPanel audit, string gatePrefix)
{
    /// <summary><see langword="true"/> si escribió el registro ahora; <see langword="false"/> si el evento ya estaba auditado.</summary>
    protected Task<bool> WriteOnceAsync(string eventId, string eventName, string? sub, string permission, string? resourceId, CancellationToken cancellationToken) =>
        KeyedGate.RunAsync(
            $"{gatePrefix}:{eventId}",
            () => audit.WriteOnceAsync(eventId, PanelAuditValues.TipoAccion, eventName, PanelAuditValues.ResultadoPermitido, sub, permission, resourceId, cancellationToken),
            cancellationToken);
}

/// <summary>
/// Consumidor de <c>OpinionModerada</c> (spec 061, R-06 y R-07): deja en <c>panel_auditoria</c> <c>opinion_ocultada</c> u <c>opinion_mostrada</c> con la persona del panel y la opinión, nunca su texto. Es
/// idempotente por <c>eventoId</c> (que es el <c>_id</c> del registro): una entrega repetida no escribe otro. Dentro de la instancia, las entregas del mismo evento se turnan (<see cref="KeyedGate"/>).
/// </summary>
internal sealed class OpinionModeradaConsumer(AuditoriaPanel audit) : PanelEventConsumer(audit, "opinion-moderada")
{
    public const string EventType = "OpinionModerada";
    public const string HiddenAction = "ocultada";
    public const string ShownAction = "mostrada";
    public const string Permission = "reviews:moderate";

    /// <summary><see langword="true"/> si escribió el registro ahora; <see langword="false"/> si el evento ya estaba auditado.</summary>
    public Task<bool> ConsumeAsync(OpinionModeratedEventBody evento, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evento);
        return WriteOnceAsync(evento.EventoId!, evento.Accion == HiddenAction ? PanelAuditValues.OpinionOcultada : PanelAuditValues.OpinionMostrada, evento.PorId, Permission, evento.OpinionId, cancellationToken);
    }
}
