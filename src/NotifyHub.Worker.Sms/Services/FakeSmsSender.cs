namespace NotifyHub.Worker.Sms.Services;

public sealed class FakeSmsSender(ILogger<FakeSmsSender> logger) : ISmsSender
{
    public async Task SendAsync(string recipient, string body, CancellationToken ct)
    {
        logger.LogInformation("[FAKE] Sending SMS to {Recipient}", recipient);
        await Task.Delay(Random.Shared.Next(100, 500), ct);
        logger.LogInformation("[FAKE] SMS sent successfully to {Recipient}", recipient);
    }
}
