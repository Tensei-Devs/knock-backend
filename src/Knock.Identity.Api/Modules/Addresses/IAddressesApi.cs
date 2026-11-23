namespace Knock.Identity.Api.Modules.Addresses;

/// <summary>Una dirección de la persona tal como entra en su exportación de datos (spec 039, R-22): calle y referencias descifradas, solo para su dueña.</summary>
/// <param name="Street">Calle y número en claro.</param>
/// <param name="Colonia">Colonia.</param>
/// <param name="References">Referencias en claro; <see langword="null"/> si no hay.</param>
/// <param name="Principal">Si es la dirección principal.</param>
/// <param name="CreatedAt">Cuándo se guardó (UTC).</param>
public sealed record AddressExport(string Street, string Colonia, string? References, bool Principal, DateTime CreatedAt)
{
    /// <summary>Sin datos: un registro imprimiría la calle y las referencias.</summary>
    public sealed override string ToString() => nameof(AddressExport);
}

/// <summary>Contrato público del módulo Addresses: la única vía para que otros módulos lo usen (o por eventos).</summary>
public interface IAddressesApi
{
    string ModuleName { get; }

    /// <summary>Direcciones de la cuenta <paramref name="accountId"/> (el <c>_id</c> en hexadecimal), la más reciente primero, descifradas para su exportación. Vacía si el identificador no es válido.</summary>
    Task<IReadOnlyList<AddressExport>> ExportAsync(string accountId, CancellationToken cancellationToken);
}
