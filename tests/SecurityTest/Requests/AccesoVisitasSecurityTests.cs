using System.Globalization;
using System.Net;
using Knock.SecurityTest.Support;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Knock.SecurityTest.Requests;

/// <summary>
/// Specs 022, 049, 050, 063 y 064: quién puede ver una visita y usar cada una de sus acciones. Mercado real en Production, MongoDB y Redis reales, con la visita sembrada en el estado en que la
/// acción funciona. Cada caso prueba sin token, token vencido, persona ajena y rol incorrecto, que nada cambie y que quien tiene derecho sí pueda. Solo datos ficticios.
/// </summary>
public sealed class AccesoVisitasSecurityTests
{
    private static readonly Actor[] _bothParts = [Actor.Client, Actor.Chosen];

    private static readonly Dictionary<string, AccessCase> _reads = new(StringComparer.Ordinal)
    {
        ["ver la visita"] = Read(string.Empty, AccessCase.Nothing),
        ["ver la cotizacion final"] = Read("/cotizacion-final", WithFinalQuote),
        ["ver los ajustes pendientes"] = Read("/ajustes-pendientes", (host, s) => host.SeedAdjustmentAsync(s)),
    };

    private static readonly Dictionary<string, AccessCase> _eitherPart = new(StringComparer.Ordinal)
    {
        ["proponer otra fecha"] = Post("reprogramar", _ => new { fechaHora = DateTimeOffset.UtcNow.AddDays(5) }, AccessCase.Nothing, Actor.Client, Strangers()),
        ["responder la propuesta"] = Post("reprogramar/respuesta", _ => new { acepta = true }, WithTechnicianProposal, Actor.Client, StrangersAnd(Actor.Chosen, HttpStatusCode.Conflict)),
        ["cancelar"] = Post("cancelar", _ => new { motivo = "Cambio de planes del cliente" }, AccessCase.Nothing, Actor.Client, Strangers()),
    };

    private static readonly Dictionary<string, AccessCase> _clientOnly = new(StringComparer.Ordinal)
    {
        ["reportar que el tecnico falto"] = Post("tecnico-ausente", AccessCase.NoBody, AppointmentJustPassed, Actor.Client, NotTheClient()),
        ["reportar no terminado"] = Post("no-terminado", _ => new { texto = "La llave sigue goteando igual" }, Finished, Actor.Client, NotTheClient()) with { Success = HttpStatusCode.Created },
        ["dar permiso de fotos"] = Post("permiso-fotos", _ => new { permiso = true }, AccessCase.Nothing, Actor.Client, NotTheClient()) with { Method = "PUT" },
        ["preparar el aviso de confianza"] = Post("aviso", AccessCase.NoBody, AccessCase.Nothing, Actor.Client, NotTheClient()),
        ["decidir la cotizacion final"] = Post(
            "cotizacion-final/decision",
            _ => new { decision = "aprueba" },
            WithFinalQuote,
            Actor.Client,
            StrangersAnd(Actor.Chosen, HttpStatusCode.Forbidden)),
    };

    private static readonly Dictionary<string, AccessCase> _technicianOnly = new(StringComparer.Ordinal)
    {
        ["registrar la llegada con el codigo correcto"] = Post("llegada", _ => new { codigo = MarketAccessHost.VisitCode }, ArrivalWindowOpen, Actor.Chosen, NotTheTechnician()),
        ["reportar que el cliente falto"] = Post("cliente-ausente", AccessCase.NoBody, AppointmentJustPassed, Actor.Chosen, NotTheTechnician()),
        ["terminar"] = Post(
            "terminar",
            _ => new { descripcion = "Se cambio la llave y se sello", fotos = new[] { Guid.NewGuid().ToString("N") } },
            InProgress,
            Actor.Chosen,
            NotTheTechnician()),
        ["enviar la cotizacion final"] = Post("cotizacion-final", _ => new { precio = 850, descripcion = "Cambio completo de la llave" }, VisitQuoteInProgress, Actor.Chosen, NotTheTechnician()) with { Success = HttpStatusCode.Created },
    };

    private static readonly Dictionary<string, AccessCase> _cases = new[] { _reads, _eitherPart, _clientOnly, _technicianOnly }
        .SelectMany(group => group)
        .ToDictionary(pair => pair.Key + " (" + pair.Value.Method + ")", pair => pair.Value, StringComparer.Ordinal);

