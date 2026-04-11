using FluentValidation;
using FluentValidation.Results;
using MassTransit;
using ValidationResult = FluentValidation.Results.ValidationResult;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using NSubstitute;
using NotifyHub.Api.Endpoints;
using NotifyHub.Api.Hubs;
using NotifyHub.Contracts.Messages;
using NotifyHub.Contracts.Requests;
using NotifyHub.Contracts.Responses;
using NotifyHub.Core.Entities;
using NotifyHub.Core.Enums;
using NotifyHub.Core.Repositories;

namespace NotifyHub.Tests.Unit.Endpoints;

public class CreateNotificationEndpointTests
{
    private readonly INotificationRepository _repository = Substitute.For<INotificationRepository>();
    private readonly IValidator<CreateNotificationRequest> _validator = Substitute.For<IValidator<CreateNotificationRequest>>();
    private readonly IHubContext<NotificationsHub> _hubContext = Substitute.For<IHubContext<NotificationsHub>>();
    private readonly IPublishEndpoint _publishEndpoint = Substitute.For<IPublishEndpoint>();
    private readonly IClientProxy _clientProxy = Substitute.For<IClientProxy>();

    public CreateNotificationEndpointTests()
    {
        var hubClients = Substitute.For<IHubClients>();
        hubClients.Group(Arg.Any<string>()).Returns(_clientProxy);
        _hubContext.Clients.Returns(hubClients);
    }

    private static CreateNotificationRequest ValidRequest() => new(
        RecipientUserId: Guid.NewGuid(),
        Title: "Test Title",
        Body: "Test Body",
        Channels: new Dictionary<string, string> { { "email", "user@example.com" } });

    private void SetupValidValidator(CreateNotificationRequest request)
    {
        _validator.ValidateAsync(request, Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());
    }

    private Task<IResult> CallEndpoint(CreateNotificationRequest request) =>
        CreateNotificationEndpoint.HandleAsync(request, _repository, _validator, _hubContext, _publishEndpoint, CancellationToken.None);

    [Fact]
    public async Task HandleAsync_ValidRequest_Returns202WithNotificationResponse()
    {
        var request = ValidRequest();
        SetupValidValidator(request);

        var result = await CallEndpoint(request);

        var created = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Created<NotificationResponse>>(result);
        Assert.NotNull(created.Value);
        Assert.Equal(request.Title, created.Value.Title);
        Assert.Equal(request.Body, created.Value.Body);
        Assert.Equal(request.RecipientUserId, created.Value.RecipientUserId);
    }

    [Fact]
    public async Task HandleAsync_ValidRequest_PersistsNotificationViaRepository()
    {
        var request = ValidRequest();
        SetupValidValidator(request);

        await CallEndpoint(request);

        await _repository.Received(1).AddAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ValidRequest_ParsesChannelStringsToEnum()
    {
        var request = ValidRequest() with
        {
            Channels = new Dictionary<string, string>
            {
                { "email", "user@example.com" },
                { "sms", "+1234567890" }
            }
        };
        SetupValidValidator(request);

        var result = await CallEndpoint(request);

        var created = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Created<NotificationResponse>>(result);
        Assert.Equal(2, created.Value!.Deliveries.Count);
        Assert.Contains(created.Value.Deliveries, d => d.Channel == "email");
        Assert.Contains(created.Value.Deliveries, d => d.Channel == "sms");
    }

    [Fact]
    public async Task HandleAsync_InvalidRequest_ReturnsValidationProblem()
    {
        var request = ValidRequest() with { Title = "" };
        _validator.ValidateAsync(request, Arg.Any<CancellationToken>())
            .Returns(new ValidationResult(new[] { new ValidationFailure("Title", "Title is required.") }));

        var result = await CallEndpoint(request);

        var problem = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult>(result);
        Assert.Equal(400, problem.StatusCode);
    }

    [Fact]
    public async Task HandleAsync_InvalidRequest_DoesNotCallRepository()
    {
        var request = ValidRequest() with { Title = "" };
        _validator.ValidateAsync(request, Arg.Any<CancellationToken>())
            .Returns(new ValidationResult(new[] { new ValidationFailure("Title", "Title is required.") }));

        await CallEndpoint(request);

        await _repository.DidNotReceive().AddAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>());
    }

    // --- SignalR push delivery tests ---

