extern alias IdentityApi;

using System.Net;
using System.Security.Cryptography;
using AwesomeAssertions;
using Azure.Storage.Blobs;
using IdentityApi::Knock.Identity.Api.Modules.Trust.Data;
using IdentityApi::Knock.Identity.Api.Modules.Trust.Domain;
using IdentityApi::Knock.Identity.Api.Modules.Trust.Services;
using IdentityApi::Knock.Identity.Api.Modules.Trust.Storage;
using Knock.IntegrationTest.Support;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.IntegrationTest.Identity;

/// <summary>
/// Spec 025, T-05 y T-07 (casos con fotos): envío a revisión, CURP duplicada y confirmación de fotos contra MongoDB real y, en los casos de blob, Azurite real con el
/// <c>AzureVerificationBlobStore</c> de producción. Azurite no firma SAS de delegación: los enlaces de subida son de mentira y la subida del cliente a la cuarentena se simula directo. Solo datos ficticios.
/// </summary>
public sealed partial class Spec025VerificacionTests
{
    private const string _quarantineContainer = "verificaciones-cuarentena";
    private const string _finalContainer = "verificaciones";
    private static readonly string[] _basicTypes = ["ine_frente", "selfie"];

    /// <summary>Abre una captura básica con el POST real y, si se pide, deja sus fotos como <c>cifrada</c> (la confirmación real de las fotos la prueban los casos con Azurite).</summary>
    private async Task<string> OpenCaptureAsync(Spec025Person person, bool photosEncrypted)
    {
        using var created = await SendAsync(HttpMethod.Post, person.Token, Body());
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await JsonAsync(created)).GetProperty("id").GetString()!;
        if (photosEncrypted)
        {
            await Verifications.UpdateOneAsync(
                Builders<BsonDocument>.Filter.Eq("_id", ObjectId.Parse(id)),
                Builders<BsonDocument>.Update.Set("fotos", new BsonArray(_basicTypes.Select(type => new BsonDocument { ["tipo"] = type, ["estado"] = "cifrada" }))));
        }

