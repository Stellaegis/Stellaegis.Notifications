using System.Text.Json;

namespace Stellaegis.Notifications.Tests;

public class NotificationTypeTests
{
    [Fact]
    public void A_type_is_a_target_and_a_name_under_the_identifier_rule()
    {
        var type = new NotificationType("invoices", "batch_ready");

        Assert.Equal("invoices.batch_ready", type.Identity);
        Assert.Equal(type, NotificationType.Parse("invoices.batch_ready"));
        Assert.True(type == new NotificationType("invoices", "batch_ready"));
        Assert.True(NotificationType.TryParse("documents.ready", out _));
    }

    [Theory]
    [InlineData("invoices")]
    [InlineData("invoices.batch-ready")]
    [InlineData("Invoices.ready")]
    [InlineData("invoices.batch.ready")]
    [InlineData("")]
    public void An_invalid_type_is_refused(string value)
    {
        Assert.False(NotificationType.TryParse(value, out _));
        Assert.Throws<FormatException>(() => NotificationType.Parse(value));
    }

    [Fact]
    public void A_type_is_refused_at_construction_too()
    {
        Assert.Throws<ArgumentException>(() => new NotificationType("invoices", "batch-ready"));
        Assert.Throws<ArgumentException>(() => new NotificationType("", "ready"));
    }

    [Fact]
    public void Json_reads_and_writes_the_identity()
    {
        var json = JsonSerializer.Serialize(NotificationType.Parse("invoices.batch_ready"));

        Assert.Equal("\"invoices.batch_ready\"", json);
        Assert.Equal(NotificationType.Parse("invoices.batch_ready"), JsonSerializer.Deserialize<NotificationType>(json));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NotificationType>("\"invoices\""));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NotificationType>("1"));
    }
}
