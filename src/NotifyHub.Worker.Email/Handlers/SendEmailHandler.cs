using Rebus.Handlers;
using NotifyHub.Contracts.Messages;
using NotifyHub.Core.Repositories;
using NotifyHub.Worker.Email.Services;

namespace NotifyHub.Worker.Email.Handlers;

public sealed class SendEmailHandler(
    IEmailSender emailSender,
    INotificationRepository repository,
    ILogger<SendEmailHandler> logger) : IHandleMessages<SendEmailMessage>
{
    public async Task Handle(SendEmailMessage message)
    {
        logger.LogInformation("Processing email delivery {DeliveryId} for notification {NotificationId}",
            message.DeliveryId, message.NotificationId);

        var notification = await repository.GetByIdAsync(message.NotificationId, CancellationToken.None);
        if (notification is null)
        {
            logger.LogWarning("Notification {NotificationId} not found, skipping email delivery",
                message.NotificationId);
            return;
        }

        var delivery = notification.Deliveries.FirstOrDefault(d => d.Id == message.DeliveryId);
        if (delivery is null)
        {
            logger.LogWarning("Delivery {DeliveryId} not found for notification {NotificationId}",
                message.DeliveryId, message.NotificationId);
            return;
        }

        try
        {
            await emailSender.SendAsync(message.Recipient, message.Title, message.Body, CancellationToken.None);
            delivery.MarkAsSent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send email to {Recipient}", message.Recipient);
            delivery.MarkAsFailed(ex.Message);
        }

        notification.RefreshStatus();
        await repository.UpdateAsync(notification, CancellationToken.None);
    }
}
