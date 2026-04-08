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
        ArgumentNullException.ThrowIfNull(channelRecipients);

        if (recipientUserId == Guid.Empty)
            throw new ArgumentException("RecipientUserId cannot be empty.", nameof(recipientUserId));

        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title cannot be empty.", nameof(title));

        if (string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("Body cannot be empty.", nameof(body));

        if (channelRecipients.Count == 0)
            throw new ArgumentException("At least one channel must be specified.", nameof(channelRecipients));

        foreach (var (channel, recipient) in channelRecipients)
        {
            if (string.IsNullOrWhiteSpace(recipient))
                throw new ArgumentException($"Recipient for channel {channel} cannot be empty.", nameof(channelRecipients));
        }

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
        if (!_deliveries.Any(d => d.Channel == Channel.Push))
            throw new InvalidOperationException("Cannot mark as read a notification without a Push delivery.");

        if (IsRead)
            return;

        IsRead = true;
        ReadAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    private void RecalculateStatus()
    {
        var statuses = _deliveries.Select(d => d.Status).ToList();

        Status = statuses switch
        {
            _ when statuses.Any(s => s == DeliveryStatus.Pending)  => NotificationStatus.Pending,
            _ when statuses.All(s => s == DeliveryStatus.Sent)     => NotificationStatus.Delivered,
            _ when statuses.All(s => s == DeliveryStatus.Failed)   => NotificationStatus.Failed,
            _                                                        => NotificationStatus.Partial
        };

        UpdatedAt = DateTime.UtcNow;
    }
}
