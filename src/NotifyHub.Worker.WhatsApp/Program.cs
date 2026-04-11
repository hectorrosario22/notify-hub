using MassTransit;
using NotifyHub.Infrastructure;
using NotifyHub.Worker.WhatsApp.Consumers;
using NotifyHub.Worker.WhatsApp.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<IWhatsAppSender, FakeWhatsAppSender>();

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<SendWhatsAppConsumer>();
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

var host = builder.Build();
host.Run();
