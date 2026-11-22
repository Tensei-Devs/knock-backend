using AwesomeAssertions;
using Knock.Market.Api.Modules.Tecnicos.Data;
using Knock.Market.Api.Modules.Tecnicos.Dtos;
using Knock.Market.Api.Modules.Tecnicos.Services;
using Knock.Market.Api.Modules.Tecnicos.Validators;
using Microsoft.Extensions.Caching.Memory;
using MongoDB.Bson;
using Xunit;

namespace Knock.UnitTest.Tecnicos;

/// <summary>Fotos de trabajos (spec 046, TEC-07) y reputación propia (TEC-13): validación, caché de 1 minuto, paginación y lectura del documento. Solo datos ficticios.</summary>
public sealed class Spec046FotosYReputacionTests : IDisposable
{
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    private static readonly DateTimeOffset _now = new(2026, 11, 21, 18, 0, 0, TimeSpan.Zero);

    public void Dispose() => _cache.Dispose();

    private static TechnicianProfileRecord Technician(ObjectId id, bool oculto = false) =>
        new(id, "Ana Pruebas", null, null, "ninguno", null, null, null, ["plo"], "domicilio", 5, "6adfe980511e3d878359ca07", "activo", oculto, null, null);

    private (TechnicianWorkPhotoService Service, FakePhotos Photos) CreatePhotos(ObjectId id, int total, bool hidden = false)
    {
        var photos = new FakePhotos(total);
        var profiles = new TechnicianProfileService(new FakeProfiles(Technician(id, hidden)), _cache, new FixedClock(_now));
        return (new TechnicianWorkPhotoService(profiles, photos, _cache), photos);
    }

    [Theory]
    [InlineData(null, null, true)]
    [InlineData("66f000000000000000000001", "50", true)]
    [InlineData("zzz", null, false)]
    [InlineData(null, "51", false)]
    [InlineData(null, "0", false)]
    [InlineData(null, "abc", false)]
    public void Validador_De_Fotos_Acepta_Solo_Cursor_Y_Limite_Validos_CA_046_27(string? cursor, string? limite, bool valid) =>
        new WorkPhotoQueryValidator().Validate(new WorkPhotoQuery { Cursor = cursor, Limite = limite }).IsValid.Should().Be(valid);

    [Fact]
    public async Task Sin_Limite_Devuelve_20_Y_El_Cursor_De_La_Siguiente_Pagina_CA_046_29()
    {
        var id = ObjectId.GenerateNewId();
        var (service, _) = CreatePhotos(id, total: 45);

        var page = await service.ListAsync(id, new WorkPhotoQuery(), CancellationToken.None);

        page!.Fotos.Should().HaveCount(20);
        page.NextCursor.Should().Be(page.Fotos[^1].Id);
    }

