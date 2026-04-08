using NotifyHub.Core.Entities;
using NotifyHub.Core.Enums;

namespace NotifyHub.Tests.Unit.Entities;

public class NotificationDeliveryTests
{
    private static NotificationDelivery CreateDelivery(
        Channel channel = Channel.Email,
        string recipient = "test@example.com",
        Action? onStatusChanged = null)
        => new(Guid.NewGuid(), channel, recipient, onStatusChanged ?? (() => { }));

    [Fact]
    public void MarkAsSent_SetsSentStatus()
    {
        var delivery = CreateDelivery();

        delivery.MarkAsSent();

        Assert.Equal(DeliveryStatus.Sent, delivery.Status);
    }

    [Fact]
    public void MarkAsSent_SetsSentAt()
    {
        var before = DateTime.UtcNow;
        var delivery = CreateDelivery();

        delivery.MarkAsSent();

        Assert.NotNull(delivery.SentAt);
        Assert.True(delivery.SentAt >= before);
    }

    [Fact]
    public void MarkAsSent_InvokesOnStatusChangedCallback()
    {
        var callbackInvoked = false;
        var delivery = CreateDelivery(onStatusChanged: () => callbackInvoked = true);

        delivery.MarkAsSent();

        Assert.True(callbackInvoked);
    }
}