    [Fact]
    public async Task HandleAsync_WithPushChannel_SendsNewNotificationToSignalRGroup()
    {
        var userId = Guid.NewGuid();
        var request = new CreateNotificationRequest(userId, "Title", "Body",
            new Dictionary<string, string> { { "push", "device-token" } });
        SetupValidValidator(request);

        await CallEndpoint(request);

        await _clientProxy.Received(1).SendCoreAsync(
            "NewNotification",
            Arg.Any<object?[]>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithPushChannel_SendsUnreadCountUpdatedToSignalRGroup()
    {
        var userId = Guid.NewGuid();
        var request = new CreateNotificationRequest(userId, "Title", "Body",
            new Dictionary<string, string> { { "push", "device-token" } });
        SetupValidValidator(request);
        _repository.GetUnreadCountAsync(userId, Arg.Any<CancellationToken>()).Returns(5);

        await CallEndpoint(request);

        await _clientProxy.Received(1).SendCoreAsync(
            "UnreadCountUpdated",
            Arg.Any<object?[]>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithPushChannel_MarksPushDeliveryAsSent()
    {
        var userId = Guid.NewGuid();
        var request = new CreateNotificationRequest(userId, "Title", "Body",
            new Dictionary<string, string> { { "push", "device-token" } });
        SetupValidValidator(request);

        var result = await CallEndpoint(request);

        var created = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Created<NotificationResponse>>(result);
        var pushDelivery = created.Value!.Deliveries.Single(d => d.Channel == "push");
        Assert.Equal("sent", pushDelivery.Status);
    }

    [Fact]
    public async Task HandleAsync_WithoutPushChannel_DoesNotSendSignalRMessages()
    {
        var request = ValidRequest(); // email only
        SetupValidValidator(request);

        await CallEndpoint(request);

        await _clientProxy.DidNotReceive().SendCoreAsync(
            Arg.Any<string>(),
            Arg.Any<object?[]>(),
            Arg.Any<CancellationToken>());
    }

    // --- MassTransit publishing tests ---

    [Fact]
    public async Task HandleAsync_WithEmailChannel_PublishesSendEmailMessage()
    {
        var request = ValidRequest(); // email channel
        SetupValidValidator(request);

        await CallEndpoint(request);

        await _publishEndpoint.Received(1).Publish(
            Arg.Is<SendEmailMessage>(m =>
                m.Recipient == "user@example.com" &&
                m.Title == request.Title &&
                m.Body == request.Body),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithSmsChannel_PublishesSendSmsMessage()
    {
        var request = new CreateNotificationRequest(Guid.NewGuid(), "Title", "Body",
            new Dictionary<string, string> { { "sms", "+1234567890" } });
        SetupValidValidator(request);

        await CallEndpoint(request);

        await _publishEndpoint.Received(1).Publish(
            Arg.Is<SendSmsMessage>(m => m.Recipient == "+1234567890"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithWhatsAppChannel_PublishesSendWhatsAppMessage()
    {
        var request = new CreateNotificationRequest(Guid.NewGuid(), "Title", "Body",
            new Dictionary<string, string> { { "whatsapp", "+1234567890" } });
        SetupValidValidator(request);

        await CallEndpoint(request);

        await _publishEndpoint.Received(1).Publish(
            Arg.Is<SendWhatsAppMessage>(m => m.Recipient == "+1234567890"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithPushOnly_DoesNotPublishAnyMessage()
    {
        var request = new CreateNotificationRequest(Guid.NewGuid(), "Title", "Body",
            new Dictionary<string, string> { { "push", "device-token" } });
        SetupValidValidator(request);

        await CallEndpoint(request);

        await _publishEndpoint.DidNotReceive().Publish(
            Arg.Any<SendEmailMessage>(), Arg.Any<CancellationToken>());
        await _publishEndpoint.DidNotReceive().Publish(
            Arg.Any<SendSmsMessage>(), Arg.Any<CancellationToken>());
        await _publishEndpoint.DidNotReceive().Publish(
            Arg.Any<SendWhatsAppMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithMultipleAsyncChannels_PublishesAllMessages()
    {
        var request = new CreateNotificationRequest(Guid.NewGuid(), "Title", "Body",
            new Dictionary<string, string>
            {
                { "email", "user@example.com" },
                { "sms", "+1234567890" }
            });
        SetupValidValidator(request);

        await CallEndpoint(request);

        await _publishEndpoint.Received(1).Publish(
            Arg.Any<SendEmailMessage>(), Arg.Any<CancellationToken>());
        await _publishEndpoint.Received(1).Publish(
            Arg.Any<SendSmsMessage>(), Arg.Any<CancellationToken>());
    }
}
