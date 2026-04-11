namespace NotifyHub.Worker.Email.Services;

public interface IEmailSender
{
    Task SendAsync(string recipient, string subject, string body, CancellationToken ct = default);
}
