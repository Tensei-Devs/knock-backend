using System.Globalization;
using System.Text;
using Knock.Market.Api.Modules.Assistant.Data;
using Knock.Market.Api.Modules.Assistant.Dtos;
using Microsoft.Extensions.Caching.Memory;

namespace Knock.Market.Api.Modules.Assistant.Services;

/// <summary>Puertos hacia otros módulos y servicios (spec 052, §8). Cuentas y solicitudes tienen implementación real (<c>AsistentePuertosReales</c>); conversaciones (spec 030) y referencia de mercado (spec 048) siguen con respaldo que falla cerrado.</summary>
internal interface IAsistenteCuentas
{
    /// <summary>Verdadero si la cuenta del <c>sub</c> tiene perfil de técnico.</summary>
    Task<bool> IsTechnicianAsync(string sub, CancellationToken cancellationToken);
}

internal enum ConversationAccessStatus { Ok, NotParticipant, Unavailable }

/// <summary>Texto de la conversación (spec 030) al que el técnico tiene acceso, sin teléfono ni dirección; vacío salvo con <see cref="ConversationAccessStatus.Ok"/>.</summary>
internal sealed record ConversationAccess(ConversationAccessStatus Status, string Transcript);

internal interface IConversacionesAsistente
{
    Task<ConversationAccess> GetForTechnicianAsync(string conversationId, string sub, CancellationToken cancellationToken);
}

/// <summary>Participación del técnico en una solicitud y, si ya se cerró, cuándo (para borrar la bitácora 30 días después).</summary>
internal sealed record RequestAccess(bool Participates, DateTime? ClosedAtUtc);

internal interface ISolicitudesAsistente
{
    Task<RequestAccess> GetAccessAsync(string solicitudId, string sub, CancellationToken cancellationToken);
}

internal sealed record PriceRange(decimal Minimum, decimal Maximum, int Samples, string Source);

/// <summary>Referencia de mercado de la spec 048 (percentiles 25 a 75 de cotizaciones aceptadas, con el rango de la categoría como respaldo).</summary>
internal interface IReferenciaMercado
{
    Task<PriceRange?> GetRangeAsync(string oficio, string tipoTrabajo, CancellationToken cancellationToken);
}

internal sealed class UnavailableConversaciones : IConversacionesAsistente
{
    public Task<ConversationAccess> GetForTechnicianAsync(string conversationId, string sub, CancellationToken cancellationToken) =>
        Task.FromResult(new ConversationAccess(ConversationAccessStatus.Unavailable, string.Empty));
}

internal sealed class UnavailableReferenciaMercado : IReferenciaMercado
{
    public Task<PriceRange?> GetRangeAsync(string oficio, string tipoTrabajo, CancellationToken cancellationToken) => Task.FromResult<PriceRange?>(null);
}

/// <summary>Resultado de una operación del asistente que puede fallar por una regla de negocio.</summary>
internal enum AssistantStatus { Ok, Created, NotTechnician, Forbidden, NotFound, AiUnavailable, DailyLimit, ReferenceUnavailable }

internal sealed record AssistantOutcome<T>(AssistantStatus Status, T? Value = default, int RetryAfterSeconds = 0) where T : class;

/// <summary>Reglas de riesgo, tiempo y limpieza de la salida del asistente.</summary>
internal static class AsistenteSeguridad
{
    /// <summary>Instrucciones fijas de seguridad ante olor a gas (spec 052, CA-32). Las pone el servidor, no el modelo: no dependen de que el modelo las recuerde.</summary>
    public const string GasSafety = "Si huele a gas: abre puertas y ventanas, no enciendas luces ni aparatos, cierra la llave de paso, sal del lugar y llama a emergencias (911) desde afuera.";

    public static bool MentionsGas(string text)
    {
        var folded = new string([.. text.Normalize(NormalizationForm.FormD).Where(c => char.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)]).ToUpperInvariant();
        return folded.Contains("GAS", StringComparison.Ordinal) && (folded.Contains("HUEL", StringComparison.Ordinal) || folded.Contains("FUGA", StringComparison.Ordinal) || folded.Contains("OLOR", StringComparison.Ordinal)
            || folded.Contains("BOILER", StringComparison.Ordinal) || folded.Contains("ESTUFA", StringComparison.Ordinal));
    }

    public static int SecondsUntilMidnight(DateTimeOffset now)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City");
        var local = TimeZoneInfo.ConvertTime(now, zone);
        var next = local.Date.AddDays(1);
        return Math.Max(1, (int)Math.Ceiling((new DateTimeOffset(next, zone.GetUtcOffset(next)) - now).TotalSeconds));
    }

    /// <summary>La salida del modelo también pasa por el filtro de datos personales (spec 052, R-18).</summary>
    public static string Scrub(string text) => FiltroDatosPersonales.Censurar(text).Text;
}

