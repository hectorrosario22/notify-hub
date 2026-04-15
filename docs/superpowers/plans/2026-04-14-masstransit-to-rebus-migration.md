# MassTransit → Rebus Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace `MassTransit.RabbitMQ` with `Rebus` + `Rebus.RabbitMQ` across the API and 3 Worker projects, keeping RabbitMQ as the transport and using explicit queue names with retry support.

**Architecture:** The API is a one-way Rebus client (publish only) that routes messages to named queues via TypeBased routing. Each Worker hosts a full Rebus bus that listens on its own named queue and auto-registers its handler. Retries are configured per worker with a dead-letter queue.

**Tech Stack:** .NET 10, Rebus 8, Rebus.RabbitMQ, Rebus.ServiceProvider, RabbitMQ, NSubstitute (tests)

---

## File Map

| Action | Path |
|---|---|
| Modify | `src/NotifyHub.Api/NotifyHub.Api.csproj` |
| Modify | `src/NotifyHub.Worker.Email/NotifyHub.Worker.Email.csproj` |
| Modify | `src/NotifyHub.Worker.Sms/NotifyHub.Worker.Sms.csproj` |
| Modify | `src/NotifyHub.Worker.WhatsApp/NotifyHub.Worker.WhatsApp.csproj` |
| Create | `src/NotifyHub.Worker.Email/Handlers/SendEmailHandler.cs` |
| Create | `src/NotifyHub.Worker.Sms/Handlers/SendSmsHandler.cs` |
| Create | `src/NotifyHub.Worker.WhatsApp/Handlers/SendWhatsAppHandler.cs` |
| Delete | `src/NotifyHub.Worker.Email/Consumers/SendEmailConsumer.cs` |
| Delete | `src/NotifyHub.Worker.Sms/Consumers/SendSmsConsumer.cs` |
| Delete | `src/NotifyHub.Worker.WhatsApp/Consumers/SendWhatsAppConsumer.cs` |
| Modify | `src/NotifyHub.Api/Program.cs` |
| Modify | `src/NotifyHub.Worker.Email/Program.cs` |
| Modify | `src/NotifyHub.Worker.Sms/Program.cs` |
| Modify | `src/NotifyHub.Worker.WhatsApp/Program.cs` |
| Modify | `src/NotifyHub.Api/Endpoints/CreateNotificationEndpoint.cs` |
| Rename+Modify | `tests/NotifyHub.Tests.Unit/Workers/SendEmailConsumerTests.cs` → `SendEmailHandlerTests.cs` |
| Modify | `tests/NotifyHub.Tests.Unit/Endpoints/CreateNotificationEndpointTests.cs` |

---

## Task 1: Replace NuGet packages in all 4 projects

**Files:**
- Modify: `src/NotifyHub.Api/NotifyHub.Api.csproj`
- Modify: `src/NotifyHub.Worker.Email/NotifyHub.Worker.Email.csproj`
- Modify: `src/NotifyHub.Worker.Sms/NotifyHub.Worker.Sms.csproj`
- Modify: `src/NotifyHub.Worker.WhatsApp/NotifyHub.Worker.WhatsApp.csproj`

- [ ] **Step 1: Remove MassTransit and add Rebus packages in the API project**

```bash
cd src/NotifyHub.Api
dotnet remove package MassTransit.RabbitMQ
dotnet add package Rebus
dotnet add package Rebus.RabbitMQ
dotnet add package Rebus.ServiceProvider
```

- [ ] **Step 2: Remove MassTransit and add Rebus packages in Worker.Email**

```bash
cd ../NotifyHub.Worker.Email
dotnet remove package MassTransit.RabbitMQ
dotnet add package Rebus
dotnet add package Rebus.RabbitMQ
dotnet add package Rebus.ServiceProvider
```

- [ ] **Step 3: Remove MassTransit and add Rebus packages in Worker.Sms**

```bash
cd ../NotifyHub.Worker.Sms
dotnet remove package MassTransit.RabbitMQ
dotnet add package Rebus
dotnet add package Rebus.RabbitMQ
dotnet add package Rebus.ServiceProvider
```

- [ ] **Step 4: Remove MassTransit and add Rebus packages in Worker.WhatsApp**

```bash
cd ../NotifyHub.Worker.WhatsApp
dotnet remove package MassTransit.RabbitMQ
dotnet add package Rebus
dotnet add package Rebus.RabbitMQ
dotnet add package Rebus.ServiceProvider
```

- [ ] **Step 5: Verify packages are updated**

From the solution root:
```bash
cd /home/hrosario/dev/notify-hub
grep -r "MassTransit\|Rebus" src/**/*.csproj
```

Expected: no `MassTransit` lines; all 4 `.csproj` files show `Rebus`, `Rebus.RabbitMQ`, `Rebus.ServiceProvider`.

> **Note:** The solution won't build yet — the source files still reference MassTransit types. That's expected at this stage.

---

## Task 2: Email Handler — Write test first, then implement

**Files:**
- Create: `src/NotifyHub.Worker.Email/Handlers/SendEmailHandler.cs`
- Modify: `tests/NotifyHub.Tests.Unit/Workers/SendEmailConsumerTests.cs` → rename to `SendEmailHandlerTests.cs`

- [ ] **Step 1: Rename the test file and rewrite it for the new handler**

Replace the entire contents of `tests/NotifyHub.Tests.Unit/Workers/SendEmailConsumerTests.cs` and rename it to `SendEmailHandlerTests.cs`. In the IDE, rename the file, then set its contents to:

