using Microsoft.Extensions.Options;

namespace Knock.Identity.Api.Modules.Safety.Domain;

/// <summary>Tipos de suspensión de <c>usuarios.suspension.tipo</c> (spec 059, §6).</summary>
internal static class SuspensionTypes
{
    public const string Preventive = "preventiva";
    public const string Gradual = "gradual";
}

/// <summary>Estados de la cuenta que la suspensión necesita conocer (spec 015, R-07). Valores guardados en <c>usuarios.estado</c>.</summary>
internal static class SuspensionAccountStates
{
    public const string Active = "activa";
    public const string Suspended = "suspendida";
    public const string DeletionScheduled = "eliminacion_programada";
}

/// <summary>Qué hacer al pedir <c>Suspender</c> sobre una cuenta (spec 059, R-03, R-05 a R-07).</summary>
internal enum SuspendDecision
{
    /// <summary>La cuenta pasa a <c>suspendida</c>.</summary>
    Suspend,

    /// <summary>Ya estaba suspendida: no cambia nada ni se emite nada (R-05).</summary>
    AlreadySuspended,

    /// <summary>La cuenta es de un cliente: no se suspende (R-03).</summary>
    NotTechnician,

    /// <summary>El moderador es la persona reportada (R-07).</summary>
    SelfSuspension,
}

/// <summary>Qué hacer al pedir <c>Reactivar</c> (spec 059, R-15).</summary>
internal enum ReactivateDecision
{
    Reactivate,
    NotSuspended,
}

/// <summary>
/// Reglas puras de la suspensión (spec 059). No tocan MongoDB, Redis ni el reloj: reciben el estado de la cuenta y devuelven la decisión; la operación condicionada al estado que las aplica
/// vive en el almacén de cuentas.
/// </summary>
internal static class AccountSuspensionRules
{
    /// <summary>Decide si se suspende. La propia cuenta se rechaza primero (R-07); una suspendida no cambia (R-05); un cliente no se suspende (R-03); una con eliminación programada sí (R-06).</summary>
    public static SuspendDecision Suspender(string state, bool isTechnician, string targetSub, string moderatorSub)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetSub);
        ArgumentException.ThrowIfNullOrWhiteSpace(moderatorSub);
        if (string.Equals(targetSub, moderatorSub, StringComparison.OrdinalIgnoreCase))
        {
            return SuspendDecision.SelfSuspension;
        }

        if (state == SuspensionAccountStates.Suspended)
        {
            return SuspendDecision.AlreadySuspended;
        }

        return isTechnician ? SuspendDecision.Suspend : SuspendDecision.NotTechnician;
    }

    /// <summary>Solo una cuenta <c>suspendida</c> se reactiva (R-15).</summary>
    public static ReactivateDecision Reactivar(string state)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(state);
        return state == SuspensionAccountStates.Suspended ? ReactivateDecision.Reactivate : ReactivateDecision.NotSuspended;
    }

    /// <summary>
    /// Regla gradual (R-16): con el umbral sin configurar la regla está apagada (sin valor por omisión); con él, se aplica al llegar al umbral. Solo se cuentan advertencias decididas
    /// y vigentes: el llamador no pasa reportes abiertos ni desestimados (R-17).
    /// </summary>
    public static bool ShouldApplyGradual(int validWarnings, int? threshold) => threshold is { } limit && limit >= 1 && validWarnings >= limit;
}

/// <summary>Configuración <c>Suspension:*</c> (spec 059, R-16). Sin <see cref="UmbralAdvertencias"/> la regla gradual queda apagada.</summary>
internal sealed class SuspensionOptions
{
    public const string SectionName = "Suspension";

    public int? UmbralAdvertencias { get; set; }
}

/// <summary>Valida al arrancar que el umbral, si existe, sea mayor o igual que 1 (spec 059, CA-13).</summary>
internal sealed class SuspensionOptionsValidator : IValidateOptions<SuspensionOptions>
{
    public ValidateOptionsResult Validate(string? name, SuspensionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.UmbralAdvertencias is < 1
            ? ValidateOptionsResult.Fail("Suspension:UmbralAdvertencias must be 1 or greater when it is configured.")
            : ValidateOptionsResult.Success;
    }
}
