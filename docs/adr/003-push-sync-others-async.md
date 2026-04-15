# ADR 003 — Push notifications are synchronous; all other channels are asynchronous

**Status:** Accepted  
**Date:** April 2026

---

## Context

When a notification request arrives at the API, each channel must be dispatched. There are two options per channel:

- **Synchronous:** dispatch happens inline during the request, before returning the response
- **Asynchronous:** the channel is queued to RabbitMQ and processed later by a Worker Service

---

## Decision

- **Push** notifications are dispatched **synchronously** via SignalR during the request handler.
- **Email, SMS, and WhatsApp** are dispatched **asynchronously** via RabbitMQ queues.

---

## Reasons for push being synchronous

- **Real-time delivery is the defining characteristic of push.** Queuing push through RabbitMQ introduces latency that defeats its purpose — the user would not see a real-time update.
- **SignalR dispatch is fast and in-process.** There is no external HTTP call, no retries, no provider rate limits. The operation is non-blocking and completes in milliseconds.
- **Failure mode is acceptable.** If the SignalR dispatch fails (e.g., the user is not connected), the notification is still persisted in the database. The user can retrieve it on next load. This is consistent with how real-time notification systems behave.

---

## Reasons for email/SMS/WhatsApp being asynchronous

- **External provider calls are slow and unreliable.** SendGrid, SMS providers, and Meta Cloud API all introduce network latency and potential failures. Performing these inline would slow every notification request and couple API availability to third-party uptime.
- **Retry semantics are naturally supported by the queue.** If a provider call fails, Rebus retries up to 3 times before moving the message to the error queue. No retry logic is needed in the API.
- **Independent scaling.** Each worker can be scaled independently based on queue depth, without affecting the API.

---

## Consequences

- The API always returns `202 Accepted` — delivery is never guaranteed at request time, even for push.
- Push delivery status transitions to `sent` synchronously; async channels start at `pending` and update when the worker processes them.
- If a client is not connected to the SignalR hub, the push delivery is still recorded, but the real-time event is lost (not queued for retry). This is an explicit tradeoff for simplicity.
