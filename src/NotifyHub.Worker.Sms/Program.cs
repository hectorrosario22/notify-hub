using Rebus.Config;
using NotifyHub.Infrastructure;
using NotifyHub.Worker.Sms.Handlers;
using NotifyHub.Worker.Sms.Services;
using Rebus.Retry.Simple;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<ISmsSender, FakeSmsSender>();

var rabbitUser = builder.Configuration["RabbitMQ:Username"] ?? "guest";
var rabbitPass = builder.Configuration["RabbitMQ:Password"] ?? "guest";
var rabbitHost = builder.Configuration["RabbitMQ:Host"] ?? "localhost";
var connectionString = $"amqp://{rabbitUser}:{rabbitPass}@{rabbitHost}";

builder.Services.AddRebus(configure => configure
    .Transport(t => t.UseRabbitMq(connectionString, "sms-notifications"))
    .Options(o => o.RetryStrategy(
        errorQueueName: "sms-notifications-error",
        maxDeliveryAttempts: 3)));

builder.Services.AutoRegisterHandlersFromAssemblyOf<SendSmsHandler>();

var host = builder.Build();
host.Run();
