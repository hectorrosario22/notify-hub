namespace NotifyHub.Contracts.Messages;

public sealed record SendWhatsAppMessage(
    Guid NotificationId,
    Guid DeliveryId,
    string Recipient,
    string Body);
