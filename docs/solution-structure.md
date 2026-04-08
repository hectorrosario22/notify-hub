# NotifyHub — Solution Structure

## Overview

This document defines the .NET solution structure for NotifyHub: the projects it contains, their responsibilities, dependencies, and acceptance criteria. It serves as the baseline requirement driving the initial implementation.

---

## Projects

### 1. `NotifyHub.Contracts`
**Type:** Class Library  
**Responsibility:** Defines the messages published and consumed through RabbitMQ via MassTransit. This is the shared contract between the API and all Workers.  
**Contents:**
- `Messages/` — message types: `SendEmailMessage`, `SendSmsMessage`, `SendWhatsAppMessage`

**Depends on:** nothing  
**Used by:** `NotifyHub.Api`, `NotifyHub.Worker.Email`, `NotifyHub.Worker.Sms`, `NotifyHub.Worker.WhatsApp`

---

### 2. `NotifyHub.Core`
**Type:** Class Library  
**Responsibility:** Defines the core domain: entities and enums. Contains no infrastructure interfaces or repositories.  
**Contents:**
- `Entities/` — `Notification`, `NotificationDelivery`
- `Enums/` — `Channel` (push/email/sms/whatsapp), `DeliveryStatus` (pending/sent/failed), `NotificationStatus` (pending/partial/delivered/failed)

**Depends on:** nothing  
**Used by:** `NotifyHub.Infrastructure`, `NotifyHub.Api`, Workers

---

### 3. `NotifyHub.Infrastructure`
**Type:** Class Library  
**Responsibility:** Implements persistence. Contains the EF Core DbContext and migrations. The only place where EF/SQL is written.  
**Contents:**
- `Persistence/NotifyHubDbContext.cs`
- `Persistence/Migrations/`

**Depends on:** `NotifyHub.Core`  
**Used by:** `NotifyHub.Api`, `NotifyHub.Worker.Email`, `NotifyHub.Worker.Sms`, `NotifyHub.Worker.WhatsApp`

---

### 4. `NotifyHub.Api`
**Type:** ASP.NET Core Web API  
**Responsibility:** HTTP entry point. Exposes REST endpoints and the SignalR hub. Organized as Vertical Slices: each feature owns its folder with everything it needs.  
**Contents:**
```
NotifyHub.Api/
├── Notifications/
│   ├── NotificationsController.cs
│   ├── NotificationsHub.cs            ← SignalR
│   ├── CreateNotification/
│   │   ├── CreateNotificationRequest.cs
│   │   └── CreateNotificationHandler.cs
│   ├── GetNotification/
│   │   ├── GetNotificationResponse.cs
│   │   └── GetNotificationHandler.cs
│   ├── ListNotifications/
│   │   ├── ListNotificationsRequest.cs
│   │   ├── ListNotificationsResponse.cs
│   │   └── ListNotificationsHandler.cs
│   ├── MarkAsRead/
│   │   └── MarkAsReadHandler.cs
│   ├── MarkAllAsRead/
│   │   └── MarkAllAsReadHandler.cs
│   └── GetUnreadCount/
│       ├── GetUnreadCountResponse.cs
│       └── GetUnreadCountHandler.cs
└── Program.cs
```

**Depends on:** `NotifyHub.Core`, `NotifyHub.Infrastructure`, `NotifyHub.Contracts`

---

### 5. `NotifyHub.Worker.Email`
**Type:** .NET Worker Service  
**Responsibility:** Consumes `SendEmailMessage` messages from RabbitMQ and delivers them via SendGrid/SMTP. Updates delivery status in PostgreSQL.  
**Contents:**
```
NotifyHub.Worker.Email/
├── Consumers/
│   └── SendEmailConsumer.cs          ← IConsumer<SendEmailMessage> (MassTransit)
├── Services/
│   ├── IEmailSender.cs
│   └── SendGridEmailSender.cs
└── Program.cs
```

**Depends on:** `NotifyHub.Contracts`, `NotifyHub.Core`, `NotifyHub.Infrastructure`

---

### 6. `NotifyHub.Worker.Sms`
**Type:** .NET Worker Service  
**Responsibility:** Consumes `SendSmsMessage` messages and delivers them via the configured SMS provider. Updates delivery status in PostgreSQL.  
**Contents:**
```
NotifyHub.Worker.Sms/
├── Consumers/
│   └── SendSmsConsumer.cs
├── Services/
│   ├── ISmsSender.cs
│   └── SmsSender.cs
└── Program.cs
```

**Depends on:** `NotifyHub.Contracts`, `NotifyHub.Core`, `NotifyHub.Infrastructure`

---

### 7. `NotifyHub.Worker.WhatsApp`
**Type:** .NET Worker Service  
**Responsibility:** Consumes `SendWhatsAppMessage` messages and delivers them via the Meta Cloud API. Updates delivery status in PostgreSQL.  
**Contents:**
```
NotifyHub.Worker.WhatsApp/
├── Consumers/
│   └── SendWhatsAppConsumer.cs
├── Services/
│   ├── IWhatsAppSender.cs
│   └── MetaWhatsAppSender.cs
└── Program.cs
```

