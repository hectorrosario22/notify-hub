namespace NotifyHub.Worker.Email.Services;

public sealed class FakeEmailSender(ILogger<FakeEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(string recipient, string subject, string body, CancellationToken ct)
    {
        logger.LogInformation("[FAKE] Sending email to {Recipient}: {Subject}", recipient, subject);
        await Task.Delay(Random.Shared.Next(100, 500), ct);
        logger.LogInformation("[FAKE] Email sent successfully to {Recipient}", recipient);
    }
}
