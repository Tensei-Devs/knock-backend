using System.Text;

namespace Knock.Messaging.Api.Modules.Conversaciones.Domain;

/// <summary>
/// Reglas puras del mensaje de una conversación (spec 030, R-10 y R-14): el texto válido, su resumen para la lista y el rol que tiene una persona en la conversación. Sin estado, sin
/// Redis ni MongoDB: lo que decide el servicio se prueba aquí sin infraestructura.
/// </summary>
internal static class ReglasDeMensaje
{
    public const int MaxCaracteres = 1_000;
    public const int MaxBytesDelTexto = 4_096;
    public const int MensajesPorMinuto = 30;
    public const int LongitudDelResumen = 80;
    public const string RolCliente = "cliente";
    public const string RolTecnico = "tecnico";

    /// <summary>
    /// El texto recortado, o <see langword="null"/> si no vale (R-10): de 1 a <see cref="MaxCaracteres"/> caracteres, sin caracteres de control salvo el salto de línea y sin pasar de
    /// <see cref="MaxBytesDelTexto"/> bytes.
    /// </summary>
    public static string? Limpiar(string? texto)
    {
        if (texto is null || Encoding.UTF8.GetByteCount(texto) > MaxBytesDelTexto)
        {
            return null;
        }

        var recortado = texto.Trim();
        return recortado.Length is >= 1 and <= MaxCaracteres && !recortado.Any(c => char.IsControl(c) && c != '\n') ? recortado : null;
    }

    /// <summary>El texto recortado a <see cref="LongitudDelResumen"/> caracteres para <c>ultimoMensaje</c>, sin partir un par sustituto en dos.</summary>
    public static string Resumir(string texto)
    {
        ArgumentNullException.ThrowIfNull(texto);
        if (texto.Length <= LongitudDelResumen)
        {
            return texto;
        }

        var corte = char.IsHighSurrogate(texto[LongitudDelResumen - 1]) ? LongitudDelResumen - 1 : LongitudDelResumen;
        return texto[..corte];
    }
}
