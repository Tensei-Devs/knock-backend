using AwesomeAssertions;
using FluentValidation;
using Knock.Market.Api.Modules.Catalog;
using Knock.Market.Api.Modules.Tecnicos.Dtos;
using Knock.Market.Api.Modules.Tecnicos.Services;
using Knock.Market.Api.Modules.Tecnicos.Validators;
using Microsoft.Extensions.Caching.Memory;
using MongoDB.Bson;
using Xunit;

namespace Knock.UnitTest.Tecnicos;

/// <summary>Mapa (TEC-04) y Explorar (TEC-14) de la spec 046: validación, agrupación por celdas, caché e invalidación. Solo datos ficticios.</summary>
public sealed class Spec046MapaYExplorarTests : IDisposable
{
    private const string _colonia = "6adfe980511e3d878359ca07";
    private readonly MemoryCache _cache = new(new MemoryCacheOptions { TrackStatistics = true });
    private readonly TechnicianReadCache _readCache = new();

    public void Dispose()
    {
        _cache.Dispose();
        _readCache.Dispose();
    }

    private static MapQuery Box(string? zoom = "12", string? oficio = null) =>
        new() { Norte = "20.10", Sur = "20.00", Este = "-99.30", Oeste = "-99.40", Zoom = zoom, Oficio = oficio };

