using System.Globalization;
using System.Text;

namespace Knock.Market.Api.Modules.Moderation.Services;

/// <summary>
/// Normalizador de la lista local (spec 031, R-03). Función pura y sin estado: minúsculas, sin acentos, sustituciones comunes («4» por «a», «0» por «o»…) y sin letras ni signos repetidos
/// (las repeticiones se vuelven una sola). Es el punto que mutan las pruebas de mutación.
/// </summary>
internal static class NormalizadorDeModeracion
{
    private static readonly Dictionary<char, char> _substitutions = new()
    {
        ['4'] = 'a',
        ['@'] = 'a',
        ['3'] = 'e',
        ['1'] = 'i',
        ['!'] = 'i',
        ['0'] = 'o',
        ['$'] = 's',
        ['5'] = 's',
        ['7'] = 't',
    };

    /// <summary>Devuelve el texto en minúsculas, sin marcas combinantes, con las sustituciones aplicadas y las repeticiones reducidas a una. Conserva los espacios y signos.</summary>
    public static string Normalizar(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var run = 0;
        var previous = '\0';
        foreach (var raw in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(raw) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var lowered = char.ToLowerInvariant(raw);
            var current = _substitutions.TryGetValue(lowered, out var replacement) ? replacement : lowered;
            run = current == previous ? run + 1 : 1;
            previous = current;
            if (run == 1)
            {
                builder.Append(current);
            }
        }

        return builder.ToString();
    }

    /// <summary>Palabras (solo letras) del texto normalizado, separadas por cualquier otro carácter.</summary>
    public static IReadOnlyList<string> Palabras(string normalized)
    {
        ArgumentNullException.ThrowIfNull(normalized);
        var words = new List<string>();
        var current = new StringBuilder();
        foreach (var c in normalized)
        {
            if (char.IsLetter(c))
            {
                current.Append(c);
            }
            else if (current.Length > 0)
            {
                words.Add(current.ToString());
                current.Clear();
            }
        }

        if (current.Length > 0)
        {
            words.Add(current.ToString());
        }

        return words;
    }
}
