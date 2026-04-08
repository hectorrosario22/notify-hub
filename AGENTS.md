# AGENTS.md

This file provides guidance to AI agents when working with code in this repository.

## Project Status

This repository is in the planning/design phase. Only the README and LICENSE exist; no code has been written yet.

## What This Is

**notify-hub** is a backend microservice for sending and managing notifications across four channels: Push (real-time), Email, SMS, and WhatsApp. It is designed to be consumed by other services via a centralized API — it does not manage users, accepting only an external `recipientUserId` (UUID).

## Planned Tech Stack

| Layer | Technology |
|---|---|
| API | ASP.NET Core |
| Real-time | SignalR (`NotificationsHub`) |
| Async messaging | RabbitMQ via MassTransit |
| Workers | .NET Worker Services (one per async channel) |
| ORM | Entity Framework Core |
| Database | PostgreSQL |
| Infrastructure | Podman Compose |
| Email | SendGrid / SMTP |
| SMS | Pluggable SMS provider |
| WhatsApp | Meta Cloud API |

## Architecture

The API handles Push synchronously (SignalR, low latency, no queue) and queues Email/SMS/WhatsApp to RabbitMQ, returning `202 Accepted` immediately. Three independent Worker Services consume their respective queues and call external providers.

```
Other Modules → POST /notifications → API → SignalR (push, sync)
                                          → RabbitMQ → email queue → Email Worker → SendGrid
                                                     → sms queue   → SMS Worker   → SMS Provider
                                                                   → WhatsApp queue → WhatsApp Worker → Meta API
All workers → PostgreSQL (update delivery status)
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

## Infrastructure

All infrastructure (PostgreSQL, RabbitMQ, API, Workers) runs via Podman Compose. Configuration requires a `.env` file (copy from `.env.example`) with provider credentials (SendGrid API key, SMS provider key, Meta WhatsApp token).

```bash
podman-compose up   # starts everything
```

## Extensibility Convention

Adding a new channel (e.g. Telegram) requires: a new RabbitMQ queue, a new Worker Service, and no schema changes — `channel_metadata` jsonb handles channel-specific data generically.
