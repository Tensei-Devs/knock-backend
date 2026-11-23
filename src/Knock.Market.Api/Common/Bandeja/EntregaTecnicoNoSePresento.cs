using Microsoft.Extensions.Options;

namespace Knock.Market.Api.Common.Bandeja;

/// <summary>
/// Entrega <c>TecnicoNoSePresento</c> (spec 064, R-07, R-08 y D-01) a <c>POST /api/v1/interno/avisos</c> de Mensajería: aviso de texto fijo <c>tecnico_no_se_presento</c> al técnico de la visita
/// (token con <c>Services:Messaging:InternalKey</c>, <c>aud</c> <c>knock-notifications</c>, permiso <c>tecnico_no_se_presento</c>). Viajan solo identificadores (el técnico y la visita): ni nombre ni datos
/// del cliente. Se marca entregado cuando Mensajería responde 202; el receptor es idempotente por <c>eventoId</c>, así que reintentar es seguro. Sin dirección o sin clave queda para reintento.
/// </summary>
internal sealed class EntregaTecnicoNoSePresento(IHttpClientFactory clients, IConfiguration configuration, IOptions<BandejaOptions> options, TimeProvider clock) : IEntregaDeBandeja
{
    public const string EventType = "TecnicoNoSePresento";
    public const string NoticeType = "tecnico_no_se_presento";

    public string Tipo => EventType;

    public Task<ResultadoDeEntrega> DeliverAsync(MensajeReservado mensaje, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mensaje);
        var data = mensaje.Datos;
        if (!data.Contains("visitaId") || !data.Contains("tecnicoUsuarioId"))
        {
            return Task.FromResult(ResultadoDeEntrega.Rechazado);
        }

        if (EntregaServicio.ReadBaseUri(configuration, EntregaOpinionModerada.MessagingBaseUrlConfigurationKey) is not { } baseUri
            || EntregaServicio.ReadKey(configuration, EntregaOpinionModerada.MessagingKeyConfigurationKey) is not { } key)
        {
            return Task.FromResult(ResultadoDeEntrega.Reintentable);
        }

        var body = new { eventoId = mensaje.Id, tipo = NoticeType, destinatarioId = data["tecnicoUsuarioId"].AsString, rol = EntregaOpinionModerada.TechnicianRole, recurso = data["visitaId"].AsString };
        return EntregaServicio.PostAsync(
            clients,
            EntregaOpinionModerada.MessagingClientName,
            TimeSpan.FromSeconds(options.Value.TimeoutSeconds),
            new Uri(baseUri, EntregaOpinionModerada.MessagingPath),
            EntregaServicio.IssueToken(key, EntregaOpinionModerada.MessagingAudience, NoticeType, clock),
            body,
            cancellationToken);
    }
}
