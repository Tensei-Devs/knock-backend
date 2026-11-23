using System.Reflection;
using AwesomeAssertions;
using Xunit;

namespace Knock.ArchitectureTest;

/// <summary>
/// Spec 061 (CA-06): fija la conclusión de que el token del panel no necesita un claim de segundo factor. Solo dos tipos pueden emitirlo (<c>PanelTokenIssuer</c>): <c>SesionPanelService</c>, que abre la sesión
/// y solo la llama <c>AccesoPanelService</c> después de verificar el TOTP, y <c>SesionPanelCicloService</c>, que renueva una sesión ya abierta con su cookie. Si aparece otro camino, esta prueba falla y
/// obliga a añadir un claim explícito y a exigirlo en la puerta de Mercado.
/// </summary>
public class Spec061DobleFactorArquitecturaTests
{
    private const string _services = "Knock.Identity.Api.Modules.Panel.Services";

    private static Assembly Identity => Assembly.Load("Knock.Identity.Api");

    private static Type Find(string name) =>
        Identity.GetType($"{_services}.{name}") ?? throw new InvalidOperationException($"{name} no existe: la regla pasaría en vacío.");

    private static IEnumerable<string> TypesDependingOn(Type dependency) =>
        Identity.GetTypes()
            .Where(type => type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Any(c => c.GetParameters().Any(p => p.ParameterType == dependency)))
            .Select(type => type.Name)
            .Order(StringComparer.Ordinal);

    [Fact]
    public void Solo_Dos_Tipos_Pueden_Emitir_El_Token_Del_Panel_CA_061_06()
    {
        TypesDependingOn(Find("PanelTokenIssuer")).Should().Equal("SesionPanelCicloService", "SesionPanelService");
    }

    [Fact]
    public void Solo_El_Acceso_Con_Totp_Abre_Una_Sesion_Del_Panel_CA_061_06()
    {
        TypesDependingOn(Find("SesionPanelService")).Should().Equal("AccesoPanelService");
        Find("SesionPanelService").GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => method.ReturnType.Name.StartsWith("Task", StringComparison.Ordinal) && method.ReturnType.GenericTypeArguments.Any(t => t.Name == "PanelGrant"))
            .Select(method => method.Name).Should().Equal("OpenAsync");
    }
}
