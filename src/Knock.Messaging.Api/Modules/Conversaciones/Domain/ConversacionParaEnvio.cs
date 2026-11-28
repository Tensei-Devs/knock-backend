namespace Knock.Messaging.Api.Modules.Conversaciones.Domain;

/// <summary>
/// Lo de una conversación guardada que hace falta para enviar o unirse (spec 030, §6): el par en Mercado, los dos participantes por <c>sub</c>.
/// Los participantes se comparan siempre contra esto, nunca contra lo que mande la persona.
/// </summary>
internal sealed record ConversacionParaEnvio(
    string Id,
    string SolicitudId,
    string TecnicoId,
    string ClienteId,
    string TecnicoUsuarioId)
{
    /// <summary><c>cliente</c> o <c>tecnico</c> si <paramref name="sub"/> participa; <see langword="null"/> si es ajeno (sin distinguir «no existe» de «es ajena»).</summary>
    public string? RolDe(string? sub)
    {
        if (string.IsNullOrEmpty(sub))
        {
            return null;
        }

        if (string.Equals(sub, ClienteId, StringComparison.Ordinal))
        {
            return ReglasDeMensaje.RolCliente;
        }

        return string.Equals(sub, TecnicoUsuarioId, StringComparison.Ordinal) ? ReglasDeMensaje.RolTecnico : null;
    }

    /// <summary>El <c>sub</c> de la otra parte, a quien se avisa del mensaje.</summary>
    public string DestinatarioDe(string rolDelAutor) =>
        string.Equals(rolDelAutor, ReglasDeMensaje.RolCliente, StringComparison.Ordinal) ? TecnicoUsuarioId : ClienteId;
}

/// <summary>Lo que el permiso de Mercado dice y se guarda en la conversación y en Redis 60 s (R-09): nunca lleva texto de mensajes.</summary>
internal sealed record PermisoVigente(bool Abierta, string? MotivoCierre, bool Elegido, string? VisitaId);
