using Microsoft.AspNetCore.SignalR;
using NotifyHub.Api.Hubs;
using NotifyHub.Core.Repositories;

namespace NotifyHub.Api.Endpoints;

public sealed class MarkAllAsReadEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPatch("/notifications/read-all", HandleAsync)
            .WithName("MarkAllAsRead")
            .Produces(StatusCodes.Status204NoContent)
            .WithOpenApi();
    }

    public static async Task<IResult> HandleAsync(
        Guid userId,
        INotificationRepository repository,
        IHubContext<NotificationsHub> hubContext,
        CancellationToken ct)
    {
        await repository.MarkAllAsReadAsync(userId, ct);

        await hubContext.Clients.Group(userId.ToString())
            .SendAsync("UnreadCountUpdated", new { count = 0 }, ct);

        return Results.NoContent();
    }
}
