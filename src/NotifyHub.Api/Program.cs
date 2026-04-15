using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Rebus.Config;
using Rebus.Routing.TypeBased;
using NotifyHub.Api.Endpoints;
using NotifyHub.Api.Middleware;
using NotifyHub.Contracts.Messages;
using NotifyHub.Infrastructure;
using NotifyHub.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

builder.Services.AddOpenApi();
builder.Services.AddSignalR();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddInfrastructure(builder.Configuration);

var rabbitUser = builder.Configuration["RabbitMQ:Username"] ?? "guest";
var rabbitPass = builder.Configuration["RabbitMQ:Password"] ?? "guest";
var rabbitHost = builder.Configuration["RabbitMQ:Host"] ?? "localhost";
var connectionString = $"amqp://{rabbitUser}:{rabbitPass}@{rabbitHost}";

builder.Services.AddRebus(configure => configure
    .Transport(t => t.UseRabbitMqAsOneWayClient(connectionString))
    .Routing(r => r.TypeBased()
        .Map<SendEmailMessage>("email-notifications")
        .Map<SendSmsMessage>("sms-notifications")
        .Map<SendWhatsAppMessage>("whatsapp-notifications")));

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
app.UseCors();
app.UseHttpsRedirection();
app.MapEndpoints();
app.MapHub<NotifyHub.Api.Hubs.NotificationsHub>("/hubs/notifications");

app.Run();

public partial class Program { }
