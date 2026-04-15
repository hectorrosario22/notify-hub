# ADR 001 — Use Rebus instead of MassTransit as messaging abstraction

**Status:** Accepted  
**Date:** April 2026

---

## Context

The project requires a .NET abstraction layer over RabbitMQ to avoid coupling Worker Services directly to the broker's client library (`RabbitMQ.Client`). The two most common options in the .NET ecosystem are MassTransit and Rebus.

MassTransit was the initial choice documented in the PRD, but was replaced during implementation.

---

## Decision

Use **Rebus** as the messaging abstraction layer.

---

## Reasons

- **Simpler mental model.** Rebus maps cleanly to handlers (`IHandleMessages<T>`) with minimal ceremony. MassTransit's consumer/saga/pipeline model introduces concepts that are unnecessary for a straightforward point-to-point worker pattern.
- **Lighter configuration.** Rebus setup for a single-queue worker is a few lines. MassTransit requires more wiring (bus factory, receive endpoint configuration, consumer registration) for the same outcome.
- **Easier to read for portfolio purposes.** This project is meant to be cloned and understood quickly. Rebus code is more immediately legible to developers unfamiliar with either framework.
- **Full feature parity for this use case.** Both libraries support RabbitMQ, dead-letter queues, retries, and handler auto-registration. No feature was sacrificed by switching.

---

## Consequences

- Workers implement `IHandleMessages<T>` and are auto-registered via `AutoRegisterHandlersFromAssemblyOf<T>()`.
- Retry behavior (max 3 attempts) and error queues are configured at the Rebus transport layer.
- Any developer familiar with MassTransit can understand Rebus quickly — the concepts are analogous, just with different class names.
- If this project were to scale to sagas or complex routing, MassTransit would be worth revisiting.
