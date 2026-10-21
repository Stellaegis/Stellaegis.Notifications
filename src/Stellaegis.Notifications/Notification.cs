using System.Text.Json;
using Stellaegis.Primitives;

namespace Stellaegis.Notifications;

/// <summary>
/// A notification pushed to the subscribers of one compartment: its recipient, its type, its payload, when it
/// occurred, and who sent it.
/// <code>
/// await channel.PublishAsync(new Notification
/// {
///     Recipient = acme,
///     Type = InvoiceNotifications.BatchReady,
///     Payload = JsonSerializer.SerializeToElement(new { batchId }),
///     SentBy = context.Actor,
/// }, ct);
/// </code>
/// </summary>
/// <remarks>
/// A notification always names its recipient, a compartment: there is no broadcast, and the shared space receives
/// nothing — a subscriber only ever receives the notifications of the compartment it is authorized for. The sender
/// is the actor of the operation that publishes, recorded for traceability and never delivered to subscribers.
/// </remarks>
public sealed record Notification
{
    /// <summary>The compartment whose subscribers receive the notification. Never the shared space.</summary>
    public required Compartment Recipient { get; init; }

    /// <summary>What the notification is about; one of the types the application declares.</summary>
    public required NotificationType Type { get; init; }

    /// <summary>What subscribers receive, as JSON. Empty unless set.</summary>
    public JsonElement Payload { get; init; }

    /// <summary>When the fact the notification reports occurred. Now unless set.</summary>
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Who sent the notification: the actor of the operation that publishes it.</summary>
    public required Actor SentBy { get; init; }
}
