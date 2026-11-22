using AwesomeAssertions;
using Knock.Market.Api.Modules.Media.Data;
using Knock.Market.Api.Modules.Media.Domain;
using Xunit;

namespace Knock.UnitTest.Media;

/// <summary>Spec 058 (T-06): el tope de 10 pendientes nunca se supera con peticiones simultáneas, repetidas varias veces para destapar la carrera (R-10, CA-08, CA-09).</summary>
public sealed class Spec058TopePendientesTests
{
    private const int _cap = 10;
    private const int _rounds = 30;
    private static readonly DateTimeOffset _now = new(2026, 11, 21, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Veinticinco_Subidas_Simultaneas_No_Superan_El_Tope_CA_058_08()
    {
        for (var round = 0; round < _rounds; round++)
        {
            var store = new InMemoryMediaFileStore();

            var added = await Burst(store, 25);

            added.Count(result => result.Added).Should().Be(_cap, $"round {round}: never 11");
            added.Count(result => !result.Added).Should().Be(15);
            added.Where(result => !result.Added).Should().OnlyContain(result => result.RetryAt != null);
        }
    }

    [Fact]
    public async Task Ocho_Mas_Cinco_Simultaneas_No_Superan_El_Tope_CA_058_09()
    {
        for (var round = 0; round < _rounds; round++)
        {
            var store = new InMemoryMediaFileStore();
            await Burst(store, 8);

            var added = await Burst(store, 5);

            added.Count(result => result.Added).Should().Be(2, $"round {round}: 8 + 2 = 10, never more");
            added.Count(result => !result.Added).Should().Be(3);
        }
    }

    private static Task<PendingAddResult[]> Burst(InMemoryMediaFileStore store, int count) =>
        Task.WhenAll(Enumerable.Range(0, count).Select(_ => Task.Run(() =>
            store.AddPendingWithinLimitAsync(MediaFile.CreatePending("owner-a", MediaUsage.Request, "image/jpeg", 100, _now), _cap, _now, CancellationToken.None))));
}
