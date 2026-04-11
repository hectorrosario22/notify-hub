using MassTransit;
using NotifyHub.Contracts.Messages;
using NotifyHub.Core.Repositories;
using NotifyHub.Worker.Sms.Services;

namespace NotifyHub.Worker.Sms.Consumers;

public sealed class SendSmsConsumer(
    ISmsSender smsSender,
    INotificationRepository repository,
    ILogger<SendSmsConsumer> logger) : IConsumer<SendSmsMessage>
{
    public async Task Consume(ConsumeContext<SendSmsMessage> context)
    {
        var message = context.Message;
        logger.LogInformation("Processing SMS delivery {DeliveryId} for notification {NotificationId}",
            message.DeliveryId, message.NotificationId);

        var notification = await repository.GetByIdAsync(message.NotificationId, context.CancellationToken);
        if (notification is null)
        {
            logger.LogWarning("Notification {NotificationId} not found, skipping SMS delivery",
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
            await smsSender.SendAsync(message.Recipient, message.Body, context.CancellationToken);
            delivery.MarkAsSent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send SMS to {Recipient}", message.Recipient);
            delivery.MarkAsFailed(ex.Message);
        }

        notification.RefreshStatus();
        await repository.UpdateAsync(notification, context.CancellationToken);
    }
}
