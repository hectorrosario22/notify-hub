using MassTransit;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NotifyHub.Contracts.Messages;
using NotifyHub.Core.Entities;
using NotifyHub.Core.Enums;
using NotifyHub.Core.Repositories;
using NotifyHub.Worker.Sms.Consumers;
using NotifyHub.Worker.Sms.Services;

namespace NotifyHub.Tests.Unit.Workers;

public class SendSmsConsumerTests
{
    private readonly ISmsSender _smsSender = Substitute.For<ISmsSender>();
    private readonly INotificationRepository _repository = Substitute.For<INotificationRepository>();
    private readonly ILogger<SendSmsConsumer> _logger = Substitute.For<ILogger<SendSmsConsumer>>();
    private readonly SendSmsConsumer _consumer;

    public SendSmsConsumerTests()
    {
        _consumer = new SendSmsConsumer(_smsSender, _repository, _logger);
    }

    private static Notification CreateTestNotification(out Guid deliveryId)
    {
        var notification = Notification.Create(
            Guid.NewGuid(), "Title", "Body",
            new Dictionary<Channel, string> { { Channel.Sms, "+1234567890" } });
        deliveryId = notification.Deliveries.First().Id;
        return notification;
    }

    private ConsumeContext<SendSmsMessage> CreateContext(SendSmsMessage message)
    {
        var context = Substitute.For<ConsumeContext<SendSmsMessage>>();
        context.Message.Returns(message);
        context.CancellationToken.Returns(CancellationToken.None);
        return context;
    }

    [Fact]
    public async Task Consume_SuccessfulSend_MarksDeliveryAsSent()
    {
        var notification = CreateTestNotification(out var deliveryId);
        var message = new SendSmsMessage(notification.Id, deliveryId, "+1234567890", "Body");
        _repository.GetByIdAsync(notification.Id, Arg.Any<CancellationToken>()).Returns(notification);

        await _consumer.Consume(CreateContext(message));

        Assert.Equal(DeliveryStatus.Sent, notification.Deliveries.First().Status);
        await _repository.Received(1).UpdateAsync(notification, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Consume_FailedSend_MarksDeliveryAsFailed()
    {
        var notification = CreateTestNotification(out var deliveryId);
        var message = new SendSmsMessage(notification.Id, deliveryId, "+1234567890", "Body");
        _repository.GetByIdAsync(notification.Id, Arg.Any<CancellationToken>()).Returns(notification);
        _smsSender.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("SMS provider error"));

        await _consumer.Consume(CreateContext(message));

        Assert.Equal(DeliveryStatus.Failed, notification.Deliveries.First().Status);
    }

    [Fact]
    public async Task Consume_NotificationNotFound_DoesNotThrow()
    {
        var message = new SendSmsMessage(Guid.NewGuid(), Guid.NewGuid(), "+1234567890", "Body");
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Notification?)null);

        await _consumer.Consume(CreateContext(message));

        await _smsSender.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
