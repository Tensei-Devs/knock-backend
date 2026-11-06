using Knock.Contracts.Colonias;
using Knock.Identity.Api.Modules.Usuarios.Data;
using Knock.Identity.Api.Modules.Usuarios.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Knock.Identity.Api.Modules.Usuarios;

/// <summary>Registro del módulo Usuarios (spec 017: perfil de la persona). Solo esta clase es pública; el resto del módulo es internal.</summary>
public static class UsuariosModuleExtensions
{
    public static IServiceCollection AddUsuariosModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton(TimeProvider.System);
        services.AddOptions<ProfileOptions>().BindConfiguration(ProfileOptions.SectionName);
        services.AddSingleton<IUsuariosStore, UsuariosStore>();
        services.AddSingleton<IProfileCache, RedisProfileCache>();
        services.TryAddSingleton<IColoniaCatalog, UnavailableColoniaCatalog>();
        services.AddSingleton<ProfileRules>();
        services.AddSingleton<ProfileService>();
        return services;
    }
}
