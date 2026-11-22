using MongoDB.Bson;

namespace Knock.Identity.Api.Hosting.Procesos.Outbox;

/// <summary>Un evento de la bandeja de salida (spec 054, §6). La <see cref="Carga"/> lleva solo identificadores.</summary>
internal sealed record EventoDeOutbox(string Id, string Tipo, string Destino, BsonDocument Carga);

/// <summary>Un evento reservado por el procesador, con el sello de su arrendamiento.</summary>
internal sealed record OutboxReservado(string Id, string Tipo, string Destino, BsonDocument Carga, string ReservaId, int Intentos);

/// <summary>Resultado de intentar entregar un evento al servicio destino (R-03 a R-05).</summary>
internal enum ResultadoDeEntrega
{
    /// <summary>El destino respondió 202.</summary>
    Aceptado,

    /// <summary>Red, 5xx o 429: se reintenta.</summary>
    Reintentable,

    /// <summary>401 o 403: no se reintenta.</summary>
    Rechazado,
}

/// <summary>Reglas cerradas de la bandeja: identificador determinista, tipos, destinos y carga sin datos personales (R-02).</summary>
internal static class OutboxReglas
{
    public static readonly IReadOnlyCollection<string> Tipos =
    [
        "solicitud_nueva", "solicitud_sin_tecnicos", "cotizacion_nueva", "cotizacion_aceptada", "solicitud_cancelada", "solicitud_vencida", "cotizacion_vencida", "mensaje_nuevo",
        "sesion_revocada", "CotizacionElegida", "BloqueoCreado", "BloqueoEliminado",
    ];

    public static readonly IReadOnlyCollection<string> Destinos = ["messaging", "market", "identity"];

    // Nombres de campo que nunca pueden viajar en la carga.
    private static readonly string[] _camposProhibidos = ["telefono", "celular", "phone", "direccion", "address", "nombre", "name", "texto", "text", "mensaje", "correo", "email", "curp"];

    /// <summary>El <c>_id</c> determinista <c>{tipo}:{recursoId}:{version}</c>: reintentar la operación no duplica el evento.</summary>
    public static string BuildId(string tipo, string recursoId, int version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tipo);
        ArgumentException.ThrowIfNullOrWhiteSpace(recursoId);
        return $"{tipo}:{recursoId}:{version}";
    }

    /// <summary>Valida tipo, destino y carga. Lanza <see cref="ArgumentException"/> con un código interno si algo no cumple.</summary>
    public static void Validate(EventoDeOutbox evento)
    {
        ArgumentNullException.ThrowIfNull(evento);
        if (!Tipos.Contains(evento.Tipo))
        {
            throw new ArgumentException("unknown_event_type", nameof(evento));
        }

        if (!Destinos.Contains(evento.Destino))
        {
            throw new ArgumentException("unknown_destination", nameof(evento));
        }

        foreach (var field in evento.Carga)
        {
            var identifierOnly = field.Value.IsString || field.Value.IsObjectId || field.Value.IsInt32 || field.Value.IsInt64;
            if (!identifierOnly || _camposProhibidos.Contains(field.Name, StringComparer.OrdinalIgnoreCase))
            {
                throw new ArgumentException("payload_not_allowed", nameof(evento));
            }
        }
    }
}
