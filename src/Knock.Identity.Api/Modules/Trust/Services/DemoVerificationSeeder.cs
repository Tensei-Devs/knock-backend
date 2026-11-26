using System.Globalization;
using System.Text.RegularExpressions;
using Knock.Identity.Api.Modules.Trust.Data;
using Knock.Identity.Api.Modules.Trust.Domain;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;

namespace Knock.Identity.Api.Modules.Trust.Services;

/// <summary>
/// Carga la verificación decidida de la demo (spec 025, T-08): sin HU-067 nada llega a <c>verificada</c>, así que una cuenta demo ya viene verificada. Solo corre con <c>Seed:Demo</c> fuera de
/// Producción, solo con host local, solo si el archivo completo es válido y solo para cuentas demo que ya existan. Datos inventados: sin CURP, sin fotos y sin dirección.
/// </summary>
internal sealed partial class DemoVerificationSeeder(DemoVerificationStore store, IConfiguration configuration, IHostEnvironment environment, ILogger<DemoVerificationSeeder> logger)
{
    private static readonly string[] _fields = ["_id", "usuario", "nivel", "estado", "nivelOtorgado", "creadoEn", "enviadoEn", "decididoEn", "demo"];

    /// <summary>Solo con <c>Seed:Demo</c> encendido y fuera de Producción.</summary>
    public static bool ShouldRun(IConfiguration configuration, IHostEnvironment environment) =>
        configuration.GetValue<bool>("Seed:Demo") && !string.Equals(environment.EnvironmentName, Environments.Production, StringComparison.OrdinalIgnoreCase);

    public async Task SeedAsync(string path, CancellationToken cancellationToken)
    {
        if (!ShouldRun(configuration, environment))
        {
            throw new InvalidOperationException("Demo seed is disabled or the environment is Production.");
        }

        await store.ValidateAsync(cancellationToken);
        var input = BsonSerializer.Deserialize<BsonArray>(await File.ReadAllTextAsync(path, cancellationToken));
        if (input.Count == 0)
        {
            throw new InvalidOperationException("Invalid demo seed file.");
        }

        var ids = new HashSet<ObjectId>();
        var users = new HashSet<ObjectId>();
        var pending = new List<(BsonDocument Verification, BsonDocument Consent)>();
        foreach (var entry in input.Select(item => item.AsBsonDocument))
        {
            ValidateEntry(entry);
            if (!ids.Add(entry["_id"].AsObjectId) || !users.Add(entry["usuario"].AsObjectId))
            {
                throw new InvalidOperationException("Invalid demo seed file.");
            }

            var sub = await store.FindDemoSubAsync(entry["usuario"].AsObjectId, cancellationToken);
            if (sub is null)
            {
                LogUserMissing(logger, entry["_id"].AsObjectId.ToString());
                return;
            }

            pending.Add(Build(entry, sub));
        }

        foreach (var (verification, consent) in pending)
        {
            await store.UpsertAsync(verification, consent, cancellationToken);
        }
    }

    [LoggerMessage(EventId = 2251, Level = LogLevel.Warning, Message = "Demo verification {DemoId} skipped: its demo account is not loaded")]
    private static partial void LogUserMissing(ILogger logger, string demoId);

    private static (BsonDocument Verification, BsonDocument Consent) Build(BsonDocument entry, string sub)
    {
        var id = entry["_id"].AsObjectId;
        var created = Utc(entry["creadoEn"].AsString);
        var photos = new BsonArray(VerificationLevels.PhotosFor(entry["nivel"].AsString)
            .Select(type => new BsonDocument { ["tipo"] = type, ["estado"] = PhotoStates.Deleted }));
        var verification = new BsonDocument
        {
            ["_id"] = id,
            ["usuarioId"] = sub,
            ["nivel"] = entry["nivel"].AsString,
            ["estado"] = VerificationStates.Verified,
            ["fotos"] = photos,
            ["curpVigente"] = false,
            ["nivelOtorgado"] = entry["nivelOtorgado"].AsString,
            ["creadoEn"] = created,
            ["enviadoEn"] = Utc(entry["enviadoEn"].AsString),
            ["decididoEn"] = Utc(entry["decididoEn"].AsString),
            ["demo"] = true,
        };
        var consent = new BsonDocument
        {
            ["usuarioId"] = sub,
            ["tipo"] = "biometria",
            ["texto"] = "Consentimiento de demostración, sin datos reales.",
            ["version"] = "1.0",
            ["aceptadoEn"] = created,
            ["dispositivoId"] = "demo-sin-dispositivo",
            ["verificacionId"] = id,
            ["demo"] = true,
        };
        return (verification, consent);
    }

    private static void ValidateEntry(BsonDocument entry)
    {
        if (entry.ElementCount != _fields.Length || entry.Names.Any(name => !_fields.Contains(name, StringComparer.Ordinal)))
        {
            throw new InvalidOperationException("Invalid demo seed file.");
        }

        var level = entry["nivel"].AsString;
        if (!entry["_id"].IsObjectId || !entry["usuario"].IsObjectId || !entry["demo"].AsBoolean
            || !Regex.IsMatch(entry["usuario"].AsObjectId.ToString(), "^de0{18}[0-9a-f]{4}$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1))
            || level is not (VerificationLevels.Basic or VerificationLevels.Full)
            || entry["estado"].AsString != VerificationStates.Verified || entry["nivelOtorgado"].AsString != level)
        {
            throw new InvalidOperationException("Invalid demo seed file.");
        }

        var created = Utc(entry["creadoEn"].AsString);
        var sent = Utc(entry["enviadoEn"].AsString);
        var decided = Utc(entry["decididoEn"].AsString);
        if (created > sent || sent > decided)
        {
            throw new InvalidOperationException("Invalid demo seed file.");
        }
    }

    private static DateTime Utc(string value)
    {
        if (!value.EndsWith('Z', StringComparison.Ordinal) || !DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) || date.Offset != TimeSpan.Zero)
        {
            throw new InvalidOperationException("Invalid demo seed file.");
        }

        return date.UtcDateTime;
    }
}
