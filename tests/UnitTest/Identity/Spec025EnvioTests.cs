using AwesomeAssertions;
using Knock.Identity.Api.Common.Errors;
using Knock.Identity.Api.Modules.Trust.Data;
using Knock.Identity.Api.Modules.Trust.Dtos;
using MongoDB.Bson;
using Xunit;

namespace Knock.UnitTest.Identity;

/// <summary>Spec 025, T-05: creación con enlaces, confirmación y envío con un almacén y un almacenamiento de mentira. CURP y fotos ficticias.</summary>
public sealed class Spec025EnvioTests
{
    private const string _curp = "GODE561231HDFRRN09";
    private static readonly byte[] _jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3];
    private static readonly DateTime _now = new(2026, 11, 20, 18, 0, 0, DateTimeKind.Utc);

    private static async Task<DomainException> FailsAsync(Func<Task> action) => (await action.Should().ThrowAsync<DomainException>()).Which;

    private static async Task<(Spec025ServicioTests.Rig Rig, ObjectId Id)> OpenCaptureAsync(string state = "activa", params string[] encrypted)
    {
        var rig = Spec025ServicioTests.CreateRig(state);
        var created = await rig.Service.CreateAsync("sub-1", "sid-1", new CreateVerificationRequest("basica", true, "1.0", _curp), CancellationToken.None);
        var id = ObjectId.Parse(created!.Id);
        rig.Store.Owned = new VerificationForCapture(id, "basica", "en_captura", _now.AddHours(20), rig.Store.Created!.WrappedKey, rig.Store.Created.CurpHmac,
            [new StoredPhoto("ine_frente", encrypted.Contains("ine_frente") ? "cifrada" : "pendiente"), new StoredPhoto("selfie", encrypted.Contains("selfie") ? "cifrada" : "pendiente")]);
        return (rig, id);
    }

    [Fact]
    public async Task Crea_Con_Enlaces_Y_Guarda_Solo_El_Hmac_Y_La_Llave_Envuelta_CA_025_06()
    {
        var rig = Spec025ServicioTests.CreateRig();
        var created = await rig.Service.CreateAsync("sub-1", "sid-1", new CreateVerificationRequest("basica", true, "1.0", $"  {_curp} "), CancellationToken.None);

        created!.Fotos.Should().OnlyContain(f => f.UrlSubida != null && f.VenceEn == _now.AddMinutes(5));
        var stored = rig.Store.Created!;
        stored.CurpHmac.Should().StartWith("1.").And.NotContain(_curp);
        stored.WrappedKey.Value.Should().NotBeNullOrEmpty();
        stored.WrappedKey.KekVersion.Should().Be("kek-1");
        rig.Blobs.Links.Should().HaveCount(2);
    }

    [Fact]
    public async Task Curp_Duplicada_Al_Crear_Responde_409_Y_Avisa_A_Soporte_CA_025_07()
    {
        var rig = Spec025ServicioTests.CreateRig();
        rig.Store.LiveCurp = true;
        var error = await FailsAsync(() => rig.Service.CreateAsync("sub-1", "sid-1", new CreateVerificationRequest("basica", true, "1.0", _curp), CancellationToken.None));
        error.Code.Should().Be("curp_duplicate");
        error.Message.Should().NotContain(_curp);
        rig.Events.Published.Should().Equal("CurpDuplicada");
        rig.Store.Created.Should().BeNull();
    }

    [Fact]
    public async Task Confirma_La_Foto_Y_La_Marca_Cifrada_CA_025_09()
    {
        var (rig, id) = await OpenCaptureAsync();
        rig.Blobs.Quarantine[$"{id}/selfie"] = _jpeg;
        var confirmed = await rig.Service.ConfirmPhotoAsync("sub-1", id.ToString(), "selfie", CancellationToken.None);
        confirmed.Should().Be(new PhotoConfirmedResponse("selfie", "cifrada"));
        rig.Store.Encrypted.Should().Equal("selfie");
        rig.Blobs.Quarantine.Should().BeEmpty();
        rig.Blobs.Final.Should().ContainKey($"{id}/selfie");
    }

    [Fact]
    public async Task Tipo_De_Otro_Nivel_Responde_409_Type_Invalid_CA_025_04()
    {
        var (rig, id) = await OpenCaptureAsync();
        (await FailsAsync(() => rig.Service.ConfirmPhotoAsync("sub-1", id.ToString(), "ine_reverso", CancellationToken.None))).Code.Should().Be("type_invalid");
        (await FailsAsync(() => rig.Service.ConfirmPhotoAsync("sub-1", id.ToString(), "pasaporte", CancellationToken.None))).Kind.Should().Be(DomainErrorKind.BadRequest);
    }

    [Fact]
    public async Task Captura_Vencida_O_Sin_Blob_Responde_409_CA_025_11()
    {
        var (rig, id) = await OpenCaptureAsync();
        (await FailsAsync(() => rig.Service.ConfirmPhotoAsync("sub-1", id.ToString(), "selfie", CancellationToken.None))).Code.Should().Be("photo_not_uploaded");
        rig.Store.Owned = rig.Store.Owned! with { CaptureExpiresAt = _now.AddMinutes(-1) };
        (await FailsAsync(() => rig.Service.ConfirmPhotoAsync("sub-1", id.ToString(), "selfie", CancellationToken.None))).Code.Should().Be("capture_expired");
    }

    [Fact]
    public async Task Foto_Ya_Confirmada_Responde_409()
    {
        var (rig, id) = await OpenCaptureAsync("activa", "selfie");
        (await FailsAsync(() => rig.Service.ConfirmPhotoAsync("sub-1", id.ToString(), "selfie", CancellationToken.None))).Code.Should().Be("photo_already_confirmed");
    }

    [Fact]
    public async Task Verificacion_Ajena_O_Mal_Formada_CA_025_17()
    {
        var (rig, _) = await OpenCaptureAsync();
        (await FailsAsync(() => rig.Service.ConfirmPhotoAsync("sub-1", ObjectId.GenerateNewId().ToString(), "selfie", CancellationToken.None))).Kind.Should().Be(DomainErrorKind.NotFound);
        (await FailsAsync(() => rig.Service.SubmitAsync("sub-1", ObjectId.GenerateNewId().ToString(), CancellationToken.None))).Kind.Should().Be(DomainErrorKind.NotFound);
        (await FailsAsync(() => rig.Service.SubmitAsync("sub-1", "no-es-un-id", CancellationToken.None))).Kind.Should().Be(DomainErrorKind.BadRequest);
    }

    [Fact]
    public async Task Envia_A_Revision_Con_Vencimiento_De_Siete_Dias_CA_025_12()
    {
        var (rig, id) = await OpenCaptureAsync("activa", "ine_frente", "selfie");
        var sent = await rig.Service.SubmitAsync("sub-1", id.ToString(), CancellationToken.None);
        sent!.Estado.Should().Be("en_revision");
        sent.ExpiraEn.Should().Be(sent.EnviadoEn.AddDays(7));
        rig.Events.Published.Should().Equal("VerificacionEnviada");
    }

    [Fact]
    public async Task Faltan_Fotos_Responde_409_Y_No_Cambia_Nada_CA_025_13()
    {
        var (rig, id) = await OpenCaptureAsync("activa", "selfie");
        (await FailsAsync(() => rig.Service.SubmitAsync("sub-1", id.ToString(), CancellationToken.None))).Code.Should().Be("photos_missing");
        rig.Store.Submitted.Should().BeNull();
        rig.Events.Published.Should().BeEmpty();
    }

    [Fact]
    public async Task Carrera_Por_La_Misma_Curp_Deja_Una_Sola_En_Revision_CA_025_20()
    {
        var (rig, id) = await OpenCaptureAsync("activa", "ine_frente", "selfie");
        rig.Store.SubmitResult = SubmitOutcome.DuplicateCurp;
        (await FailsAsync(() => rig.Service.SubmitAsync("sub-1", id.ToString(), CancellationToken.None))).Code.Should().Be("curp_duplicate");
        rig.Events.Published.Should().Equal("CurpDuplicada");

        rig.Store.SubmitResult = SubmitOutcome.Submitted;
        rig.Store.LiveCurp = true;
        (await FailsAsync(() => rig.Service.SubmitAsync("sub-1", id.ToString(), CancellationToken.None))).Code.Should().Be("curp_duplicate");
    }

    [Fact]
    public async Task Cuenta_En_Eliminacion_Programada_No_Confirma_Ni_Envia_CA_025_21()
    {
        var (rig, id) = await OpenCaptureAsync();
        var scheduled = Spec025ServicioTests.CreateRig("eliminacion_programada");
        scheduled.Store.Owned = rig.Store.Owned;
        (await FailsAsync(() => scheduled.Service.ConfirmPhotoAsync("sub-1", id.ToString(), "selfie", CancellationToken.None))).Code.Should().Be("deletion_scheduled");
        (await FailsAsync(() => scheduled.Service.SubmitAsync("sub-1", id.ToString(), CancellationToken.None))).Code.Should().Be("deletion_scheduled");
        scheduled.Store.Submitted.Should().BeNull();
    }
}
