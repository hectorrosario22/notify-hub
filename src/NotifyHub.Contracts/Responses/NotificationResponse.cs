namespace NotifyHub.Contracts.Responses;

public sealed record NotificationResponse(
    Guid Id,
    Guid RecipientUserId,
    string Title,
    string Body,
    string Status,
    bool IsRead,
    DateTime? ReadAt,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<NotificationDeliveryResponse> Deliveries);
