# AGENTS.md

This file provides guidance to AI agents when working with code in this repository.

## Project Status

**Phase: Early Implementation**

| Layer | Status |
|---|---|
| Domain entities (Core) | ✅ Complete — `Notification`, `NotificationDelivery`, enums |
| Data contracts | ✅ Complete — request/response DTOs, `INotificationRepository` |
| Infrastructure (EF Core / PostgreSQL) | ⬜ Not started |
| API endpoints (ASP.NET Core) | ⬜ Not started (still has template WeatherForecast code) |
| SignalR Hub | ⬜ Not started |
| RabbitMQ / MassTransit integration | ⬜ Not started |
| Worker Services (Email, SMS, WhatsApp) | ⬜ Stubs only |
| Unit tests | ✅ 55+ tests passing (domain layer) |
| Integration tests | ⬜ Project scaffolded, no tests yet |
| Podman Compose / infrastructure files | ⬜ Not started |

## What This Is

**NotifyHub** is a backend microservice for sending and managing notifications across four channels: Push (real-time), Email, SMS, and WhatsApp. It is designed to be consumed by other services via a centralized API — it does not manage users, accepting only an external `recipientUserId` (UUID).

## Tech Stack

| Layer | Technology |
|---|---|
| Framework | .NET 10.0 / C# 13 |
| API | ASP.NET Core |
| Real-time | SignalR (`NotificationsHub`) — *planned* |
| Async messaging | RabbitMQ via MassTransit — *planned* |
| Workers | .NET Worker Services (one per async channel) |
| ORM | Entity Framework Core — *planned* |
| Database | PostgreSQL — *planned* |
| Infrastructure | Podman Compose — *planned* |
| Email | SendGrid / SMTP — *planned* |
| SMS | Pluggable SMS provider — *planned* |
| WhatsApp | Meta Cloud API — *planned* |
| Testing | xUnit 2.9+ with coverlet (code coverage) |

## Architecture

The API handles Push synchronously (SignalR, low latency, no queue) and queues Email/SMS/WhatsApp to RabbitMQ, returning `202 Accepted` immediately. Three independent Worker Services consume their respective queues and call external providers.

```
Other Modules → POST /notifications → API → SignalR (push, sync)
                                          → RabbitMQ → email queue → Email Worker → SendGrid
                                                     → sms queue   → SMS Worker   → SMS Provider
                                                                   → WhatsApp queue → WhatsApp Worker → Meta API
All workers → PostgreSQL (update delivery status)
```

## Project Structure

```
src/
  NotifyHub.Core/           ← Domain entities, enums, interfaces
  NotifyHub.Contracts/      ← Request/response DTOs, MassTransit messages
  NotifyHub.Infrastructure/ ← EF Core DbContext, repository implementations
  NotifyHub.Api/            ← ASP.NET Core endpoints, SignalR hub
  NotifyHub.Worker.Email/   ← MassTransit consumer → SendGrid
  NotifyHub.Worker.Sms/     ← MassTransit consumer → SMS provider
  NotifyHub.Worker.WhatsApp/← MassTransit consumer → Meta Cloud API
tests/
  NotifyHub.Tests.Unit/     ← Pure domain logic tests (no I/O)
  NotifyHub.Tests.Integration/ ← Real DB + RabbitMQ (Testcontainers, when added)
demo/
  index.html / app.js / app.css ← Vanilla JS test UI for manual SignalR testing
```

## Database Schema

Two tables:

- **Notifications** — canonical record per notification (`id`, `recipient_user_id`, `title`, `body`, `status` [pending/partial/delivered/failed], `is_read`, `read_at`, `metadata` jsonb, timestamps)
- **NotificationDeliveries** — one row per channel per notification (`notification_id` FK, `channel` [push/email/sms/whatsapp], `status` [pending/sent/failed], `recipient`, `retry_count`, `error_message`, `channel_metadata` jsonb, `sent_at`)

`status` on Notifications is the aggregate across all deliveries (partial if any channel fails). `is_read`/`read_at` apply only to push. `channel_metadata` stores channel-specific fields (email subject, WhatsApp template ID, etc.) as JSON to avoid sparse columns.

## API Surface