    [Theory]
    [InlineData("norte", "91")]
    [InlineData("sur", "abc")]
    [InlineData("este", "181")]
    [InlineData("oeste", "")]
    [InlineData("zoom", "0")]
    [InlineData("zoom", "21")]
    [InlineData("oficio", "a b")]
    public async Task Mapa_Con_Valor_Invalido_Responde_422_Sin_Repetir_El_Valor_CA_046_14(string field, string value)
    {
        var query = Box();
        switch (field)
        {
            case "norte": query.Norte = value; break;
            case "sur": query.Sur = value; break;
            case "este": query.Este = value; break;
            case "oeste": query.Oeste = value; break;
            case "zoom": query.Zoom = value; break;
            default: query.Oficio = value; break;
        }

        var result = await new MapQueryValidator().ValidateAsync(query);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().OnlyContain(e => string.Equals(e.PropertyName, field, StringComparison.OrdinalIgnoreCase) && (value.Length == 0 || !e.ErrorMessage.Contains(value, StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Mapa_Con_Norte_Bajo_El_Sur_Responde_422_CA_046_14()
    {
        var query = Box();
        query.Norte = "19.00";
        (await new MapQueryValidator().ValidateAsync(query)).Errors.Should().Contain(e => string.Equals(e.PropertyName, "sur", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Mapa_Valido_Pasa_La_Validacion_CA_046_13()
    {
        (await new MapQueryValidator().ValidateAsync(Box())).IsValid.Should().BeTrue();
        (await new MapQueryValidator().ValidateAsync(Box(zoom: null))).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Mapa_Repetido_Se_Responde_Desde_La_Cache_Y_La_Base_Recibe_Una_Consulta_CA_046_15()
    {
        var reader = new FakeMapReader([new MapPoint(ObjectId.GenerateNewId(), 20.05, -99.35)]);
        var service = new TechnicianMapService(reader, new FakeCatalog(), _cache, _readCache);
        (await service.SearchAsync(Box(), CancellationToken.None)).Response!.Grupos.Should().ContainSingle();
        await service.SearchAsync(Box(), CancellationToken.None);
        reader.Calls.Should().Be(1);
    }

    [Fact]
    public async Task Mapa_Invalidado_Al_Escribir_Vuelve_A_Consultar_La_Base_CA_046_15()
    {
        var reader = new FakeMapReader([]);
        var service = new TechnicianMapService(reader, new FakeCatalog(), _cache, _readCache);
        await service.SearchAsync(Box(), CancellationToken.None);
        _readCache.Invalidate();
        await service.SearchAsync(Box(), CancellationToken.None);
        reader.Calls.Should().Be(2);
    }

    [Fact]
    public async Task Mapa_La_Clave_De_Cache_No_Lleva_Coordenadas_Exactas_CA_046_56()
    {
        var reader = new FakeMapReader([]);
        var service = new TechnicianMapService(reader, new FakeCatalog(), _cache, _readCache);
        await service.SearchAsync(Box(), CancellationToken.None);
        var moved = Box();
        moved.Norte = "20.1000001";
        moved.Oeste = "-99.4000001";
        await service.SearchAsync(moved, CancellationToken.None);
        reader.Calls.Should().Be(1, "una variación menor que la celda cae en la misma celda");
        _cache.GetCurrentStatistics()!.CurrentEntryCount.Should().Be(1);
    }

    [Fact]
    public async Task Mapa_Agrupa_Por_Celda_Y_Devuelve_Como_Maximo_200_Grupos_CA_046_13()
    {
        var cell = TechnicianMapService.CellDegrees(12);
        var points = Enumerable.Range(0, 300).Select(i => new MapPoint(ObjectId.GenerateNewId(), (i * cell) + (cell / 2), -99.35)).ToList();
        points.Add(new MapPoint(ObjectId.GenerateNewId(), (cell / 2), -99.35));
        var groups = TechnicianMapService.Group(points, cell);
        groups.Should().HaveCount(TechnicianMapService.MaxGroups);
        groups[0].Cantidad.Should().Be(2);
        groups[0].TecnicoId.Should().BeNull();
        groups[1].TecnicoId.Should().NotBeNull();
    }

    [Fact]
    public async Task Mapa_Con_Oficio_Inactivo_Responde_422_CA_046_14()
    {
        var service = new TechnicianMapService(new FakeMapReader([]), new FakeCatalog(), _cache, _readCache);
        (await service.SearchAsync(Box(oficio: "zzz"), CancellationToken.None)).Status.Should().Be(MapStatus.OficioInvalid);
    }

    [Theory]
    [InlineData("todo", null, true)]
    [InlineData("auto", null, true)]
    [InlineData("agua", _colonia, true)]
    [InlineData("luz", null, true)]
    [InlineData("moto", null, false)]
    [InlineData("auto", "xyz", false)]
    public async Task Explorar_Valida_Filtro_Y_Colonia_CA_046_53(string filter, string? colonia, bool valid) =>
        (await new ExploreQueryValidator().ValidateAsync(new ExploreQuery { Filtro = filter, Colonia = colonia })).IsValid.Should().Be(valid);

    [Fact]
    public async Task Explorar_Sin_Sesion_Solo_Trae_Los_Bloques_Publicos_Y_Con_Sesion_Suma_El_Detalle_CA_046_52()
    {
        var service = new TechnicianExploreService(new FakeExploreReader(), new FakeCatalog(), _cache, _readCache);
        var anonymous = (await service.ExploreAsync(new ExploreQuery(), signedIn: false, CancellationToken.None)).Response!;
        anonymous.ServiciosPopulares.Should().NotBeEmpty();
        anonymous.TrabajosReales.Should().BeNull();
        anonymous.CercaDeTi.Should().BeNull();
        var signed = (await service.ExploreAsync(new ExploreQuery { Colonia = _colonia }, signedIn: true, CancellationToken.None)).Response!;
        signed.TrabajosReales.Should().NotBeNull();
        signed.CercaDeTi.Should().NotBeNull();
    }

    [Fact]
    public async Task Explorar_Cada_Bloque_Trae_Como_Maximo_10_Elementos_CA_046_65()
    {
        var service = new TechnicianExploreService(new FakeExploreReader(), new FakeCatalog(trades: 25), _cache, _readCache);
        var response = (await service.ExploreAsync(new ExploreQuery(), signedIn: true, CancellationToken.None)).Response!;
        response.ServiciosPopulares.Count.Should().BeLessThanOrEqualTo(10);
        response.Ideas.Count.Should().BeLessThanOrEqualTo(10);
        response.MejorCalificados.Count.Should().BeLessThanOrEqualTo(10);
        response.TrabajosReales!.Count.Should().BeLessThanOrEqualTo(10);
    }

    [Fact]
    public async Task Explorar_Con_Filtro_Solo_Pide_El_Oficio_Correspondiente_CA_046_65()
    {
        var reader = new FakeExploreReader();
        var service = new TechnicianExploreService(reader, new FakeCatalog(), _cache, _readCache);
        var response = (await service.ExploreAsync(new ExploreQuery { Filtro = "agua" }, signedIn: false, CancellationToken.None)).Response!;
        reader.LastTrades.Should().BeEquivalentTo("plo");
        response.ServiciosPopulares.Should().OnlyContain(s => s.Clave == "plo");
    }

    [Fact]
    public async Task Explorar_Repetido_Se_Responde_Desde_La_Cache_CA_046_54()
    {
        var reader = new FakeExploreReader();
        var service = new TechnicianExploreService(reader, new FakeCatalog(), _cache, _readCache);
        await service.ExploreAsync(new ExploreQuery(), signedIn: false, CancellationToken.None);
        await service.ExploreAsync(new ExploreQuery(), signedIn: false, CancellationToken.None);
        reader.Calls.Should().Be(1);
        _readCache.Invalidate();
        await service.ExploreAsync(new ExploreQuery(), signedIn: false, CancellationToken.None);
        reader.Calls.Should().Be(2);
    }

    [Fact]
    public async Task Explorar_Con_Colonia_Fuera_Del_Catalogo_Responde_422_CA_046_53()
    {
        var service = new TechnicianExploreService(new FakeExploreReader(), new FakeCatalog(), _cache, _readCache);
        (await service.ExploreAsync(new ExploreQuery { Colonia = "000000000000000000000000" }, signedIn: false, CancellationToken.None)).Status
            .Should().Be(ExploreStatus.ColoniaNotFound);
    }

    [Fact]
    public async Task El_Servicio_De_Lista_Invalida_Mapa_Y_Explorar_Al_Escribir_CA_046_15()
    {
        var reader = new FakeMapReader([]);
        var map = new TechnicianMapService(reader, new FakeCatalog(), _cache, _readCache);
        var lookup = new TechnicianLookupService(null!, new FakeCatalog(), null!, _cache, TimeProvider.System, _readCache);
        await map.SearchAsync(Box(), CancellationToken.None);
        lookup.ForgetListCache();
        await map.SearchAsync(Box(), CancellationToken.None);
        reader.Calls.Should().Be(2);
    }

    private sealed class FakeMapReader(IReadOnlyList<MapPoint> points) : ITechnicianMapReader
    {
        public int Calls { get; private set; }

        public Task<IReadOnlyList<MapPoint>> FindPointsAsync(double south, double west, double north, double east, string? oficio, int limit, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(points);
        }
    }

    private sealed class FakeExploreReader : ITechnicianExploreReader
    {
        public int Calls { get; private set; }

        public IReadOnlyCollection<string>? LastTrades { get; private set; }

        public Task<IReadOnlyDictionary<string, int>> CountByTradeAsync(IReadOnlyCollection<string>? trades, CancellationToken cancellationToken)
        {
            Calls++;
            LastTrades = trades;
            return Task.FromResult<IReadOnlyDictionary<string, int>>(Enumerable.Range(0, 25).ToDictionary(i => $"t{i}", i => i + 1).Append(new("plo", 3)).ToDictionary());
        }

        public Task<IReadOnlyList<TechnicianProfileRecord>> TopRatedAsync(IReadOnlyCollection<string>? trades, string? colonia, int limit, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TechnicianProfileRecord>>([.. Enumerable.Range(0, limit).Select(i =>
                new TechnicianProfileRecord(ObjectId.GenerateNewId(), "Técnico", null, null, "ninguno", 4.5, 3, null, ["plo"], "domicilio", 5, _colonia, "activo", false, null, null))]);

        public Task<IReadOnlyList<WorkPhotoResponse>> RecentWorkPhotosAsync(int limit, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<WorkPhotoResponse>>([.. Enumerable.Range(0, limit).Select(i => new WorkPhotoResponse(ObjectId.GenerateNewId().ToString(), "https://img.example/x.jpg", DateTime.UtcNow))]);
    }

    private sealed class FakeCatalog(int trades = 3) : ICatalogApi
    {
        public string ModuleName => "Catalog";

        public Task<(string Nombre, string Modalidad)?> FindActiveTradeAsync(string clave, CancellationToken cancellationToken) =>
            Task.FromResult<(string, string)?>(clave is "plo" or "ele" or "mec" ? ("Oficio", "domicilio") : null);

        public Task<(string Nombre, string Cp)?> FindNeighborhoodAsync(string coloniaId, CancellationToken cancellationToken) =>
            Task.FromResult<(string, string)?>(coloniaId == _colonia ? ("Colonia", "42800") : null);

        public Task<string?> FindNeighborhoodAtAsync(double latitude, double longitude, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<string>> FindNeighborhoodsByPostalCodeAsync(string postalCode, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> NeighborhoodExistsAsync(string coloniaId, string cp, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<(string Clave, string Nombre)>> ListActiveTradesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<(string, string)>>([.. Enumerable.Range(0, trades).Select(i => ($"t{i}", $"Oficio {i:00}")), ("plo", "Plomería")]);
    }
}
