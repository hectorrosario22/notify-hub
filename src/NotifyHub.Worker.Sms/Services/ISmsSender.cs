namespace NotifyHub.Worker.Sms.Services;

public interface ISmsSender
{
    Task SendAsync(string recipient, string body, CancellationToken ct = default);
}