| Method | Path | Purpose |
|---|---|---|
| `POST` | `/notifications` | Create notification (one or more channels) |
| `GET` | `/notifications/{id}` | Full status + per-delivery detail |
| `GET` | `/notifications` | Paginated list for a user (`userId`, `page`, `pageSize`, `unreadOnly`) |
| `GET` | `/notifications/unread-count` | Badge count only (`userId`) |
| `PATCH` | `/notifications/{id}/read` | Mark single push notification read |
| `PATCH` | `/notifications/read-all` | Mark all as read for a user (`userId`) |

## SignalR Events

Hub: `NotificationsHub`. Clients join a group keyed by `userId` on connect.

| Event | When | Payload |
|---|---|---|
| `NewNotification` | Push notification created | `{ id, title, body, metadata }` |
| `UnreadCountUpdated` | Notification created or marked read | `{ count: N }` |

## Git Workflow

**All changes must be made on a feature branch. Never commit directly to `main`.**

### Branch Naming

Use the format `<type>/<short-description>`:

```
feat/notification-api-endpoints
fix/delivery-status-aggregation
chore/add-podman-compose
docs/update-agents
```

Types mirror Conventional Commits: `feat`, `fix`, `refactor`, `chore`, `docs`, `test`.

### Commit Messages

Follow [Conventional Commits](https://www.conventionalcommits.org/):

```
feat(core): add MarkAsRead validation for non-push channels
fix(api): return 404 when notification not found
test(unit): add edge cases for partial delivery status
chore: add Podman Compose configuration
```

### Opening a Pull Request

Use the GitHub CLI (`gh`). Once all tests pass on the feature branch:

```bash
gh pr create --title "feat: add notification API endpoints" \
  --body "## Summary
- Add POST /notifications endpoint
- Add GET /notifications/{id} endpoint

## Test plan
- [ ] dotnet test passes
- [ ] Manual test via demo UI" \
  --base main
```

PRs must have passing tests before merging. Use squash merge or merge commit — no rebase merges to `main`.

## Development Practices

### Test-Driven Development (TDD)

**All new functionality must follow the Red → Green → Refactor cycle.** This is not optional.

1. **Red** — Write a failing test that describes the expected behavior
2. **Green** — Write the minimal code to make it pass
3. **Refactor** — Clean up without breaking tests

Run tests at any time:

```bash
dotnet test
```

Test organization:
- `NotifyHub.Tests.Unit` — pure domain logic; no database, no network, no mocks needed for domain entities
- `NotifyHub.Tests.Integration` — real infrastructure via Testcontainers (to be configured when Infrastructure layer is implemented)

### Domain-Driven Design (DDD)

The domain layer (`NotifyHub.Core`) follows strict DDD patterns already established in the codebase — follow them:

- **Private constructors + static `Create()` factory methods** with full argument validation
- **No anemic models** — behavior lives on entities, not in services or handlers
- **`Notification` is the aggregate root**; `NotificationDelivery` is owned by it and cannot exist independently
- **Status aggregation** (`RecalculateStatus`) is computed inside the aggregate when delivery statuses change — do not calculate it outside

Example of the established pattern (see `src/NotifyHub.Core/Entities/Notification.cs`):

```csharp
public static Notification Create(Guid recipientUserId, string title, string body,
    Dictionary<Channel, string> channels)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(title);
    // ... validation
    return new Notification { ... };
}
```

### Code Style

Enforced via `.editorconfig` at the repo root. Key rules:

- Private fields: `_camelCase` (underscore prefix, e.g. `_repository`)
- No `this.` qualifier
- System `using` directives go first
- Nullable reference types enabled on all projects (`<Nullable>enable</Nullable>`)
- Implicit usings enabled (`<ImplicitUsings>enable</ImplicitUsings>`)

### Extensibility Convention

Adding a new channel (e.g. Telegram) requires: a new RabbitMQ queue, a new Worker Service, and no schema changes — `channel_metadata` jsonb handles channel-specific data generically.

## Infrastructure

All infrastructure (PostgreSQL, RabbitMQ, API, Workers) will run via Podman Compose. Configuration will require a `.env` file (copy from `.env.example`) with provider credentials.

> **Note:** Podman Compose files and `.env.example` have not been created yet.

```bash
podman-compose up   # starts everything (once files exist)
```
