using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Stellaegis.Primitives;
using Stellaegis.Primitives.Hosting;

namespace Stellaegis.Notifications;

/// <summary>Declares the types an application publishes.</summary>
public sealed class NotificationTypesBuilder
{
    private readonly HashSet<NotificationType> _types = [];

    /// <summary>Declares <paramref name="type"/> as publishable.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="type"/> is <see langword="null"/>.</exception>
    public NotificationTypesBuilder Allow(NotificationType type)
    {
        _types.Add(Guard.NotNull(type));
        return this;
    }

    /// <summary>Declares <paramref name="type"/> as publishable, written <c>target.name</c>.</summary>
    /// <exception cref="FormatException"><paramref name="type"/> is not a valid notification type.</exception>
    public NotificationTypesBuilder Allow(string type) => Allow(NotificationType.Parse(Guard.NotNull(type)));

    internal NotificationTypeAllowList Build() => new(_types);
}

/// <summary>
/// Dependency injection helpers for Stellaegis.Notifications.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the notifications module: the types the application publishes, declared by
    /// <paramref name="types"/> and refused otherwise, and the in-process <see cref="INotificationChannel"/>,
    /// which a transport replaces. Declares <see cref="NotificationsModule"/> to the application, as a module
    /// with compartments — a notification is addressed to one —, compatible with an isolation by owner since it
    /// holds no data of an actor, with its operation <see cref="NotificationOperations.Subscribe"/>.
    /// </summary>
    /// <remarks>A host calls it once: declaring the module twice fails the startup.</remarks>
    /// <example>
    /// <code>
    /// builder.Services.AddStellaegisNotifications(types => types
    ///     .Allow("invoices.batch_ready")
    ///     .Allow("documents.ready"));
    /// </code>
    /// </example>
    public static IServiceCollection AddStellaegisNotifications(this IServiceCollection services, Action<NotificationTypesBuilder> types)
    {
        Guard.NotNull(services);
        Guard.NotNull(types);

        var builder = new NotificationTypesBuilder();
        types(builder);

        services.AddStellaegisModule(ModuleDeclaration.Compartmented<NotificationsModule>()
            .WithOperations(NotificationOperations.Subscribe)
            .WithCapability(ModuleCapability.OwnerIsolation));
        services.AddSingleton<INotificationTypeAllowList>(builder.Build());
        services.TryAddSingleton<INotificationChannel, InMemoryNotificationChannel>();
        return services;
    }

    /// <summary>
    /// Registers <typeparamref name="TSubscriber"/> as a subscriber of the process, receiving every notification
    /// the in-process channel delivers.
    /// </summary>
    public static IServiceCollection AddStellaegisNotificationSubscriber<TSubscriber>(this IServiceCollection services)
        where TSubscriber : class, INotificationSubscriber
    {
        Guard.NotNull(services);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<INotificationSubscriber, TSubscriber>());
        return services;
    }
}
