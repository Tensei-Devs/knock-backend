using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using MongoDB.Bson;
using Xunit;

namespace Knock.IntegrationTest.Requests;

/// <summary>Spec 020, T-06: listado propio con paginación por cursor (MongoDB real).</summary>
public sealed class Spec020ListadoTests
{
    private static async Task SeedRequestsAsync(Spec020Host host, string client, int count, string state = "enviada", int minuteOffset = 0)
    {
        var start = DateTime.UtcNow.AddHours(-3);
        var documents = Enumerable.Range(0, count).Select(i => new BsonDocument
        {
            { "_id", ObjectId.GenerateNewId() },
            { "clienteId", client },
            { "oficioClave", Spec020Host.Plumbing },
            { "oficioNombre", "Plomería" },
            { "modalidad", "domicilio" },
            { "descripcion", Spec020Host.Description },
            { "fotos", new BsonArray { Guid.CreateVersion7().ToString("N") } },
            { "dia", Spec020Host.Tomorrow() },
            { "franja", "tarde" },
            { "coloniaId", host.ColoniaId.ToString() },
            { "coloniaNombre", "Centro" },
            { "coloniaCp", "42800" },
            { "estado", state },
            { "venceEn", DateTime.UtcNow.AddHours(48) },
            { "creadoEn", start.AddMinutes(minuteOffset + i) },
            { "actualizadoEn", start.AddMinutes(minuteOffset + i) },
        });
        await host.Database.GetCollection<BsonDocument>("solicitudes").InsertManyAsync(documents);
    }

