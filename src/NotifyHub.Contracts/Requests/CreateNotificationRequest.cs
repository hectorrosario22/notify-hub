namespace NotifyHub.Contracts.Requests;

/// <param name="Channels">
/// Maps channel name to recipient address.
/// Valid keys: "push", "email", "sms", "whatsapp".
/// Example: { "push": "user-device-token", "email": "user@example.com" }
/// </param>
public sealed record CreateNotificationRequest(
    Guid RecipientUserId,
    string Title,
    string Body,
    Dictionary<string, string> Channels);