        return id;
    }

    private Task<HttpResponseMessage> SubmitAsync(Spec025Person person, string id) => SendAsync(HttpMethod.Post, person.Token, route: $"{_route}/{id}/enviar");

    private Task<HttpResponseMessage> ConfirmPhotoAsync(Spec025Person person, string id, string type) => SendAsync(HttpMethod.Post, person.Token, route: $"{_route}/{id}/fotos/{type}/confirmar");

    private async Task<BsonDocument> StoredAsync(string id) => await Verifications.Find(Builders<BsonDocument>.Filter.Eq("_id", ObjectId.Parse(id))).SingleAsync();

    private static byte[] SyntheticJpeg(int length)
    {
        var bytes = RandomNumberGenerator.GetBytes(length);
        bytes[0] = 0xFF;
        bytes[1] = 0xD8;
        bytes[2] = 0xFF;
        return bytes;
    }

    private static async Task UploadToQuarantineAsync(AzuriteFixture azurite, string name, byte[] content)
    {
        // En Azure Terraform crea ambos contenedores y el almacén no los crea (mínimo privilegio): en Azurite hay que crear también el definitivo.
        await azurite.Container(_finalContainer).CreateIfNotExistsAsync();
        var container = azurite.Container(_quarantineContainer);
        await container.CreateIfNotExistsAsync();
        await container.GetBlobClient(name).UploadAsync(BinaryData.FromBytes(content));
    }

    [Fact]
    public async Task Curp_En_Revision_De_Otra_Cuenta_Responde_409_Sin_Mencionarla_Y_Avisa_CA_025_07()
    {
        var ana = await NewPersonAsync();
        var bruno = await NewPersonAsync();
        using var sent = await SubmitAsync(ana, await OpenCaptureAsync(ana, photosEncrypted: true));

        using var duplicate = await SendAsync(HttpMethod.Post, bruno.Token, Body());

        sent.StatusCode.Should().Be(HttpStatusCode.OK);
        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await CodeAsync(duplicate)).Should().Be("curp_duplicate");
        var raw = await duplicate.Content.ReadAsStringAsync();
        raw.Should().NotContain(ana.Sub).And.NotContain(ana.UserId.ToString(), "el mensaje no menciona a la otra cuenta");
        _events.Published.Should().Contain(("CurpDuplicada", bruno.Sub));
        (await Verifications.CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("usuarioId", bruno.Sub))).Should().Be(0);
    }

    [Fact]
    public async Task Envia_A_Revision_Con_Vencimiento_De_Siete_Dias_Y_Aviso_CA_025_12()
    {
        var ana = await NewPersonAsync();
        var id = await OpenCaptureAsync(ana, photosEncrypted: true);

        using var sent = await SubmitAsync(ana, id);

        sent.StatusCode.Should().Be(HttpStatusCode.OK);
        (await JsonAsync(sent)).GetProperty("estado").GetString().Should().Be("en_revision");
        var stored = await StoredAsync(id);
        stored["estado"].AsString.Should().Be("en_revision");
        stored["curpVigente"].AsBoolean.Should().BeTrue();
        (stored["expiraEn"].ToUniversalTime() - stored["enviadoEn"].ToUniversalTime()).Should().Be(TimeSpan.FromDays(7));
        _events.Published.Should().Contain(("VerificacionEnviada", ana.Sub));
    }

    [Fact]
    public async Task Con_Una_Foto_Sin_Confirmar_Responde_409_Y_Sigue_En_Captura_CA_025_13()
    {
        var ana = await NewPersonAsync();
        var id = await OpenCaptureAsync(ana, photosEncrypted: false);

        using var sent = await SubmitAsync(ana, id);

        sent.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await CodeAsync(sent)).Should().Be("photos_missing");
        (await StoredAsync(id))["estado"].AsString.Should().Be("en_captura");
        _events.Published.Should().NotContain(("VerificacionEnviada", ana.Sub));
    }

    [Fact]
    public async Task Dos_Envios_Simultaneos_Con_La_Misma_Curp_Dejan_Una_Sola_En_Revision_CA_025_20()
    {
        var ana = await NewPersonAsync();
        var bruno = await NewPersonAsync();
        var anaId = await OpenCaptureAsync(ana, photosEncrypted: true);
        var brunoId = await OpenCaptureAsync(bruno, photosEncrypted: true);

        var responses = await Task.WhenAll(SubmitAsync(ana, anaId), SubmitAsync(bruno, brunoId));
        try
        {
            responses.Select(r => r.StatusCode).Should().BeEquivalentTo([HttpStatusCode.OK, HttpStatusCode.Conflict]);
            (await CodeAsync(responses.Single(r => r.StatusCode == HttpStatusCode.Conflict))).Should().Be("curp_duplicate");
            (await Verifications.CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("estado", "en_revision"))).Should().Be(1);
            (await Verifications.CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("curpVigente", true))).Should().Be(1);
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    [Fact]
    public async Task Confirma_Una_Foto_Cifrada_En_Azurite_Y_Vacia_La_Cuarentena_CA_025_09()
    {
        await using var azurite = await AzuriteFixture.StartAsync();
        _blobs.Use(azurite);
        var ana = await NewPersonAsync();
        var id = await OpenCaptureAsync(ana, photosEncrypted: false);
        var photo = SyntheticJpeg(2048);
        await UploadToQuarantineAsync(azurite, $"{id}/selfie", photo);

        using var confirmed = await ConfirmPhotoAsync(ana, id, "selfie");

        confirmed.StatusCode.Should().Be(HttpStatusCode.OK);
        (await JsonAsync(confirmed)).GetProperty("estado").GetString().Should().Be("cifrada");
        (await azurite.ListBlobNamesAsync(_finalContainer)).Should().Equal($"{id}/selfie");
        (await azurite.ListBlobNamesAsync(_quarantineContainer)).Should().BeEmpty("la cuarentena queda vacía");
        var stored = (await azurite.Container(_finalContainer).GetBlobClient($"{id}/selfie").DownloadContentAsync()).Value.Content.ToArray();
        stored.AsSpan().IndexOf(photo.AsSpan(0, 64)).Should().Be(-1, "el blob definitivo no lleva la imagen en claro");
        (await StoredAsync(id))["fotos"].AsBsonArray.Single(p => p["tipo"] == "selfie")["estado"].AsString.Should().Be("cifrada");
    }

    [Fact]
    public async Task Contenido_Que_No_Es_Imagen_O_Pesa_Mas_De_8_MB_Responde_422_Y_No_Deja_Ningun_Blob_CA_025_10()
    {
        await using var azurite = await AzuriteFixture.StartAsync();
        _blobs.Use(azurite);
        var ana = await NewPersonAsync();
        var id = await OpenCaptureAsync(ana, photosEncrypted: false);
        await UploadToQuarantineAsync(azurite, $"{id}/selfie", "%PDF-1.7 documento sintetico"u8.ToArray());
        await UploadToQuarantineAsync(azurite, $"{id}/ine_frente", SyntheticJpeg((8 * 1024 * 1024) + 1));

        using var pdf = await ConfirmPhotoAsync(ana, id, "selfie");
        using var heavy = await ConfirmPhotoAsync(ana, id, "ine_frente");

        foreach (var response in new[] { pdf, heavy })
        {
            response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            (await CodeAsync(response)).Should().Be("content_invalid");
        }

        (await azurite.ListBlobNamesAsync(_quarantineContainer)).Should().BeEmpty();
        (await azurite.ListBlobNamesAsync(_finalContainer)).Should().BeEmpty();
    }

    [Fact]
    public async Task Siete_Dias_Sin_Decision_Borra_Los_Blobs_Y_La_Llave_Y_Se_Puede_Reenviar_CA_025_16()
    {
        await using var azurite = await AzuriteFixture.StartAsync();
        _blobs.Use(azurite);
        var ana = await NewPersonAsync();
        var id = await OpenCaptureAsync(ana, photosEncrypted: false);
        foreach (var type in _basicTypes)
        {
            await UploadToQuarantineAsync(azurite, $"{id}/{type}", SyntheticJpeg(1024));
            (await ConfirmPhotoAsync(ana, id, type)).Dispose();
        }

        (await SubmitAsync(ana, id)).Dispose();
        (await azurite.ListBlobNamesAsync(_finalContainer)).Should().HaveCount(2);

        // Pasados los 7 días, la tarea programada llama a la purga por vencimiento; aquí se invoca directo (sin esperar el reloj).
        using var scope = _factory.Services.CreateScope();
        var outcome = await scope.ServiceProvider.GetRequiredService<PurgaDeVerificacion>().PurgeAsync(ObjectId.Parse(id), PurgeReasons.Expired, PurgeReasons.SystemActor, CancellationToken.None);

        outcome.Should().Be(PurgeOutcome.Purged);
        (await azurite.ListBlobNamesAsync(_finalContainer)).Should().BeEmpty();
        (await azurite.ListBlobNamesAsync(_quarantineContainer)).Should().BeEmpty();
        var stored = await StoredAsync(id);
        stored["estado"].AsString.Should().Be("sin_verificar");
        stored["motivo"].AsString.Should().Be("fotos_borradas");
        stored["llaveEnvuelta"].IsBsonNull.Should().BeTrue("la llave envuelta se destruye");
        using var status = await SendAsync(HttpMethod.Get, ana.Token);
        (await JsonAsync(status)).GetProperty("puedeReintentar").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Sin_Subida_O_Con_La_Captura_Vencida_Responde_409_Y_Se_Puede_Pedir_Otra_CA_025_11()
    {
        await using var azurite = await AzuriteFixture.StartAsync();
        _blobs.Use(azurite);
        var ana = await NewPersonAsync();
        var id = await OpenCaptureAsync(ana, photosEncrypted: false);

        using var notUploaded = await ConfirmPhotoAsync(ana, id, "selfie");
        await Verifications.UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", ObjectId.Parse(id)), Builders<BsonDocument>.Update.Set("capturaVenceEn", new BsonDateTime(DateTime.UtcNow.AddMinutes(-1))));
        using var expired = await ConfirmPhotoAsync(ana, id, "selfie");
        using var again = await SendAsync(HttpMethod.Post, ana.Token, Body());

        notUploaded.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await CodeAsync(notUploaded)).Should().Be("photo_not_uploaded");
        (await CodeAsync(expired)).Should().Be("capture_expired");
        again.StatusCode.Should().Be(HttpStatusCode.Created, "la app puede pedir una captura nueva");
        (await Verifications.CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("estado", "en_captura"))).Should().Be(1, "la anterior se cancela y no queda una verificación a medias");
    }
}

