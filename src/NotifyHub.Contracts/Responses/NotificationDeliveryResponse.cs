namespace NotifyHub.Contracts.Responses;

public sealed record NotificationDeliveryResponse(
    Guid Id,
    string Channel,
    string Status,
    string Recipient,
    int RetryCount,
    string? ErrorMessage,
    DateTime? SentAt,
    DateTime CreatedAt);
