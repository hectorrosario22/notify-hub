using Microsoft.AspNetCore.Http;
using NSubstitute;
using NotifyHub.Api.Endpoints;
using NotifyHub.Contracts.Responses;
using NotifyHub.Core.Entities;
using NotifyHub.Core.Enums;
using NotifyHub.Core.Repositories;

namespace NotifyHub.Tests.Unit.Endpoints;

public class GetNotificationEndpointTests
{
    private readonly INotificationRepository _repository = Substitute.For<INotificationRepository>();

    [Fact]
    public async Task HandleAsync_ExistingNotification_Returns200WithResponse()
    {
        var notification = Notification.Create(Guid.NewGuid(), "Title", "Body",
            new Dictionary<Channel, string> { { Channel.Email, "user@example.com" } });
        _repository.GetByIdAsync(notification.Id, Arg.Any<CancellationToken>()).Returns(notification);

        var result = await GetNotificationEndpoint.HandleAsync(notification.Id, _repository, CancellationToken.None);

        var ok = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Ok<NotificationResponse>>(result);
        Assert.Equal(notification.Id, ok.Value!.Id);
    }

    [Fact]
    public async Task HandleAsync_NonExistentNotification_Returns404()
    {
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Notification?)null);

        var result = await GetNotificationEndpoint.HandleAsync(Guid.NewGuid(), _repository, CancellationToken.None);

        Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.NotFound>(result);
    }
}
