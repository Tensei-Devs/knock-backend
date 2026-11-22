using System.Text.Json;
using System.Text.Json.Serialization;

namespace Knock.Market.Api.Modules.Assistant.Services;

/// <summary>Funciones de producto que usan la IA (spec 024, §5). Cada una tiene su esquema, su prompt y su plantilla.</summary>
internal enum AiFunction { AsistenteKnock, ResumenChat, BorradorCotizacion }

/// <summary>Quién produjo el valor: el proveedor de Azure OpenAI o la plantilla local.</summary>
internal enum AiOrigin { Provider, Template }

/// <summary>Por qué se respondió con la plantilla (spec 024, §5). Nombres estables en inglés.</summary>
internal static class FallbackReasons
{
    public const string BudgetExhausted = "budget_exhausted";
    public const string Disabled = "disabled";
    public const string Unavailable = "unavailable";
    public const string ImageRejected = "image_rejected";
    public const string LimitUnavailable = "limit_unavailable";
    public const string InvalidSchema = "invalid_schema";
    public const string NoFallback = "no_fallback";
}

/// <summary>Entrada del cliente. <paramref name="UsuarioId"/> (el <c>sub</c>) solo sirve para topes y registro de uso: nunca se envía al proveedor.</summary>
internal sealed record SolicitudIa(AiFunction Funcion, string UsuarioId, string Texto, IReadOnlyList<string> ArchivoIds, string VersionEsquema = "1");

/// <summary>Salida: el valor es siempre el tipo cerrado de la función, venga del proveedor o de la plantilla.</summary>
internal sealed record ResultadoIa<T>(T? Valor, AiOrigin Origen, string? MotivoRespaldo, IReadOnlyList<CensorshipSummary> TiposCensurados, bool Recortado, int LatenciaMs) where T : class;

/// <summary>Tipo de dato censurado y cuántos; nunca el valor.</summary>
internal readonly record struct CensorshipSummary(string Type, int Count);

/// <summary>Resuelve la función de IA de cada tipo cerrado de salida.</summary>
internal static class AiOutputs
{
    public static AiFunction FunctionOf<T>() where T : class, IAiOutput<T>
    {
        if (typeof(T) == typeof(AsistenteKnockSalida))
        {
            return AiFunction.AsistenteKnock;
        }

        return typeof(T) == typeof(ResumenChatSalida) ? AiFunction.ResumenChat : AiFunction.BorradorCotizacion;
    }
}

/// <summary>Tipo cerrado de salida de una función (spec 024, R-05 y R-06).</summary>
internal interface IAiOutput<TSelf> where TSelf : class, IAiOutput<TSelf>
{
    /// <summary>Verdadero si cumple las reglas de contenido más allá del tipo (campos no vacíos, rangos y tamaños de listas).</summary>
    bool IsValid();

    /// <summary>Copia con cada campo de texto recortado a <paramref name="max"/>; <paramref name="clipped"/> indica si hubo recorte.</summary>
    TSelf Clip(int max, out bool clipped);
}

internal sealed record AsistenteKnockSalida : IAiOutput<AsistenteKnockSalida>
{
    [JsonRequired] public string Categoria { get; init; } = string.Empty;

    [JsonRequired] public string Mensaje { get; init; } = string.Empty;

    [JsonRequired] public bool PideFoto { get; init; }

    [JsonRequired] public string? Seguridad { get; init; }

    public static AiFunction Function => AiFunction.AsistenteKnock;

    public bool IsValid() => !string.IsNullOrWhiteSpace(Categoria) && !string.IsNullOrWhiteSpace(Mensaje);

    public AsistenteKnockSalida Clip(int max, out bool clipped)
    {
        var any = false;
        var result = this with { Categoria = AiText.Clip(Categoria, max, ref any), Mensaje = AiText.Clip(Mensaje, max, ref any), Seguridad = Seguridad is null ? null : AiText.Clip(Seguridad, max, ref any) };
        clipped = any;
        return result;
    }
}

internal sealed record ResumenChatSalida : IAiOutput<ResumenChatSalida>
{
    public const int MaxItems = 10;
    public const int MaxItemLength = 200;

    [JsonRequired] public IReadOnlyList<string> Sabe { get; init; } = [];

    [JsonRequired] public IReadOnlyList<string> PorConfirmar { get; init; } = [];

    [JsonRequired] public IReadOnlyList<string> QueLlevar { get; init; } = [];

    public static AiFunction Function => AiFunction.ResumenChat;

    public bool IsValid() => new[] { Sabe, PorConfirmar, QueLlevar }.All(list => list.Count is >= 1 and <= MaxItems && list.All(item => !string.IsNullOrWhiteSpace(item)));

    public ResumenChatSalida Clip(int max, out bool clipped)
    {
        var any = false;
        var limit = Math.Min(max, MaxItemLength);
        var result = this with { Sabe = ClipList(Sabe, limit, ref any), PorConfirmar = ClipList(PorConfirmar, limit, ref any), QueLlevar = ClipList(QueLlevar, limit, ref any) };
        clipped = any;
        return result;
    }

    private static List<string> ClipList(IReadOnlyList<string> list, int limit, ref bool any)
    {
        if (list.Count > MaxItems)
        {
            any = true;
        }

        var items = new List<string>();
        foreach (var item in list.Take(MaxItems))
        {
            items.Add(AiText.Clip(item, limit, ref any));
        }

        return items;
    }
}

internal sealed record BorradorCotizacionSalida : IAiOutput<BorradorCotizacionSalida>
{
    public const decimal MaxPrice = 1_000_000m;

    [JsonRequired] public decimal PrecioSugerido { get; init; }

    [JsonRequired] public string TiempoEstimado { get; init; } = string.Empty;

    [JsonRequired] public string Dia { get; init; } = string.Empty;

    [JsonRequired] public string Nota { get; init; } = string.Empty;

    [JsonRequired] public string Explicacion { get; init; } = string.Empty;

    public static AiFunction Function => AiFunction.BorradorCotizacion;

    public bool IsValid() => PrecioSugerido is >= 0 and <= MaxPrice && new[] { TiempoEstimado, Dia, Nota, Explicacion }.All(text => !string.IsNullOrWhiteSpace(text));

    public BorradorCotizacionSalida Clip(int max, out bool clipped)
    {
        var any = false;
        var result = this with { TiempoEstimado = AiText.Clip(TiempoEstimado, max, ref any), Dia = AiText.Clip(Dia, max, ref any), Nota = AiText.Clip(Nota, max, ref any), Explicacion = AiText.Clip(Explicacion, max, ref any) };
        clipped = any;
        return result;
    }
}

internal static class AiText
{
    /// <summary>Opciones del esquema cerrado: sin miembros desconocidos, camelCase y campos obligatorios (spec 024, R-05).</summary>
    public static readonly JsonSerializerOptions Strict = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectNullableAnnotations = true,
        NumberHandling = JsonNumberHandling.Strict,
    };

    public static string Clip(string value, int max, ref bool clipped)
    {
        if (value.Length <= max)
        {
            return value;
        }

        clipped = true;
        return value[..max];
    }

    public static string Name(AiFunction function) => function switch
    {
        AiFunction.AsistenteKnock => "asistente_knock",
        AiFunction.ResumenChat => "resumen_chat",
        _ => "borrador_cotizacion",
    };
}

/// <summary>Único punto de entrada a la IA generativa del backend (spec 024, R-01).</summary>
internal interface IAiClient
{
    Task<ResultadoIa<T>> GenerarAsync<T>(SolicitudIa solicitud, CancellationToken cancellationToken) where T : class, IAiOutput<T>;
}
