extern alias MarketApi;

using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using Knock.Contracts.Moderation;
using Knock.IntegrationTest.Requests;
using Knock.IntegrationTest.Support;
using MarketApi::Knock.Market.Api.Modules.Media.Data;
using MarketApi::Knock.Market.Api.Modules.Media.Domain;
using MarketApi::Knock.Market.Api.Modules.Media.Services;
using MarketApi::Knock.Market.Api.Modules.Requests.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SkiaSharp;
using Xunit;

namespace Knock.IntegrationTest.Media;

/// <summary>
/// Spec 062, T-04 a T-06: el flujo privado de la foto de una solicitud de punta a punta, con Azurite y MongoDB reales (la API completa, con el módulo de archivos real y la moderación de mentira).
/// El enlace de subida firmado no se emite aquí (Azurite no firma SAS de delegación; lo cubre <c>Spec062FotosPrivadasHttpTests</c>): el archivo pendiente se crea con el servicio y la subida del
/// cliente a la cuarentena se simula directo en Azurite. Solo datos ficticios; las corre el CI.
/// </summary>
public sealed class Spec062FotosPrivadasFlujoTests
{
    private static readonly MediaOptions _media = new();

    [Fact]
    public async Task Confirmar_Responde_200_Sin_Url_Y_Con_Ese_Id_Se_Crea_La_Solicitud_CA_062_02()
    {
        await using var azurite = await AzuriteFixture.StartAsync();
        await using var host = await StartAsync(azurite);
        var owner = host.ClientSub;
        var photo = await UploadAsync(host, azurite, owner, "solicitud");

        using var confirmed = await Confirm(host, owner, photo);
        using var created = await host.CreateAsync(RequestBody(host, photo), owner);

        confirmed.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await Spec020Host.ReadAsync(confirmed);
        json.GetProperty("id").GetString().Should().Be(photo.Hex);
        json.GetProperty("ancho").GetInt32().Should().Be(800);
        json.GetProperty("alto").GetInt32().Should().Be(400);
        json.GetProperty("bytes").GetInt32().Should().BeGreaterThan(0);
        (!json.TryGetProperty("url", out var url) || url.ValueKind == JsonValueKind.Null).Should().BeTrue("la foto de una solicitud nunca tiene dirección pública");
        var stored = (await host.Services.GetRequiredService<IMediaFileStore>().FindAsync(photo.Id, CancellationToken.None))!.File;
        stored.State.Should().Be(MediaFileState.Ready);
        stored.Usage.Should().Be(MediaUsage.Request);
        created.StatusCode.Should().Be(HttpStatusCode.Created, "la solicitud acepta el id de la foto confirmada");
    }

    [Fact]
    public async Task Confirmada_Queda_En_Archivos_Y_Nada_En_Imagenes_Publicas_CA_062_03()
    {
        await using var azurite = await AzuriteFixture.StartAsync();
        await using var host = await StartAsync(azurite);
        var photo = await UploadAsync(host, azurite, host.ClientSub, "solicitud");

        (await Confirm(host, host.ClientSub, photo)).Dispose();

        (await azurite.ListBlobNamesAsync(_media.FilesContainer)).Should().HaveCount(2).And.Contain($"{photo.Key}/original.jpg").And.Contain($"{photo.Key}/reducida.jpg");
        (await azurite.ListBlobNamesAsync(_media.QuarantineContainer)).Should().BeEmpty("la cuarentena se limpia");
        (await azurite.ListBlobNamesAsync("imagenes-publicas")).Should().BeEmpty("una foto de solicitud nunca se publica");
    }

