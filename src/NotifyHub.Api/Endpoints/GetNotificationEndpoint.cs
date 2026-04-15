using NotifyHub.Api.Mapping;
using NotifyHub.Contracts.Responses;
using NotifyHub.Core.Repositories;

namespace NotifyHub.Api.Endpoints;

public sealed class GetNotificationEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/notifications/{id:guid}", HandleAsync)
            .WithName("GetNotification")
            .Produces<NotificationResponse>()
            .Produces(StatusCodes.Status404NotFound);
    }

    public static async Task<IResult> HandleAsync(
        Guid id,
        INotificationRepository repository,
        CancellationToken ct)
    {
        var notification = await repository.GetByIdAsync(id, ct);
        if (notification is null)
            return Results.NotFound();

        return Results.Ok(NotificationMapper.ToResponse(notification));
    }
}
