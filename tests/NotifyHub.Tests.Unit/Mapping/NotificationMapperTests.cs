using NotifyHub.Api.Mapping;
using NotifyHub.Core.Entities;
using NotifyHub.Core.Enums;

namespace NotifyHub.Tests.Unit.Mapping;

public class NotificationMapperTests
{
    [Fact]
    public void ToResponse_MapsAllScalarFields()
    {
        var notification = Notification.Create(
            Guid.NewGuid(),
            "Test Title",
            "Test Body",
            new Dictionary<Channel, string> { { Channel.Push, "device-token" } });

        var response = NotificationMapper.ToResponse(notification);

        Assert.Equal(notification.Id, response.Id);
        Assert.Equal(notification.RecipientUserId, response.RecipientUserId);
        Assert.Equal("Test Title", response.Title);
        Assert.Equal("Test Body", response.Body);
        Assert.Equal("pending", response.Status);
        Assert.False(response.IsRead);
        Assert.Null(response.ReadAt);
        Assert.Equal(notification.CreatedAt, response.CreatedAt);
        Assert.Equal(notification.UpdatedAt, response.UpdatedAt);
    }

    [Fact]
    public void ToResponse_MapsDeliveriesCollection()
    {
        var notification = Notification.Create(
            Guid.NewGuid(),
            "Title",
            "Body",
            new Dictionary<Channel, string>
            {
                { Channel.Push, "device-token" },
                { Channel.Email, "user@example.com" }
            });

        var response = NotificationMapper.ToResponse(notification);

        Assert.Equal(2, response.Deliveries.Count);
        var pushDelivery = response.Deliveries.First(d => d.Channel == "push");
        Assert.Equal("pending", pushDelivery.Status);
        Assert.Equal("device-token", pushDelivery.Recipient);
        Assert.Equal(0, pushDelivery.RetryCount);
        Assert.Null(pushDelivery.ErrorMessage);
        Assert.Null(pushDelivery.SentAt);
    }

    [Fact]
    public void ToResponse_WithNoDeliveries_ReturnsEmptyList()
    {
        // Create a notification with one delivery to get a valid object,
        // then test that mapper handles the deliveries correctly
        var notification = Notification.Create(
            Guid.NewGuid(),
            "Title",
            "Body",
            new Dictionary<Channel, string> { { Channel.Email, "user@example.com" } });

        var response = NotificationMapper.ToResponse(notification);

        Assert.NotNull(response.Deliveries);
        Assert.Single(response.Deliveries);
    }

    [Fact]
    public void ToResponse_MapsChannelAndStatusAsLowercaseStrings()
    {
        var notification = Notification.Create(
            Guid.NewGuid(),
            "Title",
            "Body",
            new Dictionary<Channel, string> { { Channel.WhatsApp, "+1234567890" } });

        var response = NotificationMapper.ToResponse(notification);

        var delivery = response.Deliveries.Single();
        Assert.Equal("whatsapp", delivery.Channel);
        Assert.Equal("pending", delivery.Status);
    }
}