/// <summary>Registra los eventos que Identidad deja en el buzón de salida (<c>VerificacionEnviada</c>, <c>CurpDuplicada</c>) como pares tipo y <c>sub</c>.</summary>
internal sealed class RecordingVerificationEvents : IVerificationEvents
{
    private readonly System.Collections.Concurrent.ConcurrentQueue<(string Type, string Sub)> _published = new();

    public IReadOnlyCollection<(string Type, string Sub)> Published => _published;

    public Task PublishAsync(string eventType, string sub, string? verificationId, CancellationToken cancellationToken)
    {
        _published.Enqueue((eventType, sub));
        return Task.CompletedTask;
    }
}

/// <summary>
/// Almacén privado del host de pruebas: por omisión solo emite enlaces (como <see cref="LinksOnlyVerificationBlobStore"/>); con <see cref="Use"/> lee, escribe y borra en un Azurite real con el
/// <see cref="AzureVerificationBlobStore"/> de producción. Azurite no firma SAS de delegación, así que los enlaces siguen siendo de mentira.
/// </summary>
internal sealed class SwitchableVerificationBlobStore : IVerificationBlobStore
{
    private readonly LinksOnlyVerificationBlobStore _links = new();
    private IVerificationBlobStore? _real;

    private IVerificationBlobStore Store => _real ?? _links;

