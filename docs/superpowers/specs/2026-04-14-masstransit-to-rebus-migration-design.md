# MassTransit → Rebus Migration Design

**Date:** 2026-04-14  
**Status:** Approved

## Context

MassTransit se volvió de pago (requiere licencia comercial). NotifyHub es un proyecto no comercial que usa `MassTransit.RabbitMQ v9.1.0` para publicar y consumir mensajes entre el API y 3 Workers. Se migra a **Rebus**, que es open-source y gratuito, manteniendo RabbitMQ como broker de mensajería.

## Decisiones clave

| Decisión | Elección | Razón |
|---|---|---|
| Transport | RabbitMQ (sin cambios) | Ya está en compose.yml y appsettings |
| Nombres de colas | Explícitos y descriptivos | Más claros que los convention-based de MassTransit |
| Reintentos | 3 intentos + dead-letter queue por canal | Resiliencia ante fallos transitorios |
| Enfoque | Rebus directo por proyecto | Simplicidad, sin capas de abstracción innecesarias |

## Alcance

### Lo que cambia

| Categoría | Archivos |
|---|---|
| Paquetes NuGet | 4 `.csproj` (Api + 3 Workers) |
| Configuración DI | 4 `Program.cs` |
| Handlers | 3 consumers → 3 handlers (rename + refactor) |
| API endpoint | `CreateNotificationEndpoint.cs` |
| Tests unitarios | `SendEmailConsumerTests.cs`, `CreateNotificationEndpointTests.cs` |
| Tests integración | `NotifyHubApiFactory.cs` |

### Lo que NO cambia

- `src/NotifyHub.Contracts/` — los mensajes son POCOs puros sin atributos MassTransit
- `compose.yml` — RabbitMQ permanece igual
- `appsettings.*.json` — configuración de conexión sin cambios
- Lógica de negocio dentro de cada handler

## Arquitectura tras la migración

```
NotifyHub.Api
  └─ CreateNotificationEndpoint
       └─ IBus.Publish(message)  ──→  RabbitMQ
                                         ├── email-notifications  ──→  Worker.Email / SendEmailHandler
                                         ├── sms-notifications    ──→  Worker.Sms / SendSmsHandler
                                         └── whatsapp-notifications──→  Worker.WhatsApp / SendWhatsAppHandler
```

## Paquetes NuGet

### Remover (todos los proyectos)
```xml
<PackageReference Include="MassTransit.RabbitMQ" Version="9.1.0" />
```

### Agregar (todos los proyectos)
```xml
<PackageReference Include="Rebus" Version="8.*" />
<PackageReference Include="Rebus.RabbitMQ" Version="9.*" />
<PackageReference Include="Rebus.ServiceProvider" Version="9.*" />
```

## Workers: Consumers → Handlers

### Interfaz

| | MassTransit (antes) | Rebus (después) |
|---|---|---|
| Interfaz | `IConsumer<T>` | `IHandleMessages<T>` |
| Método | `Consume(ConsumeContext<T> context)` | `Handle(T message)` |
| Acceso al mensaje | `context.Message` | `message` directamente |

### Estructura del handler

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
        // misma lógica que antes — sin cambios en el cuerpo del método
    }
}
```

Los archivos se mueven de `Consumers/` a `Handlers/` y los Consumer se eliminan.

## API: Publisher

```csharp
// Antes
private readonly IPublishEndpoint _publishEndpoint;
await _publishEndpoint.Publish<SendEmailMessage>(new SendEmailMessage(...));

// Después
private readonly IBus _bus;
await _bus.Publish(new SendEmailMessage(...));
```

## Configuración Program.cs

### API (one-way client — solo publica)

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

### Workers (consume + reintentos)

```csharp
// Email Worker — idem para Sms y WhatsApp con sus colas respectivas
builder.Services.AddRebus(configure => configure
    .Transport(t => t.UseRabbitMq(connectionString, "email-notifications"))
    .Options(o => o.SimpleRetryStrategy(
        errorQueueAddress: "email-notifications-error",
        maxDeliveryAttempts: 3))
);

builder.Services.AutoRegisterHandlersFromAssemblyOf<SendEmailHandler>();
```

## Colas RabbitMQ

| Canal | Cola principal | Cola de errores |
|---|---|---|
| Email | `email-notifications` | `email-notifications-error` |
| SMS | `sms-notifications` | `sms-notifications-error` |
| WhatsApp | `whatsapp-notifications` | `whatsapp-notifications-error` |

## Tests

### Tests unitarios de Workers

```csharp
// Antes — con mock de ConsumeContext
var context = Substitute.For<ConsumeContext<SendEmailMessage>>();
context.Message.Returns(new SendEmailMessage(...));
await _consumer.Consume(context);

// Después — directo
var message = new SendEmailMessage(...);
await _handler.Handle(message);
```

### Tests de endpoint e integración

```csharp
// Antes
services.AddSingleton(Substitute.For<IPublishEndpoint>());

// Después
services.AddSingleton(Substitute.For<IBus>());
```

## Verificación end-to-end

1. `dotnet build` — sin errores en todos los proyectos
2. `dotnet test --filter Unit` — todos los tests unitarios pasan
3. `podman compose up postgres rabbitmq -d`
4. Iniciar los 3 Workers y el API
5. POST a `/api/notifications` con canales Email, SMS y WhatsApp
6. Verificar en RabbitMQ Management (`http://localhost:15672`) que las colas se crean y los mensajes se procesan
7. Verificar en DB que los deliveries quedan en estado `Sent`
8. `dotnet test --filter Integration`