```csharp
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NotifyHub.Contracts.Messages;
using NotifyHub.Core.Entities;
using NotifyHub.Core.Enums;
using NotifyHub.Core.Repositories;
using NotifyHub.Worker.Email.Handlers;
using NotifyHub.Worker.Email.Services;

namespace NotifyHub.Tests.Unit.Workers;

public class SendEmailHandlerTests
{
    private readonly IEmailSender _emailSender = Substitute.For<IEmailSender>();
    private readonly INotificationRepository _repository = Substitute.For<INotificationRepository>();
    private readonly ILogger<SendEmailHandler> _logger = Substitute.For<ILogger<SendEmailHandler>>();
    private readonly SendEmailHandler _handler;

    public SendEmailHandlerTests()
    {
        _handler = new SendEmailHandler(_emailSender, _repository, _logger);
    }

    private static Notification CreateTestNotification(out Guid deliveryId)
    {
        var notification = Notification.Create(
            Guid.NewGuid(), "Title", "Body",
            new Dictionary<Channel, string> { { Channel.Email, "user@example.com" } });
        deliveryId = notification.Deliveries.First().Id;
        return notification;
    }

    [Fact]
    public async Task Handle_SuccessfulSend_MarksDeliveryAsSent()
    {
        var notification = CreateTestNotification(out var deliveryId);
        var message = new SendEmailMessage(notification.Id, deliveryId, "user@example.com", "Title", "Body");
        _repository.GetByIdAsync(notification.Id, Arg.Any<CancellationToken>()).Returns(notification);

        await _handler.Handle(message);

        var delivery = notification.Deliveries.First();
        Assert.Equal(DeliveryStatus.Sent, delivery.Status);
        await _repository.Received(1).UpdateAsync(notification, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SuccessfulSend_CallsEmailSender()
    {
        var notification = CreateTestNotification(out var deliveryId);
        var message = new SendEmailMessage(notification.Id, deliveryId, "user@example.com", "Title", "Body");
        _repository.GetByIdAsync(notification.Id, Arg.Any<CancellationToken>()).Returns(notification);

        await _handler.Handle(message);

        await _emailSender.Received(1).SendAsync("user@example.com", "Title", "Body", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_FailedSend_MarksDeliveryAsFailed()
    {
        var notification = CreateTestNotification(out var deliveryId);
        var message = new SendEmailMessage(notification.Id, deliveryId, "user@example.com", "Title", "Body");
        _repository.GetByIdAsync(notification.Id, Arg.Any<CancellationToken>()).Returns(notification);
        _emailSender.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("SMTP connection failed"));

        await _handler.Handle(message);

        var delivery = notification.Deliveries.First();
        Assert.Equal(DeliveryStatus.Failed, delivery.Status);
        Assert.Equal("SMTP connection failed", delivery.ErrorMessage);
        await _repository.Received(1).UpdateAsync(notification, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NotificationNotFound_DoesNotCallEmailSender()
    {
        var message = new SendEmailMessage(Guid.NewGuid(), Guid.NewGuid(), "user@example.com", "Title", "Body");
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Notification?)null);

        await _handler.Handle(message);

        await _emailSender.DidNotReceive().SendAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SuccessfulSend_RefreshesNotificationStatus()
    {
        var notification = CreateTestNotification(out var deliveryId);
        var message = new SendEmailMessage(notification.Id, deliveryId, "user@example.com", "Title", "Body");
        _repository.GetByIdAsync(notification.Id, Arg.Any<CancellationToken>()).Returns(notification);

        await _handler.Handle(message);

        Assert.Equal(NotificationStatus.Delivered, notification.Status);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail to compile**

```bash
dotnet test tests/NotifyHub.Tests.Unit --no-build 2>&1 | head -20
```

Expected: build error — `SendEmailHandler` type not found in `NotifyHub.Worker.Email.Handlers`.

- [ ] **Step 3: Create the handler**

Create `src/NotifyHub.Worker.Email/Handlers/SendEmailHandler.cs`:

```csharp
using Rebus.Handlers;
using NotifyHub.Contracts.Messages;
using NotifyHub.Core.Repositories;
using NotifyHub.Worker.Email.Services;

namespace NotifyHub.Worker.Email.Handlers;

public sealed class SendEmailHandler(
    IEmailSender emailSender,
    INotificationRepository repository,
    ILogger<SendEmailHandler> logger) : IHandleMessages<SendEmailMessage>
{
    public async Task Handle(SendEmailMessage message)
    {
        logger.LogInformation("Processing email delivery {DeliveryId} for notification {NotificationId}",
            message.DeliveryId, message.NotificationId);

        var notification = await repository.GetByIdAsync(message.NotificationId, CancellationToken.None);
        if (notification is null)
        {
            logger.LogWarning("Notification {NotificationId} not found, skipping email delivery",
                message.NotificationId);
            return;
        }

        var delivery = notification.Deliveries.FirstOrDefault(d => d.Id == message.DeliveryId);
        if (delivery is null)
        {
            logger.LogWarning("Delivery {DeliveryId} not found for notification {NotificationId}",
                message.DeliveryId, message.NotificationId);
            return;
        }

        try
        {
            await emailSender.SendAsync(message.Recipient, message.Title, message.Body, CancellationToken.None);
            delivery.MarkAsSent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send email to {Recipient}", message.Recipient);
            delivery.MarkAsFailed(ex.Message);
        }

        notification.RefreshStatus();
        await repository.UpdateAsync(notification, CancellationToken.None);
    }
}
```

- [ ] **Step 4: Run the email handler tests**

```bash
dotnet test tests/NotifyHub.Tests.Unit --filter "FullyQualifiedName~SendEmailHandlerTests" -v
```

Expected: all 5 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add src/NotifyHub.Worker.Email/Handlers/SendEmailHandler.cs \
        tests/NotifyHub.Tests.Unit/Workers/SendEmailHandlerTests.cs
git commit -m "feat: migrate email worker from MassTransit consumer to Rebus handler"
```

---

