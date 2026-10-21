using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Stellaegis.Notifications.SignalR;
using Stellaegis.Primitives;
using Stellaegis.Primitives.AspNetCore;
using Stellaegis.Primitives.Hosting;

namespace Stellaegis.Notifications.Tests;

/// <summary>The hub in the application's host, under its authentication, with the application's policy deciding each subscription.</summary>
public class HubTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly Compartment Acme = Compartment.Parse("acme");
    private static readonly Compartment Globex = Compartment.Parse("globex");

    /// <summary>The application's rule: an authenticated user is a member of acme, and of nothing else.</summary>
    private sealed class MemberOfAcme : INotificationSubscriptionPolicy
    {
        public Task<bool> CanSubscribeAsync(ClaimsPrincipal user, Compartment compartment, CancellationToken cancellationToken = default)
            => Task.FromResult(user.Identity?.IsAuthenticated == true && compartment == Acme);
    }

    private static Task<WebApplication> StartHostAsync(Action<WebApplication>? map = null) => TestApp.StartAsync(
        services => services
            .AddStellaegisFrame(ApplicationFrame.ManyCompartments())
            .AddStellaegisNotifications(types => types.Allow(InProcessTests.BatchReady))
            .AddStellaegisNotificationsHub<MemberOfAcme>(),
        map ?? (app => app.MapNotificationsHub().RequireAuthorization()));

    private static HubConnection Connect(WebApplication app, params (string Type, string Value)[] claims)
        => new HubConnectionBuilder()
            .WithUrl("http://localhost/notifications", options =>
            {
                options.HttpMessageHandlerFactory = _ => app.GetTestServer().CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                foreach (var (type, value) in claims)
                    options.Headers.Add(TestApp.ClaimHeaderPrefix + type, value);
            })
            .Build();

    [Fact]
    public async Task A_member_subscribes_to_its_compartment_and_receives_what_is_published_to_it()
    {
        await using var app = await StartHostAsync();
        await using var connection = Connect(app, ("sub", "jane"));
        var received = new TaskCompletionSource<(string Type, JsonElement Payload, DateTimeOffset OccurredAt)>();
        connection.On<string, JsonElement, DateTimeOffset>(NotificationsHub.ReceiveMethod, (type, payload, at) => received.TrySetResult((type, payload, at)));
        await connection.StartAsync(Ct);

        await connection.InvokeAsync("Subscribe", "acme", Ct);
        var channel = app.Services.GetRequiredService<INotificationChannel>();
        Assert.IsType<SignalRNotificationChannel>(channel);
        var notification = InProcessTests.Sample();
        await channel.PublishAsync(InProcessTests.Sample(recipient: Globex), Ct);   // not for acme: never received
        await channel.PublishAsync(notification, Ct);

        var (type, payload, occurredAt) = await received.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        Assert.Equal("invoices.batch_ready", type);
        Assert.Equal(7, payload.GetProperty("batch").GetInt32());
        Assert.Equal(notification.OccurredAt, occurredAt);
    }

    [Fact]
    public async Task The_policy_refuses_a_compartment_the_user_is_not_a_member_of()
    {
        await using var app = await StartHostAsync();
        await using var connection = Connect(app, ("sub", "jane"));
        await connection.StartAsync(Ct);

        var exception = await Assert.ThrowsAsync<HubException>(() => connection.InvokeAsync("Subscribe", "globex", Ct));
        Assert.Contains("Not authorized to receive the notifications of globex", exception.Message);

        var invalid = await Assert.ThrowsAsync<HubException>(() => connection.InvokeAsync("Subscribe", "Acme", Ct));
        Assert.Contains("is not a compartment", invalid.Message);
    }

    [Fact]
    public async Task An_anonymous_connection_is_refused_by_the_host()
    {
        await using var app = await StartHostAsync();
        await using var connection = Connect(app);

        await Assert.ThrowsAnyAsync<Exception>(() => connection.StartAsync(Ct));
    }

    [Fact]
    public async Task A_hub_mapped_without_authorization_fails_the_startup()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => StartHostAsync(app => app.MapNotificationsHub()));

        Assert.Contains("notifications.subscribe", exception.Message);
        Assert.Contains("declare no authorization", exception.Message);
    }

    [Fact]
    public async Task A_hub_in_the_shared_space_fails_the_startup()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => TestApp.StartAsync(
            services => services
                .AddStellaegisFrame(ApplicationFrame.ManyCompartments().WithSharedSpace())
                .AddStellaegisNotifications(types => types.Allow(InProcessTests.BatchReady))
                .AddStellaegisNotificationsHub<MemberOfAcme>(),
            app => app.MapNotificationsHub().RequireAuthorization().InSharedSpace()));

        // The hub declares no request context: the shared space, a place, has nothing to give a route acting on spaces.
        Assert.Contains("in the shared space, and nothing declares they accept it", exception.Message);
        Assert.Contains("(no request context declared)", exception.Message);
    }

    [Fact]
    public async Task The_hub_channel_refuses_an_undeclared_type_too()
    {
        await using var app = await StartHostAsync();

        await Assert.ThrowsAsync<NotificationTypeNotAllowedException>(() =>
            app.Services.GetRequiredService<INotificationChannel>().PublishAsync(InProcessTests.Sample(new NotificationType("invoices", "deleted")), Ct));
    }
}
