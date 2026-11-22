using Knock.Contracts.Moderation;
using Knock.Messaging.Api.Modules.Conversaciones.Data;
using Knock.Messaging.Api.Modules.Conversaciones.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Knock.Messaging.Api.Modules.Conversaciones;

/// <summary>Registro del módulo Conversaciones (spec 030). Solo esta clase es pública; el resto del módulo es internal.</summary>
public static class ConversacionesModuleExtensions
{
    /// <summary>Registra el manejador que traduce <c>DomainException</c> a ProblemDetails con el <c>code</c> de su catálogo.</summary>
    public static IServiceCollection AddConversacionesModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddExceptionHandler<DomainExceptionHandler>();
        services.AddOptions<ConversacionesOptions>().BindConfiguration(ConversacionesOptions.SectionName).ValidateOnStart();
        services.AddSingleton<IValidateOptions<ConversacionesOptions>, ConversacionesOptionsValidator>();
        services.AddSingleton<ConversacionesStore>();
        services.AddSingleton<MensajesStore>();
        services.AddSingleton<INotificadorEnVivo, NotificadorEnVivo>();
        services.AddScoped<ConversacionService>();
        services.AddScoped<LecturaService>();
        services.AddSingleton<PermisoCache>();
        services.AddMarketPermisoDeConversacion();
        services.TryAddSingleton<IContentModerator, ModeradorNoDisponible>();
        services.AddScoped<EnvioDeMensajeService>();
        services.AddScoped<AccesoAConversacionService>();
        return services;
    }
}
