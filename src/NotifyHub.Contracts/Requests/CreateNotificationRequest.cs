namespace NotifyHub.Contracts.Requests;

/// <summary>
/// Channels is a dictionary mapping channel name to recipient address.
/// Valid channel names: "push", "email", "sms", "whatsapp".
/// Example: { "push": "user-device-token", "email": "user@example.com" }
/// </summary>
public sealed record CreateNotificationRequest(
    Guid RecipientUserId,
    string Title,
    string Body,
    Dictionary<string, string> Channels);
