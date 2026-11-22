using AwesomeAssertions;
using Knock.Market.Api.Modules.Catalog;
using Knock.Market.Api.Modules.Tecnicos.Data;
using Knock.Market.Api.Modules.Tecnicos.Dtos;
using Knock.Market.Api.Modules.Tecnicos.Services;
using Microsoft.Extensions.Caching.Memory;
using MongoDB.Bson;
using Xunit;

namespace Knock.UnitTest.Tecnicos;

/// <summary>Spec 047: caché de 1 minuto de SRV-01 con invalidación al escribir (R-04, R-16, CA-05, CA-25) y reglas de negocio (R-13 a R-15, CA-22 a CA-24). Solo datos ficticios.</summary>
public sealed class Spec047CacheYReglasTests : IDisposable
{
    private const string _sub = "sub-1";
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly FakeStore _store = new();

    public void Dispose() => _cache.Dispose();

    private ServiceService Service() => new(_store, new FakeCatalog(), _cache);

    [Fact]
    public async Task Consulta_Repetida_Sale_De_La_Cache_Y_La_Base_Recibe_Una_Sola_CA_047_05()
    {
        var service = Service();

        var first = await service.ListAsync(_store.TechnicianId.ToString(), CancellationToken.None);
        var second = await service.ListAsync(_store.TechnicianId.ToString(), CancellationToken.None);

        first.Should().NotBeNull();
        second.Should().BeSameAs(first);
        _store.Lists.Should().Be(1);
        _store.VisibilityChecks.Should().Be(1);
    }

    [Fact]
    public async Task Tecnico_Oculto_Responde_Nulo_Y_No_Se_Guarda_CA_047_04()
    {
        _store.Visible = false;

        (await Service().ListAsync(_store.TechnicianId.ToString(), CancellationToken.None)).Should().BeNull();
        _cache.TryGetValue(ServiceService.CacheKey(_store.TechnicianId), out _).Should().BeFalse();
    }

    [Fact]
    public async Task Crear_Editar_Y_Borrar_Invalidan_La_Cache_CA_047_25()
    {
        var service = Service();
        var id = _store.TechnicianId.ToString();
        await service.ListAsync(id, CancellationToken.None);

        var created = await service.CreateAsync(_sub, new CreateServiceBody("Cambio de llave", null, 350, "plo"), CancellationToken.None);
        _cache.TryGetValue(ServiceService.CacheKey(_store.TechnicianId), out _).Should().BeFalse();
        await service.ListAsync(id, CancellationToken.None);

        await service.UpdateAsync(_sub, created.Service!.Id, new UpdateServiceBody("Otro nombre", null, null, null), CancellationToken.None);
        _cache.TryGetValue(ServiceService.CacheKey(_store.TechnicianId), out _).Should().BeFalse();
        await service.ListAsync(id, CancellationToken.None);

        await service.DeleteAsync(_sub, created.Service.Id, CancellationToken.None);
        _cache.TryGetValue(ServiceService.CacheKey(_store.TechnicianId), out _).Should().BeFalse();
        _store.Lists.Should().Be(3);
    }

    [Fact]
    public void La_Clave_De_Cache_Solo_Lleva_El_Id_Del_Tecnico_R_04()
    {
        var id = ObjectId.GenerateNewId();

        ServiceService.CacheKey(id).Should().Be($"tecnicos:servicios:{id}");
    }

    [Fact]
    public async Task El_Servicio_Cincuenta_Y_Uno_Responde_Limite_CA_047_23()
    {
        _store.Count = ServiceStore.MaxPerTechnician;

        var outcome = await Service().CreateAsync(_sub, new CreateServiceBody("Uno más", null, 100, "plo"), CancellationToken.None);

        outcome.Status.Should().Be(ServiceStatus.LimitReached);
        outcome.Service.Should().BeNull();
    }

    [Fact]
    public async Task Oficio_Que_No_Es_Del_Tecnico_O_Inactivo_Responde_Oficio_Invalido_CA_047_22()
    {
        var service = Service();

        (await service.CreateAsync(_sub, new CreateServiceBody("Pintar", null, 100, "pin"), CancellationToken.None)).Status.Should().Be(ServiceStatus.TradeInvalid);
        (await service.CreateAsync(_sub, new CreateServiceBody("Viejo", null, 100, "inactivo"), CancellationToken.None)).Status.Should().Be(ServiceStatus.TradeInvalid);
        (await service.UpdateAsync(_sub, ObjectId.GenerateNewId().ToString(), new UpdateServiceBody(null, null, null, "pin"), CancellationToken.None)).Status.Should().Be(ServiceStatus.TradeInvalid);
    }