## Task 3: SMS Handler — Write test first, then implement

**Files:**
- Create: `src/NotifyHub.Worker.Sms/Handlers/SendSmsHandler.cs`
- Create: `tests/NotifyHub.Tests.Unit/Workers/SendSmsHandlerTests.cs`

> The SMS worker never had a unit test file. This task creates one from scratch alongside the handler.

- [ ] **Step 1: Create the test file**

Create `tests/NotifyHub.Tests.Unit/Workers/SendSmsHandlerTests.cs`:

```csharp
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NotifyHub.Contracts.Messages;
using NotifyHub.Core.Entities;
using NotifyHub.Core.Enums;
using NotifyHub.Core.Repositories;
using NotifyHub.Worker.Sms.Handlers;
using NotifyHub.Worker.Sms.Services;

namespace NotifyHub.Tests.Unit.Workers;

public class SendSmsHandlerTests
{
    private readonly ISmsSender _smsSender = Substitute.For<ISmsSender>();
    private readonly INotificationRepository _repository = Substitute.For<INotificationRepository>();
    private readonly ILogger<SendSmsHandler> _logger = Substitute.For<ILogger<SendSmsHandler>>();
    private readonly SendSmsHandler _handler;

    public SendSmsHandlerTests()
    {
        _handler = new SendSmsHandler(_smsSender, _repository, _logger);
    }

    private static Notification CreateTestNotification(out Guid deliveryId)
    {
        var notification = Notification.Create(
            Guid.NewGuid(), "Title", "Body",
            new Dictionary<Channel, string> { { Channel.Sms, "+1234567890" } });
        deliveryId = notification.Deliveries.First().Id;
        return notification;
    }

    [Fact]
    public async Task Handle_SuccessfulSend_MarksDeliveryAsSent()
    {
        var notification = CreateTestNotification(out var deliveryId);
        var message = new SendSmsMessage(notification.Id, deliveryId, "+1234567890", "Body");
        _repository.GetByIdAsync(notification.Id, Arg.Any<CancellationToken>()).Returns(notification);

        await _handler.Handle(message);

        var delivery = notification.Deliveries.First();
        Assert.Equal(DeliveryStatus.Sent, delivery.Status);
        await _repository.Received(1).UpdateAsync(notification, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SuccessfulSend_CallsSmsSender()
    {
        var notification = CreateTestNotification(out var deliveryId);
        var message = new SendSmsMessage(notification.Id, deliveryId, "+1234567890", "Body");
        _repository.GetByIdAsync(notification.Id, Arg.Any<CancellationToken>()).Returns(notification);

        await _handler.Handle(message);

        await _smsSender.Received(1).SendAsync("+1234567890", "Body", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_FailedSend_MarksDeliveryAsFailed()
    {
        var notification = CreateTestNotification(out var deliveryId);
        var message = new SendSmsMessage(notification.Id, deliveryId, "+1234567890", "Body");
        _repository.GetByIdAsync(notification.Id, Arg.Any<CancellationToken>()).Returns(notification);
        _smsSender.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("SMS gateway error"));

        await _handler.Handle(message);

        var delivery = notification.Deliveries.First();
        Assert.Equal(DeliveryStatus.Failed, delivery.Status);
        Assert.Equal("SMS gateway error", delivery.ErrorMessage);
        await _repository.Received(1).UpdateAsync(notification, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NotificationNotFound_DoesNotCallSmsSender()
    {
        var message = new SendSmsMessage(Guid.NewGuid(), Guid.NewGuid(), "+1234567890", "Body");
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Notification?)null);

        await _handler.Handle(message);

        await _smsSender.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SuccessfulSend_RefreshesNotificationStatus()
    {
        var notification = CreateTestNotification(out var deliveryId);
        var message = new SendSmsMessage(notification.Id, deliveryId, "+1234567890", "Body");
        _repository.GetByIdAsync(notification.Id, Arg.Any<CancellationToken>()).Returns(notification);

        await _handler.Handle(message);

        Assert.Equal(NotificationStatus.Delivered, notification.Status);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail to compile**

```bash
dotnet test tests/NotifyHub.Tests.Unit --no-build 2>&1 | head -20
```

Expected: build error — `SendSmsHandler` not found in `NotifyHub.Worker.Sms.Handlers`.

- [ ] **Step 3: Create the handler**

Create `src/NotifyHub.Worker.Sms/Handlers/SendSmsHandler.cs`:

```csharp
using Rebus.Handlers;
using NotifyHub.Contracts.Messages;
using NotifyHub.Core.Repositories;
using NotifyHub.Worker.Sms.Services;

namespace NotifyHub.Worker.Sms.Handlers;

public sealed class SendSmsHandler(
    ISmsSender smsSender,
    INotificationRepository repository,
    ILogger<SendSmsHandler> logger) : IHandleMessages<SendSmsMessage>
{
    public async Task Handle(SendSmsMessage message)
    {
        logger.LogInformation("Processing SMS delivery {DeliveryId} for notification {NotificationId}",
            message.DeliveryId, message.NotificationId);

        var notification = await repository.GetByIdAsync(message.NotificationId, CancellationToken.None);
        if (notification is null)
        {
            logger.LogWarning("Notification {NotificationId} not found, skipping SMS delivery",
                message.NotificationId);
            return;
        }

        var delivery = notification.Deliveries.FirstOrDefault(d => d.Id == message.DeliveryId);
        if (delivery is null)
        {
            logger.LogWarning("Delivery {DeliveryId} not found for notification {NotificationId}",
                message.DeliveryId, message.NotificationId);
            return;
        }

        try
        {
            await smsSender.SendAsync(message.Recipient, message.Body, CancellationToken.None);
            delivery.MarkAsSent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send SMS to {Recipient}", message.Recipient);
            delivery.MarkAsFailed(ex.Message);
        }

        notification.RefreshStatus();
        await repository.UpdateAsync(notification, CancellationToken.None);
    }
}
```

- [ ] **Step 4: Run the SMS handler tests**

```bash
dotnet test tests/NotifyHub.Tests.Unit --filter "FullyQualifiedName~SendSmsHandlerTests" -v
```

Expected: all 5 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add src/NotifyHub.Worker.Sms/Handlers/SendSmsHandler.cs \
        tests/NotifyHub.Tests.Unit/Workers/SendSmsHandlerTests.cs
git commit -m "feat: migrate SMS worker from MassTransit consumer to Rebus handler"
```

