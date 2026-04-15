using NotifyHub.Contracts.Responses;
using NotifyHub.Core.Repositories;

namespace NotifyHub.Api.Endpoints;

public sealed class GetUnreadCountEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/notifications/unread-count", HandleAsync)
            .WithName("GetUnreadCount")
            .Produces<UnreadCountResponse>();
    }

    public static async Task<IResult> HandleAsync(
        Guid userId,
        INotificationRepository repository,
        CancellationToken ct)
    {
        var count = await repository.GetUnreadCountAsync(userId, ct);
        return Results.Ok(new UnreadCountResponse(count));
    }
}
