using Microsoft.Extensions.Options;

namespace Knock.Market.Api.Modules.Catalog.Services;

/// <summary>Checks the original input before normalization; an unavailable filter must reject counting.</summary>
internal interface IFiltroDatosPersonales
{
    bool CanCount(string originalText);
}

internal sealed class FiltroDatosPersonalesNoDisponible : IFiltroDatosPersonales
{
    public bool CanCount(string originalText) => false;
}

internal sealed class SearchCursorOptions
{
    public const string SectionName = "Busqueda:Cursor";
    public const string KeyName = "Busqueda:Cursor:Key";
    public string? Key { get; set; }
}

internal sealed class SearchCursorOptionsValidator : IValidateOptions<SearchCursorOptions>
{
    public ValidateOptionsResult Validate(string? name, SearchCursorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        Span<byte> bytes = stackalloc byte[32];
        return options.Key is not null && Convert.TryFromBase64String(options.Key, bytes, out var count) && count == bytes.Length
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail($"{SearchCursorOptions.KeyName} must be a base64-encoded 256-bit key.");
    }
}
