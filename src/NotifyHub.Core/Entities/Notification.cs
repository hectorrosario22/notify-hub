using NotifyHub.Core.Enums;

namespace NotifyHub.Core.Entities;

public class Notification
{
    private readonly List<NotificationDelivery> _deliveries = new();

    private Notification() { }

    public Guid Id { get; private set; }
    public Guid RecipientUserId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public NotificationStatus Status { get; private set; }
    public bool IsRead { get; private set; }
    public DateTime? ReadAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public IReadOnlyCollection<NotificationDelivery> Deliveries => _deliveries.AsReadOnly();

    public static Notification Create(
        Guid recipientUserId,
        string title,
        string body,
        Dictionary<Channel, string> channelRecipients)
    {
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            RecipientUserId = recipientUserId,
            Title = title,
            Body = body,
            Status = NotificationStatus.Pending,
            IsRead = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        foreach (var (channel, recipient) in channelRecipients)
        {
            var delivery = new NotificationDelivery(
                notification.Id,
                channel,
                recipient,
                notification.RecalculateStatus);

            notification._deliveries.Add(delivery);
        }

        return notification;
    }

    public void MarkAsRead()
    {
        throw new NotImplementedException();
    }

    private void RecalculateStatus()
    {
        // implemented in Task 7
    }
}
