namespace Knock.Market.Api.Modules.Catalog.Domain;

/// <summary>
/// Oficio activo tal como lo devuelve <c>CatalogStore.FindActiveOficiosAsync</c>: clave, nombre, modalidad y frases sugeridas (spec 045, OFI-02). <paramref name="FrasesSugeridas"/> es opcional
/// para que las entradas que ya estaban en la caché antes de OFI-02 se sigan leyendo.
/// </summary>
internal sealed record OficioListing(string Clave, string Nombre, string Modalidad, IReadOnlyList<string>? FrasesSugeridas = null);
