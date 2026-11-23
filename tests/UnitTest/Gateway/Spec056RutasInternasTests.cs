using System.Text.Json;
using AwesomeAssertions;
using Xunit;

namespace Knock.UnitTest.Gateway;

/// <summary>Spec 056 (T-04, R-07): ninguna ruta interna (<c>/api/v1/interno/*</c>) está en la lista cerrada de rutas del gateway.</summary>
public sealed class Spec056RutasInternasTests
{
    private const string _internalSample = "/api/v1/interno/avisos";

    [Fact]
    public void Ninguna_Ruta_Del_Gateway_Alcanza_Las_Rutas_Internas_CA_056_07()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "Knock.Gateway", "appsettings.json")));
        var routes = document.RootElement.GetProperty("ReverseProxy").GetProperty("Routes").EnumerateObject().ToList();
        var patterns = routes.Select(route => route.Value.GetProperty("Match").GetProperty("Path").GetString()!).ToList();

        patterns.Should().NotBeEmpty();
        patterns.Should().NotContain(pattern => pattern.Contains("interno", StringComparison.OrdinalIgnoreCase), "la configuración del gateway no contiene ninguna ruta interna");
        patterns.Where(Reaches).Should().BeEmpty("ningún patrón (ni uno general de captura total) enruta una ruta interna");
    }

    [Fact]
    public void La_Regla_Puede_Fallar_CA_056_07()
    {
        Reaches("/api/v1/{**resto}").Should().BeTrue();
        Reaches("/api/v1/interno/{**resto}").Should().BeTrue();
        Reaches("/api/v1/auth/{**resto}").Should().BeFalse();
    }

    private static bool Reaches(string pattern)
    {
        var catchAll = pattern.IndexOf("{**", StringComparison.Ordinal);
        return catchAll >= 0 ? _internalSample.StartsWith(pattern[..catchAll], StringComparison.OrdinalIgnoreCase) : string.Equals(pattern, _internalSample, StringComparison.OrdinalIgnoreCase);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Knock.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("No se encontró la raíz del repositorio.");
    }
}
