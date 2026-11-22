extern alias messaging;

using System.Security.Claims;
using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Controllers = messaging::Knock.Messaging.Api.Modules.Notices.Controllers.V1;
using Data = messaging::Knock.Messaging.Api.Modules.Notices.Data;
using Domain = messaging::Knock.Messaging.Api.Modules.Notices.Domain;
using Dtos = messaging::Knock.Messaging.Api.Modules.Notices.Dtos;
using Services = messaging::Knock.Messaging.Api.Modules.Notices.Services;

namespace Knock.UnitTest.Messaging;

/// <summary>
/// Spec 044 (avisos dentro de la app): validadores, reglas puras, servicio y controladores con un almacén de mentira. Lo que necesita MongoDB, el JWT real o el limitador corre en
/// integración y en CI (<c>tests/IntegrationTest/Messaging</c>). Solo datos ficticios. La clase es parcial: cada tarea de la spec agrega su archivo.
/// </summary>
public sealed partial class Spec044AvisosTests
{
    private const string _me = "11111111-1111-4111-8111-111111111111";
    private const string _other = "22222222-2222-4222-8222-222222222222";

    private sealed class FakeStore : Data.INoticesStore
    {
        public List<(string User, string Id, bool Read, string Role, DateTime At)> Notices { get; } = [];

        public string? LastUser { get; private set; }

        public Task<Data.NoticePage> ListAsync(string userId, bool? read, string? role, Domain.NoticeCursor? after, int limit, CancellationToken cancellationToken)
        {
            LastUser = userId;
            var items = Notices.Where(n => n.User == userId && (read is null || n.Read == read) && (role is null || n.Role == role))
                .OrderByDescending(n => n.At).ThenByDescending(n => n.Id, StringComparer.Ordinal).ToList();
            var page = items.Take(limit).Select(n => new Data.StoredNotice(n.Id, "cotizaciones", n.Role, "Titulo", "Texto", null, n.Read, n.At)).ToList();
            Domain.NoticeCursor? next = items.Count > limit
                ? new Domain.NoticeCursor(new DateTimeOffset(page[^1].CreatedAt, TimeSpan.Zero).ToUnixTimeMilliseconds(), page[^1].Id)
                : null;
            return Task.FromResult(new Data.NoticePage(page, next));
        }

        public Task<int> CountUnreadAsync(string userId, int cap, CancellationToken cancellationToken)
        {
            LastUser = userId;
            return Task.FromResult(Math.Min(cap, Notices.Count(n => n.User == userId && !n.Read)));
        }

        public Task<bool> MarkReadAsync(string userId, string noticeId, CancellationToken cancellationToken)
        {
            LastUser = userId;
            var index = Notices.FindIndex(n => n.User == userId && n.Id == noticeId);
            if (index < 0)
            {
                return Task.FromResult(false);
            }

            Notices[index] = Notices[index] with { Read = true };
            return Task.FromResult(true);
        }
    }

    private static string Id(int n) => n.ToString("x24", System.Globalization.CultureInfo.InvariantCulture);

    private static FakeStore StoreWith(int mine, int others = 0, bool read = false)
    {
        var store = new FakeStore();
        var start = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < mine; i++)
        {
            store.Notices.Add((_me, Id(i + 1), read, i % 2 == 0 ? "cliente" : "tecnico", start.AddMinutes(i)));
        }

        for (var i = 0; i < others; i++)
        {
            store.Notices.Add((_other, Id(1000 + i), false, "cliente", start.AddMinutes(i)));
        }

        return store;
    }

    private static T Controller<T>(Func<Services.NoticesService, T> create, FakeStore store, string? sub = _me)
        where T : ControllerBase
    {
        var controller = create(new Services.NoticesService(store));
        var identity = sub is null ? new ClaimsIdentity() : new ClaimsIdentity([new Claim("sub", sub)], "test");
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } };
        return controller;
    }

    private static Controllers.AvisosController Avisos(FakeStore store, string? sub = _me) => Controller(s => new Controllers.AvisosController(s), store, sub);

    private static async Task<string> ReadAsync(Stream body)
    {
        using var reader = new StreamReader(body, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }

    private static Dtos.NoticeListResponse ListOf(IActionResult result) =>
        (Dtos.NoticeListResponse)result.Should().BeOfType<OkObjectResult>().Which.Value!;
}
