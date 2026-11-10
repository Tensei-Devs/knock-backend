using AwesomeAssertions;
using Xunit;

namespace Knock.UnitTest.Infrastructure;

/// <summary>
/// Spec 011, CA-03 (R-05): un pico de errores dispara la alerta. Es una prueba estática de la infraestructura: lee
/// los <c>.tf</c> de <c>deploy/</c> y comprueba que la regla de alerta de métricas existe sobre las peticiones
/// fallidas del Application Insights, con ventana de 5 minutos, evaluada cada minuto, umbral por variable y grupo
/// de acciones enlazado, con el correo también por variable y sin valor por defecto real. El disparo real (que el
/// umbral se cruce y llegue el correo) se comprueba tras desplegar, contra Application Insights y el buzón del
/// receptor; Terraform no corre en el CI.
/// </summary>
public sealed class Spec011AlertRuleTests
{
    [Fact]
    public async Task SharedModule_MetricAlertWatchesFailedRequestsInFiveMinuteWindow_CA_011_03()
    {
        var main = await ReadTerraformAsync("deploy", "modules", "shared", "main.tf");
        var alert = ExtractBlock(main, "resource \"azurerm_monitor_metric_alert\"");

        alert.Should().NotBeEmpty("debe existir una regla de alerta de métricas en el módulo compartido");
        alert.Should().Contain("requests/failed", "la métrica son las peticiones fallidas");
        HasAttribute(alert, "scopes", "[azurerm_application_insights.this.id]").Should()
            .BeTrue("la regla se define sobre el Application Insights del módulo compartido");
        HasAttribute(alert, "window_size", "\"PT5M\"").Should().BeTrue("la ventana es de 5 minutos");
        HasAttribute(alert, "frequency", "\"PT1M\"").Should().BeTrue("se evalúa cada minuto");
        HasAttribute(alert, "aggregation", "\"Count\"").Should().BeTrue(
            "la tabla de métricas de Microsoft.Insights/components admite solo Count para requests/failed: https://learn.microsoft.com/azure/azure-monitor/reference/supported-metrics/microsoft-insights-components-metrics");
        HasAttribute(alert, "operator", "\"GreaterThan\"").Should().BeTrue("dispara por encima del umbral");
        HasAttribute(alert, "severity", "2").Should().BeTrue("es severidad 2 (aviso)");
        HasAttribute(alert, "threshold", "var.alert_failed_requests_threshold").Should()
            .BeTrue("el umbral viene de una variable y no de un número fijo en el recurso");
        HasAttribute(alert, "action_group_id", "azurerm_monitor_action_group.this.id").Should()
            .BeTrue("la regla está enlazada al grupo de acciones");
    }

    [Fact]
    public async Task SharedModule_ActionGroupEmailComesFromVariableWithoutRealDefault_CA_011_03()
    {
        var main = await ReadTerraformAsync("deploy", "modules", "shared", "main.tf");
        var variables = await ReadTerraformAsync("deploy", "modules", "shared", "variables.tf");
        var group = ExtractBlock(main, "resource \"azurerm_monitor_action_group\"");
        var receiver = ExtractBlock(variables, "variable \"alert_email_receiver\"");
        var threshold = ExtractBlock(variables, "variable \"alert_failed_requests_threshold\"");

        group.Should().NotBeEmpty("debe existir el grupo de acciones que recibe la alerta");
        group.Should().Contain("dynamic \"email_receiver\"", "el receptor es dinámico: sin correo, el grupo no tiene receptores");
        group.Should().Contain("var.alert_email_receiver", "el correo se toma de una variable");

        receiver.Should().NotBeEmpty("la variable del correo debe estar declarada");
        HasAttribute(receiver, "default", "\"\"").Should()
            .BeTrue("sin valor por defecto real: ninguna dirección vive en el repositorio");

        threshold.Should().NotBeEmpty("la variable del umbral debe estar declarada");
        HasAttribute(threshold, "default", "10").Should().BeTrue("el umbral por defecto es razonable (10 fallos en la ventana)");
    }

    [Fact]
    public async Task InfraRoot_PassesAlertVariablesToSharedModule_CA_011_03()
    {
        var main = await ReadTerraformAsync("deploy", "infra", "main.tf");
        var variables = await ReadTerraformAsync("deploy", "infra", "variables.tf");
        var module = ExtractBlock(main, "module \"shared\"");
        var receiver = ExtractBlock(variables, "variable \"alert_email_receiver\"");
        var threshold = ExtractBlock(variables, "variable \"alert_failed_requests_threshold\"");

        HasAttribute(module, "alert_email_receiver", "var.alert_email_receiver").Should()
            .BeTrue("infra pasa el correo al módulo compartido: sin eso no se puede configurar al desplegar");
        HasAttribute(module, "alert_failed_requests_threshold", "var.alert_failed_requests_threshold").Should()
            .BeTrue("infra pasa el umbral al módulo compartido");

        receiver.Should().NotBeEmpty("la raíz infra expone la variable del correo");
        HasAttribute(receiver, "default", "\"\"").Should().BeTrue("en infra tampoco hay correo por defecto");
        var sharedVariables = await ReadTerraformAsync("deploy", "modules", "shared", "variables.tf");
        var sharedReceiver = ExtractBlock(sharedVariables, "variable \"alert_email_receiver\"");
        var validation = ExtractBlock(receiver, "validation");
        validation.Should().NotBeEmpty("infra valida el correo antes de pasarlo al módulo");
        validation.Should().Be(ExtractBlock(sharedReceiver, "validation"), "la validación debe coincidir con la del módulo");
        threshold.Should().NotBeEmpty("la raíz infra expone la variable del umbral");
        HasAttribute(threshold, "default", "10").Should().BeTrue("el umbral por defecto de infra coincide con el del módulo");
    }

    /// <summary>Devuelve el bloque de <paramref name="declaration"/> hasta su llave de cierre; vacío si no existe.</summary>
    private static string ExtractBlock(string content, string declaration)
    {
        var start = content.IndexOf(declaration, StringComparison.Ordinal);
        if (start < 0)
        {
            return string.Empty;
        }

        var body = content.IndexOf('{', start, StringComparison.Ordinal);
        if (body < 0)
        {
            return string.Empty;
        }

        var depth = 0;
        for (var i = body; i < content.Length; i++)
        {
            if (content[i] == '{')
            {
                depth++;
            }
            else if (content[i] == '}')
            {
                depth--;
            }

            if (depth == 0)
            {
                return content[start..(i + 1)];
            }
        }

        return string.Empty;
    }

    /// <summary>Comprueba que el bloque declara <paramref name="attribute"/> con el literal exacto de <paramref name="value"/>.</summary>
    private static bool HasAttribute(string block, string attribute, string value)
    {
        foreach (var line in block.Split('\n'))
        {
            var separator = line.IndexOf('=', StringComparison.Ordinal);
            if (separator < 0)
            {
                continue;
            }

            if (line[..separator].Trim() == attribute && line[(separator + 1)..].Trim() == value)
            {
                return true;
            }
        }

        return false;
    }

    private static async Task<string> ReadTerraformAsync(params string[] segments)
    {
        var path = RepositoryRoot();
        foreach (var segment in segments)
        {
            path = Path.Combine(path, segment);
        }

        return await File.ReadAllTextAsync(path);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Knock.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("No se encontró Knock.slnx subiendo desde el directorio de pruebas.");
    }
}