    public void Use(AzuriteFixture azurite) => _real = new AzureVerificationBlobStore(new BlobServiceClient(azurite.ConnectionString), new VerificationBlobOptions(), TimeProvider.System);

    public Task<Uri> IssueQuarantineUploadLinkAsync(string blobName, TimeSpan lifetime, CancellationToken cancellationToken) => _links.IssueQuarantineUploadLinkAsync(blobName, lifetime, cancellationToken);

    public Task<Uri> IssueFinalReadLinkAsync(string blobName, TimeSpan lifetime, CancellationToken cancellationToken) => _links.IssueFinalReadLinkAsync(blobName, lifetime, cancellationToken);

    public Task<byte[]?> ReadQuarantineAsync(string blobName, int maxBytes, CancellationToken cancellationToken) => Store.ReadQuarantineAsync(blobName, maxBytes, cancellationToken);

    public Task<byte[]?> ReadFinalAsync(string blobName, CancellationToken cancellationToken) => Store.ReadFinalAsync(blobName, cancellationToken);

    public Task<bool> WriteFinalAsync(string blobName, byte[] content, CancellationToken cancellationToken) => Store.WriteFinalAsync(blobName, content, cancellationToken);

    public Task DeleteQuarantineAsync(string blobName, CancellationToken cancellationToken) => Store.DeleteQuarantineAsync(blobName, cancellationToken);

    public Task DeleteFinalAsync(string blobName, CancellationToken cancellationToken) => Store.DeleteFinalAsync(blobName, cancellationToken);

    public void DisposeReal() => (_real as IDisposable)?.Dispose();
}
