using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Stellaegis.Primitives;
using Stellaegis.Primitives.Hosting;

namespace Stellaegis.Notifications.Tests;

/// <summary>The channel of a single process: the declared types, the subscribers of the process, the module.</summary>
public class InProcessTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static readonly NotificationType BatchReady = new("invoices", "batch_ready");
    public static readonly NotificationType DocumentReady = new("documents", "ready");

    private static readonly Compartment Acme = Compartment.Parse("acme");
    private static readonly Actor Jane = Actor.Parse("jane");

    public static Notification Sample(NotificationType? type = null, Compartment? recipient = null) => new()
    {
        Recipient = recipient ?? Acme,
        Type = type ?? BatchReady,
        Payload = JsonSerializer.SerializeToElement(new { batch = 7 }),
        SentBy = Jane,
    };

    private sealed class Recorder : INotificationSubscriber
    {
        public List<Notification> Received { get; } = [];

        public Task ReceiveAsync(Notification notification, CancellationToken cancellationToken = default)
        {
            Received.Add(notification);
            return Task.CompletedTask;
        }
    }

    private sealed class Failing : INotificationSubscriber
    {
        public Task ReceiveAsync(Notification notification, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("the view is gone");
    }

    private static ServiceProvider Build(Action<IServiceCollection>? more = null)
    {
        var services = new ServiceCollection()
            .AddStellaegisFrame(ApplicationFrame.SingleCompartment(Acme))
            .AddStellaegisNotifications(types => types.Allow(BatchReady).Allow("documents.ready"));
        more?.Invoke(services);
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task A_notification_reaches_every_subscriber_of_the_process()
    {
        var first = new Recorder();
        var second = new Recorder();
        using var provider = Build(s => s.AddSingleton<INotificationSubscriber>(first).AddSingleton<INotificationSubscriber>(second));
        var channel = provider.GetRequiredService<INotificationChannel>();
        var notification = Sample();

        await channel.PublishAsync(notification, Ct);

        Assert.IsType<InMemoryNotificationChannel>(channel);
        Assert.Equal([notification], first.Received);
        Assert.Equal([notification], second.Received);
        Assert.Equal(7, notification.Payload.GetProperty("batch").GetInt32());
        Assert.Equal(Jane, notification.SentBy);
    }

    [Fact]
    public async Task A_type_the_application_did_not_declare_cannot_leave()
    {
        using var provider = Build();
        var channel = provider.GetRequiredService<INotificationChannel>();

        var exception = await Assert.ThrowsAsync<NotificationTypeNotAllowedException>(() =>
            channel.PublishAsync(Sample(new NotificationType("invoices", "deleted")), Ct));

        Assert.Equal("NotificationType.NotAllowed", exception.Error.Code);
        Assert.Equal(ErrorType.Validation, exception.Error.Type);
        Assert.Contains("invoices.deleted", exception.Message);
        Assert.Equal([DocumentReady, BatchReady], provider.GetRequiredService<INotificationTypeAllowList>().AllowedTypes.OrderBy(t => t.Identity));
    }

    [Fact]
    public async Task A_subscriber_that_throws_fails_the_publication()
    {
        using var provider = Build(s => s.AddStellaegisNotificationSubscriber<Failing>());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.GetRequiredService<INotificationChannel>().PublishAsync(Sample(), Ct));
    }

    [Fact]
    public void A_subscriber_type_is_registered_once()
    {
        using var provider = Build(s => s.AddStellaegisNotificationSubscriber<Recorder>().AddStellaegisNotificationSubscriber<Recorder>());

        Assert.Single(provider.GetServices<INotificationSubscriber>());
    }

    [Fact]
    public void The_module_is_declared_with_compartments_its_operation_and_its_compatibility()
    {
        using var provider = Build();
        provider.ValidateStellaegisModules();

        var module = provider.GetStellaegisModule<NotificationsModule>()!;
        Assert.Equal("notifications", module.Name);
        Assert.Equal(ModuleCompartments.Required, module.Compartments);
        Assert.Equal([NotificationOperations.Subscribe], module.Operations);
        Assert.Contains(ModuleCapability.OwnerIsolation, module.Capabilities);
        Assert.False(module.AcceptsSharedSpace);

        new ServiceCollection()
            .AddStellaegisFrame(ApplicationFrame.SingleCompartment(Acme).WithOwnerIsolation())
            .AddStellaegisNotifications(types => types.Allow(BatchReady))
            .BuildServiceProvider().ValidateStellaegisModules();
    }

    [Fact]
    public void An_application_without_compartments_cannot_use_the_module()
    {
        using var provider = new ServiceCollection()
            .AddStellaegisNotifications(types => types.Allow(BatchReady))
            .BuildServiceProvider();

        var exception = Assert.Throws<InvalidOperationException>(provider.ValidateStellaegisModules);
        Assert.Contains("notifications has compartments, and the application has none", exception.Message);
    }

    [Fact]
    public void The_module_is_declared_once()
    {
        using var provider = Build(s => s.AddStellaegisNotifications(types => types.Allow(BatchReady)));

        Assert.Throws<InvalidOperationException>(provider.ValidateStellaegisModules);
    }

    [Fact]
    public void A_declared_type_is_written_once()
    {
        Assert.Throws<FormatException>(() => new ServiceCollection().AddStellaegisNotifications(types => types.Allow("invoices.batch-ready")));
    }
}
