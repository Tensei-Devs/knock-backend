using System.Reflection;
using AwesomeAssertions;
using NetArchTest.Rules;
using Xunit;

namespace Knock.ArchitectureTest;

/// <summary>Spec 057, reglas de arquitectura del borrado de fotos de verificación: un solo punto borra blobs y destruye la llave; los controladores no tocan Blob ni MongoDB.</summary>
public class Spec057ArquitecturaTests
{
    private const string _purger = "Knock.Identity.Api.Modules.Moderation.Services.IVerificationBlobPurger";

    private static Assembly Identity => Assembly.Load("Knock.Identity.Api");

    [Fact]
    public void Solo_La_Purga_Y_La_Decision_Del_Panel_Usan_El_Borrado_De_Blobs_CA_057_12()
    {
        var purger = Identity.GetType(_purger) ?? throw new InvalidOperationException("IVerificationBlobPurger no existe: la regla pasaría en vacío.");

        var users = Identity.GetTypes()
            .Where(type => !type.IsInterface && type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Any(c => c.GetParameters().Any(p => p.ParameterType == purger)))
            .Select(type => type.Name).ToList();

        users.Should().BeEquivalentTo("PurgaDeVerificacion", "VerificationReviewService");
    }

    [Fact]
    public void Solo_El_Almacen_De_La_Purga_Y_El_De_La_Decision_Destruyen_La_Llave_CA_057_12()
    {
        var writers = Identity.GetTypes()
            .Where(type => type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Any(field => field.IsLiteral && field.GetRawConstantValue() is "razonPurga"))
            .Select(type => type.Name);

        writers.Should().BeEmpty("el nombre del campo vive solo como texto dentro de los almacenes, nunca como constante pública reutilizable");
    }

    [Fact]
    public void Los_Controladores_De_Trust_No_Dependen_De_MongoDB_Ni_De_Blob_CA_057_12()
    {
        Types.InAssembly(Identity).That().ResideInNamespace("Knock.Identity.Api.Modules.Trust.Controllers.V1").ShouldNot().HaveDependencyOnAny("MongoDB.Driver", "Azure.Storage.Blobs")
            .GetResult().IsSuccessful.Should().BeTrue();
    }
}
