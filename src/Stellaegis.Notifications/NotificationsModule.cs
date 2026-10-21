using Stellaegis.Primitives;

namespace Stellaegis.Notifications;

/// <summary>
/// The identity of the notifications module, <c>notifications</c>: the hub of <c>Stellaegis.Notifications.SignalR</c>
/// belongs to it.
/// </summary>
public sealed class NotificationsModule : IStellaegisModule
{
    /// <inheritdoc />
    public static string Name => "notifications";
}

/// <summary>
/// The operations of the notifications module. Publishing is not one: an application publishes in process, through
/// the port, and the operation it performs is its own. Subscribing is: a subscriber reaches the hub of a transport
/// to receive the notifications of a compartment.
/// </summary>
public static class NotificationOperations
{
    /// <summary>Subscribing to the notifications of a compartment, <c>notifications.subscribe</c>.</summary>
    public static readonly Operation Subscribe = new("notifications", "subscribe", OperationKind.Read);
}
