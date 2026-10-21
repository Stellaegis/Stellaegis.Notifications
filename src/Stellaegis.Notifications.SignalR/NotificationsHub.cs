using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using Stellaegis.Primitives;
using Stellaegis.Primitives.AspNetCore;

namespace Stellaegis.Notifications.SignalR;

/// <summary>
/// Decides whether a connection may receive the notifications of a compartment. The isolation of the channel:
/// a connection joins the group of a compartment only after the application authorized it here, server-side,
/// never on the compartment the client names. Refusal is the default — there is no built-in policy, and the
/// hub cannot be registered without one.
/// <code>
/// sealed class MembershipPolicy(IMemberships memberships) : INotificationSubscriptionPolicy
/// {
///     public Task&lt;bool&gt; CanSubscribeAsync(ClaimsPrincipal user, Compartment compartment, CancellationToken ct)
///         =&gt; memberships.IsMemberAsync(user.FindFirst("sub")!.Value, compartment, ct);
/// }
/// </code>
/// </summary>
/// <remarks>
/// A claim naming the compartment is not a policy: a compact token carries no membership, and the family puts
/// membership in the application's rights, never in the token.
/// </remarks>
public interface INotificationSubscriptionPolicy
{
    /// <summary>Whether <paramref name="user"/> may receive the notifications of <paramref name="compartment"/>.</summary>
    Task<bool> CanSubscribeAsync(ClaimsPrincipal user, Compartment compartment, CancellationToken cancellationToken = default);
}

/// <summary>
/// The hub subscribers connect to, mapped in the application's host by <c>MapNotificationsHub</c>, under the
/// authentication and the authorization the host declares. A connection subscribes to one compartment,
/// <see cref="Subscribe"/>, which the application's <see cref="INotificationSubscriptionPolicy"/> authorizes; it
/// then receives, as <c>Receive(type, payload, occurredAt)</c>, every notification published to that compartment.
/// </summary>
/// <remarks>
/// The hub acts on spaces: the compartment is the object of the subscription, named by the client and verified by
/// the policy, never a context. It belongs to <see cref="NotificationsModule"/> and performs
/// <see cref="NotificationOperations.Subscribe"/>, which the startup check reads as on any route of a module.
/// </remarks>
[BelongsTo<NotificationsModule>]
[Operation(typeof(NotificationOperations), nameof(NotificationOperations.Subscribe))]
public sealed class NotificationsHub(INotificationSubscriptionPolicy policy) : Hub
{
    /// <summary>The method a subscriber receives notifications on.</summary>
    public const string ReceiveMethod = "Receive";

    /// <summary>
    /// Subscribes the connection to the notifications of <paramref name="compartment"/>, once the application's
    /// policy authorized it.
    /// </summary>
    /// <exception cref="HubException">The value is not a compartment, or the policy refuses.</exception>
    public async Task Subscribe(string compartment)
    {
        if (!Compartment.TryParse(compartment, out var recipient))
            throw new HubException($"'{compartment}' is not a compartment.");

        var user = Context.User ?? new ClaimsPrincipal(new ClaimsIdentity());
        if (!await policy.CanSubscribeAsync(user, recipient, Context.ConnectionAborted).ConfigureAwait(false))
            throw new HubException($"Not authorized to receive the notifications of {recipient}.");

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupOf(recipient), Context.ConnectionAborted).ConfigureAwait(false);
    }

    /// <summary>Unsubscribes the connection from the notifications of <paramref name="compartment"/>.</summary>
    public Task Unsubscribe(string compartment)
        => Compartment.TryParse(compartment, out var recipient)
            ? Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupOf(recipient), Context.ConnectionAborted)
            : Task.CompletedTask;

    /// <summary>The SignalR group of a compartment's subscribers.</summary>
    internal static string GroupOf(Compartment compartment) => $"compartment:{compartment.Value}";
}
