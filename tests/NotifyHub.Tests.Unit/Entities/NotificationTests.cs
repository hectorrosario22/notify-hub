using NotifyHub.Core.Entities;
using NotifyHub.Core.Enums;

namespace NotifyHub.Tests.Unit.Entities;

public class NotificationTests
{
    private static readonly Guid ValidRecipientId = Guid.NewGuid();

    [Fact]
    public void Create_WithValidInputs_ReturnsNotificationWithPendingStatus()
    {
        var channels = new Dictionary<Channel, string>
        {
            { Channel.Email, "user@example.com" }
        };

        var notification = Notification.Create(ValidRecipientId, "Hello", "World", channels);

        Assert.Equal(NotificationStatus.Pending, notification.Status);
    }

    [Fact]
    public void Create_WithValidInputs_SetsProperties()
    {
        var channels = new Dictionary<Channel, string>
        {
            { Channel.Email, "user@example.com" }
        };

        var notification = Notification.Create(ValidRecipientId, "Hello", "World", channels);

        Assert.Equal(ValidRecipientId, notification.RecipientUserId);
        Assert.Equal("Hello", notification.Title);
        Assert.Equal("World", notification.Body);
        Assert.False(notification.IsRead);
        Assert.Null(notification.ReadAt);
        Assert.NotEqual(Guid.Empty, notification.Id);
    }

    [Fact]
    public void Create_WithMultipleChannels_CreatesOneDeliveryPerChannel()
    {
        var channels = new Dictionary<Channel, string>
        {
            { Channel.Email, "user@example.com" },
            { Channel.Sms, "+1234567890" },
            { Channel.Push, "device-token" }
        };

        var notification = Notification.Create(ValidRecipientId, "Hello", "World", channels);

        Assert.Equal(3, notification.Deliveries.Count);
    }

    [Fact]
    public void Create_WithSingleChannel_CreatesDeliveryWithCorrectChannelAndRecipient()
    {
        var channels = new Dictionary<Channel, string>
        {
            { Channel.Email, "user@example.com" }
        };

        var notification = Notification.Create(ValidRecipientId, "Hello", "World", channels);

        var delivery = notification.Deliveries.Single();
        Assert.Equal(Channel.Email, delivery.Channel);
        Assert.Equal("user@example.com", delivery.Recipient);
        Assert.Equal(DeliveryStatus.Pending, delivery.Status);
    }

    [Fact]
    public void Create_WithEmptyRecipientUserId_ThrowsArgumentException()
    {
        var channels = new Dictionary<Channel, string> { { Channel.Email, "user@example.com" } };

        Assert.Throws<ArgumentException>(() =>
            Notification.Create(Guid.Empty, "Hello", "World", channels));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyOrNullTitle_ThrowsArgumentException(string? title)
    {
        var channels = new Dictionary<Channel, string> { { Channel.Email, "user@example.com" } };

        Assert.Throws<ArgumentException>(() =>
            Notification.Create(ValidRecipientId, title!, "World", channels));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyOrNullBody_ThrowsArgumentException(string? body)
    {
        var channels = new Dictionary<Channel, string> { { Channel.Email, "user@example.com" } };

        Assert.Throws<ArgumentException>(() =>
            Notification.Create(ValidRecipientId, "Hello", body!, channels));
    }

    [Fact]
    public void Create_WithNoChannels_ThrowsArgumentException()
    {
        var channels = new Dictionary<Channel, string>();

        Assert.Throws<ArgumentException>(() =>
            Notification.Create(ValidRecipientId, "Hello", "World", channels));
    }

    [Fact]
    public void Create_WithNullChannels_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Notification.Create(ValidRecipientId, "Hello", "World", null!));
    }

    [Fact]
    public void Create_WithEmptyRecipientForChannel_ThrowsArgumentException()
    {
        var channels = new Dictionary<Channel, string>
        {
            { Channel.Email, "" }
        };

        Assert.Throws<ArgumentException>(() =>
            Notification.Create(ValidRecipientId, "Hello", "World", channels));
    }

    [Fact]
    public void MarkAsRead_WithPushDelivery_SetsIsReadTrue()
    {
        var channels = new Dictionary<Channel, string> { { Channel.Push, "device-token" } };
        var notification = Notification.Create(ValidRecipientId, "Hello", "World", channels);

        notification.MarkAsRead();

        Assert.True(notification.IsRead);
    }

    [Fact]
    public void MarkAsRead_WithPushDelivery_SetsReadAt()
    {
        var before = DateTime.UtcNow;
        var channels = new Dictionary<Channel, string> { { Channel.Push, "device-token" } };
        var notification = Notification.Create(ValidRecipientId, "Hello", "World", channels);

        notification.MarkAsRead();

        Assert.NotNull(notification.ReadAt);
        Assert.True(notification.ReadAt >= before);
    }

    [Fact]
    public void MarkAsRead_WithNoPushDelivery_ThrowsInvalidOperationException()
    {
        var channels = new Dictionary<Channel, string> { { Channel.Email, "user@example.com" } };
        var notification = Notification.Create(ValidRecipientId, "Hello", "World", channels);

        Assert.Throws<InvalidOperationException>(() => notification.MarkAsRead());
    }

    [Fact]
    public void MarkAsRead_WhenAlreadyRead_IsIdempotent()
    {
        var channels = new Dictionary<Channel, string> { { Channel.Push, "device-token" } };
        var notification = Notification.Create(ValidRecipientId, "Hello", "World", channels);
        notification.MarkAsRead();
        var firstReadAt = notification.ReadAt;

        notification.MarkAsRead();

        Assert.True(notification.IsRead);
        Assert.Equal(firstReadAt, notification.ReadAt);
    }
}
