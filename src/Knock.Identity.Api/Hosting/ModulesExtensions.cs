// jscpd:ignore-start -- EXC-015
using Knock.Identity.Api.Modules.Sesiones;

namespace Knock.Identity.Api.Hosting;

internal static class ModulesExtensions
{
    /// <summary>Registra los módulos del servicio de Identidad.</summary>
    public static IServiceCollection AddKnockModules(this IServiceCollection services) => services.AddSesionesModule();
}
// jscpd:ignore-end
