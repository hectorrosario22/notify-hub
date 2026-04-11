using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NotifyHub.Contracts.Messages;
using NotifyHub.Core.Entities;
using NotifyHub.Core.Enums;
using NotifyHub.Core.Repositories;
using NotifyHub.Tests.Integration.Fixtures;
using NotifyHub.Worker.Email.Consumers;
using NotifyHub.Worker.Email.Services;

namespace NotifyHub.Tests.Integration.Workers;

[Collection(IntegrationTestCollection.Name)]
public class SendEmailConsumerTests(NotifyHubApiFactory factory)
{
    [Fact]
    public async Task Consume_ValidMessage_MarksDeliveryAsSent()
    {
        using var scope = factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<INotificationRepository>();

        var notification = Notification.Create(
            Guid.NewGuid(),
            "Email Integration Test",
            "Body",
            new Dictionary<Channel, string>
            {
                { Channel.Push, "device-token" },
                { Channel.Email, "test@example.com" }
            });
        await repo.AddAsync(notification);

        var emailDelivery = notification.Deliveries.First(d => d.Channel == Channel.Email);

        await using var provider = new ServiceCollection()
            .AddScoped<IEmailSender, FakeEmailSender>()
            .AddScoped<INotificationRepository>(_ =>
            {
                var s = factory.Services.CreateScope();
                return s.ServiceProvider.GetRequiredService<INotificationRepository>();
            })
            .AddLogging(b => b.AddConsole())
            .AddMassTransitTestHarness(cfg =>
            {
                cfg.AddConsumer<SendEmailConsumer>();
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        await harness.Bus.Publish(new SendEmailMessage(
            notification.Id,
            emailDelivery.Id,
            "test@example.com",
            "Email Integration Test",
            "Body"));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        Assert.True(await harness.Consumed.Any<SendEmailMessage>(cts.Token));

        await harness.Stop();

        using var verifyScope = factory.Services.CreateScope();
        var verifyRepo = verifyScope.ServiceProvider.GetRequiredService<INotificationRepository>();
        var updated = await verifyRepo.GetByIdAsync(notification.Id);
        var updatedDelivery = updated!.Deliveries.First(d => d.Id == emailDelivery.Id);
        Assert.Equal(DeliveryStatus.Sent, updatedDelivery.Status);
    }
}
