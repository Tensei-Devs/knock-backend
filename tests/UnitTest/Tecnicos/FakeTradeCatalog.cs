using Knock.Market.Api.Modules.Catalog;

namespace Knock.UnitTest.Tecnicos;

/// <summary>Catálogo falso de las pruebas de Técnicos: solo conoce el oficio <c>plo</c> (Plomería, a domicilio); lo demás no se usa.</summary>
internal sealed class FakeTradeCatalog : ICatalogApi
{
    public string ModuleName => "Catalog";

    public Task<(string Nombre, string Modalidad)?> FindActiveTradeAsync(string clave, CancellationToken cancellationToken) =>
        Task.FromResult<(string, string)?>(clave is "plo" ? ("Plomería", "domicilio") : null);

    public Task<(string Nombre, string Cp)?> FindNeighborhoodAsync(string coloniaId, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<string?> FindNeighborhoodAtAsync(double latitude, double longitude, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<IReadOnlyList<string>> FindNeighborhoodsByPostalCodeAsync(string postalCode, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<bool> NeighborhoodExistsAsync(string coloniaId, string cp, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<IReadOnlyList<(string Clave, string Nombre)>> ListActiveTradesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
}
