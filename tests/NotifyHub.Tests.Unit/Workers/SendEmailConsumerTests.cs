using MassTransit;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NotifyHub.Contracts.Messages;
using NotifyHub.Core.Entities;
using NotifyHub.Core.Enums;
using NotifyHub.Core.Repositories;
using NotifyHub.Worker.Email.Consumers;
using NotifyHub.Worker.Email.Services;

namespace NotifyHub.Tests.Unit.Workers;

public class SendEmailConsumerTests
{
    private readonly IEmailSender _emailSender = Substitute.For<IEmailSender>();
    private readonly INotificationRepository _repository = Substitute.For<INotificationRepository>();
    private readonly ILogger<SendEmailConsumer> _logger = Substitute.For<ILogger<SendEmailConsumer>>();
    private readonly SendEmailConsumer _consumer;

    public SendEmailConsumerTests()
    {
        _consumer = new SendEmailConsumer(_emailSender, _repository, _logger);
    }

    private static Notification CreateTestNotification(out Guid deliveryId)
    {
        var notification = Notification.Create(
            Guid.NewGuid(), "Title", "Body",
            new Dictionary<Channel, string> { { Channel.Email, "user@example.com" } });
        deliveryId = notification.Deliveries.First().Id;
        return notification;
    }

    private ConsumeContext<SendEmailMessage> CreateContext(SendEmailMessage message)
    {
        var context = Substitute.For<ConsumeContext<SendEmailMessage>>();
        context.Message.Returns(message);
        context.CancellationToken.Returns(CancellationToken.None);
        return context;
    }

    [Fact]
    public async Task Consume_SuccessfulSend_MarksDeliveryAsSent()
    {
        var notification = CreateTestNotification(out var deliveryId);
        var message = new SendEmailMessage(notification.Id, deliveryId, "user@example.com", "Title", "Body");
        _repository.GetByIdAsync(notification.Id, Arg.Any<CancellationToken>()).Returns(notification);

        await _consumer.Consume(CreateContext(message));

        var delivery = notification.Deliveries.First();
        Assert.Equal(DeliveryStatus.Sent, delivery.Status);
        await _repository.Received(1).UpdateAsync(notification, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Consume_SuccessfulSend_CallsEmailSender()
    {
        var notification = CreateTestNotification(out var deliveryId);
        var message = new SendEmailMessage(notification.Id, deliveryId, "user@example.com", "Title", "Body");
        _repository.GetByIdAsync(notification.Id, Arg.Any<CancellationToken>()).Returns(notification);

        await _consumer.Consume(CreateContext(message));

        await _emailSender.Received(1).SendAsync("user@example.com", "Title", "Body", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Consume_FailedSend_MarksDeliveryAsFailed()
    {
        var notification = CreateTestNotification(out var deliveryId);
        var message = new SendEmailMessage(notification.Id, deliveryId, "user@example.com", "Title", "Body");
        _repository.GetByIdAsync(notification.Id, Arg.Any<CancellationToken>()).Returns(notification);
        _emailSender.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("SMTP connection failed"));

        await _consumer.Consume(CreateContext(message));

        var delivery = notification.Deliveries.First();
        Assert.Equal(DeliveryStatus.Failed, delivery.Status);
        Assert.Equal("SMTP connection failed", delivery.ErrorMessage);
        await _repository.Received(1).UpdateAsync(notification, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Consume_NotificationNotFound_DoesNotThrow()
    {
        var message = new SendEmailMessage(Guid.NewGuid(), Guid.NewGuid(), "user@example.com", "Title", "Body");
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Notification?)null);

        await _consumer.Consume(CreateContext(message));

        await _emailSender.DidNotReceive().SendAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Consume_SuccessfulSend_CallsRefreshStatus()
    {
        var notification = CreateTestNotification(out var deliveryId);
        var message = new SendEmailMessage(notification.Id, deliveryId, "user@example.com", "Title", "Body");
        _repository.GetByIdAsync(notification.Id, Arg.Any<CancellationToken>()).Returns(notification);

        await _consumer.Consume(CreateContext(message));

        Assert.Equal(NotificationStatus.Delivered, notification.Status);
    }
}
