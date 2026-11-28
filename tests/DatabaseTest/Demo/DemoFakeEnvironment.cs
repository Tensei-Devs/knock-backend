using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Knock.DatabaseTest.Demo;

/// <summary>Entorno de pruebas para los cargadores de datos de demostración: solo importa <see cref="EnvironmentName"/>.</summary>
internal sealed class DemoFakeEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; } = Environments.Development;
    public string ApplicationName { get; set; } = string.Empty;
    public string ContentRootPath { get; set; } = string.Empty;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
