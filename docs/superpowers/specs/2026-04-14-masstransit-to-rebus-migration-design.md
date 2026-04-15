# MassTransit → Rebus Migration Design

**Date:** 2026-04-14  
**Status:** Approved

## Context

MassTransit has become a paid library requiring a commercial license. NotifyHub is a non-commercial project that uses `MassTransit.RabbitMQ v9.1.0` to publish and consume messages between the API and 3 Workers. The project migrates to **Rebus**, which is open-source and free, keeping RabbitMQ as the message broker.

## Key Decisions

| Decision | Choice | Reason |
|---|---|---|
| Transport | RabbitMQ (unchanged) | Already configured in compose.yml and appsettings |
| Queue names | Explicit and descriptive | Clearer than MassTransit's convention-based names |
| Retries | 3 attempts + dead-letter queue per channel | Resilience against transient failures |
| Approach | Direct Rebus per project | Simplicity, no unnecessary abstraction layers |

## Scope

### What changes

| Category | Files |
|---|---|
| NuGet packages | 4 `.csproj` files (Api + 3 Workers) |
| DI configuration | 4 `Program.cs` files |
| Handlers | 3 consumers → 3 handlers (rename + refactor) |
| API endpoint | `CreateNotificationEndpoint.cs` |
| Unit tests | `SendEmailConsumerTests.cs`, `CreateNotificationEndpointTests.cs` |
| Integration tests | `NotifyHubApiFactory.cs` |

### What does NOT change

- `src/NotifyHub.Contracts/` — messages are plain POCOs with no MassTransit attributes
- `compose.yml` — RabbitMQ remains unchanged
- `appsettings.*.json` — connection configuration unchanged
- Business logic inside each handler

## Architecture After Migration

```
NotifyHub.Api
  └─ CreateNotificationEndpoint
       └─ IBus.Publish(message)  ──→  RabbitMQ
                                         ├── email-notifications    ──→  Worker.Email / SendEmailHandler
                                         ├── sms-notifications      ──→  Worker.Sms / SendSmsHandler
                                         └── whatsapp-notifications ──→  Worker.WhatsApp / SendWhatsAppHandler
```

## NuGet Packages

### Remove (all projects)
```xml
<PackageReference Include="MassTransit.RabbitMQ" Version="9.1.0" />
```

### Add (all projects)
```xml
<PackageReference Include="Rebus" Version="8.*" />
<PackageReference Include="Rebus.RabbitMQ" Version="9.*" />
<PackageReference Include="Rebus.ServiceProvider" Version="9.*" />
```

## Workers: Consumers → Handlers

### Interface comparison

| | MassTransit (before) | Rebus (after) |
|---|---|---|
| Interface | `IConsumer<T>` | `IHandleMessages<T>` |
| Method | `Consume(ConsumeContext<T> context)` | `Handle(T message)` |
| Message access | `context.Message` | `message` directly |

### Handler structure

```csharp
using Rebus.Handlers;
using NotifyHub.Contracts.Messages;

public class SendEmailHandler : IHandleMessages<SendEmailMessage>
{
    private readonly IEmailSender _emailSender;
    private readonly INotificationRepository _repository;
    private readonly ILogger<SendEmailHandler> _logger;

    public SendEmailHandler(IEmailSender emailSender, INotificationRepository repository, ILogger<SendEmailHandler> logger)
    {
        _emailSender = emailSender;
        _repository = repository;
        _logger = logger;
    }

    public async Task Handle(SendEmailMessage message)
    {
        // same business logic as before — method body unchanged
    }
}
```

Files move from `Consumers/` to `Handlers/` and the old Consumer files are deleted.

## API: Publisher

```csharp
// Before
private readonly IPublishEndpoint _publishEndpoint;
await _publishEndpoint.Publish<SendEmailMessage>(new SendEmailMessage(...));

// After
private readonly IBus _bus;
await _bus.Publish(new SendEmailMessage(...));
```

## Program.cs Configuration

### API (one-way client — publish only)

```csharp
var rabbitUser = builder.Configuration["RabbitMQ:Username"] ?? "guest";
var rabbitPass = builder.Configuration["RabbitMQ:Password"] ?? "guest";
var rabbitHost = builder.Configuration["RabbitMQ:Host"] ?? "localhost";
var connectionString = $"amqp://{rabbitUser}:{rabbitPass}@{rabbitHost}";

builder.Services.AddRebus(configure => configure
    .Transport(t => t.UseRabbitMqAsOneWayClient(connectionString))
    .Routing(r => r.TypeBased()
        .Map<SendEmailMessage>("email-notifications")
        .Map<SendSmsMessage>("sms-notifications")
        .Map<SendWhatsAppMessage>("whatsapp-notifications"))
);
```

### Workers (consume + retries)

```csharp
// Email Worker — same pattern for Sms and WhatsApp with their respective queues
builder.Services.AddRebus(configure => configure
    .Transport(t => t.UseRabbitMq(connectionString, "email-notifications"))
    .Options(o => o.SimpleRetryStrategy(
        errorQueueAddress: "email-notifications-error",
        maxDeliveryAttempts: 3))
);

builder.Services.AutoRegisterHandlersFromAssemblyOf<SendEmailHandler>();
```

## RabbitMQ Queues

| Channel | Main queue | Error queue |
|---|---|---|
| Email | `email-notifications` | `email-notifications-error` |
| SMS | `sms-notifications` | `sms-notifications-error` |
| WhatsApp | `whatsapp-notifications` | `whatsapp-notifications-error` |

## Tests

### Worker unit tests

```csharp
// Before — with ConsumeContext mock
var context = Substitute.For<ConsumeContext<SendEmailMessage>>();
context.Message.Returns(new SendEmailMessage(...));
await _consumer.Consume(context);

// After — direct
var message = new SendEmailMessage(...);
await _handler.Handle(message);
```

### Endpoint and integration tests

```csharp
// Before
services.AddSingleton(Substitute.For<IPublishEndpoint>());

// After
services.AddSingleton(Substitute.For<IBus>());
```

## End-to-End Verification

1. `dotnet build` — no errors across all projects
2. `dotnet test --filter Unit` — all unit tests pass
3. `docker compose up postgres rabbitmq -d`
4. Start the 3 Workers and the API
5. POST to `/api/notifications` with Email, SMS and WhatsApp channels
6. Verify in RabbitMQ Management (`http://localhost:15672`) that queues are created and messages are processed
7. Verify in DB that deliveries are in `Sent` status
8. `dotnet test --filter Integration`
