using MassTransit;
using NotifyHub.Contracts.Messages;
using NotifyHub.Core.Repositories;
using NotifyHub.Worker.WhatsApp.Services;

namespace NotifyHub.Worker.WhatsApp.Consumers;

public sealed class SendWhatsAppConsumer(
    IWhatsAppSender whatsAppSender,
    INotificationRepository repository,
    ILogger<SendWhatsAppConsumer> logger) : IConsumer<SendWhatsAppMessage>
{
    public async Task Consume(ConsumeContext<SendWhatsAppMessage> context)
    {
        var message = context.Message;
        logger.LogInformation("Processing WhatsApp delivery {DeliveryId} for notification {NotificationId}",
            message.DeliveryId, message.NotificationId);

        var notification = await repository.GetByIdAsync(message.NotificationId, context.CancellationToken);
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
            await whatsAppSender.SendAsync(message.Recipient, message.Body, context.CancellationToken);
            delivery.MarkAsSent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send WhatsApp message to {Recipient}", message.Recipient);
            delivery.MarkAsFailed(ex.Message);
        }

        notification.RefreshStatus();
        await repository.UpdateAsync(notification, context.CancellationToken);
    }
}