---

## Task 4: WhatsApp Handler — Write test first, then implement

**Files:**
- Create: `src/NotifyHub.Worker.WhatsApp/Handlers/SendWhatsAppHandler.cs`
- Create: `tests/NotifyHub.Tests.Unit/Workers/SendWhatsAppHandlerTests.cs`

- [ ] **Step 1: Create the test file**

Create `tests/NotifyHub.Tests.Unit/Workers/SendWhatsAppHandlerTests.cs`:

```csharp
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NotifyHub.Contracts.Messages;
using NotifyHub.Core.Entities;
using NotifyHub.Core.Enums;
using NotifyHub.Core.Repositories;
using NotifyHub.Worker.WhatsApp.Handlers;
using NotifyHub.Worker.WhatsApp.Services;

namespace NotifyHub.Tests.Unit.Workers;

public class SendWhatsAppHandlerTests
{
    private readonly IWhatsAppSender _whatsAppSender = Substitute.For<IWhatsAppSender>();
    private readonly INotificationRepository _repository = Substitute.For<INotificationRepository>();
    private readonly ILogger<SendWhatsAppHandler> _logger = Substitute.For<ILogger<SendWhatsAppHandler>>();
    private readonly SendWhatsAppHandler _handler;

    public SendWhatsAppHandlerTests()
    {
        _handler = new SendWhatsAppHandler(_whatsAppSender, _repository, _logger);
    }

    private static Notification CreateTestNotification(out Guid deliveryId)
    {
        var notification = Notification.Create(
            Guid.NewGuid(), "Title", "Body",
            new Dictionary<Channel, string> { { Channel.WhatsApp, "+1234567890" } });
        deliveryId = notification.Deliveries.First().Id;
        return notification;
    }

    [Fact]
    public async Task Handle_SuccessfulSend_MarksDeliveryAsSent()
    {
        var notification = CreateTestNotification(out var deliveryId);
        var message = new SendWhatsAppMessage(notification.Id, deliveryId, "+1234567890", "Body");
        _repository.GetByIdAsync(notification.Id, Arg.Any<CancellationToken>()).Returns(notification);

        await _handler.Handle(message);

        var delivery = notification.Deliveries.First();
        Assert.Equal(DeliveryStatus.Sent, delivery.Status);
        await _repository.Received(1).UpdateAsync(notification, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SuccessfulSend_CallsWhatsAppSender()
    {
        var notification = CreateTestNotification(out var deliveryId);
        var message = new SendWhatsAppMessage(notification.Id, deliveryId, "+1234567890", "Body");
        _repository.GetByIdAsync(notification.Id, Arg.Any<CancellationToken>()).Returns(notification);

        await _handler.Handle(message);

        await _whatsAppSender.Received(1).SendAsync("+1234567890", "Body", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_FailedSend_MarksDeliveryAsFailed()
    {
        var notification = CreateTestNotification(out var deliveryId);
        var message = new SendWhatsAppMessage(notification.Id, deliveryId, "+1234567890", "Body");
        _repository.GetByIdAsync(notification.Id, Arg.Any<CancellationToken>()).Returns(notification);
        _whatsAppSender.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("WhatsApp API error"));

        await _handler.Handle(message);

        var delivery = notification.Deliveries.First();
        Assert.Equal(DeliveryStatus.Failed, delivery.Status);
        Assert.Equal("WhatsApp API error", delivery.ErrorMessage);
        await _repository.Received(1).UpdateAsync(notification, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NotificationNotFound_DoesNotCallWhatsAppSender()
    {
        var message = new SendWhatsAppMessage(Guid.NewGuid(), Guid.NewGuid(), "+1234567890", "Body");
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Notification?)null);

        await _handler.Handle(message);

        await _whatsAppSender.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SuccessfulSend_RefreshesNotificationStatus()
    {
        var notification = CreateTestNotification(out var deliveryId);
        var message = new SendWhatsAppMessage(notification.Id, deliveryId, "+1234567890", "Body");
        _repository.GetByIdAsync(notification.Id, Arg.Any<CancellationToken>()).Returns(notification);

        await _handler.Handle(message);

        Assert.Equal(NotificationStatus.Delivered, notification.Status);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail to compile**

```bash
dotnet test tests/NotifyHub.Tests.Unit --no-build 2>&1 | head -20
```

Expected: build error — `SendWhatsAppHandler` not found in `NotifyHub.Worker.WhatsApp.Handlers`.

- [ ] **Step 3: Create the handler**

Create `src/NotifyHub.Worker.WhatsApp/Handlers/SendWhatsAppHandler.cs`:

```csharp
using Rebus.Handlers;
using NotifyHub.Contracts.Messages;
using NotifyHub.Core.Repositories;
using NotifyHub.Worker.WhatsApp.Services;

namespace NotifyHub.Worker.WhatsApp.Handlers;

