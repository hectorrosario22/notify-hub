# Contributing to NotifyHub

Thanks for taking the time to look at this project. This document covers how to get started, the conventions used throughout the codebase, and what to keep in mind before opening a PR.

---

## Table of Contents

- [Getting Started](#getting-started)
- [Running Tests](#running-tests)
- [Code Conventions](#code-conventions)
- [Adding a New Notification Channel](#adding-a-new-notification-channel)
- [Pull Request Process](#pull-request-process)

---

## Getting Started

**Prerequisites:** Docker, .NET 10 SDK, Node.js 22

```bash
git clone https://github.com/YOUR_USERNAME/notify-hub.git
cd notify-hub

# Copy environment config
cp .env.example .env

# Start all services (API, workers, PostgreSQL, RabbitMQ, demo UI)
docker compose up --build
```

The API will be available at `http://localhost:5000` and the demo UI at `http://localhost:5173`.

To run the API locally outside Docker (e.g., for faster iteration):

```bash
# Start only infrastructure
docker compose up postgres rabbitmq -d

# Run the API
cd src/NotifyHub.Api
dotnet run
```

---

## Running Tests

```bash
# All tests (unit + integration)
dotnet test

# Unit tests only
dotnet test tests/NotifyHub.Tests.Unit

# Integration tests only (requires Docker for Testcontainers)
dotnet test tests/NotifyHub.Tests.Integration
```

Integration tests use [Testcontainers](https://dotnet.testcontainers.org/) — Docker must be running.

---

## Code Conventions

**Project structure:** The solution uses a vertical slice approach inside the API. Each endpoint lives in its own file under `src/NotifyHub.Api/Endpoints/` and implements `IEndpoint`. New endpoints are auto-registered via reflection — no manual registration needed.

**Naming:**
- Files and classes: `PascalCase`
- Private fields: `_camelCase`
- Endpoints: named after the operation, e.g. `CreateNotificationEndpoint`, `GetNotificationEndpoint`

**Database:** Use EF Core Fluent API for all configuration. Do not use Data Annotations. New columns or tables require a migration:

```bash
cd src/NotifyHub.Infrastructure
dotnet ef migrations add <MigrationName> --startup-project ../NotifyHub.Api
```

**Workers:** Each worker handler implements `IHandleMessages<T>` (Rebus). Update the `NotificationDeliveries` table directly in the handler — do not add new dependencies between workers.

**Error handling:** Handlers should let Rebus manage retries (configured at 3 attempts). Only catch exceptions you intend to handle; let others propagate to the error queue.

---

## Adding a New Notification Channel

The system is designed so that adding a new channel (e.g., Telegram) requires no database schema changes. Here is the full checklist:

1. **Add the channel to the `Channel` enum** in `NotifyHub.Core/Enums/Channel.cs`
2. **Define a message type** in `NotifyHub.Contracts/` (e.g., `SendTelegramMessage.cs`)
3. **Publish the message** in `CreateNotificationEndpoint.cs` alongside the existing channel dispatch logic
4. **Create a new Worker project** under `src/` following the pattern of `NotifyHub.Worker.Email`:
   - Add `IHandleMessages<SendTelegramMessage>` handler
   - Configure Rebus with the new queue name (e.g., `telegram-notifications`)
   - Update `NotificationDelivery` status after each attempt
5. **Add the worker to `compose.yml`** using the same pattern as the other workers
6. **Add tests** for the handler in `tests/`

The `channel_metadata` JSON column in `NotificationDeliveries` handles any channel-specific fields — no schema migration needed.

---

## Pull Request Process

- Branch off `main` and name your branch descriptively: `feat/telegram-channel`, `fix/unread-count-query`
- Keep commits focused. One logical change per commit.
- Run `dotnet test` before pushing — PRs with failing tests will not be reviewed.
- Include a short description of *what* changed and *why* in the PR body.
- For significant changes, reference or update the relevant section of [PRD.md](./PRD.md) or the [ADRs](./docs/adr/).
