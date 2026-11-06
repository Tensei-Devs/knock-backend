namespace Knock.Contracts.Pagination;

/// <summary>Límite de página común de la API (spec 002, R-05): 20 por defecto y 50 como máximo, para que un listado nunca sirva para extraer los datos en bloque.</summary>
public static class PageLimit
{
    public const int Default = 20;

    public const int Max = 50;

    /// <summary>Sin valor o menor que 1 devuelve el valor por defecto; más de <see cref="Max"/> se recorta a <see cref="Max"/>.</summary>
    public static int Resolve(int? requested) => requested switch
    {
        null or <= 0 => Default,
        > Max => Max,
        _ => requested.Value,
    };
}

/// <summary>Una página de un listado paginado por cursor (<c>cursor</c>, <c>limit</c>, <c>nextCursor</c>); <c>NextCursor</c> es <c>null</c> en la última página.</summary>
/// <typeparam name="T">Tipo de cada elemento.</typeparam>
/// <param name="Items">Elementos de la página, como máximo <see cref="PageLimit.Max"/>.</param>
/// <param name="NextCursor">Cursor opaco para pedir la página siguiente, o <c>null</c> si no hay más.</param>
public sealed record CursorPage<T>(IReadOnlyList<T> Items, string? NextCursor);
