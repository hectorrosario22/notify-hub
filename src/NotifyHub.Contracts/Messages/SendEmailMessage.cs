namespace NotifyHub.Contracts.Messages;

public sealed record SendEmailMessage(
    Guid NotificationId,
    Guid DeliveryId,
    string Recipient,
    string Title,
    string Body);
