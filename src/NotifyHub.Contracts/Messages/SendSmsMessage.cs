namespace NotifyHub.Contracts.Messages;

public sealed record SendSmsMessage(
    Guid NotificationId,
    Guid DeliveryId,
    string Recipient,
    string Body);
