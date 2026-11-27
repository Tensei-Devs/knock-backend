using MongoDB.Bson;

namespace Knock.Market.Api.Common.Bandeja;

/// <summary>Lectura de los <c>datos</c> de un evento del buzón para armar sus avisos: los roles y las personas de la visita.</summary>
internal static class DatosDeAviso
{
    public const string Client = "cliente";
    public const string Technician = EntregaOpinionModerada.TechnicianRole;
    public const string VisitField = "visitaId";

    public static string? Text(BsonDocument data, string field) => data.TryGetValue(field, out var value) && value.IsString ? value.AsString : null;

    /// <summary>La persona de la visita con ese rol; con un rol desconocido o ausente no hay destinatario.</summary>
    public static DestinoDeAviso ToRole(string notice, BsonDocument data, string? role)
    {
        var person = role switch
        {
            Client => Text(data, "clienteId"),
            Technician => Text(data, "tecnicoUsuarioId"),
            _ => null,
        };
        return new(notice, role, person, Text(data, VisitField));
    }
}
