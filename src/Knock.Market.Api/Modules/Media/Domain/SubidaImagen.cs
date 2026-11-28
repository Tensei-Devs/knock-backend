namespace Knock.Market.Api.Modules.Media.Domain;

/// <summary>Estado de una subida de imagen pública (spec 032, §6): <c>pendiente</c> pasa a <c>publicada</c>, <c>rechazada</c> o <c>invalida</c>.</summary>
internal enum EstadoSubida { Pendiente, Publicada, Rechazada, Invalida }

internal static class EstadoSubidaExtensions
{
    public static string ToDb(this EstadoSubida estado) => estado switch
    {
        EstadoSubida.Pendiente => "pendiente",
        EstadoSubida.Publicada => "publicada",
        EstadoSubida.Rechazada => "rechazada",
        _ => "invalida",
    };

    public static EstadoSubida ParseDb(string? valor) => valor switch
    {
        "publicada" => EstadoSubida.Publicada,
        "rechazada" => EstadoSubida.Rechazada,
        "invalida" => EstadoSubida.Invalida,
        _ => EstadoSubida.Pendiente,
    };
}

/// <summary>
/// Documento de <c>subidas_imagen</c> (spec 032, §6). <c>Sub</c> es el dueño y nunca se devuelve; <c>ClaveBlob</c> es el nombre del blob de
/// cuarentena (128 bits aleatorios, independiente de <c>Id</c>) y tampoco sale de Mercado.
/// </summary>
internal sealed record SubidaImagen(
    string Id,
    string ClaveBlob,
    string Sub,
    PropositoImagen Proposito,
    string TipoDeclarado,
    int TamanoDeclarado,
    EstadoSubida Estado,
    string? Url,
    int? Ancho,
    int? Alto,
    int? Bytes,
    DateTime CreadaEn,
    DateTime ConfirmarAntesDe,
    DateTime ExpiraEn)
{
    /// <summary>Una subida publicada no vence nunca: se fija una fecha lejana en lugar de quitar el campo del TTL (spec 032, §6).</summary>
    public static readonly DateTime SinVencimiento = new(9999, 12, 31, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>Días que viven los documentos que no quedaron publicados (R-19).</summary>
    public const int DiasDeVidaNoPublicada = 7;

    /// <summary>Minutos entre la emisión del enlace y el límite para confirmar (R-05).</summary>
    public const int MinutosParaConfirmar = 30;

    public static SubidaImagen Nueva(string id, string claveBlob, string sub, PropositoImagen proposito, string tipo, int tamano, DateTime ahora) =>
        new(id, claveBlob, sub, proposito, tipo, tamano, EstadoSubida.Pendiente, null, null, null, null,
            ahora, ahora.AddMinutes(MinutosParaConfirmar), ahora.AddDays(DiasDeVidaNoPublicada));
}
