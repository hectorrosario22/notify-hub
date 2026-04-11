using FluentValidation;
using FluentValidation.Results;
using NSubstitute;
using NotifyHub.Api.Endpoints;
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

    private static CreateNotificationRequest ValidRequest() => new(
        RecipientUserId: Guid.NewGuid(),
        Title: "Test Title",
        Body: "Test Body",
        Channels: new Dictionary<string, string> { { "email", "user@example.com" } });

    [Fact]
    public async Task HandleAsync_ValidRequest_Returns202WithNotificationResponse()
    {
        var request = ValidRequest();
        _validator.ValidateAsync(request, Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        var result = await CreateNotificationEndpoint.HandleAsync(request, _repository, _validator, CancellationToken.None);

        var created = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Created<NotificationResponse>>(result);
        Assert.NotNull(created.Value);
        Assert.Equal(request.Title, created.Value.Title);
        Assert.Equal(request.Body, created.Value.Body);
        Assert.Equal(request.RecipientUserId, created.Value.RecipientUserId);
        Assert.Equal("pending", created.Value.Status);
    }

    [Fact]
    public async Task HandleAsync_ValidRequest_PersistsNotificationViaRepository()
    {
        var request = ValidRequest();
        _validator.ValidateAsync(request, Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        await CreateNotificationEndpoint.HandleAsync(request, _repository, _validator, CancellationToken.None);

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
        _validator.ValidateAsync(request, Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        var result = await CreateNotificationEndpoint.HandleAsync(request, _repository, _validator, CancellationToken.None);

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

        var result = await CreateNotificationEndpoint.HandleAsync(request, _repository, _validator, CancellationToken.None);

        var problem = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult>(result);
        Assert.Equal(400, problem.StatusCode);
    }

    [Fact]
    public async Task HandleAsync_InvalidRequest_DoesNotCallRepository()
    {
        var request = ValidRequest() with { Title = "" };
        _validator.ValidateAsync(request, Arg.Any<CancellationToken>())
            .Returns(new ValidationResult(new[] { new ValidationFailure("Title", "Title is required.") }));

        await CreateNotificationEndpoint.HandleAsync(request, _repository, _validator, CancellationToken.None);

        await _repository.DidNotReceive().AddAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>());
    }
}
