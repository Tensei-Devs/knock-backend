extern alias messaging;

using System.Reflection;
using AwesomeAssertions;
using Microsoft.AspNetCore.Mvc;
using Xunit;
using Controllers = messaging::Knock.Messaging.Api.Modules.Notices.Controllers.V1;
using Domain = messaging::Knock.Messaging.Api.Modules.Notices.Domain;
using Dtos = messaging::Knock.Messaging.Api.Modules.Notices.Dtos;

namespace Knock.UnitTest.Messaging;

/// <summary>Spec 044, T-07: reglas de negocio (R-10 a R-14), criterios CA-16 a CA-20. CA-17, CA-18 y CA-19 también los cubren las pruebas de sus rutas.</summary>
public sealed partial class Spec044AvisosTests
{
    [Fact]
    public async Task List_FiltersByRole_PagedAndNewestFirst_CA_044_16()
    {
        var store = StoreWith(30, others: 3);

        var first = ListOf(await Avisos(store).List(new Dtos.ListNoticesQuery { ComoRol = "cliente", Limite = "10" }, CancellationToken.None));
        var technician = ListOf(await Avisos(store).List(new Dtos.ListNoticesQuery { ComoRol = "tecnico" }, CancellationToken.None));

        first.Items.Should().HaveCount(10).And.OnlyContain(n => n.ComoRol == "cliente");
        first.Items.Select(n => n.CreadoEn).Should().BeInDescendingOrder();
        first.SiguienteCursor.Should().NotBeNullOrEmpty();
        technician.Items.Should().HaveCount(15).And.OnlyContain(n => n.ComoRol == "tecnico");
    }

    [Fact]
    public async Task List_FiltersByReadState_AndWithoutTheFilterReturnsReadAndUnread_CA_044_16()
    {
        var store = StoreWith(4);
        await Avisos(store).MarkRead(new Dtos.MarkNoticeReadRequest { Id = Id(1) }, CancellationToken.None);

        var unread = ListOf(await Avisos(store).List(new Dtos.ListNoticesQuery { Leido = "false" }, CancellationToken.None));
        var read = ListOf(await Avisos(store).List(new Dtos.ListNoticesQuery { Leido = "true" }, CancellationToken.None));
        var all = ListOf(await Avisos(store).List(new Dtos.ListNoticesQuery(), CancellationToken.None));

        unread.Items.Should().HaveCount(3);
        read.Items.Should().ContainSingle().Which.Id.Should().Be(Id(1));
        all.Items.Should().HaveCount(4);
    }

    [Fact]
    public void Notice_ExpiresThirtyDaysAfterItWasCreated_CA_044_20()
    {
        var createdAt = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);

        Domain.NoticeRules.ExpiresAt(createdAt).Should().Be(new DateTime(2026, 12, 1, 12, 0, 0, DateTimeKind.Utc));
        Domain.NoticeRules.RetentionDays.Should().Be(30);
    }

    [Fact]
    public void NoRouteOfTheModuleCreatesNoticesOrPreferences_CA_044_20()
    {
        var controllers = new[] { typeof(Controllers.AvisosController), typeof(Controllers.MePreferenciasAvisosController) };

        var actions = controllers.SelectMany(c => c.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)).ToList();

        actions.Should().NotBeEmpty();
        actions.Where(m => m.GetCustomAttributes<HttpPutAttribute>().Any() || m.GetCustomAttributes<HttpDeleteAttribute>().Any() || m.GetCustomAttributes<HttpPatchAttribute>().Any())
            .Should().BeEmpty("el cambio de preferencias lo define la spec 037 y los avisos solo los crea su despachador");
        actions.Where(m => m.GetCustomAttributes<HttpPostAttribute>().Any()).Select(m => m.Name).Should().HaveCount(2).And.Contain("MarkRead").And.Contain("MarkAllRead");
    }
}