public sealed class SendWhatsAppHandler(
    IWhatsAppSender whatsAppSender,
    INotificationRepository repository,
    ILogger<SendWhatsAppHandler> logger) : IHandleMessages<SendWhatsAppMessage>
{
    public async Task Handle(SendWhatsAppMessage message)
    {
        logger.LogInformation("Processing WhatsApp delivery {DeliveryId} for notification {NotificationId}",
            message.DeliveryId, message.NotificationId);

        var notification = await repository.GetByIdAsync(message.NotificationId, CancellationToken.None);
        if (notification is null)
        {
            logger.LogWarning("Notification {NotificationId} not found, skipping WhatsApp delivery",
                message.NotificationId);
            return;
        }

        var delivery = notification.Deliveries.FirstOrDefault(d => d.Id == message.DeliveryId);
        if (delivery is null)
        {
            logger.LogWarning("Delivery {DeliveryId} not found for notification {NotificationId}",
                message.DeliveryId, message.NotificationId);
            return;
        }

        try
        {
            await whatsAppSender.SendAsync(message.Recipient, message.Body, CancellationToken.None);
            delivery.MarkAsSent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send WhatsApp message to {Recipient}", message.Recipient);
            delivery.MarkAsFailed(ex.Message);
        }

        notification.RefreshStatus();
        await repository.UpdateAsync(notification, CancellationToken.None);
    }
}
```

- [ ] **Step 4: Run the WhatsApp handler tests**

```bash
dotnet test tests/NotifyHub.Tests.Unit --filter "FullyQualifiedName~SendWhatsAppHandlerTests" -v
```

Expected: all 5 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add src/NotifyHub.Worker.WhatsApp/Handlers/SendWhatsAppHandler.cs \
        tests/NotifyHub.Tests.Unit/Workers/SendWhatsAppHandlerTests.cs
git commit -m "feat: migrate WhatsApp worker from MassTransit consumer to Rebus handler"
```

---

## Task 5: Update API endpoint and its unit tests

**Files:**
- Modify: `src/NotifyHub.Api/Endpoints/CreateNotificationEndpoint.cs`
- Modify: `tests/NotifyHub.Tests.Unit/Endpoints/CreateNotificationEndpointTests.cs`

- [ ] **Step 1: Update the endpoint tests first**

Replace the entire contents of `tests/NotifyHub.Tests.Unit/Endpoints/CreateNotificationEndpointTests.cs`:

```csharp
using FluentValidation;
using FluentValidation.Results;
using Rebus.Bus;
using ValidationResult = FluentValidation.Results.ValidationResult;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using NSubstitute;
using NotifyHub.Api.Endpoints;
using NotifyHub.Api.Hubs;
using NotifyHub.Contracts.Messages;
using NotifyHub.Contracts.Requests;
using NotifyHub.Contracts.Responses;
using NotifyHub.Core.Entities;
using NotifyHub.Core.Enums;
using NotifyHub.Core.Repositories;

namespace NotifyHub.Tests.Unit.Endpoints;

public class CreateNotificationEndpointTests
{
    private readonly INotificationRepository _repository = Substitute.For<INotificationRepository>();
    private readonly IValidator<CreateNotificationRequest> _validator = Substitute.For<IValidator<CreateNotificationRequest>>();
    private readonly IHubContext<NotificationsHub> _hubContext = Substitute.For<IHubContext<NotificationsHub>>();
    private readonly IBus _bus = Substitute.For<IBus>();
    private readonly IClientProxy _clientProxy = Substitute.For<IClientProxy>();

    public CreateNotificationEndpointTests()
    {
        var hubClients = Substitute.For<IHubClients>();
        hubClients.Group(Arg.Any<string>()).Returns(_clientProxy);
        _hubContext.Clients.Returns(hubClients);
    }

    private static CreateNotificationRequest ValidRequest() => new(
        RecipientUserId: Guid.NewGuid(),
        Title: "Test Title",
        Body: "Test Body",
        Channels: new Dictionary<string, string> { { "email", "user@example.com" } });

    private void SetupValidValidator(CreateNotificationRequest request)
    {
        _validator.ValidateAsync(request, Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());
    }

    private Task<IResult> CallEndpoint(CreateNotificationRequest request) =>
        CreateNotificationEndpoint.HandleAsync(request, _repository, _validator, _hubContext, _bus, CancellationToken.None);

    [Fact]
    public async Task HandleAsync_ValidRequest_Returns202WithNotificationResponse()
    {
        var request = ValidRequest();
        SetupValidValidator(request);

        var result = await CallEndpoint(request);

        var created = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Created<NotificationResponse>>(result);
        Assert.NotNull(created.Value);
        Assert.Equal(request.Title, created.Value.Title);
        Assert.Equal(request.Body, created.Value.Body);
        Assert.Equal(request.RecipientUserId, created.Value.RecipientUserId);
    }

    [Fact]
    public async Task HandleAsync_ValidRequest_PersistsNotificationViaRepository()
    {
        var request = ValidRequest();
        SetupValidValidator(request);

        await CallEndpoint(request);

        await _repository.Received(1).AddAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ValidRequest_ParsesChannelStringsToEnum()
    {
        var request = ValidRequest() with
        {
            Channels = new Dictionary<string, string>
            {
                { "email", "user@example.com" },
                { "sms", "+1234567890" }
            }
        };
        SetupValidValidator(request);

        var result = await CallEndpoint(request);

        var created = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Created<NotificationResponse>>(result);
        Assert.Equal(2, created.Value!.Deliveries.Count);
        Assert.Contains(created.Value.Deliveries, d => d.Channel == "email");
        Assert.Contains(created.Value.Deliveries, d => d.Channel == "sms");
    }

    [Fact]
    public async Task HandleAsync_InvalidRequest_ReturnsValidationProblem()
    {
        var request = ValidRequest() with { Title = "" };
        _validator.ValidateAsync(request, Arg.Any<CancellationToken>())
            .Returns(new ValidationResult(new[] { new ValidationFailure("Title", "Title is required.") }));

        var result = await CallEndpoint(request);

        var problem = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult>(result);
        Assert.Equal(400, problem.StatusCode);
    }

    [Fact]
    public async Task HandleAsync_InvalidRequest_DoesNotCallRepository()
    {
        var request = ValidRequest() with { Title = "" };
        _validator.ValidateAsync(request, Arg.Any<CancellationToken>())
            .Returns(new ValidationResult(new[] { new ValidationFailure("Title", "Title is required.") }));

        await CallEndpoint(request);

        await _repository.DidNotReceive().AddAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>());
    }

    // --- SignalR push delivery tests ---

    [Fact]
    public async Task HandleAsync_WithPushChannel_SendsNewNotificationToSignalRGroup()
    {
        var userId = Guid.NewGuid();
        var request = new CreateNotificationRequest(userId, "Title", "Body",
            new Dictionary<string, string> { { "push", "device-token" } });
        SetupValidValidator(request);

        await CallEndpoint(request);

        await _clientProxy.Received(1).SendCoreAsync(
            "NewNotification",
            Arg.Any<object?[]>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithPushChannel_SendsUnreadCountUpdatedToSignalRGroup()
    {
        var userId = Guid.NewGuid();
        var request = new CreateNotificationRequest(userId, "Title", "Body",
            new Dictionary<string, string> { { "push", "device-token" } });
        SetupValidValidator(request);
        _repository.GetUnreadCountAsync(userId, Arg.Any<CancellationToken>()).Returns(5);

        await CallEndpoint(request);

        await _clientProxy.Received(1).SendCoreAsync(
            "UnreadCountUpdated",
            Arg.Any<object?[]>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithPushChannel_MarksPushDeliveryAsSent()
    {
        var userId = Guid.NewGuid();
        var request = new CreateNotificationRequest(userId, "Title", "Body",
            new Dictionary<string, string> { { "push", "device-token" } });
        SetupValidValidator(request);

        var result = await CallEndpoint(request);

        var created = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Created<NotificationResponse>>(result);
        var pushDelivery = created.Value!.Deliveries.Single(d => d.Channel == "push");
        Assert.Equal("sent", pushDelivery.Status);
    }

    [Fact]
    public async Task HandleAsync_WithoutPushChannel_DoesNotSendSignalRMessages()
    {
        var request = ValidRequest(); // email only
        SetupValidValidator(request);

        await CallEndpoint(request);

        await _clientProxy.DidNotReceive().SendCoreAsync(
            Arg.Any<string>(),
            Arg.Any<object?[]>(),
            Arg.Any<CancellationToken>());
    }

    // --- Rebus publishing tests ---

    [Fact]
    public async Task HandleAsync_WithEmailChannel_SendsSendEmailMessage()
    {
        var request = ValidRequest(); // email channel
        SetupValidValidator(request);

        await CallEndpoint(request);

        await _bus.Received(1).Send(
            Arg.Is<SendEmailMessage>(m =>
                m.Recipient == "user@example.com" &&
                m.Title == request.Title &&
                m.Body == request.Body),
            Arg.Any<Dictionary<string, string>>());
    }

    [Fact]
    public async Task HandleAsync_WithSmsChannel_SendsSendSmsMessage()
    {
        var request = new CreateNotificationRequest(Guid.NewGuid(), "Title", "Body",
            new Dictionary<string, string> { { "sms", "+1234567890" } });
        SetupValidValidator(request);

        await CallEndpoint(request);

        await _bus.Received(1).Send(
            Arg.Is<SendSmsMessage>(m => m.Recipient == "+1234567890"),
            Arg.Any<Dictionary<string, string>>());
    }

    [Fact]
    public async Task HandleAsync_WithWhatsAppChannel_SendsSendWhatsAppMessage()
    {
        var request = new CreateNotificationRequest(Guid.NewGuid(), "Title", "Body",
            new Dictionary<string, string> { { "whatsapp", "+1234567890" } });
        SetupValidValidator(request);

        await CallEndpoint(request);

        await _bus.Received(1).Send(
            Arg.Is<SendWhatsAppMessage>(m => m.Recipient == "+1234567890"),
            Arg.Any<Dictionary<string, string>>());
    }

    [Fact]
    public async Task HandleAsync_WithPushOnly_DoesNotSendAnyMessage()
    {
        var request = new CreateNotificationRequest(Guid.NewGuid(), "Title", "Body",
            new Dictionary<string, string> { { "push", "device-token" } });
        SetupValidValidator(request);

        await CallEndpoint(request);

        await _bus.DidNotReceive().Send(Arg.Any<SendEmailMessage>(), Arg.Any<Dictionary<string, string>>());
        await _bus.DidNotReceive().Send(Arg.Any<SendSmsMessage>(), Arg.Any<Dictionary<string, string>>());
        await _bus.DidNotReceive().Send(Arg.Any<SendWhatsAppMessage>(), Arg.Any<Dictionary<string, string>>());
    }

    [Fact]
    public async Task HandleAsync_WithMultipleAsyncChannels_SendsAllMessages()
    {
        var request = new CreateNotificationRequest(Guid.NewGuid(), "Title", "Body",
            new Dictionary<string, string>
            {
                { "email", "user@example.com" },
                { "sms", "+1234567890" }
            });
        SetupValidValidator(request);

        await CallEndpoint(request);

        await _bus.Received(1).Send(Arg.Any<SendEmailMessage>(), Arg.Any<Dictionary<string, string>>());
        await _bus.Received(1).Send(Arg.Any<SendSmsMessage>(), Arg.Any<Dictionary<string, string>>());
    }
}
```

- [ ] **Step 2: Run the endpoint tests to verify they fail**

```bash
dotnet test tests/NotifyHub.Tests.Unit --filter "FullyQualifiedName~CreateNotificationEndpointTests" -v 2>&1 | head -30
```