    private static async Task<(JsonElement Page, string[] Ids)> PageAsync(Spec020Host host, string query)
    {
        using var response = await host.SendAsync(HttpMethod.Get, "/api/v1/solicitudes" + query, host.ClientSub);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await Spec020Host.ReadAsync(response);
        return (page, [.. page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetString()!)]);
    }

    [Fact]
    public async Task Pagina_Solicitudes_Propias_CA_020_14()
    {
        await using var host = await Spec020Host.StartAsync();
        await SeedRequestsAsync(host, host.ClientSub, 25);
        await SeedRequestsAsync(host, Guid.NewGuid().ToString("D"), 3);

        var (first, firstIds) = await PageAsync(host, string.Empty);
        var (second, secondIds) = await PageAsync(host, "?cursor=" + Uri.EscapeDataString(first.GetProperty("nextCursor").GetString()!));
        var (all, allIds) = await PageAsync(host, "?limit=50");

        firstIds.Should().HaveCount(20);
        first.GetProperty("nextCursor").GetString().Should().NotBeNullOrEmpty();
        secondIds.Should().HaveCount(5);
        second.GetProperty("nextCursor").ValueKind.Should().Be(JsonValueKind.Null, "la última página no trae cursor");
        allIds.Should().HaveCount(25, "solo las del sub del token");
        all.GetProperty("nextCursor").ValueKind.Should().Be(JsonValueKind.Null);
        firstIds.Concat(secondIds).Should().Equal(allIds, "las dos páginas siguen el mismo orden");
        var created = all.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("creadoEn").GetDateTimeOffset()).ToList();
        created.Should().BeInDescendingOrder("de la más reciente a la más antigua");
        var row = all.GetProperty("items")[0];
        row.TryGetProperty("descripcion", out _).Should().BeFalse("el resumen no lleva descripción");
        row.TryGetProperty("direccion", out _).Should().BeFalse();
        row.GetProperty("totalFotos").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Filtra_Por_Estado_R_09()
    {
        await using var host = await Spec020Host.StartAsync();
        await SeedRequestsAsync(host, host.ClientSub, 3, "enviada");
        await SeedRequestsAsync(host, host.ClientSub, 2, "cotizando", minuteOffset: 100);

        var (_, ids) = await PageAsync(host, "?estado=cotizando");

        ids.Should().HaveCount(2);
    }

    [Fact]
    public async Task Cursor_Y_Limite_Invalidos_CA_020_21()
    {
        await using var host = await Spec020Host.StartAsync();
        await SeedRequestsAsync(host, host.ClientSub, 3);

        using var altered = await host.SendAsync(HttpMethod.Get, "/api/v1/solicitudes?cursor=abc", host.ClientSub);
        using var zero = await host.SendAsync(HttpMethod.Get, "/api/v1/solicitudes?limit=0", host.ClientSub);
        using var tooMany = await host.SendAsync(HttpMethod.Get, "/api/v1/solicitudes?limit=51", host.ClientSub);
        using var badState = await host.SendAsync(HttpMethod.Get, "/api/v1/solicitudes?estado=inventado", host.ClientSub);
        var oldCursor = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(System.Text.Encoding.UTF8.GetBytes($"1700000000000:{ObjectId.GenerateNewId()}"));
        using var oldFormat = await host.SendAsync(HttpMethod.Get, "/api/v1/solicitudes?cursor=" + oldCursor, host.ClientSub);

        altered.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Spec020Host.ReadAsync(altered)).GetProperty("code").GetString().Should().Be("invalid_cursor");
        zero.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await Spec020Host.ReadAsync(zero)).GetProperty("errors").GetProperty("limit").GetArrayLength().Should().BeGreaterThan(0);
        tooMany.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        badState.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        oldFormat.StatusCode.Should().Be(HttpStatusCode.BadRequest, "el cursor de dos datos ya no se acepta (CA-24)");
        (await Spec020Host.ReadAsync(oldFormat)).GetProperty("code").GetString().Should().Be("invalid_cursor");
    }

    [Fact]
    public async Task Activas_Primero_Y_Cursor_Estable_CA_020_24()
    {
        await using var host = await Spec020Host.StartAsync();
        // Las terminadas y canceladas son más recientes que las activas.
        await SeedRequestsAsync(host, host.ClientSub, 3, "enviada", minuteOffset: 0);
        await SeedRequestsAsync(host, host.ClientSub, 2, "cotizando", minuteOffset: 10);
        await SeedRequestsAsync(host, host.ClientSub, 1, "cita_confirmada", minuteOffset: 20);
        await SeedRequestsAsync(host, host.ClientSub, 3, "terminada", minuteOffset: 30);
        await SeedRequestsAsync(host, host.ClientSub, 2, "cancelada", minuteOffset: 40);
        await SeedRequestsAsync(host, Guid.NewGuid().ToString("D"), 2, "enviada", minuteOffset: 50);

        var (all, allIds) = await PageAsync(host, "?limit=50");
        var states = all.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("estado").GetString()!).ToList();
        var active = new[] { "enviada", "cotizando", "cita_confirmada" };

        allIds.Should().HaveCount(11, "solo las del sub del token");
        states.Take(6).Should().OnlyContain(state => active.Contains(state));
        states.Skip(6).Should().OnlyContain(state => !active.Contains(state));
        all.GetProperty("items").EnumerateArray().Take(6).Select(i => i.GetProperty("creadoEn").GetDateTimeOffset()).Should().BeInDescendingOrder();
        all.GetProperty("items").EnumerateArray().Skip(6).Select(i => i.GetProperty("creadoEn").GetDateTimeOffset()).Should().BeInDescendingOrder();

        // Páginas de 4: activas (4), activas y terminadas (2 + 2), terminadas (3), sin repetir ni omitir.
        var collected = new List<string>();
        string query = "?limit=4";
        for (var guard = 0; guard < 5; guard++)
        {
            var (page, ids) = await PageAsync(host, query);
            collected.AddRange(ids);
            var next = page.GetProperty("nextCursor");
            if (next.ValueKind == JsonValueKind.Null)
            {
                break;
            }

            query = "?limit=4&cursor=" + Uri.EscapeDataString(next.GetString()!);
        }

        collected.Should().Equal(allIds, "las páginas siguen el mismo orden sin repetir ni omitir");

        var (_, finished) = await PageAsync(host, "?estado=terminada");
        finished.Should().HaveCount(3, "con filtro rige el orden de CA-14 dentro de ese estado");
    }
}
