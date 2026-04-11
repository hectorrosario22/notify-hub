using Microsoft.AspNetCore.SignalR;
using NotifyHub.Api.Hubs;
using NotifyHub.Core.Repositories;

namespace NotifyHub.Api.Endpoints;

public sealed class MarkAsReadEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPatch("/notifications/{id:guid}/read", HandleAsync)
            .WithName("MarkAsRead")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .WithOpenApi();
    }

    public static async Task<IResult> HandleAsync(
        Guid id,
        INotificationRepository repository,
        IHubContext<NotificationsHub> hubContext,
        CancellationToken ct)
    {
        var notification = await repository.GetByIdAsync(id, ct);
        if (notification is null)
            return Results.NotFound();

        notification.MarkAsRead();
        await repository.UpdateAsync(notification, ct);

        var unreadCount = await repository.GetUnreadCountAsync(notification.RecipientUserId, ct);
        await hubContext.Clients.Group(notification.RecipientUserId.ToString())
            .SendAsync("UnreadCountUpdated", new { count = unreadCount }, ct);

        return Results.NoContent();
    }
}