Expected: build error — `CreateNotificationEndpoint.HandleAsync` still expects `IPublishEndpoint`.

- [ ] **Step 3: Update the endpoint**

Replace the entire contents of `src/NotifyHub.Api/Endpoints/CreateNotificationEndpoint.cs`:

```csharp
using FluentValidation;
using Microsoft.AspNetCore.SignalR;
using NotifyHub.Api.Hubs;
using NotifyHub.Api.Mapping;
using NotifyHub.Contracts.Messages;
using NotifyHub.Contracts.Requests;
using NotifyHub.Contracts.Responses;
using NotifyHub.Core.Entities;
using NotifyHub.Core.Enums;
using NotifyHub.Core.Repositories;
using Rebus.Bus;

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
        IBus bus,
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

        foreach (var delivery in notification.Deliveries)
        {
            switch (delivery.Channel)
            {
                case Channel.Email:
                    await bus.Send(new SendEmailMessage(
                        notification.Id, delivery.Id, delivery.Recipient, notification.Title, notification.Body));
                    break;
                case Channel.Sms:
                    await bus.Send(new SendSmsMessage(
                        notification.Id, delivery.Id, delivery.Recipient, notification.Body));
                    break;
                case Channel.WhatsApp:
                    await bus.Send(new SendWhatsAppMessage(
                        notification.Id, delivery.Id, delivery.Recipient, notification.Body));
                    break;
            }
        }

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
```

- [ ] **Step 4: Run the endpoint tests**

```bash
dotnet test tests/NotifyHub.Tests.Unit --filter "FullyQualifiedName~CreateNotificationEndpointTests" -v
```

Expected: all tests PASS.

- [ ] **Step 5: Commit**

```bash
git add src/NotifyHub.Api/Endpoints/CreateNotificationEndpoint.cs \
        tests/NotifyHub.Tests.Unit/Endpoints/CreateNotificationEndpointTests.cs
git commit -m "feat: replace IPublishEndpoint with IBus in notification endpoint"
```

---

## Task 6: Configure Rebus in the 3 Worker Program.cs files

**Files:**
- Modify: `src/NotifyHub.Worker.Email/Program.cs`
- Modify: `src/NotifyHub.Worker.Sms/Program.cs`
- Modify: `src/NotifyHub.Worker.WhatsApp/Program.cs`

- [ ] **Step 1: Update Worker.Email Program.cs**

Replace the entire contents of `src/NotifyHub.Worker.Email/Program.cs`:

```csharp
using NotifyHub.Infrastructure;
using NotifyHub.Worker.Email.Handlers;
using NotifyHub.Worker.Email.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<IEmailSender, FakeEmailSender>();

var rabbitHost = builder.Configuration["RabbitMQ:Host"] ?? "localhost";
var rabbitUser = builder.Configuration["RabbitMQ:Username"] ?? "guest";
var rabbitPass = builder.Configuration["RabbitMQ:Password"] ?? "guest";
var connectionString = $"amqp://{rabbitUser}:{rabbitPass}@{rabbitHost}";

builder.Services.AddRebus(configure => configure
    .Transport(t => t.UseRabbitMq(connectionString, "email-notifications"))
    .Options(o => o.SimpleRetryStrategy(
        errorQueueAddress: "email-notifications-error",
        maxDeliveryAttempts: 3))
);

builder.Services.AutoRegisterHandlersFromAssemblyOf<SendEmailHandler>();

var host = builder.Build();
host.Run();
```

- [ ] **Step 2: Update Worker.Sms Program.cs**

Replace the entire contents of `src/NotifyHub.Worker.Sms/Program.cs`:

```csharp
using NotifyHub.Infrastructure;
using NotifyHub.Worker.Sms.Handlers;
using NotifyHub.Worker.Sms.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<ISmsSender, FakeSmsSender>();

var rabbitHost = builder.Configuration["RabbitMQ:Host"] ?? "localhost";
var rabbitUser = builder.Configuration["RabbitMQ:Username"] ?? "guest";
var rabbitPass = builder.Configuration["RabbitMQ:Password"] ?? "guest";
var connectionString = $"amqp://{rabbitUser}:{rabbitPass}@{rabbitHost}";

builder.Services.AddRebus(configure => configure
    .Transport(t => t.UseRabbitMq(connectionString, "sms-notifications"))
    .Options(o => o.SimpleRetryStrategy(
        errorQueueAddress: "sms-notifications-error",
        maxDeliveryAttempts: 3))
);

builder.Services.AutoRegisterHandlersFromAssemblyOf<SendSmsHandler>();

var host = builder.Build();
host.Run();
```

- [ ] **Step 3: Update Worker.WhatsApp Program.cs**

Replace the entire contents of `src/NotifyHub.Worker.WhatsApp/Program.cs`:

```csharp
using NotifyHub.Infrastructure;
using NotifyHub.Worker.WhatsApp.Handlers;
using NotifyHub.Worker.WhatsApp.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<IWhatsAppSender, FakeWhatsAppSender>();

var rabbitHost = builder.Configuration["RabbitMQ:Host"] ?? "localhost";
var rabbitUser = builder.Configuration["RabbitMQ:Username"] ?? "guest";
var rabbitPass = builder.Configuration["RabbitMQ:Password"] ?? "guest";
var connectionString = $"amqp://{rabbitUser}:{rabbitPass}@{rabbitHost}";

builder.Services.AddRebus(configure => configure
    .Transport(t => t.UseRabbitMq(connectionString, "whatsapp-notifications"))
    .Options(o => o.SimpleRetryStrategy(
        errorQueueAddress: "whatsapp-notifications-error",
        maxDeliveryAttempts: 3))
);

builder.Services.AutoRegisterHandlersFromAssemblyOf<SendWhatsAppHandler>();

var host = builder.Build();
host.Run();
```

