# AGENTS.md

This file provides guidance to AI agents when working with code in this repository.

## Project Status

**Phase: Core Complete (M1–M4)**

| Layer | Status |
|---|---|
| Domain entities (Core) | ✅ Complete — `Notification`, `NotificationDelivery`, enums, `RefreshStatus` |
| Data contracts | ✅ Complete — request/response DTOs, `INotificationRepository` |
| Infrastructure (EF Core / PostgreSQL) | ✅ Complete — DbContext, entity configs, repository, `MarkAllAsReadAsync` |
| DB migrations | ✅ Complete — InitialCreate (notifications + notification_deliveries) |
| docker compose / infrastructure files | ✅ Complete — `compose.yml` with PostgreSQL, RabbitMQ, API, 3 workers |
| API endpoints (ASP.NET Core) | ✅ Complete — 6 endpoints with FluentValidation |
| SignalR Hub | ✅ Complete — `NotificationsHub` with group management |
| RabbitMQ / MassTransit integration | ✅ Complete — publishing from API, consumers in workers |
| Worker Services (Email, SMS, WhatsApp) | ✅ Complete — fake providers with MassTransit consumers |
| Unit tests | ✅ 90 tests passing |
| Integration tests | ✅ 10 tests passing (compose-managed PostgreSQL + RabbitMQ) |
| Dockerfiles | ✅ Complete — multi-stage for API (aspnet) and workers (runtime) |
| Demo UI (M5) | ⬜ Not started |

## What This Is

**NotifyHub** is a backend microservice for sending and managing notifications across four channels: Push (real-time), Email, SMS, and WhatsApp. It is designed to be consumed by other services via a centralized API — it does not manage users, accepting only an external `recipientUserId` (UUID).

## Tech Stack

| Layer | Technology |
|---|---|
| Framework | .NET 10.0 / C# 13 |
| API | ASP.NET Core Minimal APIs (vertical slice `IEndpoint` pattern) |
| Real-time | SignalR (`NotificationsHub`) |
| Async messaging | RabbitMQ via MassTransit 9.1 |
| Workers | .NET Worker Services (one per async channel) |
| ORM | Entity Framework Core 10 (Npgsql provider) |
| Database | PostgreSQL 17 |
| Validation | FluentValidation 12 |
| Infrastructure | docker compose |
| Email | Fake provider (logs + simulates latency) — swap for SendGrid/SMTP |
| SMS | Fake provider — swap for real SMS provider |
| WhatsApp | Fake provider — swap for Meta Cloud API |
| Testing | xUnit 2.9, NSubstitute 5.3, coverlet |

## Architecture

The API handles Push synchronously (SignalR, low latency, no queue) and queues Email/SMS/WhatsApp to RabbitMQ, returning `201 Created` immediately. Three independent Worker Services consume their respective queues and call external providers.

```
Other Modules → POST /notifications → API → SignalR (push, sync)
                                          → RabbitMQ → email queue → Email Worker → Email Provider
                                                     → sms queue   → SMS Worker   → SMS Provider
                                                     → whatsapp queue → WhatsApp Worker → WhatsApp Provider
All workers → PostgreSQL (update delivery status)
```

## Project Structure

```
src/
  NotifyHub.Core/           ← Domain entities, enums, interfaces
  NotifyHub.Contracts/      ← Request/response DTOs, MassTransit messages
  NotifyHub.Infrastructure/ ← EF Core DbContext, repository implementations
  NotifyHub.Api/            ← ASP.NET Core endpoints, SignalR hub, middleware
  NotifyHub.Worker.Email/   ← MassTransit consumer → FakeEmailSender
  NotifyHub.Worker.Sms/     ← MassTransit consumer → FakeSmsSender
  NotifyHub.Worker.WhatsApp/← MassTransit consumer → FakeWhatsAppSender
tests/
  NotifyHub.Tests.Unit/     ← Pure domain + endpoint handler tests (90 tests)
  NotifyHub.Tests.Integration/ ← Real DB + RabbitMQ via compose (10 tests)
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

Hub: `NotificationsHub` at `/hubs/notifications`. Clients join a group keyed by `userId` via `JoinUserGroup`.

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
chore/add-compose
docs/update-agents
```

Types mirror Conventional Commits: `feat`, `fix`, `refactor`, `chore`, `docs`, `test`.

### Incremental Commits

Commit each logical step independently as soon as it is verified. Do not batch unrelated changes into a single commit. Examples of appropriate commit boundaries: adding packages, creating a DbContext, implementing a repository, adding a migration, updating a configuration file.

### Commit Messages

Follow [Conventional Commits](https://www.conventionalcommits.org/):

```
feat(core): add MarkAsRead validation for non-push channels
fix(api): return 404 when notification not found
test(unit): add edge cases for partial delivery status
chore: add docker compose configuration
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
- `NotifyHub.Tests.Unit` — pure domain logic and endpoint handler tests; NSubstitute for mocks
- `NotifyHub.Tests.Integration` — real infrastructure via compose-managed PostgreSQL and RabbitMQ

### Running Integration Tests

Integration tests require PostgreSQL and RabbitMQ running via compose:

```bash
docker compose up postgres rabbitmq -d
dotnet test
```

### Domain-Driven Design (DDD)

The domain layer (`NotifyHub.Core`) follows strict DDD patterns already established in the codebase — follow them:

- **Private constructors + static `Create()` factory methods** with full argument validation
- **No anemic models** — behavior lives on entities, not in services or handlers
- **`Notification` is the aggregate root**; `NotificationDelivery` is owned by it and cannot exist independently
- **Status aggregation** (`RecalculateStatus`) is computed inside the aggregate when delivery statuses change — do not calculate it outside
- **`RefreshStatus()`** must be called explicitly after `MarkAsSent()`/`MarkAsFailed()` on DB-loaded entities (delivery callback is no-op when loaded from DB)

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

All infrastructure runs via docker compose:

```bash
cp .env.example .env     # provider credentials optional for local dev
docker compose up -d      # starts PostgreSQL, RabbitMQ, API (port 5000), 3 workers
```

- API: `http://localhost:5000`
- RabbitMQ Management: `http://localhost:15672` (notifyhub/notifyhub)
- SignalR Hub: `ws://localhost:5000/hubs/notifications`
