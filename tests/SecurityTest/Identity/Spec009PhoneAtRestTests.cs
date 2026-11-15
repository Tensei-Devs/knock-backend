extern alias IdentityApi;

using AwesomeAssertions;
using IdentityApi::Knock.Identity.Api.Modules.Sesiones.Data;
using IdentityApi::Knock.Identity.Api.Modules.Sesiones.Services;
using Knock.SecurityTest.Support;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.SecurityTest.Identity;

/// <summary>
/// Spec 009, CA-05: el celular guardado no aparece en claro al leer la base directo, y la búsqueda por HMAC lo encuentra sin descifrar nada. Host real de Identidad
/// con MongoDB real. El celular es ficticio.
/// </summary>
public sealed class Spec009PhoneAtRestTests
{
    [Fact]
    public async Task StoredPhone_IsNotInClearInTheDatabase_AndTheHmacSearchFindsIt_CA_009_05()
    {
        var hosts = await SecurityHosts.SharedAsync();
        var person = await hosts.Identity.SignInAsync(hosts.IdentityClient);
        var digits = person.Phone.TrimStart('+');

        var stored = await hosts.Identity.Users.Find(new BsonDocument(SesionesStore.SubField, person.Sub)).SingleAsync();
        var raw = stored.ToJson();

        raw.Should().NotContain(person.Phone, "el celular no se guarda en claro").And.NotContain(digits);
        stored.Names.Should().NotContain(["celular", "telefono", "phone"]);
        stored[SesionesStore.PhoneCiphertextField].IsBsonBinaryData.Should().BeTrue("se guarda cifrado");
        stored[SesionesStore.PhoneHashField].IsBsonBinaryData.Should().BeTrue("y con su HMAC para buscarlo");

        var hash = hosts.Identity.Services.GetRequiredService<PhoneProtector>().Hash(person.Phone);
        var found = await hosts.Identity.Users.Find(new BsonDocument(SesionesStore.PhoneHashField, new BsonBinaryData(hash.Value))).ToListAsync();
        found.Should().ContainSingle().Which[SesionesStore.SubField].AsString.Should().Be(person.Sub, "la búsqueda por HMAC encuentra la cuenta de ese celular");

        var other = await hosts.Identity.Users.Find(new BsonDocument(SesionesStore.PhoneHashField, new BsonBinaryData(
            hosts.Identity.Services.GetRequiredService<PhoneProtector>().Hash(SecurityIdentityFactory.NewPhone()).Value))).ToListAsync();
        other.Should().BeEmpty("otro celular no encuentra esta cuenta");
    }
}
