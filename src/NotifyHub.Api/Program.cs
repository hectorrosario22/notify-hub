using FluentValidation;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using NotifyHub.Api.Endpoints;
using NotifyHub.Api.Middleware;
using NotifyHub.Infrastructure;
using NotifyHub.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddSignalR();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((context, cfg) =>
    {
        var rabbitHost = builder.Configuration["RabbitMQ:Host"] ?? "localhost";
        var rabbitPort = builder.Configuration["RabbitMQ:Port"] ?? "5672";
        var rabbitUser = builder.Configuration["RabbitMQ:Username"] ?? "guest";
        var rabbitPass = builder.Configuration["RabbitMQ:Password"] ?? "guest";

        cfg.Host(new Uri($"rabbitmq://{rabbitHost}:{rabbitPort}"), h =>
        {
            h.Username(rabbitUser);
            h.Password(rabbitPass);
        });
        cfg.ConfigureEndpoints(context);
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider
        .GetRequiredService<NotifyHubDbContext>()
        .Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseMiddleware<ExceptionHandlerMiddleware>();
app.UseHttpsRedirection();
app.MapEndpoints();
app.MapHub<NotifyHub.Api.Hubs.NotificationsHub>("/hubs/notifications");

app.Run();

public partial class Program { }
