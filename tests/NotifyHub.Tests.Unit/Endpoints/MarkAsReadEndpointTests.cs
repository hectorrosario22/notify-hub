using Microsoft.AspNetCore.SignalR;
using NSubstitute;
using NotifyHub.Api.Endpoints;
using NotifyHub.Api.Hubs;
using NotifyHub.Core.Entities;
using NotifyHub.Core.Enums;
using NotifyHub.Core.Repositories;

namespace NotifyHub.Tests.Unit.Endpoints;

public class MarkAsReadEndpointTests
{
    private readonly INotificationRepository _repository = Substitute.For<INotificationRepository>();
    private readonly IHubContext<NotificationsHub> _hubContext = Substitute.For<IHubContext<NotificationsHub>>();
    private readonly IClientProxy _clientProxy = Substitute.For<IClientProxy>();

    public MarkAsReadEndpointTests()
    {
        var hubClients = Substitute.For<IHubClients>();
        hubClients.Group(Arg.Any<string>()).Returns(_clientProxy);
        _hubContext.Clients.Returns(hubClients);
    }

    [Fact]
    public async Task HandleAsync_ExistingWithPush_Returns204()
    {
        var notification = Notification.Create(Guid.NewGuid(), "Title", "Body",
            new Dictionary<Channel, string> { { Channel.Push, "device-token" } });
        _repository.GetByIdAsync(notification.Id, Arg.Any<CancellationToken>()).Returns(notification);

        var result = await MarkAsReadEndpoint.HandleAsync(notification.Id, _repository, _hubContext, CancellationToken.None);

        Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.NoContent>(result);
        Assert.True(notification.IsRead);
        await _repository.Received(1).UpdateAsync(notification, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_NonExistent_Returns404()
    {
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Notification?)null);

        var result = await MarkAsReadEndpoint.HandleAsync(Guid.NewGuid(), _repository, _hubContext, CancellationToken.None);

        Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.NotFound>(result);
    }

    [Fact]
    public async Task HandleAsync_WithPush_SendsUnreadCountUpdated()
    {
        var notification = Notification.Create(Guid.NewGuid(), "Title", "Body",
            new Dictionary<Channel, string> { { Channel.Push, "device-token" } });
        _repository.GetByIdAsync(notification.Id, Arg.Any<CancellationToken>()).Returns(notification);
        _repository.GetUnreadCountAsync(notification.RecipientUserId, Arg.Any<CancellationToken>()).Returns(3);

        await MarkAsReadEndpoint.HandleAsync(notification.Id, _repository, _hubContext, CancellationToken.None);

        await _clientProxy.Received(1).SendCoreAsync(
            "UnreadCountUpdated",
            Arg.Any<object?[]>(),
            Arg.Any<CancellationToken>());
    }
}