**Depends on:** `NotifyHub.Contracts`, `NotifyHub.Core`, `NotifyHub.Infrastructure`

---

### 8. `NotifyHub.Tests.Unit`
**Type:** xUnit Test Project  
**Responsibility:** Isolated unit tests. Covers business logic, API handlers, and worker consumers in memory — no real I/O.  
**Contents:**
```
NotifyHub.Tests.Unit/
├── Api/
│   └── Notifications/               ← handler tests
└── Workers/
    ├── Email/
    ├── Sms/
    └── WhatsApp/
```

**Depends on:** all `src/` projects under test  
**Tooling:** xUnit, Moq (or NSubstitute)

---

### 9. `NotifyHub.Tests.Integration`
**Type:** xUnit Test Project  
**Responsibility:** End-to-end integration tests against real infrastructure (PostgreSQL, RabbitMQ). Verifies complete flows: `POST /notifications` → message enqueued → worker consumes → status updated in DB.  
**Contents:**
```
NotifyHub.Tests.Integration/
└── Api/
    └── Notifications/               ← HTTP endpoint tests
```

**Depends on:** `NotifyHub.Api`, `NotifyHub.Infrastructure`, `NotifyHub.Contracts`  
**Tooling:** xUnit, Testcontainers (PostgreSQL + RabbitMQ), `WebApplicationFactory<Program>`

---

## Demo UI

**Location:** `demo/` (outside the .NET solution)  
**Type:** HTML/JS vanilla — no frameworks  
**Responsibility:** Test interface for simulating notification sends and observing real-time delivery via SignalR.  
**Contents:**
```
demo/
├── index.html
├── app.js
└── styles.css
```

---

## Full Repository Tree

```
notify-hub/
├── NotifyHub.sln
├── .env.example
├── podman-compose.yml
├── .editorconfig
├── docs/
│   └── solution-structure.md
│
├── src/
│   ├── NotifyHub.Api/
│   ├── NotifyHub.Contracts/
│   ├── NotifyHub.Core/
│   ├── NotifyHub.Infrastructure/
│   ├── NotifyHub.Worker.Email/
│   ├── NotifyHub.Worker.Sms/
│   └── NotifyHub.Worker.WhatsApp/
│
├── tests/
│   ├── NotifyHub.Tests.Unit/
│   └── NotifyHub.Tests.Integration/
│
└── demo/
    ├── index.html
    ├── app.js
    └── styles.css
```

---

## Dependency Graph

```
NotifyHub.Contracts   (no dependencies)
NotifyHub.Core        (no dependencies)
NotifyHub.Infrastructure  → Core

NotifyHub.Api             → Core, Infrastructure, Contracts
NotifyHub.Worker.Email    → Core, Infrastructure, Contracts
NotifyHub.Worker.Sms      → Core, Infrastructure, Contracts
NotifyHub.Worker.WhatsApp → Core, Infrastructure, Contracts

NotifyHub.Tests.Unit        → src projects (mocked)
NotifyHub.Tests.Integration → Api, Infrastructure, Contracts
```

---

## Acceptance Criteria

### Per project

| Project | Criteria |
|---|---|
| `NotifyHub.Contracts` | Builds with no external dependencies. All 3 message types exist and are serializable by MassTransit. |
| `NotifyHub.Core` | Builds with no external dependencies. Entities reflect the DB schema defined in the README. |
| `NotifyHub.Infrastructure` | `dotnet ef migrations list` runs without errors. `NotifyHubDbContext` registers `Notifications` and `NotificationDeliveries`. |
| `NotifyHub.Api` | `dotnet run` starts on the configured port. All endpoints from the README respond (even with empty data). SignalR hub reachable at `/notificationsHub`. |
| `NotifyHub.Worker.Email` | `dotnet run` starts and connects to RabbitMQ. Consumer registered for `SendEmailMessage`. |
| `NotifyHub.Worker.Sms` | Same — consumer registered for `SendSmsMessage`. |
| `NotifyHub.Worker.WhatsApp` | Same — consumer registered for `SendWhatsAppMessage`. |
| `NotifyHub.Tests.Unit` | `dotnet test` passes. At least 1 test per API handler and 1 per worker consumer. |
| `NotifyHub.Tests.Integration` | `dotnet test` passes with Testcontainers running. Full flow POST → queue → worker → DB verified. |
| `Demo UI` | Opens in browser. Can send a notification to the API and receive the push in real time via SignalR. |

### Solution-wide

- `dotnet build NotifyHub.sln` completes with no errors or warnings.
- `podman-compose up` brings up all services (API + 3 Workers + PostgreSQL + RabbitMQ).
- A `POST /notifications` with channel `push` delivers the notification to the Demo UI in real time.
- A `POST /notifications` with channel `email` results in a `NotificationDeliveries` row with `status = sent` (or `failed` with a clear error message if credentials are not configured).
