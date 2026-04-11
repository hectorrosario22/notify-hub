using NotifyHub.Api.Mapping;
using NotifyHub.Contracts.Responses;
using NotifyHub.Core.Repositories;

namespace NotifyHub.Api.Endpoints;

public sealed class ListNotificationsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/notifications", HandleAsync)
            .WithName("ListNotifications")
            .Produces<PagedResponse<NotificationResponse>>()
            .WithOpenApi();
    }

    public static async Task<IResult> HandleAsync(
        Guid userId,
        int page,
        int pageSize,
        bool unreadOnly,
        INotificationRepository repository,
        CancellationToken ct)
    {
        var (notifications, totalCount) = await repository.GetByUserIdAsync(userId, page, pageSize, unreadOnly, ct);

        var items = notifications.Select(NotificationMapper.ToResponse).ToList();
        var response = new PagedResponse<NotificationResponse>(items, totalCount, page, pageSize);
        return Results.Ok(response);
    }
}
