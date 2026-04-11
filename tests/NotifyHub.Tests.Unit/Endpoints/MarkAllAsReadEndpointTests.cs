using Microsoft.AspNetCore.SignalR;
using NSubstitute;
using NotifyHub.Api.Endpoints;
using NotifyHub.Api.Hubs;
using NotifyHub.Core.Repositories;

namespace NotifyHub.Tests.Unit.Endpoints;

public class MarkAllAsReadEndpointTests
{
    private readonly INotificationRepository _repository = Substitute.For<INotificationRepository>();
    private readonly IHubContext<NotificationsHub> _hubContext = Substitute.For<IHubContext<NotificationsHub>>();
    private readonly IClientProxy _clientProxy = Substitute.For<IClientProxy>();

    public MarkAllAsReadEndpointTests()
    {
        var hubClients = Substitute.For<IHubClients>();
        hubClients.Group(Arg.Any<string>()).Returns(_clientProxy);
        _hubContext.Clients.Returns(hubClients);
    }

    [Fact]
    public async Task HandleAsync_CallsMarkAllAsReadAsync_Returns204()
    {
        var userId = Guid.NewGuid();

        var result = await MarkAllAsReadEndpoint.HandleAsync(userId, _repository, _hubContext, CancellationToken.None);

        Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.NoContent>(result);
        await _repository.Received(1).MarkAllAsReadAsync(userId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_SendsUnreadCountUpdatedWithZero()
    {
        var userId = Guid.NewGuid();

        await MarkAllAsReadEndpoint.HandleAsync(userId, _repository, _hubContext, CancellationToken.None);

        await _clientProxy.Received(1).SendCoreAsync(
            "UnreadCountUpdated",
            Arg.Is<object?[]>(args => args.Length == 1),
            Arg.Any<CancellationToken>());
    }
}
