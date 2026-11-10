using System.Reflection;
using System.Text.RegularExpressions;
using NetArchTest.Rules;

namespace Knock.UnitTest.Architecture;

/// <summary>
/// Acceso a Redis (spec 003, CA-02): <c>StackExchange.Redis</c> solo en <c>Modules/*/Data</c> y en <c>Hosting/</c>, que lo comparte
/// entre módulos. El parámetro <c>root</c> de <c>Evaluate</c> es el espacio raíz (<c>Knock.Market.Api</c> o el de un fixture), con
/// <c>.Modules</c> y <c>.Hosting</c> debajo. Es el mismo predicado de la prueba positiva, para que la prueba negativa con un fixture
/// que viola la regla falle si la regla se rompe.
/// </summary>
internal static class RedisAccessRule
{
    public const string ClientNamespace = "StackExchange.Redis";

    public static BoundaryResult Evaluate(Assembly assembly, string root)
    {
        var moduleData = $"^{Regex.Escape($"{root}.Modules")}[.][^.]+[.]Data([.]|$)";
        var hosting = $"^{Regex.Escape($"{root}.Hosting")}([.]|$)";

        var result = Types.InAssembly(assembly)
            .That().DoNotResideInNamespaceMatching(moduleData)
            .And().DoNotResideInNamespaceMatching(hosting)
            .ShouldNot().HaveDependencyOnAny(ClientNamespace)
            .GetResult();

        var failing = (result.FailingTypes ?? []).Select(type => type.FullName).ToList();
        return new BoundaryResult(failing.Count == 0, failing);
    }
}
