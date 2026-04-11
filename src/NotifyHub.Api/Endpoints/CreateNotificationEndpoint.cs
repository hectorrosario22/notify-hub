using FluentValidation;
using MassTransit;
using Microsoft.AspNetCore.SignalR;
using NotifyHub.Api.Hubs;
using NotifyHub.Api.Mapping;
using NotifyHub.Contracts.Messages;
using NotifyHub.Contracts.Requests;
using NotifyHub.Contracts.Responses;
using NotifyHub.Core.Entities;
using NotifyHub.Core.Enums;
using NotifyHub.Core.Repositories;

namespace NotifyHub.Api.Endpoints;

public sealed class CreateNotificationEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/notifications", HandleAsync)
            .WithName("CreateNotification")
            .Produces<NotificationResponse>(StatusCodes.Status202Accepted)
            .ProducesValidationProblem()
            .WithOpenApi();
    }

    public static async Task<IResult> HandleAsync(
        CreateNotificationRequest request,
        INotificationRepository repository,
        IValidator<CreateNotificationRequest> validator,
        IHubContext<NotificationsHub> hubContext,
        IPublishEndpoint publishEndpoint,
        CancellationToken ct)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            return Results.ValidationProblem(errors);
        }

        var channelRecipients = request.Channels.ToDictionary(
            kv => Enum.Parse<Channel>(kv.Key, ignoreCase: true),
            kv => kv.Value);

        var notification = Notification.Create(
            request.RecipientUserId,
            request.Title,
            request.Body,
            channelRecipients);

        await repository.AddAsync(notification, ct);

        // Publish async channel messages to RabbitMQ
        foreach (var delivery in notification.Deliveries)
        {
            switch (delivery.Channel)
            {
                case Channel.Email:
                    await publishEndpoint.Publish(new SendEmailMessage(
                        notification.Id, delivery.Id, delivery.Recipient, notification.Title, notification.Body), ct);
                    break;
                case Channel.Sms:
                    await publishEndpoint.Publish(new SendSmsMessage(
                        notification.Id, delivery.Id, delivery.Recipient, notification.Body), ct);
                    break;
                case Channel.WhatsApp:
                    await publishEndpoint.Publish(new SendWhatsAppMessage(
                        notification.Id, delivery.Id, delivery.Recipient, notification.Body), ct);
                    break;
            }
        }

        // Handle push delivery synchronously via SignalR
        var pushDelivery = notification.Deliveries.FirstOrDefault(d => d.Channel == Channel.Push);
        if (pushDelivery is not null)
        {
            pushDelivery.MarkAsSent();
            await repository.UpdateAsync(notification, ct);

            var group = hubContext.Clients.Group(request.RecipientUserId.ToString());
            var response = NotificationMapper.ToResponse(notification);

            await group.SendAsync("NewNotification", response, ct);

            var unreadCount = await repository.GetUnreadCountAsync(request.RecipientUserId, ct);
            await group.SendAsync("UnreadCountUpdated", new { count = unreadCount }, ct);

            return Results.Created($"/notifications/{notification.Id}", response);
        }

        var result = NotificationMapper.ToResponse(notification);
        return Results.Created($"/notifications/{notification.Id}", result);
    }
}
