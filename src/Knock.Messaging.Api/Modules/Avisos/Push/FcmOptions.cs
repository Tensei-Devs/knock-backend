using System.Text.Json;

namespace Knock.Messaging.Api.Modules.Avisos.Push;

/// <summary>Configuración de FCM: proyecto Firebase y cuenta de servicio (secreto <c>fcm-service-account</c> de Key Vault, JSON). Solo se exige con <c>Notificaciones:PushHabilitado=true</c> y falla cerrado.</summary>
internal sealed record FcmOptions(string ProjectId, string ClientEmail, string PrivateKeyPem, Uri TokenUri)
{
    public const string ProjectIdKey = "Notificaciones:Fcm:ProjectId";
    public const string ServiceAccountKey = "Notificaciones:Fcm:ServiceAccount";
    private static readonly string _defaultTokenUri = new UriBuilder(Uri.UriSchemeHttps, "oauth2.googleapis.com", -1, "/token").Uri.ToString();

    /// <summary>Lee y valida la configuración; cualquier dato faltante o mal formado impide arrancar con el envío encendido (sin revelar el contenido del secreto).</summary>
    public static FcmOptions Read(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var projectId = configuration[ProjectIdKey];
        if (string.IsNullOrWhiteSpace(projectId))
        {
            throw new InvalidOperationException($"{ProjectIdKey} is required when {PushOptions.EnabledKey} is true.");
        }

        var raw = configuration[ServiceAccountKey];
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new InvalidOperationException($"{ServiceAccountKey} is required when {PushOptions.EnabledKey} is true.");
        }

        try
        {
            using var document = JsonDocument.Parse(raw);
            var root = document.RootElement;
            var tokenUri = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("token_uri", out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()!
                : _defaultTokenUri;
            if (!Uri.TryCreate(tokenUri, UriKind.Absolute, out var parsed) || parsed.Scheme != Uri.UriSchemeHttps)
            {
                throw new InvalidOperationException("token_uri must be an absolute https URL.");
            }

            return new FcmOptions(projectId.Trim(), ReadString(root, "client_email"), ReadString(root, "private_key"), parsed);
        }
        catch (JsonException)
        {
            throw new InvalidOperationException($"{ServiceAccountKey} is not valid JSON.");
        }
    }

    private static string ReadString(JsonElement root, string name)
    {
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(value.GetString()))
        {
            return value.GetString()!;
        }

        throw new InvalidOperationException($"{ServiceAccountKey} lacks {name}.");
    }
}