    [Fact]
    public async Task Ultima_Pagina_No_Trae_Cursor_CA_046_29()
    {
        var id = ObjectId.GenerateNewId();
        var (service, _) = CreatePhotos(id, total: 3);
        var page = await service.ListAsync(id, new WorkPhotoQuery(), CancellationToken.None);
        page!.Fotos.Should().HaveCount(3);
        page.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task Tecnico_Oculto_O_Inexistente_Es_No_Encontrado_CA_046_28()
    {
        var id = ObjectId.GenerateNewId();
        var (hidden, _) = CreatePhotos(id, total: 3, hidden: true);
        (await hidden.ListAsync(id, new WorkPhotoQuery(), CancellationToken.None)).Should().BeNull();
        var (service, _) = CreatePhotos(id, total: 3);
        (await service.ListAsync(ObjectId.GenerateNewId(), new WorkPhotoQuery(), CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task Consulta_Repetida_Se_Responde_Desde_La_Cache_Y_Se_Invalida_Al_Escribir_CA_046_30()
    {
        var id = ObjectId.GenerateNewId();
        var (service, photos) = CreatePhotos(id, total: 3);

        await service.ListAsync(id, new WorkPhotoQuery(), CancellationToken.None);
        await service.ListAsync(id, new WorkPhotoQuery(), CancellationToken.None);
        photos.Calls.Should().Be(1);

        service.Forget(id);
        await service.ListAsync(id, new WorkPhotoQuery(), CancellationToken.None);
        photos.Calls.Should().Be(2);
    }

    [Fact]
    public void Clave_De_Cache_De_Fotos_Solo_Lleva_Tecnico_Cursor_Y_Limite_CA_046_30()
    {
        var id = ObjectId.GenerateNewId();
        TechnicianWorkPhotoService.CacheKey(id, "abc", 20).Should().Be($"tecnicos:fotos:{id}:abc:20");
    }

    [Fact]
    public async Task Reputacion_Se_Lee_Una_Sola_Vez_Dentro_Del_Minuto_CA_046_51()
    {
        var reader = new FakeReputation(new ReputationResponse(4.2, 5, new Dictionary<string, int>(), "confiable"));
        var service = new TechnicianReputationService(reader, _cache);

        await service.GetOwnAsync("sub-1", CancellationToken.None);
        var second = await service.GetOwnAsync("sub-1", CancellationToken.None);

        reader.Calls.Should().Be(1);
        second!.Nivel.Should().Be("confiable");
        service.Forget("sub-1");
        await service.GetOwnAsync("sub-1", CancellationToken.None);
        reader.Calls.Should().Be(2);
    }

    [Fact]
    public async Task Cuenta_Sin_Perfil_De_Tecnico_Recibe_Nulo_Y_No_Se_Guarda_CA_046_50()
    {
        var reader = new FakeReputation(null);
        var service = new TechnicianReputationService(reader, _cache);
        (await service.GetOwnAsync("sub-2", CancellationToken.None)).Should().BeNull();
        (await service.GetOwnAsync("sub-2", CancellationToken.None)).Should().BeNull();
        reader.Calls.Should().Be(2);
    }

    [Fact]
    public void Documento_De_Reputacion_Se_Convierte_Con_Promedio_De_Un_Decimal_Y_Distribucion_CA_046_64()
    {
        var document = new BsonDocument
        {
            ["promedio"] = 4.26,
            ["total"] = 7,
            ["distribucion"] = new BsonDocument { ["5"] = 4, ["4"] = 2, ["3"] = 1 },
            ["nivel"] = "confiable",
        };

        var reputation = TechnicianReputationStore.ToReputation(document);

        reputation.Promedio.Should().Be(4.3);
        reputation.Total.Should().Be(7);
        reputation.Distribucion.Should().BeEquivalentTo(new Dictionary<string, int> { ["1"] = 0, ["2"] = 0, ["3"] = 1, ["4"] = 2, ["5"] = 4 });
        reputation.Nivel.Should().Be("confiable");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Sin_Opiniones_O_Documento_Ilegible_Es_Nuevo_Y_En_Ceros_CA_046_64(bool garbled)
    {
        var reputation = TechnicianReputationStore.ToReputation(garbled ? new BsonString("x") : BsonNull.Value);
        reputation.Promedio.Should().BeNull();
        reputation.Total.Should().Be(0);
        reputation.Nivel.Should().Be("nuevo");
        reputation.Distribucion.Values.Should().AllBeEquivalentTo(0);
        reputation.Distribucion.Should().HaveCount(5);
    }

    private sealed class FakePhotos(int total) : IWorkPhotoReader
    {
        public int Calls { get; private set; }

        public Task<IReadOnlyList<WorkPhotoResponse>> ListAsync(ObjectId technicianId, ObjectId? after, int limit, CancellationToken cancellationToken)
        {
            Calls++;
            IReadOnlyList<WorkPhotoResponse> all = [.. Enumerable.Range(0, total).Select(_ => new WorkPhotoResponse(ObjectId.GenerateNewId().ToString(), "https://files.test/f.jpg", _now.UtcDateTime))];
            return Task.FromResult<IReadOnlyList<WorkPhotoResponse>>([.. all.Take(limit + 1)]);
        }
    }

    private sealed class FakeProfiles(TechnicianProfileRecord record) : ITechnicianProfileReader
    {
        public Task<TechnicianProfileRecord?> FindByIdAsync(ObjectId id, CancellationToken cancellationToken) =>
            Task.FromResult(record.Id == id ? record : null);
    }

    private sealed class FakeReputation(ReputationResponse? response) : IReputationReader
    {
        public int Calls { get; private set; }

        public Task<ReputationResponse?> FindOwnAsync(string sub, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(response);
        }
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
