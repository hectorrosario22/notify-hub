# Product Requirements Document — NotifyHub

**Version:** 1.0  
**Status:** In Progress  
**Last updated:** April 2026

---

## 1. Overview

NotifyHub is a multi-channel notification microservice built as a portfolio project. It exposes a centralized API that any module or service can call to send notifications to users across multiple channels: real-time push (browser), email, SMS, and WhatsApp.

The project demonstrates backend engineering skills in distributed systems, event-driven architecture, and cloud-native patterns using .NET and C#.

---

## 2. Goals

### Primary goals
- Build a production-grade notification service that showcases backend architecture skills
- Demonstrate real-world patterns: async messaging, event-driven processing, real-time delivery, and per-channel delivery tracking
- Serve as a runnable portfolio project that any engineer can clone and run locally in minutes

### Non-goals
- User authentication and authorization
- User management (users are external references — the service accepts a UUID)
- High availability or production infrastructure (this is a local demo)
- Notification templates or scheduling

---

## 3. Users & Roles

Since this is a microservice consumed by other services, there are two distinct "users":

**Integrating services** — other modules or APIs that call NotifyHub to trigger notifications. They interact exclusively via the HTTP API.

**End users (demo context)** — the recipients of notifications. In the demo UI, a UUID represents a user. The service does not validate whether the user exists.

---

## 4. Functional Requirements

### 4.1 Notification delivery

| ID | Requirement |
|---|---|
| F-01 | The API must accept a notification request with one or more target channels: `push`, `email`, `sms`, `whatsapp` |
| F-02 | Push notifications must be delivered in real-time via SignalR to the connected browser client |
| F-03 | Email, SMS, and WhatsApp notifications must be queued asynchronously via RabbitMQ |
| F-04 | Each channel must have a dedicated Worker Service that consumes its queue and calls the appropriate provider |
| F-05 | The API must return `202 Accepted` immediately — delivery is not guaranteed at request time |
| F-06 | Each delivery attempt must be recorded independently per channel |

### 4.2 Delivery tracking

| ID | Requirement |
|---|---|
| F-07 | Every notification must have an aggregated status: `pending`, `partial`, `delivered`, or `failed` |
| F-08 | Every delivery must track its own status: `pending`, `sent`, or `failed` |
| F-09 | Failed deliveries must record the error message and retry count |
| F-10 | The API must expose an endpoint to query the full status of a notification including per-channel deliveries |

### 4.3 Read state (push only)

| ID | Requirement |
|---|---|
| F-11 | Push notifications must support a read/unread state (`is_read`, `read_at`) |
| F-12 | The API must allow marking a single notification as read |
| F-13 | The API must allow marking all notifications as read for a given user |
| F-14 | The API must expose an unread count endpoint, separate from the full listing, for use by the bell icon badge |
| F-15 | Read state only applies to notifications with a `push` delivery — it is not relevant for email, SMS, or WhatsApp |

### 4.4 Listing

| ID | Requirement |
|---|---|
| F-16 | The API must return a paginated list of notifications for a given user |
| F-17 | The listing must support filtering by unread only |

### 4.5 Real-time

| ID | Requirement |
|---|---|
| F-18 | Clients must connect to the SignalR hub and join a group identified by their `userId` |
| F-19 | When a push notification is created, the server must emit a `NewNotification` event to the user's group |
| F-20 | When a notification is created or marked as read, the server must emit an `UnreadCountUpdated` event |

---

## 5. Non-Functional Requirements

| ID | Requirement |
|---|---|
| NF-01 | All infrastructure must be runnable locally via a single `podman compose up` command |
| NF-02 | No cloud account or paid service should be required to run the core system (provider credentials are optional for demo purposes) |
| NF-03 | Provider integrations (email, SMS, WhatsApp) must be pluggable — swapping a provider should require no architectural changes |
| NF-04 | Adding a new notification channel must require no database schema changes |
| NF-05 | The codebase must follow standard .NET conventions and be consistent enough for a new developer to navigate without guidance |

---

## 6. Architecture Summary

The system is composed of five deployable units:

**Notifications.API** — ASP.NET Core REST API. Receives notification requests, persists them to PostgreSQL, dispatches push notifications via SignalR, and publishes async messages to RabbitMQ for other channels.

**Notifications.Worker.Email** — .NET Worker Service. Consumes the `email` queue and sends via SendGrid or SMTP depending on configuration.

**Notifications.Worker.Sms** — .NET Worker Service. Consumes the `sms` queue and sends via a pluggable SMS provider.

**Notifications.Worker.WhatsApp** — .NET Worker Service. Consumes the `whatsapp` queue and sends via the Meta Cloud API.

**Demo UI** — Minimal frontend for testing. Allows generating a UUID, sending notifications across channels, and observing real-time delivery and delivery status.

All services communicate with a shared PostgreSQL database. RabbitMQ is the message broker. MassTransit is used as the .NET abstraction layer over RabbitMQ.

---

## 7. Data Model

Two tables:

**`Notifications`** — one record per notification request. Holds the recipient, content, aggregated status, and read state.

**`NotificationDeliveries`** — one record per channel per notification. Holds per-channel status, recipient address, retry count, error details, and channel-specific metadata as JSON.

See [README — Database Schema](./README.md#database-schema) for the full ER diagram.

---

## 8. API Surface

| Method | Endpoint | Description |
|---|---|---|
| `POST` | `/notifications` | Create and dispatch a notification |
| `GET` | `/notifications` | List notifications for a user (paginated) |
| `GET` | `/notifications/unread-count` | Get unread count for a user |
| `GET` | `/notifications/{id}` | Get full status of a notification |
| `PATCH` | `/notifications/{id}/read` | Mark a notification as read |
| `PATCH` | `/notifications/read-all` | Mark all notifications as read for a user |

See [README — API Reference](./README.md#api-reference) for full request/response contracts.

---

## 9. Provider Integrations

| Channel | Provider | Notes |
|---|---|---|
| Email | SendGrid / SMTP | Configurable via environment variables |
| SMS | Pluggable | Provider-agnostic interface; concrete implementation injected via DI |
| WhatsApp | Meta Cloud API | Requires a Meta Business account and approved template |
| Push | SignalR (built-in) | No external provider |

---

## 10. Out of Scope

The following are explicitly out of scope for v1 and may be considered for future iterations:

- Notification templates (predefined message structures with variable substitution)
- Scheduled notifications (send at a specific time)
- Notification preferences (user opt-in/opt-out per channel)
- Delivery webhooks (callback to the caller when delivery status changes)
- Multi-tenancy
- Authentication on the API

---

## 11. Milestones

| Milestone | Description |
|---|---|
| M1 — Foundation | Solution structure, Docker Compose, DB migrations, EF Core setup |
| M2 — Core API | `POST /notifications`, SignalR hub, push delivery end-to-end |
| M3 — Async workers | RabbitMQ setup, MassTransit, Email/SMS/WhatsApp workers |
| M4 — Read state & listing | Remaining endpoints, unread count, pagination |
| M5 — Demo UI | Minimal frontend: UUID generator, notification form, bell icon, deliveries tab |
| M6 — Polish | README finalization, `.env.example`, CONTRIBUTING.md, AGENTS.md |