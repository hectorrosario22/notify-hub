# NotifyHub Constitution

## Core Principles

### I. Single Responsibility

Each class/module has ONE reason to change. Core domain logic separate from infrastructure. API handlers orchestrate, don't implement business rules. Workers consume messages, don't call other workers.

**Why**: Isolating responsibilities prevents cascading changes. Infrastructure swaps (RabbitMQ → SQS, PostgreSQL → DynamoDB) require no domain logic edits.

### II. Input Validation at Boundaries

ALL external input (HTTP requests, queue messages, user data) validated before entering domain. Invalid requests fail fast with clear errors. Internal code assumes pre-validated data.

**Why**: Single validation layer prevents scattered defensive checks. Boundaries are the trust frontier.

### III. Async-by-Design Split

Synchronous paths (real-time delivery) vs asynchronous paths (external integrations). Real-time paths must not block on external calls. External calls must be retriable without API changes.

**Why**: Decoupling sync/async prevents customer-facing latency. Retry safety ensures resilience without breaking contracts.

### IV. Provider Independence

Domain logic never directly calls external services (email, SMS, WhatsApp, etc). Providers injected via interface/abstraction. Adding new channel requires new implementation only, zero domain changes.

**Why**: Interfaces shield core logic from provider details. New channels → new adapter only, no core touching.

### V. Data Consistency & Auditing

Every operation (send, retry, fail) persisted to database before action taken. Allows recovery, replay, and audit trail. No in-memory-only state for critical operations.

**Why**: Durability enables audit, recovery, and replaying. Database is source of truth for every state transition.

### VI. Explicit Error Handling

Distinguish retriable failures (network, rate limit) from permanent (invalid address, auth failure). Retry policy explicit, not implicit. Exceptions bubble up only for unexpected failures.

**Why**: Explicit categorization prevents silent losses. Implicit retries hide real failures.

### VII. Testing at Multiple Layers

Unit tests mock boundaries (database, external APIs). Integration tests verify real workflows against actual infrastructure containers. No layer tested only in isolation.

**Why**: Unit tests verify logic. Integration tests verify reality. Both required.

### VIII. Observable by Default

All entry points (HTTP, message consumption) logged. Failures include context: what was sent, why it failed, retry count. No silent failures or suppressed exceptions.

**Why**: Silent failures are the hardest to debug. Context in logs prevents hours of guessing.

## Non-Negotiables

- New channels: zero schema changes, zero domain logic changes
- Failure in one channel never blocks another
- All delivery states (pending/sent/failed) queryable at any time
- Message contracts versioned; breaking changes require explicit migration
- No business logic in API controllers — extract to handlers/services
- No directly instantiating external service clients — inject via DI/factory

## Governance

Constitution supersedes all practices. PRs must demonstrate compliance with principles. Amendments require written rationale and migration plan. Runtime guidance in CLAUDE.md.

**Version**: 1.0.0 | **Ratified**: 2026-04-20 | **Last Amended**: 2026-04-20
