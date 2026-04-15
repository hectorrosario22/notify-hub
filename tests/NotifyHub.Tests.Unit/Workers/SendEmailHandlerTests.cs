using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NotifyHub.Contracts.Messages;
using NotifyHub.Core.Entities;
using NotifyHub.Core.Enums;
using NotifyHub.Core.Repositories;
using NotifyHub.Worker.Email.Handlers;
using NotifyHub.Worker.Email.Services;

namespace NotifyHub.Tests.Unit.Workers;

public class SendEmailHandlerTests
{
    private readonly IEmailSender _emailSender = Substitute.For<IEmailSender>();
    private readonly INotificationRepository _repository = Substitute.For<INotificationRepository>();
    private readonly ILogger<SendEmailHandler> _logger = Substitute.For<ILogger<SendEmailHandler>>();
    private readonly SendEmailHandler _handler;

    public SendEmailHandlerTests()
    {
        _handler = new SendEmailHandler(_emailSender, _repository, _logger);
    }

    private static Notification CreateTestNotification(out Guid deliveryId)
    {
        var notification = Notification.Create(
            Guid.NewGuid(), "Title", "Body",
            new Dictionary<Channel, string> { { Channel.Email, "user@example.com" } });
        deliveryId = notification.Deliveries.First().Id;
        return notification;
    }

    [Fact]
    public async Task Handle_SuccessfulSend_MarksDeliveryAsSent()
    {
        var notification = CreateTestNotification(out var deliveryId);
        var message = new SendEmailMessage(notification.Id, deliveryId, "user@example.com", "Title", "Body");
        _repository.GetByIdAsync(notification.Id, Arg.Any<CancellationToken>()).Returns(notification);

        await _handler.Handle(message);

        var delivery = notification.Deliveries.First();
        Assert.Equal(DeliveryStatus.Sent, delivery.Status);
        await _repository.Received(1).UpdateAsync(notification, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SuccessfulSend_CallsEmailSender()
    {
        var notification = CreateTestNotification(out var deliveryId);
        var message = new SendEmailMessage(notification.Id, deliveryId, "user@example.com", "Title", "Body");
        _repository.GetByIdAsync(notification.Id, Arg.Any<CancellationToken>()).Returns(notification);

        await _handler.Handle(message);

        await _emailSender.Received(1).SendAsync("user@example.com", "Title", "Body", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_FailedSend_MarksDeliveryAsFailed()
    {
        var notification = CreateTestNotification(out var deliveryId);
        var message = new SendEmailMessage(notification.Id, deliveryId, "user@example.com", "Title", "Body");
        _repository.GetByIdAsync(notification.Id, Arg.Any<CancellationToken>()).Returns(notification);
        _emailSender.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("SMTP connection failed"));

        await _handler.Handle(message);

        var delivery = notification.Deliveries.First();
        Assert.Equal(DeliveryStatus.Failed, delivery.Status);
        Assert.Equal("SMTP connection failed", delivery.ErrorMessage);
        await _repository.Received(1).UpdateAsync(notification, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NotificationNotFound_DoesNotCallEmailSender()
    {
        var message = new SendEmailMessage(Guid.NewGuid(), Guid.NewGuid(), "user@example.com", "Title", "Body");
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Notification?)null);

        await _handler.Handle(message);

        await _emailSender.DidNotReceive().SendAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SuccessfulSend_RefreshesNotificationStatus()
    {
        var notification = CreateTestNotification(out var deliveryId);
        var message = new SendEmailMessage(notification.Id, deliveryId, "user@example.com", "Title", "Body");
        _repository.GetByIdAsync(notification.Id, Arg.Any<CancellationToken>()).Returns(notification);

        await _handler.Handle(message);

        Assert.Equal(NotificationStatus.Delivered, notification.Status);
    }
}
