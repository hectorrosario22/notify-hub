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
}
