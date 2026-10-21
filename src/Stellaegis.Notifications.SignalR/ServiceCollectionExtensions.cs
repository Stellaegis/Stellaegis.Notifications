using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Stellaegis.Primitives;
using Stellaegis.Primitives.AspNetCore;

namespace Stellaegis.Notifications.SignalR;

/// <summary>
/// Dependency injection helpers for the notifications hub.
/// </summary>
/// <remarks>
/// The hub lives in the application's host, under the authentication the host declares: this package registers no
/// authentication scheme of its own, so that a single-process application and a dedicated host map it the same
/// way. SignalR reads a bearer token from the <c>Authorization</c> header; a WebSocket connection cannot send one,
/// and a JWT host lets its scheme read <c>access_token</c> from the query string on the hub's path — the README
/// shows the lines.
/// </remarks>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the hub, SignalR, the application's <typeparamref name="TPolicy"/> that authorizes each
    /// subscription, the startup check of the hub's route, and the <see cref="SignalRNotificationChannel"/> as the
    /// <see cref="INotificationChannel"/> of the host, in place of the in-process one. Call it beside
    /// <c>AddStellaegisNotifications</c>, which declares the module and the types.
    /// </summary>
    /// <example>
    /// <code>
    /// builder.Services.AddStellaegisNotifications(types => types.Allow("invoices.batch_ready"));
    /// builder.Services.AddStellaegisNotificationsHub&lt;MembershipPolicy&gt;();
    /// </code>
    /// </example>
    public static IServiceCollection AddStellaegisNotificationsHub<TPolicy>(this IServiceCollection services)
        where TPolicy : class, INotificationSubscriptionPolicy
    {
        Guard.NotNull(services);
        services.AddSignalR();
        services.AddStellaegisStartupCheck();
        services.TryAddSingleton<INotificationSubscriptionPolicy, TPolicy>();
        services.Replace(ServiceDescriptor.Singleton<INotificationChannel, SignalRNotificationChannel>());
        return services;
    }

    /// <summary>
    /// Maps the hub at <paramref name="path"/>, <c>/notifications</c> unless given, and none secured: the host
    /// attaches its authorization to the returned builder, and the startup check fails a host that declares none.
    /// The hub acts on spaces — the compartment is the object of a subscription — and loads no resource.
    /// </summary>
    /// <example>
    /// <code>
    /// app.MapNotificationsHub().RequireAuthorization();
    /// </code>
    /// </example>
    public static HubEndpointConventionBuilder MapNotificationsHub(this IEndpointRouteBuilder endpoints, string path = "/notifications")
    {
        Guard.NotNull(endpoints);
        Guard.NotWhiteSpace(path);
        return endpoints.MapHub<NotificationsHub>(path).ActsOnSpaces().LoadsNoResource();
    }
}