/// <summary>ASI-01 a ASI-03: llama al cliente de IA de la spec 024 y devuelve siempre una sugerencia; nunca envía ni guarda una cotización (R-22).</summary>
internal sealed class AsistenteService(IAiClient ai, IAsistenteCuentas accounts, IConversacionesAsistente conversations, TimeProvider clock)
{
    public async Task<AssistantOutcome<TurnoResponse>> TurnAsync(string sub, TurnoBody body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        var text = string.IsNullOrWhiteSpace(body.Contexto) ? body.Mensaje! : $"{body.Mensaje}\n\nContexto: {body.Contexto}";
        var result = await ai.GenerarAsync<AsistenteKnockSalida>(new SolicitudIa(AiFunction.AsistenteKnock, sub, text, body.Foto is null ? [] : [body.Foto]), cancellationToken);
        if (Failure(result.MotivoRespaldo, out var failure))
        {
            return new AssistantOutcome<TurnoResponse>(failure, null, FailureWait(failure));
        }

        var value = result.Valor!;
        var message = AsistenteSeguridad.Scrub(value.Mensaje);
        var safety = value.Seguridad is null ? null : AsistenteSeguridad.Scrub(value.Seguridad);
        if (AsistenteSeguridad.MentionsGas(body.Mensaje!))
        {
            message = message.StartsWith(AsistenteSeguridad.GasSafety, StringComparison.Ordinal) ? message : $"{AsistenteSeguridad.GasSafety} {message}";
            safety ??= "Posible fuga de gas.";
        }

        return new AssistantOutcome<TurnoResponse>(AssistantStatus.Ok, new TurnoResponse(AsistenteSeguridad.Scrub(value.Categoria), message, value.PideFoto, safety, OriginName(result.Origen), true));
    }

    public async Task<AssistantOutcome<ResumenChatResponse>> SummarizeAsync(string sub, string conversationId, CancellationToken cancellationToken)
    {
        var access = await AccessAsync<ResumenChatResponse>(sub, conversationId, cancellationToken);
        if (access.Outcome is not null)
        {
            return access.Outcome;
        }

        var result = await ai.GenerarAsync<ResumenChatSalida>(new SolicitudIa(AiFunction.ResumenChat, sub, access.Transcript, []), cancellationToken);
        if (Failure(result.MotivoRespaldo, out var failure))
        {
            return new AssistantOutcome<ResumenChatResponse>(failure, null, FailureWait(failure));
        }

        var v = result.Valor!;
        return new AssistantOutcome<ResumenChatResponse>(
            AssistantStatus.Ok,
            new ResumenChatResponse([.. v.Sabe.Select(AsistenteSeguridad.Scrub)], [.. v.PorConfirmar.Select(AsistenteSeguridad.Scrub)], [.. v.QueLlevar.Select(AsistenteSeguridad.Scrub)], OriginName(result.Origen), true));
    }

    public async Task<AssistantOutcome<BorradorCotizacionResponse>> DraftQuoteAsync(string sub, string conversationId, CancellationToken cancellationToken)
    {
        var access = await AccessAsync<BorradorCotizacionResponse>(sub, conversationId, cancellationToken);
        if (access.Outcome is not null)
        {
            return access.Outcome;
        }

        var result = await ai.GenerarAsync<BorradorCotizacionSalida>(new SolicitudIa(AiFunction.BorradorCotizacion, sub, access.Transcript, []), cancellationToken);
        if (Failure(result.MotivoRespaldo, out var failure))
        {
            return new AssistantOutcome<BorradorCotizacionResponse>(failure, null, FailureWait(failure));
        }

        var v = result.Valor!;
        return new AssistantOutcome<BorradorCotizacionResponse>(
            AssistantStatus.Ok,
            new BorradorCotizacionResponse(v.PrecioSugerido, AsistenteSeguridad.Scrub(v.TiempoEstimado), AsistenteSeguridad.Scrub(v.Dia), AsistenteSeguridad.Scrub(v.Nota), AsistenteSeguridad.Scrub(v.Explicacion), OriginName(result.Origen), true));
    }

    private static string OriginName(AiOrigin origin) => origin == AiOrigin.Provider ? "proveedor" : "plantilla";

    /// <summary>Sin el servicio configurado (<c>disabled</c>) o sin poder contar (<c>limit_unavailable</c>) se responde 503; un tope agotado, 429. Cualquier otro respaldo es una sugerencia válida de la plantilla.</summary>
    private static bool Failure(string? reason, out AssistantStatus status)
    {
        status = reason switch
        {
            FallbackReasons.Disabled or FallbackReasons.LimitUnavailable or FallbackReasons.NoFallback => AssistantStatus.AiUnavailable,
            FallbackReasons.BudgetExhausted => AssistantStatus.DailyLimit,
            _ => AssistantStatus.Ok,
        };
        return status != AssistantStatus.Ok;
    }

