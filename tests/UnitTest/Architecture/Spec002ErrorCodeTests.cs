extern alias gateway;
extern alias messaging;

using System.Reflection;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Knock.Identity.Api.Modules.Sesiones.Services;
using Knock.Market.Api.Modules.Catalog;
using Xunit;
using GatewayHosting = gateway::Knock.Gateway.Hosting;
using MessagingHosting = messaging::Knock.Messaging.Api.Hosting;

namespace Knock.UnitTest.Architecture;

/// <summary>
/// Spec 002, convención de errores (CA-07): todos los <c>code</c> de negocio que declaran los servicios son nombres en inglés, en <c>snake_case</c> y sin repetir.
/// Recorre las constantes de texto cuyo nombre termina en <c>Code</c> de los cuatro ensamblados; un código nuevo entra en la regla solo con declararlo así.
/// </summary>
public sealed partial class Spec002ErrorCodeTests
{
    private static readonly Assembly[] _assemblies =
    [
        typeof(LoginService).Assembly,
        typeof(CatalogModuleExtensions).Assembly,
        typeof(MessagingHosting.LoggingExtensions).Assembly,
        typeof(GatewayHosting.LoggingExtensions).Assembly,
    ];

    /// <summary>Constantes de texto cuyo nombre termina en <c>Code</c> y que están en tipos del propio servicio, con el servicio al que pertenecen.</summary>
    private static IEnumerable<(string Service, string Field, string Value)> Declared() =>
        _assemblies.SelectMany(assembly => assembly.GetTypes()
            .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(field => field.IsLiteral && field.FieldType == typeof(string) && field.Name.EndsWith("Code", StringComparison.Ordinal))
            .Select(field => (assembly.GetName().Name!, $"{field.DeclaringType!.Name}.{field.Name}", (string)field.GetRawConstantValue()!)));

    /// <summary>Lo que incumple la convención: texto que no es <c>snake_case</c> en ASCII minúscula, o repetido dentro de su servicio.</summary>
    internal static IReadOnlyList<string> Violations(IEnumerable<(string Service, string Field, string Value)> codes)
    {
        var list = codes.ToList();
        var notSnake = list.Where(c => !SnakeCase().IsMatch(c.Value)).Select(c => $"{c.Service}: {c.Field} = «{c.Value}» no es snake_case en inglés");
        var duplicated = list.GroupBy(c => (c.Service, c.Value)).Where(g => g.Count() > 1)
            .Select(g => $"{g.Key.Service}: «{g.Key.Value}» está repetido en {string.Join(", ", g.Select(c => c.Field))}");
        return [.. notSnake, .. duplicated];
    }

    [Fact]
    public void AllErrorCodes_AreEnglishSnakeCaseAndUnique_CA_002_07()
    {
        var codes = Declared().ToList();

        codes.Should().NotBeEmpty("la prueba tiene que ver los códigos que ya existen");
        codes.Select(c => c.Value).Should().Contain(["account_suspended", "colonia_not_found", "deletion_scheduled"]);
        Violations(codes).Should().BeEmpty();
    }

    [Fact]
    public void TheRuleCanFail_ForSpanishCamelCaseAndDuplicatedCodes_CA_002_07()
    {
        (string Service, string Field, string Value)[] sample =
        [
            ("svc", "A.SpanishCode", "cuenta_suspendida_ñ"),
            ("svc", "A.CamelCode", "accountSuspended"),
            ("svc", "A.UpperCode", "ACCOUNT_SUSPENDED"),
            ("svc", "B.OneCode", "quote_duplicate"),
            ("svc", "B.TwoCode", "quote_duplicate"),
            ("other", "C.OneCode", "quote_duplicate"),
        ];

        var violations = Violations(sample);

        violations.Should().HaveCount(4);
        violations.Should().Contain(v => v.Contains("cuenta_suspendida_ñ", StringComparison.Ordinal))
            .And.Contain(v => v.Contains("accountSuspended", StringComparison.Ordinal))
            .And.Contain(v => v.Contains("ACCOUNT_SUSPENDED", StringComparison.Ordinal))
            .And.Contain(v => v.Contains("repetido", StringComparison.Ordinal));
    }

    [GeneratedRegex("^[a-z][a-z0-9]*(_[a-z0-9]+)*$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 200)]
    private static partial Regex SnakeCase();
}
