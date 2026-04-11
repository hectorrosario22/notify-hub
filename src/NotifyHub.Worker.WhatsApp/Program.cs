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
        cfg.Host(builder.Configuration["RabbitMQ:Host"] ?? "localhost", h =>
        {
            h.Username(builder.Configuration["RabbitMQ:Username"] ?? "guest");
            h.Password(builder.Configuration["RabbitMQ:Password"] ?? "guest");
        });
        cfg.ConfigureEndpoints(context);
    });
});

var host = builder.Build();
host.Run();
