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

    [Fact]
    public void MarkAsFailed_SetsFailedStatus()
    {
        var delivery = CreateDelivery();

        delivery.MarkAsFailed("Connection timeout");

        Assert.Equal(DeliveryStatus.Failed, delivery.Status);
    }

    [Fact]
    public void MarkAsFailed_SetsErrorMessage()
    {
        var delivery = CreateDelivery();

        delivery.MarkAsFailed("Connection timeout");

        Assert.Equal("Connection timeout", delivery.ErrorMessage);
    }

    [Fact]
    public void MarkAsFailed_IncrementsRetryCount()
    {
        var delivery = CreateDelivery();

        delivery.MarkAsFailed("Connection timeout");

        Assert.Equal(1, delivery.RetryCount);
    }

    [Fact]
    public void MarkAsFailed_CalledTwice_IncrementsRetryCountToTwo()
    {
        var delivery = CreateDelivery();

        delivery.MarkAsFailed("First failure");
        delivery.MarkAsFailed("Second failure");

        Assert.Equal(2, delivery.RetryCount);
    }

    [Fact]
    public void MarkAsFailed_InvokesOnStatusChangedCallback()
    {
        var callbackInvoked = false;
        var delivery = CreateDelivery(onStatusChanged: () => callbackInvoked = true);

        delivery.MarkAsFailed("error");

        Assert.True(callbackInvoked);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MarkAsFailed_WithEmptyOrNullErrorMessage_ThrowsArgumentException(string? errorMessage)
    {
        var delivery = CreateDelivery();

        Assert.Throws<ArgumentException>(() => delivery.MarkAsFailed(errorMessage!));
    }
}
