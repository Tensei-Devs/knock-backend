using Microsoft.Extensions.Options;

namespace Knock.Messaging.Api.Modules.Conversaciones.Services;

/// <summary>
/// Sección <c>Moderacion</c> de Mensajería. <see cref="PermitirSinModeracion"/> es el interruptor del modo sin moderación: APAGADO por omisión (falla cerrado, 503 <c>moderation_unavailable</c>)
/// y solo puede encenderse en Development; en cualquier otro entorno el servicio no arranca si está encendido. No llama a Azure ni copia la moderación de Mercado.
/// </summary>
internal sealed class ModeracionMensajeriaOptions
{
    public const string SectionName = "Moderacion";

    public bool PermitirSinModeracion { get; set; }
}

/// <summary>El modo sin moderación solo existe en Development: encenderlo en otro entorno impide arrancar. El mensaje nombra la clave, nunca el valor.</summary>
internal sealed class ModeracionMensajeriaOptionsValidator(IHostEnvironment environment) : IValidateOptions<ModeracionMensajeriaOptions>
{
    public ValidateOptionsResult Validate(string? name, ModeracionMensajeriaOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.PermitirSinModeracion && !environment.IsDevelopment()
            ? ValidateOptionsResult.Fail("Moderacion:PermitirSinModeracion is only allowed when ASPNETCORE_ENVIRONMENT is Development.")
            : ValidateOptionsResult.Success;
    }
}
