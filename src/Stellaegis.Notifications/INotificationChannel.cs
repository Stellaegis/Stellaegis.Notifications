using Stellaegis.Primitives;

namespace Stellaegis.Notifications;

/// <summary>
/// The port an application publishes through: a notification reaches the subscribers of its recipient. The
/// in-process channel delivers to the subscribers of the process; a transport, such as the SignalR hub of
/// <c>Stellaegis.Notifications.SignalR</c>, delivers to the connections subscribed to the compartment.
/// </summary>
/// <remarks>
/// Only a type the application declared may leave (<see cref="INotificationTypeAllowList"/>): publishing another
/// fails with <see cref="NotificationTypeNotAllowedException"/>. Delivery is best effort, as the publication of an
/// event is: a subscriber that is not connected misses the notification, and the committed state stays the
/// reference.
/// </remarks>
public interface INotificationChannel
{
    /// <summary>Publishes <paramref name="notification"/> to the subscribers of its recipient.</summary>
    /// <exception cref="NotificationTypeNotAllowedException">The type is not declared as publishable.</exception>
    Task PublishAsync(Notification notification, CancellationToken cancellationToken = default);
}

/// <summary>
/// What receives the notifications published in the process, through the in-process channel: a view that
/// refreshes, a local relay. Registered with <c>AddStellaegisNotificationSubscriber</c>; every subscriber of the
/// process receives every notification, and keeps the ones of the compartments it serves.
/// </summary>
public interface INotificationSubscriber
{
    /// <summary>Receives <paramref name="notification"/>. A subscriber that throws fails the publication.</summary>
    Task ReceiveAsync(Notification notification, CancellationToken cancellationToken = default);
}

/// <summary>
/// The types an application declares as publishable. Refusal is the default: a type that was never declared
/// cannot leave the application, by construction rather than by discipline.
/// </summary>
public interface INotificationTypeAllowList
{
    /// <summary>Whether <paramref name="type"/> is declared as publishable.</summary>
    bool IsAllowed(NotificationType type);

    /// <summary>The declared types.</summary>
    IReadOnlySet<NotificationType> AllowedTypes { get; }
}

/// <summary>The immutable <see cref="INotificationTypeAllowList"/> built at startup from the declared types.</summary>
public sealed class NotificationTypeAllowList : INotificationTypeAllowList
{
    private readonly HashSet<NotificationType> _allowed;

    /// <summary>Creates the list of <paramref name="allowedTypes"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="allowedTypes"/>, or one of them, is <see langword="null"/>.</exception>
    public NotificationTypeAllowList(IEnumerable<NotificationType> allowedTypes)
        => _allowed = [.. Guard.NotNull(allowedTypes).Select(type => Guard.NotNull(type))];

    /// <inheritdoc />
    public IReadOnlySet<NotificationType> AllowedTypes => _allowed;

    /// <inheritdoc />
    public bool IsAllowed(NotificationType type) => _allowed.Contains(Guard.NotNull(type));
}

/// <summary>
/// Raised when a notification of a type the application did not declare is published: a programming error of
/// the application, answered with the code <see cref="Code"/>.
/// </summary>
public sealed class NotificationTypeNotAllowedException : UserFacingException
{
    /// <summary>The code of the error, <c>NotificationType.NotAllowed</c>.</summary>
    public const string Code = "NotificationType.NotAllowed";

    /// <summary>Creates the exception for <paramref name="type"/>.</summary>
    public NotificationTypeNotAllowedException(NotificationType type)
        : base(Error.Validation(Code,
            $"The notification type {Guard.NotNull(type)} is not declared as publishable. Declare it with "
            + $"AddStellaegisNotifications(types => types.Allow(\"{type}\"))."))
    {
        NotificationType = type;
    }

    /// <summary>The rejected type.</summary>
    public NotificationType NotificationType { get; }
}