- [ ] **Step 4: Verify Worker sender class names**

Before committing, verify the fake sender class names used above match what's in each worker project:

```bash
grep -r "class Fake" src/NotifyHub.Worker.*/
```

Expected output (class names to confirm):
- `src/NotifyHub.Worker.Email/`: `FakeEmailSender`
- `src/NotifyHub.Worker.Sms/`: `FakeSmsSender`
- `src/NotifyHub.Worker.WhatsApp/`: `FakeWhatsAppSender`

If any class name differs from the above, update the `Program.cs` accordingly before committing.

- [ ] **Step 5: Commit**

```bash
git add src/NotifyHub.Worker.Email/Program.cs \
        src/NotifyHub.Worker.Sms/Program.cs \
        src/NotifyHub.Worker.WhatsApp/Program.cs
git commit -m "feat: configure Rebus in all worker Program.cs files"
```

---

## Task 7: Configure Rebus in the API Program.cs

**Files:**
- Modify: `src/NotifyHub.Api/Program.cs`

- [ ] **Step 1: Update API Program.cs**

Replace the entire contents of `src/NotifyHub.Api/Program.cs`:

```csharp
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NotifyHub.Api.Endpoints;
using NotifyHub.Api.Middleware;
using NotifyHub.Contracts.Messages;
using NotifyHub.Infrastructure;
using NotifyHub.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddSignalR();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddInfrastructure(builder.Configuration);

var rabbitHost = builder.Configuration["RabbitMQ:Host"] ?? "localhost";
var rabbitUser = builder.Configuration["RabbitMQ:Username"] ?? "guest";
var rabbitPass = builder.Configuration["RabbitMQ:Password"] ?? "guest";
var connectionString = $"amqp://{rabbitUser}:{rabbitPass}@{rabbitHost}";

builder.Services.AddRebus(configure => configure
    .Transport(t => t.UseRabbitMqAsOneWayClient(connectionString))
    .Routing(r => r.TypeBased()
        .Map<SendEmailMessage>("email-notifications")
        .Map<SendSmsMessage>("sms-notifications")
        .Map<SendWhatsAppMessage>("whatsapp-notifications"))
);

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
```

- [ ] **Step 2: Build the entire solution**

```bash
cd /home/hrosario/dev/notify-hub
dotnet build
```

Expected: **Build succeeded** with 0 errors. If there are MassTransit-related errors, it means a file was missed — check the error output and fix the remaining `using MassTransit;` references.

- [ ] **Step 3: Run all unit tests**

```bash
dotnet test tests/NotifyHub.Tests.Unit -v
```

Expected: all tests PASS.

- [ ] **Step 4: Commit**

```bash
git add src/NotifyHub.Api/Program.cs
git commit -m "feat: configure Rebus one-way client in API Program.cs"
```

---

## Task 8: Delete old Consumer files and final verification

**Files:**
- Delete: `src/NotifyHub.Worker.Email/Consumers/SendEmailConsumer.cs`
- Delete: `src/NotifyHub.Worker.Sms/Consumers/SendSmsConsumer.cs`
- Delete: `src/NotifyHub.Worker.WhatsApp/Consumers/SendWhatsAppConsumer.cs`
- Delete: `tests/NotifyHub.Tests.Unit/Workers/SendEmailConsumerTests.cs` (replaced by SendEmailHandlerTests.cs in Task 2)

- [ ] **Step 1: Delete old consumer files**

```bash
rm src/NotifyHub.Worker.Email/Consumers/SendEmailConsumer.cs
rm src/NotifyHub.Worker.Sms/Consumers/SendSmsConsumer.cs
rm src/NotifyHub.Worker.WhatsApp/Consumers/SendWhatsAppConsumer.cs
rm tests/NotifyHub.Tests.Unit/Workers/SendEmailConsumerTests.cs
```

- [ ] **Step 2: Build to confirm no broken references**

```bash
dotnet build
```

Expected: Build succeeded with 0 errors.

- [ ] **Step 3: Run all unit tests**

```bash
dotnet test tests/NotifyHub.Tests.Unit -v
```

Expected: all tests PASS. Count should be higher than before (new SMS and WhatsApp handler tests added).

- [ ] **Step 4: Verify no MassTransit references remain in source**

```bash
grep -r "MassTransit" src/ --include="*.cs"
```

Expected: no output (zero matches).

- [ ] **Step 5: Commit deletions**

```bash
git add -A
git commit -m "chore: delete old MassTransit consumer files"
```

- [ ] **Step 6: End-to-end smoke test (requires running infrastructure)**

```bash
docker compose up postgres rabbitmq -d
```

Start each worker in separate terminals:
```bash
dotnet run --project src/NotifyHub.Worker.Email
dotnet run --project src/NotifyHub.Worker.Sms
dotnet run --project src/NotifyHub.Worker.WhatsApp
```

Start the API:
```bash
dotnet run --project src/NotifyHub.Api
```

Send a test notification:
```bash
curl -s -X POST http://localhost:5000/notifications \
  -H "Content-Type: application/json" \
  -d '{
    "recipientUserId": "00000000-0000-0000-0000-000000000001",
    "title": "Test",
    "body": "Hello from Rebus",
    "channels": { "email": "test@example.com", "sms": "+1234567890" }
  }' | jq .
```

Expected: `201 Created` response with the notification. In RabbitMQ Management at `http://localhost:15672` (user: `notifyhub` / pass: `notifyhub`), queues `email-notifications` and `sms-notifications` should appear and show 0 ready messages (consumed immediately).

- [ ] **Step 7: Run integration tests**

```bash
dotnet test tests/NotifyHub.Tests.Integration -v
```

Expected: all integration tests PASS.
