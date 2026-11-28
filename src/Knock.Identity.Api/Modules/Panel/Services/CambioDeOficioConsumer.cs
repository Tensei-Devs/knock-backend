using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using Knock.Identity.Api.Modules.Panel.Data;

namespace Knock.Identity.Api.Modules.Panel.Services;

/// <summary>
/// Cuerpo de <c>POST /api/v1/interno/cambios-de-oficio</c> (spec 045, R-20): <c>CambioDeOficio</c> que Mercado entrega desde su bandeja de salida. Solo identificadores: nunca los valores del oficio. Cualquier otro
/// campo queda en <see cref="Extra"/> y el validador lo rechaza con 422.
/// </summary>
internal sealed record TradeChangedEventBody(string? EventoId, string? Tipo, string? OficioClave, string? Accion, string? PorId)
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; init; }
}

/// <summary>Formato de <c>POST /api/v1/interno/cambios-de-oficio</c>: tipo y acción del catálogo, clave del oficio y persona del panel de caracteres seguros y sin campos de más; si no, 422 sin repetir el valor.</summary>
internal sealed class TradeChangedEventBodyValidator : AbstractValidator<TradeChangedEventBody>
{
    public const string FormatReason = "The value is not valid.";

    public TradeChangedEventBodyValidator()
    {
        RuleFor(x => x.EventoId).Must(value => AuditoriaPanel.IsSafeResourceId(value)).WithMessage(FormatReason);
        RuleFor(x => x.Tipo).Equal(CambioDeOficioConsumer.EventType).WithMessage(FormatReason);
        RuleFor(x => x.OficioClave).Must(value => AuditoriaPanel.IsSafeResourceId(value)).WithMessage(FormatReason);
        RuleFor(x => x.Accion).Must(value => value is CambioDeOficioConsumer.CreatedAction or CambioDeOficioConsumer.EditedAction).WithMessage(FormatReason);
        RuleFor(x => x.PorId).Must(value => AuditoriaPanel.IsSafeResourceId(value)).WithMessage(FormatReason);
        RuleFor(x => x.Extra).Must(extra => extra is null || extra.Count == 0).WithMessage(FormatReason);
    }
}

/// <summary>
/// Consumidor de <c>CambioDeOficio</c> (spec 045, R-20, con el enfoque de la spec 061): deja en <c>panel_auditoria</c> <c>oficio_creado</c> u <c>oficio_editado</c> con la persona del panel, el permiso <c>catalog:manage</c>
/// y la clave del oficio como recurso, nunca sus valores. Es idempotente por <c>eventoId</c> (ver <see cref="PanelEventConsumer"/>).
/// </summary>
internal sealed class CambioDeOficioConsumer(AuditoriaPanel audit) : PanelEventConsumer(audit, "cambio-de-oficio")
{
    public const string EventType = "CambioDeOficio";
    public const string CreatedAction = "creado";
    public const string EditedAction = "editado";
    public const string Permission = "catalog:manage";

    public Task<bool> ConsumeAsync(TradeChangedEventBody evento, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evento);
        var eventName = evento.Accion switch
        {
            CreatedAction => PanelAuditValues.OficioCreado,
            EditedAction => PanelAuditValues.OficioEditado,
            _ => throw new ArgumentOutOfRangeException(nameof(evento), "Unknown action."),
        };
        return WriteOnceAsync(evento.EventoId!, eventName, evento.PorId, Permission, evento.OficioClave, cancellationToken);
    }
}
