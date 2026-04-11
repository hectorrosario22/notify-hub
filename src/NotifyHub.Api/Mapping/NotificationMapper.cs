using NotifyHub.Contracts.Responses;
using NotifyHub.Core.Entities;

namespace NotifyHub.Api.Mapping;

public static class NotificationMapper
{
    public static NotificationResponse ToResponse(Notification notification) => new(
        Id: notification.Id,
        RecipientUserId: notification.RecipientUserId,
        Title: notification.Title,
        Body: notification.Body,
        Status: notification.Status.ToString().ToLowerInvariant(),
        IsRead: notification.IsRead,
        ReadAt: notification.ReadAt,
        CreatedAt: notification.CreatedAt,
        UpdatedAt: notification.UpdatedAt,
        Deliveries: notification.Deliveries.Select(ToDeliveryResponse).ToList());

    public static NotificationDeliveryResponse ToDeliveryResponse(NotificationDelivery delivery) => new(
        Id: delivery.Id,
        Channel: delivery.Channel.ToString().ToLowerInvariant(),
        Status: delivery.Status.ToString().ToLowerInvariant(),
        Recipient: delivery.Recipient,
        RetryCount: delivery.RetryCount,
        ErrorMessage: delivery.ErrorMessage,
        SentAt: delivery.SentAt,
        CreatedAt: delivery.CreatedAt);
}
