namespace NotifyHub.Worker.WhatsApp.Services;

public interface IWhatsAppSender
{
    Task SendAsync(string recipient, string body, CancellationToken ct = default);
}
