using System.Reflection;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Xunit;

namespace Knock.ArchitectureTest;

/// <summary>Spec 058, R-15 (CA-16): el almacén de registros en Blob y su contenedor temporal no vuelven; el único almacén de archivos registrado es el de MongoDB.</summary>
public sealed partial class Spec058RetiroTests
{
    private static readonly string[] _folders = ["src", "deploy"];
    private static readonly string[] _retired = ["BlobMediaFileStore", "RecordsContainer", "registros-temporales"];

    [Fact]
    public void Sin_Blob_Store_Ni_Registros_Temporales_En_Src_Deploy_Ni_Appsettings_CA_058_16()
    {
        var root = RepositoryRoot();
        var files = _folders
            .SelectMany(folder => Directory.EnumerateFiles(Path.Combine(root, folder), "*", SearchOption.AllDirectories))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => Path.GetExtension(path) is ".cs" or ".json" or ".tf" or ".tfvars" or ".yml" or ".yaml" or ".sh" or ".ps1" or ".md")
            .ToList();
        files.Should().NotBeEmpty("la regla no puede pasar en vacío");

        var offenders = files
            .Where(path => _retired.Any(term => File.ReadAllText(path).Contains(term, StringComparison.Ordinal)))
            .Select(path => Path.GetRelativePath(root, path))
            .ToList();

        offenders.Should().BeEmpty("las menciones históricas viven solo en specs/_legacy y Memoria");
    }

    [Fact]
    public void El_Unico_Almacen_De_Archivos_Registrado_Es_El_De_MongoDB_CA_058_16()
    {
        var market = Assembly.Load("Knock.Market.Api");
        var implementations = market.GetTypes().Where(type => type is { IsClass: true, IsAbstract: false } && type.GetInterfaces().Any(i => i.Name == "IMediaFileStore")).Select(type => type.Name);
        implementations.Should().BeEquivalentTo("MongoMediaFileStore", "InMemoryMediaFileStore");

        var module = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "Knock.Market.Api", "Modules", "Media", "MediaModuleExtensions.cs"));
        var registrations = StoreRegistration().Matches(module).Select(match => match.Groups[1].Value).ToList();
        registrations.Should().Equal("MongoMediaFileStore");
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Knock.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Knock.slnx was not found above the test output directory.");
    }

    [GeneratedRegex(@"<IMediaFileStore,\s*(\w+)>", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 500)]
    private static partial Regex StoreRegistration();
}