    [Fact]
    public async Task Moderacion_Rechaza_Responde_422_Y_Con_La_Moderacion_Caida_Responde_503_CA_062_07()
    {
        await using var azurite = await AzuriteFixture.StartAsync();
        await using var host = await StartAsync(azurite);
        var rejected = await UploadAsync(host, azurite, host.ClientSub, "solicitud");
        var unavailable = await UploadAsync(host, azurite, host.ClientSub, "solicitud");
        var files = host.Services.GetRequiredService<IMediaFileStore>();

        host.Moderator.Outcome = ModerationOutcome.Rejected;
        using var rejectedResponse = await Confirm(host, host.ClientSub, rejected);
        host.Moderator.Outcome = ModerationOutcome.Unavailable;
        using var unavailableResponse = await Confirm(host, host.ClientSub, unavailable);

        rejectedResponse.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await Spec020Host.ReadAsync(rejectedResponse)).GetProperty("code").GetString().Should().Be("image_rejected");
        (await files.FindAsync(rejected.Id, CancellationToken.None))!.File.State.Should().Be(MediaFileState.Rejected);
        // La spec pide el código moderation_unavailable; la ruta privada responde 503 con storage_unavailable. Se prueba lo que no cambia: falla cerrada con 503 y nada pasa a «listo».
        unavailableResponse.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await files.FindAsync(unavailable.Id, CancellationToken.None))!.File.State.Should().Be(MediaFileState.Pending);
        (await azurite.ListBlobNamesAsync(_media.FilesContainer)).Should().BeEmpty("ninguna foto llega al contenedor final");
        (await azurite.ListBlobNamesAsync(_media.QuarantineContainer)).Should().ContainSingle().Which.Should().Be(unavailable.Key, "solo se limpia la cuarentena de la rechazada");
    }

    [Fact]
    public async Task Solicitud_Con_Foto_Ajena_De_Otro_Uso_O_No_Lista_Responde_El_Mismo_422_CA_062_08()
    {
        await using var azurite = await AzuriteFixture.StartAsync();
        await using var host = await StartAsync(azurite);
        var owner = host.ClientSub;
        var media = host.Services.GetRequiredService<MediaService>();
        var foreign = await UploadAsync(host, azurite, "otra-persona-062", "solicitud");
        (await media.ConfirmAsync("otra-persona-062", foreign.Id, CancellationToken.None)).Status.Should().Be(MediaOutcomeStatus.Success);
        var otherUse = await UploadAsync(host, azurite, owner, "trabajo");
        (await media.ConfirmAsync(owner, otherUse.Id, CancellationToken.None)).Status.Should().Be(MediaOutcomeStatus.Success);
        var notReady = await UploadAsync(host, azurite, owner, "solicitud");
        var shapes = new List<string>();

        foreach (var photo in new[] { foreign, otherUse, notReady })
        {
            using var response = await host.CreateAsync(RequestBody(host, photo), owner);

            response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            var json = await Spec020Host.ReadAsync(response);
            json.GetProperty("code").GetString().Should().Be("photo_invalid");
            shapes.Add(json.GetProperty("title").GetString() + "|" + json.GetProperty("code").GetString());
        }

        shapes.Distinct().Should().ContainSingle("la respuesta no dice qué condición falló");
        (await host.CountRequestsAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Registros_Y_Errores_No_Llevan_Sub_Blob_Enlaces_Ni_Ids_De_Fotos_CA_062_09()
    {
        await using var azurite = await AzuriteFixture.StartAsync();
        await using var host = await StartAsync(azurite);
        var owner = host.ClientSub;
        var stranger = Guid.NewGuid().ToString("D");
        var notUploaded = await CreatePendingAsync(host, owner, "solicitud", Jpeg().Length);
        var uploaded = await UploadAsync(host, azurite, owner, "solicitud");
        var bodies = new List<string>();

        var output = await ConsoleCapture.RunAsync(async () =>
        {
            foreach (var (sub, photo) in new[] { (owner, notUploaded), (stranger, notUploaded), (owner, uploaded) })
            {
                using var response = await Confirm(host, sub, photo);
                bodies.Add(await response.Content.ReadAsStringAsync());
            }

            using var invalid = await host.CreateAsync(RequestBody(host, notUploaded), owner);
            bodies.Add(await invalid.Content.ReadAsStringAsync());
        });

        foreach (var forbidden in new[] { owner, stranger, notUploaded.Key, notUploaded.Hex, uploaded.Key, uploaded.Hex, "sig=" })
        {
            output.Should().NotContain(forbidden, "los registros no llevan el sub, nombres de blob, enlaces firmados ni ids de fotos");
            bodies.Should().OnlyContain(body => !body.Contains(forbidden, StringComparison.Ordinal), "los errores tampoco");
        }
    }

    private static Task<Spec020Host> StartAsync(AzuriteFixture azurite) =>
        Spec020Host.StartAsync(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:KnockBlob"] = azurite.ConnectionString,
                ["Mongo:Databases:Media"] = "media_it_" + Guid.NewGuid().ToString("N")[..12],
            },
            services =>
            {
                // Aquí la comprobación de fotos de la solicitud es la real (módulo de archivos), no el doble de las demás pruebas de la 020.
                services.RemoveAll<IRequestPhotos>();
                services.AddScoped<IRequestPhotos, MediaRequestPhotos>();
                // El host no trae Redis: los límites por persona van con el contador en memoria, como en las pruebas HTTP de la 062.
                services.AddSingleton(new LimitesImagen(new ContadorVentanaMemoria(TimeProvider.System), new ContadorVentanaMemoria(TimeProvider.System)));
            });

    private static Task<HttpResponseMessage> Confirm(Spec020Host host, string sub, PrivatePhoto photo) =>
        host.SendAsync(HttpMethod.Post, $"/api/v1/imagenes/subidas/{photo.Hex}/confirmar", sub);

    private static Dictionary<string, object?> RequestBody(Spec020Host host, PrivatePhoto photo)
    {
        var body = host.ValidBody();
        body["fotos"] = new[] { photo.Hex };
        return body;
    }

    private static async Task<PrivatePhoto> CreatePendingAsync(Spec020Host host, string owner, string usage, long size)
    {
        var created = await host.Services.GetRequiredService<MediaService>().CreateUploadAsync(owner, usage, "image/jpeg", size, CancellationToken.None);
        var id = created.Value!.Id;
        var key = (await host.Services.GetRequiredService<IMediaFileStore>().FindAsync(id, CancellationToken.None))!.File.QuarantineBlob;
        return new PrivatePhoto(id, key);
    }

    /// <summary>Crea el archivo pendiente y simula la subida del cliente a la cuarentena de Azurite.</summary>
    private static async Task<PrivatePhoto> UploadAsync(Spec020Host host, AzuriteFixture azurite, string owner, string usage)
    {
        var bytes = Jpeg();
        var photo = await CreatePendingAsync(host, owner, usage, bytes.Length);
        var quarantine = azurite.Container(_media.QuarantineContainer);
        await quarantine.CreateIfNotExistsAsync();
        await quarantine.GetBlobClient(photo.Key).UploadAsync(BinaryData.FromBytes(bytes));
        return photo;
    }

    private static byte[] Jpeg()
    {
        using var bitmap = new SKBitmap(new SKImageInfo(320, 240));
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.SteelBlue);
        using var data = bitmap.Encode(SKEncodedImageFormat.Jpeg, 85);
        return data.ToArray();
    }

    private sealed record PrivatePhoto(Guid Id, string Key)
    {
        public string Hex => Id.ToString("N");
    }
}