    public static TheoryData<string> Cases() => AccessCase.Names(_cases);

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task OnlyWhoHasTheRight_CanUseTheVisit(string name)
    {
        var host = await MarketAccessHost.SharedAsync();

        await _cases[name].VerifyAsync(host, await host.SeedVisitAsync());
    }

    private static AccessCase Read(string suffix, Func<MarketAccessHost, MarketScene, Task> arrange) =>
        new("GET", s => $"/api/v1/visitas/{s.VisitId}{suffix}", AccessCase.NoBody, arrange, _bothParts, HttpStatusCode.OK, Strangers());

    private static AccessCase Post(
        string action, Func<MarketScene, object?> body, Func<MarketAccessHost, MarketScene, Task> arrange, Actor allowed, Dictionary<Actor, HttpStatusCode> denied) =>
        new("POST", s => $"/api/v1/visitas/{s.VisitId}/{action}", body, arrange, [allowed], HttpStatusCode.OK, denied);

    private static Dictionary<Actor, HttpStatusCode> Strangers() => new()
    {
        [Actor.OtherPerson] = HttpStatusCode.NotFound,
        [Actor.OtherTechnician] = HttpStatusCode.NotFound,
    };

    private static Dictionary<Actor, HttpStatusCode> StrangersAnd(Actor counterpart, HttpStatusCode status) => new(Strangers()) { [counterpart] = status };

    /// <summary>Acciones del cliente: el técnico de la visita tampoco puede.</summary>
    private static Dictionary<Actor, HttpStatusCode> NotTheClient() => StrangersAnd(Actor.Chosen, HttpStatusCode.NotFound);

    /// <summary>Acciones del técnico: quien no es técnico (también el cliente) recibe 403; un técnico ajeno, 404.</summary>
    private static Dictionary<Actor, HttpStatusCode> NotTheTechnician() => new()
    {
        [Actor.Client] = HttpStatusCode.Forbidden,
        [Actor.OtherPerson] = HttpStatusCode.Forbidden,
        [Actor.OtherTechnician] = HttpStatusCode.NotFound,
    };

    private static Task InProgress(MarketAccessHost host, MarketScene scene) =>
        host.SetVisitAsync(scene, Builders<BsonDocument>.Update.Set("estado", "en_curso").Set("llegadaEn", DateTime.UtcNow));

    private static Task Finished(MarketAccessHost host, MarketScene scene) =>
        host.SetVisitAsync(scene, Builders<BsonDocument>.Update.Set("estado", "terminada").Set("terminadaEn", DateTime.UtcNow.AddHours(-1)));

    private static async Task VisitQuoteInProgress(MarketAccessHost host, MarketScene scene)
    {
        await InProgress(host, scene);
        await host.SetQuoteAsync(scene, Builders<BsonDocument>.Update.Set("tipoPrecio", "visita"));
    }

    private static async Task WithFinalQuote(MarketAccessHost host, MarketScene scene)
    {
        await InProgress(host, scene);
        var finalQuote = new BsonDocument
        {
            ["precio"] = 850,
            ["descripcion"] = "Cambio completo de la llave",
            ["estado"] = "enviada",
            ["venceEn"] = DateTime.UtcNow.AddHours(24),
        };
        await host.SetVisitAsync(scene, Builders<BsonDocument>.Update.Set("cotizacionFinal", finalQuote));
    }

    private static Task WithTechnicianProposal(MarketAccessHost host, MarketScene scene)
    {
        var proposal = new BsonDocument
        {
            ["fechaHora"] = DateTime.UtcNow.AddDays(6),
            ["propuestaPor"] = "tecnico",
            ["venceEn"] = DateTime.UtcNow.AddHours(20),
        };
        return host.SetVisitAsync(scene, Builders<BsonDocument>.Update.Set("propuestaReprogramacion", proposal));
    }

    /// <summary>La cita fue hace 30 minutos: dentro de la ventana para reportar una ausencia.</summary>
    private static Task AppointmentJustPassed(MarketAccessHost host, MarketScene scene) => SetAppointmentAsync(host, scene, TimeSpan.FromMinutes(-30));

    private static Task ArrivalWindowOpen(MarketAccessHost host, MarketScene scene) => SetAppointmentAsync(host, scene, TimeSpan.Zero);

    private static Task SetAppointmentAsync(MarketAccessHost host, MarketScene scene, TimeSpan fromNow)
    {
        var local = MarketAccessHost.LocalNow(fromNow);
        return host.SetVisitAsync(scene, Builders<BsonDocument>.Update
            .Set("dia", local.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
            .Set("hora", local.ToString("HH:mm", CultureInfo.InvariantCulture)));
    }
}
