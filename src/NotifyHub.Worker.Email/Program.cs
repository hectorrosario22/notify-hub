using Rebus.Config;
using NotifyHub.Infrastructure;
using NotifyHub.Worker.Email.Handlers;
using NotifyHub.Worker.Email.Services;
using Rebus.Retry.Simple;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<IEmailSender, FakeEmailSender>();

var rabbitUser = builder.Configuration["RabbitMQ:Username"] ?? "guest";
var rabbitPass = builder.Configuration["RabbitMQ:Password"] ?? "guest";
var rabbitHost = builder.Configuration["RabbitMQ:Host"] ?? "localhost";
var connectionString = $"amqp://{rabbitUser}:{rabbitPass}@{rabbitHost}";

builder.Services.AddRebus(configure => configure
    .Transport(t => t.UseRabbitMq(connectionString, "email-notifications"))
    .Options(o => o.RetryStrategy(
        errorQueueName: "email-notifications-error",
        maxDeliveryAttempts: 3)));

builder.Services.AutoRegisterHandlersFromAssemblyOf<SendEmailHandler>();

var host = builder.Build();
host.Run();
