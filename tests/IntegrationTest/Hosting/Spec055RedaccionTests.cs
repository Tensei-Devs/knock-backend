using System.Text.Json;
using AwesomeAssertions;
using Knock.IntegrationTest.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Knock.IntegrationTest.Hosting;

/// <summary>Spec 055, CA-05: cada proyecto arrancado redacta sus registros de consola con la biblioteca compartida. Sin contenedores; solo datos ficticios.</summary>
public sealed class Spec055RedaccionTests
{
    private static readonly Action<Microsoft.Extensions.Logging.ILogger, string, string, string, string, Exception?> _sensitive =
        LoggerMessage.Define<string, string, string, string>(LogLevel.Error, new EventId(5505, "Spec055Redaccion"),
            "Spec055 {Coordenadas} {Correo} {Autorizacion} {Direccion}");

    public static TheoryData<string> Projects => [.. Spec055Hosts.Projects];

    [Theory]
    [MemberData(nameof(Projects))]
    public async Task Cada_Proyecto_Redacta_Con_La_Implementacion_Compartida_CA_055_05(string project)
    {
        await using var host = Spec055Hosts.Start(project, new Dictionary<string, string>());
        var token = string.Join('.', "eyJ" + "hbGciOiJub25lIn0", "eyJ" + "zdWIiOiJmaWN0aWNpbyJ9", "firma-ficticia");
        var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Spec055." + project);

        var output = await ConsoleCapture.RunAsync(() =>
        {
            _sensitive(logger, "19.4326, -99.1332", "persona@ejemplo.test", "Bearer " + token, "https://destino.test/buscar?q=consulta-ficticia", null);
            return Task.CompletedTask;
        });

        var line = output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Should().ContainSingle(l => l.Contains("Spec055Redaccion", StringComparison.Ordinal)).Subject;
        using var json = JsonDocument.Parse(line);
        json.RootElement.GetProperty("@m").GetString().Should().Contain("[redacted]");
        line.Should().NotContain("19.4326").And.NotContain("99.1332").And.NotContain("persona@ejemplo.test")
            .And.NotContain("firma-ficticia").And.NotContain("consulta-ficticia");
    }
}
