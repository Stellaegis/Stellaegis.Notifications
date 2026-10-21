using Microsoft.AspNetCore.SignalR;
using Stellaegis.Primitives;

namespace Stellaegis.Notifications.SignalR;

/// <summary>
/// The <see cref="INotificationChannel"/> of a host that maps the hub: pushes each notification to the connections
/// subscribed to its recipient, as <c>Receive(type, payload, occurredAt)</c>, after checking its type against the
/// declared ones. A notification published while no connection is subscribed is lost, as delivery is best effort.
/// </summary>
public sealed class SignalRNotificationChannel(IHubContext<NotificationsHub> hub, INotificationTypeAllowList allowList)
    : INotificationChannel
{
    private readonly IHubContext<NotificationsHub> _hub = Guard.NotNull(hub);
    private readonly INotificationTypeAllowList _allowList = Guard.NotNull(allowList);

    /// <inheritdoc />
    public Task PublishAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(notification);
        if (!_allowList.IsAllowed(notification.Type))
            throw new NotificationTypeNotAllowedException(notification.Type);

        return _hub.Clients
            .Group(NotificationsHub.GroupOf(notification.Recipient))
            .SendAsync(NotificationsHub.ReceiveMethod, notification.Type.Identity, notification.Payload, notification.OccurredAt, cancellationToken);
    }
}
