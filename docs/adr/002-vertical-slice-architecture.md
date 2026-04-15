# ADR 002 — Use Vertical Slice Architecture for the API

**Status:** Accepted  
**Date:** April 2026

---

## Context

When structuring the ASP.NET Core API, the common alternatives are:

- **Layered / Clean Architecture** — separate Projects or folders for Controllers, Services, Repositories, DTOs
- **Vertical Slice Architecture** — each feature (endpoint) is a self-contained unit with its own request, handler, and response

---

## Decision

Use **Vertical Slice Architecture** for the API endpoints.

Each endpoint lives in a single file under `src/NotifyHub.Api/Endpoints/` and implements `IEndpoint`. Endpoints are auto-registered via reflection at startup.

---

## Reasons

- **This API has 6 endpoints with no shared business logic between them.** There is no domain service layer that multiple endpoints delegate to. A layered structure would produce artificial abstractions (a `NotificationService` that is just a pass-through) with no real benefit.
- **Vertical slices are easier to navigate.** A developer looking at `CreateNotificationEndpoint.cs` sees the entire flow: validation, persistence, SignalR dispatch, queue publish. There is no need to trace through layers to understand what a request does.
- **Change isolation.** Modifying one endpoint does not risk breaking another. In a layered design, changes to a shared service affect all callers.
- **Natural fit for a minimal API style.** .NET's Minimal APIs and `IEndpoint` registration pattern align directly with vertical slices.

---

## Consequences

- Each endpoint file is responsible for its own request/response types, validation, and mapping.
- Shared infrastructure (DbContext, SignalR hub, Rebus bus) is injected directly into endpoint handlers via DI.
- If two endpoints genuinely share logic in the future, that logic can be extracted to a private helper or a domain method on the entity — not a generic service class.
