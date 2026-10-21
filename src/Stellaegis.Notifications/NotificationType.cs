using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using Stellaegis.Primitives;

namespace Stellaegis.Notifications;

/// <summary>
/// What a notification is about, <c>target.name</c> — <c>invoices.batch_ready</c>, <c>documents.ready</c>: a target
/// and a name, each following the identifier rule of the family, <see cref="NameRules.IdentifierRule"/>. The
/// application declares the types it publishes, and a subscriber recognizes a notification by its type.
/// </summary>
/// <remarks>
/// The grammar is that of an <see cref="Operation"/>'s identity, on purpose: a notification type reaches a
/// subscriber's code, a configuration key and the wire unchanged. Uppercase and hyphens are refused, never
/// converted. Two types are equal when their target and name are.
/// </remarks>
[JsonConverter(typeof(NotificationTypeJsonConverter))]
public sealed class NotificationType : IEquatable<NotificationType>, IParsable<NotificationType>
{
    /// <summary>Creates the type <paramref name="target"/>.<paramref name="name"/>.</summary>
    /// <exception cref="ArgumentException">The target or the name breaks the identifier rule.</exception>
    public NotificationType(string target, string name)
    {
        Target = Guard.Satisfies(target, NameRules.IsIdentifier, $"'{target}' is not a valid notification target: {NameRules.IdentifierRule}.");
        Name = Guard.Satisfies(name, NameRules.IsIdentifier, $"'{name}' is not a valid notification name: {NameRules.IdentifierRule}.");
    }

    /// <summary>The target the notification is about, such as <c>invoices</c>.</summary>
    public string Target { get; }

    /// <summary>What happened to it, such as <c>batch_ready</c>.</summary>
    public string Name { get; }

    /// <summary>The identity, <c>target.name</c>.</summary>
    public string Identity => $"{Target}.{Name}";

    /// <summary>Reads a type the code knows to be valid, such as a constant.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="s"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException"><paramref name="s"/> is not <c>target.name</c>, each an identifier.</exception>
    public static NotificationType Parse(string s)
    {
        ArgumentNullException.ThrowIfNull(s);
        return TryParse(s, out var type)
            ? type
            : throw new FormatException($"'{s}' is not a valid notification type: target.name, each {NameRules.IdentifierRule}.");
    }

    /// <inheritdoc cref="Parse(string)"/>
    public static NotificationType Parse(string s, IFormatProvider? provider) => Parse(s);

    /// <summary>Reads a type from an input that may be invalid, such as a value from the wire.</summary>
    public static bool TryParse([NotNullWhen(true)] string? s, [MaybeNullWhen(false)] out NotificationType result)
    {
        result = null;
        if (s is null)
            return false;
        var parts = s.Split('.');
        if (parts.Length != 2 || !NameRules.IsIdentifier(parts[0]) || !NameRules.IsIdentifier(parts[1]))
            return false;
        result = new NotificationType(parts[0], parts[1]);
        return true;
    }

    /// <inheritdoc cref="TryParse(string?, out NotificationType)"/>
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out NotificationType result)
        => TryParse(s, out result);

    /// <inheritdoc/>
    public bool Equals(NotificationType? other)
        => other is not null
           && string.Equals(Target, other.Target, StringComparison.Ordinal)
           && string.Equals(Name, other.Name, StringComparison.Ordinal);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is NotificationType t && Equals(t);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Target, Name);

    /// <summary>Value equality.</summary>
    public static bool operator ==(NotificationType? left, NotificationType? right) => left is null ? right is null : left.Equals(right);

    /// <summary>Value inequality.</summary>
    public static bool operator !=(NotificationType? left, NotificationType? right) => !(left == right);

    /// <summary>The identity, <c>target.name</c>.</summary>
    public override string ToString() => Identity;
}

/// <summary>Reads and writes a <see cref="NotificationType"/> as a JSON string. Applied by the attribute on the type.</summary>
public sealed class NotificationTypeJsonConverter : JsonConverter<NotificationType>
{
    /// <inheritdoc/>
    public override NotificationType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException($"A notification type is a JSON string, not {reader.TokenType}.");
        var value = reader.GetString();
        return NotificationType.TryParse(value, out var type) ? type : throw new JsonException($"'{value}' is not a valid notification type.");
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, NotificationType value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Identity);
}
