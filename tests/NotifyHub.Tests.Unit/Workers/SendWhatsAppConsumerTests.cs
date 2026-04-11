using MassTransit;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NotifyHub.Contracts.Messages;
using NotifyHub.Core.Entities;
using NotifyHub.Core.Enums;
using NotifyHub.Core.Repositories;
using NotifyHub.Worker.WhatsApp.Consumers;
using NotifyHub.Worker.WhatsApp.Services;

namespace NotifyHub.Tests.Unit.Workers;

public class SendWhatsAppConsumerTests
{
    private readonly IWhatsAppSender _whatsAppSender = Substitute.For<IWhatsAppSender>();
    private readonly INotificationRepository _repository = Substitute.For<INotificationRepository>();
    private readonly ILogger<SendWhatsAppConsumer> _logger = Substitute.For<ILogger<SendWhatsAppConsumer>>();
    private readonly SendWhatsAppConsumer _consumer;

    public SendWhatsAppConsumerTests()
    {
        _consumer = new SendWhatsAppConsumer(_whatsAppSender, _repository, _logger);
    }

    private static Notification CreateTestNotification(out Guid deliveryId)
    {
        var notification = Notification.Create(
            Guid.NewGuid(), "Title", "Body",
            new Dictionary<Channel, string> { { Channel.WhatsApp, "+1234567890" } });
        deliveryId = notification.Deliveries.First().Id;
        return notification;
    }

    private ConsumeContext<SendWhatsAppMessage> CreateContext(SendWhatsAppMessage message)
    {
        var context = Substitute.For<ConsumeContext<SendWhatsAppMessage>>();
        context.Message.Returns(message);
        context.CancellationToken.Returns(CancellationToken.None);
        return context;
    }

    [Fact]
    public async Task Consume_SuccessfulSend_MarksDeliveryAsSent()
    {
        var notification = CreateTestNotification(out var deliveryId);
        var message = new SendWhatsAppMessage(notification.Id, deliveryId, "+1234567890", "Body");
        _repository.GetByIdAsync(notification.Id, Arg.Any<CancellationToken>()).Returns(notification);

        await _consumer.Consume(CreateContext(message));

        Assert.Equal(DeliveryStatus.Sent, notification.Deliveries.First().Status);
        await _repository.Received(1).UpdateAsync(notification, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Consume_FailedSend_MarksDeliveryAsFailed()
    {
        var notification = CreateTestNotification(out var deliveryId);
        var message = new SendWhatsAppMessage(notification.Id, deliveryId, "+1234567890", "Body");
        _repository.GetByIdAsync(notification.Id, Arg.Any<CancellationToken>()).Returns(notification);
        _whatsAppSender.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("WhatsApp API error"));

        await _consumer.Consume(CreateContext(message));

        Assert.Equal(DeliveryStatus.Failed, notification.Deliveries.First().Status);
    }

    [Fact]
    public async Task Consume_NotificationNotFound_DoesNotThrow()
    {
        var message = new SendWhatsAppMessage(Guid.NewGuid(), Guid.NewGuid(), "+1234567890", "Body");
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Notification?)null);

        await _consumer.Consume(CreateContext(message));

        await _whatsAppSender.DidNotReceive().SendAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