    private int FailureWait(AssistantStatus status) => status == AssistantStatus.DailyLimit ? AsistenteSeguridad.SecondsUntilMidnight(clock.GetUtcNow()) : 0;

    private async Task<(AssistantOutcome<T>? Outcome, string Transcript)> AccessAsync<T>(string sub, string conversationId, CancellationToken cancellationToken) where T : class
    {
        if (!await accounts.IsTechnicianAsync(sub, cancellationToken))
        {
            return (new AssistantOutcome<T>(AssistantStatus.NotTechnician), string.Empty);
        }

        var access = await conversations.GetForTechnicianAsync(conversationId, sub, cancellationToken);
        return access.Status switch
        {
            ConversationAccessStatus.Ok => (null, access.Transcript),
            ConversationAccessStatus.NotParticipant => (new AssistantOutcome<T>(AssistantStatus.Forbidden), string.Empty),
            _ => (new AssistantOutcome<T>(AssistantStatus.AiUnavailable), string.Empty),
        };
    }
}

/// <summary>ASI-04: rango sugerido por la referencia de mercado, en caché 1 hora por oficio y tipo; la clave no lleva datos de ninguna persona (R-09).</summary>
internal sealed class RangoPrecioService(IAsistenteCuentas accounts, IReferenciaMercado reference, IMemoryCache cache)
{
    public static readonly TimeSpan CacheLifetime = TimeSpan.FromHours(1);

    public async Task<AssistantOutcome<RangoPrecioResponse>> GetAsync(string sub, string oficio, string tipoTrabajo, CancellationToken cancellationToken)
    {
        if (!await accounts.IsTechnicianAsync(sub, cancellationToken))
        {
            return new AssistantOutcome<RangoPrecioResponse>(AssistantStatus.NotTechnician);
        }

        var key = $"asistente:rango:{oficio.Trim().ToUpperInvariant()}:{tipoTrabajo.Trim().ToUpperInvariant()}";
        if (cache.TryGetValue(key, out RangoPrecioResponse? cached) && cached is not null)
        {
            return new AssistantOutcome<RangoPrecioResponse>(AssistantStatus.Ok, cached);
        }

        var range = await reference.GetRangeAsync(oficio.Trim(), tipoTrabajo.Trim(), cancellationToken);
        if (range is null)
        {
            return new AssistantOutcome<RangoPrecioResponse>(AssistantStatus.ReferenceUnavailable);
        }

        var response = new RangoPrecioResponse(range.Minimum, range.Maximum, range.Samples, range.Source);
        cache.Set(key, response, CacheLifetime);
        return new AssistantOutcome<RangoPrecioResponse>(AssistantStatus.Ok, response);
    }
}

/// <summary>ASI-05 y ASI-06: bitácora del técnico por solicitud. Solo el propio técnico la guarda y la lee; se borra 30 días después de cerrarse la solicitud (R-21).</summary>
internal sealed class BitacoraService(IAsistenteCuentas accounts, ISolicitudesAsistente requests, IBitacoraStore store, TimeProvider clock)
{
    public static readonly TimeSpan Retention = TimeSpan.FromDays(30);

    public async Task<AssistantOutcome<BitacoraResponse>> SaveAsync(string sub, string solicitudId, BitacoraBody body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        if (!await accounts.IsTechnicianAsync(sub, cancellationToken))
        {
            return new AssistantOutcome<BitacoraResponse>(AssistantStatus.NotTechnician);
        }

        var access = await requests.GetAccessAsync(solicitudId, sub, cancellationToken);
        if (!access.Participates)
        {
            return new AssistantOutcome<BitacoraResponse>(AssistantStatus.Forbidden);
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var log = new BitacoraRecord(solicitudId, sub, body.Sabe!, body.PorConfirmar!, body.QueLlevar!, now, access.ClosedAtUtc?.Add(Retention));
        var created = await store.UpsertAsync(log, cancellationToken);
        return new AssistantOutcome<BitacoraResponse>(created ? AssistantStatus.Created : AssistantStatus.Ok, ToResponse(log));
    }

    public async Task<AssistantOutcome<BitacoraResponse>> GetAsync(string sub, string solicitudId, CancellationToken cancellationToken)
    {
        if (!await accounts.IsTechnicianAsync(sub, cancellationToken))
        {
            return new AssistantOutcome<BitacoraResponse>(AssistantStatus.NotTechnician);
        }

        var log = await store.FindAsync(solicitudId, sub, cancellationToken);
        return log is null ? new AssistantOutcome<BitacoraResponse>(AssistantStatus.NotFound) : new AssistantOutcome<BitacoraResponse>(AssistantStatus.Ok, ToResponse(log));
    }

    private static BitacoraResponse ToResponse(BitacoraRecord log) => new(log.SolicitudId, log.Sabe, log.PorConfirmar, log.QueLlevar, log.ActualizadaEn);
}
