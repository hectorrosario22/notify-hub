using NotifyHub.Core.Enums;

namespace NotifyHub.Core.Entities;

public class NotificationDelivery
{
    private readonly Action _onStatusChanged;

    internal NotificationDelivery(Guid notificationId, Channel channel, string recipient, Action onStatusChanged)
    {
        Id = Guid.NewGuid();
        NotificationId = notificationId;
        Channel = channel;
        Recipient = recipient;
        Status = DeliveryStatus.Pending;
        RetryCount = 0;
        CreatedAt = DateTime.UtcNow;
        _onStatusChanged = onStatusChanged;
    }

    public Guid Id { get; private set; }
    public Guid NotificationId { get; private set; }
    public Channel Channel { get; private set; }
    public DeliveryStatus Status { get; private set; }
    public string Recipient { get; private set; }
    public int RetryCount { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTime? SentAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public void MarkAsSent()
    {
        Status = DeliveryStatus.Sent;
        SentAt = DateTime.UtcNow;
        _onStatusChanged();
    }

    public void MarkAsFailed(string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
            throw new ArgumentException("Error message cannot be empty.", nameof(errorMessage));

        Status = DeliveryStatus.Failed;
        ErrorMessage = errorMessage;
        RetryCount++;
        _onStatusChanged();
    }
}
