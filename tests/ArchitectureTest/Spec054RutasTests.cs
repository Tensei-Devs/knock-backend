using System.Reflection;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace Knock.ArchitectureTest;

/// <summary>
/// Spec 054 R-01, R-15, CA-01 y CA-22: la bandeja de salida (<c>outbox</c>) y las tareas programadas no tienen ruta HTTP en ningún servicio y el gateway no enruta ninguna ruta interna (ni la de avisos).
/// </summary>
public class Spec054RutasTests
{
    private static readonly string[] _services = ["Knock.Identity.Api", "Knock.Market.Api", "Knock.Messaging.Api"];
    private static readonly string[] _forbiddenSegments = ["outbox", "tareas", "tareas_programadas", "tareas-programadas", "bandeja"];

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Knock.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Knock.slnx was not found above the test output directory.");
    }

    /// <summary>Plantillas de ruta declaradas por controladores y acciones (<c>[Route]</c>, <c>[HttpGet]</c>…) de un servicio.</summary>
    private static List<string> RouteTemplates(string assemblyName) =>
        [.. Assembly.Load(assemblyName).GetTypes()
            .Where(type => type.IsClass && type.Name.EndsWith("Controller", StringComparison.Ordinal))
            .SelectMany(type => type.GetCustomAttributes(inherit: true).Concat(type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).SelectMany(method => method.GetCustomAttributes(inherit: true))))
            .OfType<IRouteTemplateProvider>()
            .Select(provider => provider.Template)
            .Where(template => !string.IsNullOrEmpty(template))
            .Select(template => template!)];

    private static List<string> GatewayPaths()
    {
        var paths = new List<string>();
        foreach (var file in new[] { "appsettings.json", "appsettings.Development.json" })
        {
            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "Knock.Gateway", file)));
            if (!document.RootElement.TryGetProperty("ReverseProxy", out var proxy) || !proxy.TryGetProperty("Routes", out var routes))
            {
                continue;
            }

            paths.AddRange(routes.EnumerateObject()
                .Select(route => route.Value.TryGetProperty("Match", out var match) && match.TryGetProperty("Path", out var path) ? path.GetString() : null)
                .OfType<string>());
        }

        return paths;
    }

    private static bool HasForbiddenSegment(string template) =>
        template.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(segment => _forbiddenSegments.Contains(segment, StringComparer.OrdinalIgnoreCase));

    [Theory]
    [InlineData("Knock.Identity.Api")]
    [InlineData("Knock.Market.Api")]
    [InlineData("Knock.Messaging.Api")]
    public void Ningun_Servicio_Expone_Rutas_De_La_Bandeja_Ni_De_Las_Tareas_CA_054_01(string service)
    {
        var templates = RouteTemplates(service);

        templates.Should().NotBeEmpty("si no hay rutas la regla pasaría en vacío");
        templates.Where(HasForbiddenSegment).Should().BeEmpty("outbox y tareas_programadas no tienen ruta HTTP");
    }

    [Fact]
    public void El_Gateway_No_Enruta_Ninguna_Ruta_Interna_Ni_De_La_Bandeja_CA_054_01()
    {
        var paths = GatewayPaths();

        paths.Should().HaveCountGreaterThan(20, "si no hay rutas la regla pasaría en vacío");
        paths.Where(path => HasForbiddenSegment(path) || path.Contains("interno", StringComparison.OrdinalIgnoreCase)).Should().BeEmpty("las llamadas internas van por el ingreso privado, nunca por el gateway");
    }

    [Fact]
    public void Las_Rutas_Internas_De_Los_Servicios_Si_Existen_Y_Por_Eso_La_Regla_Del_Gateway_Importa_CA_054_01()
    {
        _services.SelectMany(RouteTemplates).Count(template => template.Contains("/interno/", StringComparison.OrdinalIgnoreCase)).Should().BeGreaterThan(5);
    }

    private static bool IsInternalRoute(Type type) =>
        type.IsClass && type.Name.EndsWith("Controller", StringComparison.Ordinal)
        && type.GetCustomAttributes(inherit: true).OfType<IRouteTemplateProvider>().Any(route => route.Template?.Contains("/interno/", StringComparison.OrdinalIgnoreCase) == true);

    /// <summary>Controladores con una ruta <c>/interno/</c> que no declaran la credencial de servicio (filtro de Identidad o política <c>Internal*</c> de Mercado y Mensajería).</summary>
    private static List<string> InternalControllersWithoutServiceCredential(IEnumerable<Type> types) =>
        [.. types.Where(IsInternalRoute)
            .Where(type => !type.GetCustomAttributes(inherit: true).Any(attribute => attribute.GetType().Name == "InternalServiceTokenAttribute"
                || (attribute is AuthorizeAttribute authorize && authorize.Policy?.StartsWith("Internal", StringComparison.Ordinal) == true)))
            .Select(type => type.FullName ?? type.Name)];

    [Fact]
    public void Toda_Ruta_Interna_Exige_La_Credencial_De_Servicio_Con_Su_Permiso_Y_Ninguna_Es_Anonima_CA_054_22()
    {
        var types = _services.SelectMany(service => Assembly.Load(service).GetTypes()).ToList();

        types.Count(IsInternalRoute).Should().BeGreaterThan(8, "si no hay controladores internos la regla pasaría en vacío");
        InternalControllersWithoutServiceCredential(types).Should().BeEmpty("cada ruta interna valida emisor, destinatario, vida de 5 minutos y el permiso de su ruta");
    }

    [Fact]
    public void La_Regla_De_Credencial_Puede_Fallar_Para_Un_Controlador_Interno_Sin_Ella_CA_054_22() =>
        InternalControllersWithoutServiceCredential([typeof(SinCredencialController), typeof(ConCredencialController)])
            .Should().ContainSingle().Which.Should().Contain(nameof(SinCredencialController));

    // Controladores de muestra: solo se inspeccionan por reflexión, nunca se crean.
    [Microsoft.AspNetCore.Mvc.Route("api/v1/interno/prueba")]
    private static class SinCredencialController;

    [Microsoft.AspNetCore.Mvc.Route("api/v1/interno/prueba")]
    [Authorize(Policy = "InternalPrueba")]
    private static class ConCredencialController;
}
