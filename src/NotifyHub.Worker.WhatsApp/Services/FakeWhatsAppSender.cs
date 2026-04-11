namespace NotifyHub.Worker.WhatsApp.Services;

public sealed class FakeWhatsAppSender(ILogger<FakeWhatsAppSender> logger) : IWhatsAppSender
{
    public async Task SendAsync(string recipient, string body, CancellationToken ct)
    {
        logger.LogInformation("[FAKE] Sending WhatsApp message to {Recipient}", recipient);
        await Task.Delay(Random.Shared.Next(100, 500), ct);
        logger.LogInformation("[FAKE] WhatsApp message sent successfully to {Recipient}", recipient);
    }
}
