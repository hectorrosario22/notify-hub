using Rebus.Config;
using NotifyHub.Infrastructure;
using NotifyHub.Worker.WhatsApp.Handlers;
using NotifyHub.Worker.WhatsApp.Services;
using Rebus.Retry.Simple;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<IWhatsAppSender, FakeWhatsAppSender>();

var rabbitUser = builder.Configuration["RabbitMQ:Username"] ?? "guest";
var rabbitPass = builder.Configuration["RabbitMQ:Password"] ?? "guest";
var rabbitHost = builder.Configuration["RabbitMQ:Host"] ?? "localhost";
var connectionString = $"amqp://{rabbitUser}:{rabbitPass}@{rabbitHost}";

builder.Services.AddRebus(configure => configure
    .Transport(t => t.UseRabbitMq(connectionString, "whatsapp-notifications"))
    .Options(o => o.RetryStrategy(
        errorQueueName: "whatsapp-notifications-error",
        maxDeliveryAttempts: 3)));

builder.Services.AutoRegisterHandlersFromAssemblyOf<SendWhatsAppHandler>();

var host = builder.Build();
host.Run();
