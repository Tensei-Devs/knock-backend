using System.Text.RegularExpressions;

namespace Knock.ContractTest;

/// <summary>
/// Criterio: toda acción de un controlador de Mercado que escribe un 409 o un 503 (<c>Status409Conflict</c>, <c>Status503ServiceUnavailable</c>, <c>Error(409, ...)</c>,
/// <c>Rule(409, ...)</c>) debe declararlo con <c>ProducesResponseType</c> para que el contrato OpenAPI generado lo publique. Se lee el código fuente de cada acción
/// (el tramo entre un <c>[HttpXxx]</c> y el siguiente; los ayudantes privados quedan en el tramo de la última acción) y no se infiere nada de los servicios: así no hay
/// falsos positivos por códigos que una ruta no devuelve. Los 409 que un servicio lanza como <c>DomainException</c> sin pasar por el controlador se revisan a mano al declarar.
/// </summary>
public sealed partial class MarketConflictAndUnavailableDeclarationTests
{
    [GeneratedRegex(@"^\s*\[Http(Get|Post|Put|Patch|Delete)\b", RegexOptions.Multiline)]
    private static partial Regex ActionStart();

    [Theory]
    [InlineData(409)]
    [InlineData(503)]
    public void Every_Action_That_Writes_The_Status_Declares_It_In_The_Contract(int status)
    {
        var inherited = Documented(File.ReadAllText(Path.Combine(ModulesDirectory(), "Requests", "Controllers", "PrivateRequestsController.cs")), status);
        var missing = new List<string>();
        var actions = 0;
        foreach (var file in ControllerFiles())
        {
            actions += Check(file, status, inherited, missing);
        }

        Assert.True(actions > 50, "No se encontraron las acciones de los controladores de Mercado.");
        Assert.True(missing.Count == 0, $"Acciones que devuelven {status} sin declararlo: {string.Join(", ", missing)}");
    }

    private static int Check(string file, int status, bool inheritedByDocumented, List<string> missing)
    {
        var source = File.ReadAllText(file);
        var starts = ActionStart().Matches(source).Select(match => match.Index).ToArray();
        if (starts.Length == 0)
        {
            return 0;
        }

        var classLevel = Documented(source[..starts[0]], status)
            || (inheritedByDocumented && source.Contains(": DocumentedRequestsController", StringComparison.Ordinal));
        var tailStart = source.IndexOf("\n    private ", starts[^1], StringComparison.Ordinal);
        tailStart = tailStart < 0 ? source.Length : tailStart;
        var anyDeclares = classLevel;
        for (var index = 0; index < starts.Length; index++)
        {
            var span = source[starts[index]..(index + 1 < starts.Length ? starts[index + 1] : tailStart)];
            var declares = classLevel || Documented(span, status);
            anyDeclares |= declares;
            if (Emits(span, status) && !declares)
            {
                missing.Add($"{Path.GetFileName(file)}#{index + 1}");
            }
        }

        // Los ayudantes privados sirven a varias acciones: basta con que alguna del archivo (o la clase) declare el código.
        if (Emits(source[tailStart..], status) && !anyDeclares)
        {
            missing.Add($"{Path.GetFileName(file)}#helpers");
        }

        return starts.Length;
    }

    private static string[] CodeLines(string text) =>
        [.. text.Split('\n').Select(line => line.Trim()).Where(line => !line.StartsWith("//", StringComparison.Ordinal))];

    private static bool Documented(string text, int status) =>
        CodeLines(text).Any(line => line.Contains("ProducesResponseType", StringComparison.Ordinal) && MentionsStatus(line, status));

    private static bool Emits(string text, int status) =>
        CodeLines(text).Any(line => !line.Contains("ProducesResponseType", StringComparison.Ordinal) && MentionsStatus(line, status));

    private static bool MentionsStatus(string line, int status)
    {
        var symbol = status == 409 ? "Status409Conflict" : "Status503ServiceUnavailable";
        return line.Contains(symbol, StringComparison.Ordinal) || Regex.IsMatch(line, $@"\b(Error|Rule)\({status}\b|ProducesResponseType(<[^>]*>)?\({status}\b");
    }

    private static string ModulesDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Knock.slnx")))
        {
            directory = directory.Parent;
        }

        var root = directory?.FullName ?? throw new InvalidOperationException("Knock.slnx was not found above the test output directory.");
        return Path.Combine(root, "src", "Knock.Market.Api", "Modules");
    }

    private static IEnumerable<string> ControllerFiles() => Directory.EnumerateFiles(ModulesDirectory(), "*Controller.cs", SearchOption.AllDirectories);
}
