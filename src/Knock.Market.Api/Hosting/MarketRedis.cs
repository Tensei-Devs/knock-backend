namespace Knock.Market.Api.Hosting;

/// <summary>Nombre con el que Mercado se anuncia ante Redis (<c>CLIENT SETNAME</c>) al registrar el cliente compartido de <c>Knock.Hosting</c>.</summary>
internal static class MarketRedis
{
    public const string ClientName = "knock-api";
}
