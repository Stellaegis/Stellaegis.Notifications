using Stellaegis.Primitives;

namespace Stellaegis.Notifications;

/// <summary>
/// The in-process <see cref="INotificationChannel"/>: delivers each notification to every
/// <see cref="INotificationSubscriber"/> of the process, in registration order, after checking its type against the
/// declared ones. The channel of a single-process application, of a desktop application, and of tests.
/// </summary>
/// <remarks>
/// Delivery is sequential and awaited: a subscriber that throws fails the publication, and the subscribers after it
/// are not reached. A subscriber that must not fail the publisher catches its own failures.
/// </remarks>
public sealed class InMemoryNotificationChannel(INotificationTypeAllowList allowList, IEnumerable<INotificationSubscriber> subscribers)
    : INotificationChannel
{
    private readonly INotificationTypeAllowList _allowList = Guard.NotNull(allowList);
    private readonly IReadOnlyList<INotificationSubscriber> _subscribers = [.. Guard.NotNull(subscribers)];

    /// <inheritdoc />
    public async Task PublishAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(notification);
        if (!_allowList.IsAllowed(notification.Type))
            throw new NotificationTypeNotAllowedException(notification.Type);

        foreach (var subscriber in _subscribers)
            await subscriber.ReceiveAsync(notification, cancellationToken).ConfigureAwait(false);
    }
}
