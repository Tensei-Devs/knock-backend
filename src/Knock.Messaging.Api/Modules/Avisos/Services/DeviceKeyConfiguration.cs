namespace Knock.Messaging.Api.Modules.Avisos.Services;

internal sealed class DeviceKeyConfiguration(TokenProtection? protection, bool writesEnabled = false)
{
    public TokenProtection? Protection { get; } = protection;

    // No local setting is evidence that every replica uses the same HMAC key.
    public bool WritesEnabled => writesEnabled && Protection is not null;

    public static DeviceKeyConfiguration Read(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        try
        {
            var section = configuration.GetSection("Avisos:Devices");
            var version = section["ActiveEncryptionVersion"];
            var indexKey = section["IndexKey"];
            if (string.IsNullOrWhiteSpace(version) || string.IsNullOrWhiteSpace(indexKey))
            {
                return new DeviceKeyConfiguration(null);
            }

            var keys = section.GetSection("EncryptionKeys").GetChildren()
                .ToDictionary(entry => entry.Key, entry => Convert.FromBase64String(entry.Value ?? ""), StringComparer.Ordinal);
            var enabled = bool.TryParse(section["WritesEnabled"], out var value) && value;
            return new DeviceKeyConfiguration(new TokenProtection(version, keys, Convert.FromBase64String(indexKey)), enabled);
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException)
        {
            return new DeviceKeyConfiguration(null);
        }
    }
}
