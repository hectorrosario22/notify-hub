using NSubstitute;
using NotifyHub.Api.Endpoints;
using NotifyHub.Contracts.Responses;
using NotifyHub.Core.Entities;
using NotifyHub.Core.Enums;
using NotifyHub.Core.Repositories;

namespace NotifyHub.Tests.Unit.Endpoints;

public class ListNotificationsEndpointTests
{
    private readonly INotificationRepository _repository = Substitute.For<INotificationRepository>();

    [Fact]
    public async Task HandleAsync_ReturnsPagedResponse()
    {
        var userId = Guid.NewGuid();
        var notification = Notification.Create(userId, "Title", "Body",
            new Dictionary<Channel, string> { { Channel.Email, "user@example.com" } });
        _repository.GetByUserIdAsync(userId, 1, 20, false, Arg.Any<CancellationToken>())
            .Returns((new[] { notification } as IReadOnlyList<Notification>, 1));

        var result = await ListNotificationsEndpoint.HandleAsync(userId, 1, 20, false, _repository, CancellationToken.None);

        var ok = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Ok<PagedResponse<NotificationResponse>>>(result);
        Assert.Single(ok.Value!.Items);
        Assert.Equal(1, ok.Value.TotalCount);
    }

    [Fact]
    public async Task HandleAsync_PassesParametersToRepository()
    {
        var userId = Guid.NewGuid();
        _repository.GetByUserIdAsync(userId, 2, 10, true, Arg.Any<CancellationToken>())
            .Returns((Array.Empty<Notification>() as IReadOnlyList<Notification>, 0));

        await ListNotificationsEndpoint.HandleAsync(userId, 2, 10, true, _repository, CancellationToken.None);

        await _repository.Received(1).GetByUserIdAsync(userId, 2, 10, true, Arg.Any<CancellationToken>());
    }
}