    [Fact]
    public async Task Servicio_Ajeno_O_Inexistente_Responde_No_Encontrado_CA_047_24()
    {
        var service = Service();
        var other = ObjectId.GenerateNewId().ToString();

        (await service.UpdateAsync(_sub, other, new UpdateServiceBody("x", null, null, null), CancellationToken.None)).Status.Should().Be(ServiceStatus.NotFound);
        (await service.DeleteAsync(_sub, other, CancellationToken.None)).Status.Should().Be(ServiceStatus.NotFound);
    }

    [Fact]
    public async Task Cuenta_Sin_Perfil_Responde_Sin_Tecnico_CA_047_08()
    {
        var outcome = await Service().CreateAsync("otra-cuenta", new CreateServiceBody("Cambio", null, 100, "plo"), CancellationToken.None);

        outcome.Status.Should().Be(ServiceStatus.NotATechnician);
    }

    private sealed class FakeStore : IServiceStore
    {
        private readonly List<TechnicianService> _items = [];

        public ObjectId TechnicianId { get; } = ObjectId.GenerateNewId();

        public bool Visible { get; set; } = true;

        public int Count { get; set; }

        public int Lists { get; private set; }

        public int VisibilityChecks { get; private set; }

        public Task<(ObjectId Id, IReadOnlyList<string> Trades)?> FindOwnAsync(string sub, CancellationToken cancellationToken) =>
            Task.FromResult<(ObjectId, IReadOnlyList<string>)?>(sub == _sub ? (TechnicianId, ["plo", "inactivo"]) : null);

        public Task<bool> IsVisibleAsync(ObjectId technicianId, CancellationToken cancellationToken)
        {
            VisibilityChecks++;
            return Task.FromResult(Visible);
        }

        public Task<IReadOnlyList<TechnicianService>> ListAsync(ObjectId technicianId, CancellationToken cancellationToken)
        {
            Lists++;
            return Task.FromResult<IReadOnlyList<TechnicianService>>([.. _items]);
        }

        public Task<(ServiceCreateStatus Status, TechnicianService? Service)> CreateAsync(ObjectId technicianId, string name, string? detail, int price, string trade, CancellationToken cancellationToken)
        {
            if (Count >= ServiceStore.MaxPerTechnician)
            {
                return Task.FromResult<(ServiceCreateStatus, TechnicianService?)>((ServiceCreateStatus.LimitReached, null));
            }

            var item = new TechnicianService(ObjectId.GenerateNewId().ToString(), name, detail, price, trade);
            _items.Add(item);
            return Task.FromResult<(ServiceCreateStatus, TechnicianService?)>((ServiceCreateStatus.Created, item));
        }

        public Task<TechnicianService?> UpdateAsync(ObjectId technicianId, ObjectId id, string? name, string? detail, int? price, string? trade, CancellationToken cancellationToken)
        {
            var index = _items.FindIndex(i => i.Id == id.ToString());
            if (index < 0)
            {
                return Task.FromResult<TechnicianService?>(null);
            }

            _items[index] = _items[index] with { Nombre = name ?? _items[index].Nombre };
            return Task.FromResult<TechnicianService?>(_items[index]);
        }

        public Task<bool> DeleteAsync(ObjectId technicianId, ObjectId id, CancellationToken cancellationToken) =>
            Task.FromResult(_items.RemoveAll(i => i.Id == id.ToString()) > 0);
    }

    private sealed class FakeCatalog : ICatalogApi
    {
        public string ModuleName => "Catalog";

        public Task<(string Nombre, string Modalidad)?> FindActiveTradeAsync(string clave, CancellationToken cancellationToken) =>
            Task.FromResult<(string, string)?>(clave == "plo" ? ("Plomería", "domicilio") : null);

        public Task<(string Nombre, string Cp)?> FindNeighborhoodAsync(string coloniaId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<string?> FindNeighborhoodAtAsync(double latitude, double longitude, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<string>> FindNeighborhoodsByPostalCodeAsync(string postalCode, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> NeighborhoodExistsAsync(string coloniaId, string cp, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<(string Clave, string Nombre)>> ListActiveTradesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
