using Rebus.Handlers;
using NotifyHub.Contracts.Messages;
using NotifyHub.Core.Repositories;
using NotifyHub.Worker.WhatsApp.Services;

namespace NotifyHub.Worker.WhatsApp.Handlers;

public sealed class SendWhatsAppHandler(
    IWhatsAppSender whatsAppSender,
    INotificationRepository repository,
    ILogger<SendWhatsAppHandler> logger) : IHandleMessages<SendWhatsAppMessage>
{
    public async Task Handle(SendWhatsAppMessage message)
    {
        logger.LogInformation("Processing WhatsApp delivery {DeliveryId} for notification {NotificationId}",
            message.DeliveryId, message.NotificationId);

        var notification = await repository.GetByIdAsync(message.NotificationId, CancellationToken.None);
        if (notification is null)
        {
            logger.LogWarning("Notification {NotificationId} not found, skipping WhatsApp delivery",
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
            await whatsAppSender.SendAsync(message.Recipient, message.Body, CancellationToken.None);
            delivery.MarkAsSent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send WhatsApp message to {Recipient}", message.Recipient);
            delivery.MarkAsFailed(ex.Message);
        }

        notification.RefreshStatus();
        await repository.UpdateAsync(notification, CancellationToken.None);
    }
}
