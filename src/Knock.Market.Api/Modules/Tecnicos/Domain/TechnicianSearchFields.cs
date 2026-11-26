using Knock.Market.Api.Common.Text;
using MongoDB.Bson;

namespace Knock.Market.Api.Modules.Tecnicos.Domain;

/// <summary>
/// Campos de <c>tecnicos</c> que la búsqueda por texto lee (spec 027, R-06 y §6) y que solo puede calcular quien escribe el perfil: <c>nombrePublico</c> y
/// <c>palabrasBusqueda</c>. Las palabras usan el mismo normalizador que la consulta; si difieren, la búsqueda por nombre no encuentra al técnico.
/// </summary>
internal static class TechnicianSearchFields
{
    public const string PublicName = "nombrePublico";
    public const string Words = "palabrasBusqueda";

    public static IReadOnlyList<string> WordsOf(string name, string? businessName) =>
        [.. SearchNameNormalizer.NormalizeQuery($"{name} {businessName}").Split(' ', StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.Ordinal)];

    public static void AddTo(BsonDocument document, string name, string? businessName)
    {
        document[PublicName] = name;
        document[Words] = new BsonArray(WordsOf(name, businessName));
    }
}
