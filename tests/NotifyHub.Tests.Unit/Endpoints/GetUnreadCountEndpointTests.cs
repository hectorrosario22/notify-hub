using NSubstitute;
using NotifyHub.Api.Endpoints;
using NotifyHub.Contracts.Responses;
using NotifyHub.Core.Repositories;

namespace NotifyHub.Tests.Unit.Endpoints;

public class GetUnreadCountEndpointTests
{
    private readonly INotificationRepository _repository = Substitute.For<INotificationRepository>();

    [Fact]
    public async Task HandleAsync_ReturnsUnreadCountResponse()
    {
        var userId = Guid.NewGuid();
        _repository.GetUnreadCountAsync(userId, Arg.Any<CancellationToken>()).Returns(5);

        var result = await GetUnreadCountEndpoint.HandleAsync(userId, _repository, CancellationToken.None);

        var ok = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Ok<UnreadCountResponse>>(result);
        Assert.Equal(5, ok.Value!.Count);
    }
}
