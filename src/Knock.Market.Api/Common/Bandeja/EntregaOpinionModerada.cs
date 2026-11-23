using Microsoft.Extensions.Options;

namespace Knock.Market.Api.Common.Bandeja;

/// <summary>
/// Entrega <c>OpinionModerada</c> (spec 061, R-04, R-06 y R-07a). Primero a <c>POST /api/v1/interno/opiniones-moderadas</c> de Identidad (auditoría del panel; token con
/// <c>Security:InternalTokens:MarketKey</c>, <c>aud</c> <c>knock-identity</c>, permiso <c>opinion-moderada</c>) y, solo si la opinión se ocultó, a <c>POST /api/v1/interno/avisos</c> de Mensajería (aviso de texto
/// fijo <c>opinion_oculta</c> al técnico; token con <c>Services:Messaging:InternalKey</c>, <c>aud</c> <c>knock-notifications</c>, permiso <c>opinion_oculta</c>). Al mostrar de nuevo no hay aviso. Viajan solo
/// identificadores: nunca el motivo ni el texto de la opinión. El mensaje se marca entregado solo si todos los destinos responden 202; los receptores son idempotentes por <c>eventoId</c>, así que reintentar es seguro.
/// Sin dirección o sin clave de un destino el mensaje queda para reintento.
/// </summary>
internal sealed class EntregaOpinionModerada(IHttpClientFactory clients, IConfiguration configuration, IOptions<BandejaOptions> options, TimeProvider clock) : IEntregaDeBandeja
{
    public const string IdentityClientName = "bandeja-identidad-opiniones";
    public const string MessagingClientName = "bandeja-mensajeria";
    public const string EventType = "OpinionModerada";
    public const string IdentityPath = "api/v1/interno/opiniones-moderadas";
    public const string MessagingPath = "api/v1/interno/avisos";
    public const string IdentityKeyConfigurationKey = EntregaReporteNoTerminado.KeyConfigurationKey;
    public const string IdentityBaseUrlConfigurationKey = EntregaReporteNoTerminado.BaseUrlConfigurationKey;
    public const string MessagingKeyConfigurationKey = "Services:Messaging:InternalKey";
    public const string MessagingBaseUrlConfigurationKey = "Services:Messaging:BaseUrl";
    public const string IdentityAudience = EntregaReporteNoTerminado.Audience;
    public const string MessagingAudience = "knock-notifications";
    public const string IdentityPermission = "opinion-moderada";
    public const string NoticeType = "opinion_oculta";
    public const string TechnicianRole = "tecnico";
    public const string HiddenAction = "ocultada";

    public string Tipo => EventType;

    public async Task<ResultadoDeEntrega> DeliverAsync(MensajeReservado mensaje, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mensaje);
        var data = mensaje.Datos;
        if (!data.Contains("opinionId") || !data.Contains("tecnicoId") || !data.Contains("accion") || !data.Contains("porId"))
        {
            return ResultadoDeEntrega.Rechazado;
        }

        var action = data["accion"].AsString;
        var identity = await PostAsync(
            IdentityClientName,
            IdentityBaseUrlConfigurationKey,
            IdentityKeyConfigurationKey,
            IdentityPath,
            IdentityAudience,
            IdentityPermission,
            new { eventoId = mensaje.Id, tipo = EventType, opinionId = data["opinionId"].AsString, tecnicoId = data["tecnicoId"].AsString, accion = action, porId = data["porId"].AsString },
            cancellationToken);
        if (identity != ResultadoDeEntrega.Aceptado || action != HiddenAction || !data.Contains("tecnicoUsuarioId"))
        {
            return identity;
        }

        return await PostAsync(
            MessagingClientName,
            MessagingBaseUrlConfigurationKey,
            MessagingKeyConfigurationKey,
            MessagingPath,
            MessagingAudience,
            NoticeType,
            new { eventoId = mensaje.Id, tipo = NoticeType, destinatarioId = data["tecnicoUsuarioId"].AsString, rol = TechnicianRole, recurso = data["opinionId"].AsString },
            cancellationToken);
    }

    private Task<ResultadoDeEntrega> PostAsync(string clientName, string baseUrlKey, string keyKey, string path, string audience, string permission, object body, CancellationToken cancellationToken) =>
        EntregaServicio.ReadBaseUri(configuration, baseUrlKey) is { } baseUri && EntregaServicio.ReadKey(configuration, keyKey) is { } key
            ? EntregaServicio.PostAsync(clients, clientName, TimeSpan.FromSeconds(options.Value.TimeoutSeconds), new Uri(baseUri, path), EntregaServicio.IssueToken(key, audience, permission, clock), body, cancellationToken)
            : Task.FromResult(ResultadoDeEntrega.Reintentable);
}
