using System.Reflection;

namespace Knock.Market.Api.Common.Moderation;


/// <summary>
/// Lista local de términos prohibidos (spec 031, R-03 y R-18). Respaldo que solo puede RECHAZAR: si pudiera aprobar, una caída de Azure abriría la puerta a lo que la lista no conoce.
/// Se carga al arrancar, se valida (sin líneas vacías, sin duplicados, de 2 a 40 caracteres, ya normalizada) y compara por palabra completa o frase completa.
/// </summary>
internal sealed class ListaLocal
{
    public const string ResourceName = "lista-local.es-MX.txt";
    private const int _minLength = 2;
    private const int _maxLength = 40;

    private readonly HashSet<string> _words;
    private readonly List<string[]> _phrases;
    private readonly string[] _spelled;

    private ListaLocal(IReadOnlyList<string> terms)
    {
        Terms = terms;
        _words = [.. terms.Where(term => !term.Contains(' ', StringComparison.Ordinal))];
        _phrases = [.. terms.Where(term => term.Contains(' ', StringComparison.Ordinal)).Select(term => term.Split(' '))];
        // Los términos de una palabra también se detectan escritos letra por letra («p u t o» o «p.u.t.o»).
        _spelled = [.. _words.Where(term => term.Length >= 4)];
    }

    public IReadOnlyList<string> Terms { get; }

    /// <summary>Huella de la lista (SHA-256 del contenido canónico) para comparar las copias de Mercado y Mensajería (R-18).</summary>
    public string Fingerprint => Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(string.Join('\n', Terms))));

    public static ListaLocal LoadEmbedded()
    {
        var assembly = typeof(ListaLocal).Assembly;
        var name = assembly.GetManifestResourceNames().Single(resource => resource.EndsWith(ResourceName, StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException("Local moderation list resource not found.");
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    }

    /// <summary>Valida y carga el contenido; lanza <see cref="InvalidOperationException"/> con la línea y el motivo (sin el término).</summary>
    public static ListaLocal Parse(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var lines = content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (lines[^1].Length == 0)
        {
            lines = lines[..^1];
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            if (string.IsNullOrWhiteSpace(line))
            {
                throw new InvalidOperationException($"Local moderation list line {index + 1} is empty.");
            }

            if (line.Length is < _minLength or > _maxLength)
            {
                throw new InvalidOperationException($"Local moderation list line {index + 1} must have {_minLength} to {_maxLength} characters.");
            }

            if (!string.Equals(line, NormalizadorDeModeracion.Normalizar(line), StringComparison.Ordinal) || !string.Equals(line, line.Trim(), StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Local moderation list line {index + 1} is not normalized.");
            }

            if (!seen.Add(line))
            {
                throw new InvalidOperationException($"Local moderation list line {index + 1} is duplicated.");
            }
        }

        return new ListaLocal(lines);
    }

    /// <summary>Verdadero si el texto contiene un término por palabra completa (o frase completa) tras normalizarlo.</summary>
    public bool Contiene(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var words = NormalizadorDeModeracion.Palabras(NormalizadorDeModeracion.Normalizar(text));
        if (words.Any(_words.Contains))
        {
            return true;
        }

        return _phrases.Any(phrase => ContainsSequence(words, phrase)) || _spelled.Any(term => SpelledOut(words, term));
    }

    private static bool ContainsSequence(IReadOnlyList<string> words, string[] phrase)
    {
        for (var start = 0; start + phrase.Length <= words.Count; start++)
        {
            if (phrase.Select((word, offset) => words[start + offset] == word).All(match => match))
            {
                return true;
            }
        }

        return false;
    }

    private static bool SpelledOut(IReadOnlyList<string> words, string term)
    {
        for (var start = 0; start < words.Count; start++)
        {
            var built = new System.Text.StringBuilder();
            for (var end = start; end < words.Count && words[end].Length <= 2 && built.Length < term.Length; end++)
            {
                built.Append(words[end]);
                if (built.ToString() == term)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
