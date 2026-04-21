# Feature Specification: AWS Migration (VPS → AWS)

**Feature Branch**: `migrate/aws-infrastructure`
**Created**: 2026-04-21
**Status**: Draft

---

## Strategy Overview

```
CURRENT (VPS)                           TARGET (AWS)
─────────────────                       ──────────────────────────────────────
Nginx/ASP.NET Core API  ─────────────►  AWS Lambda + API Gateway REST
SignalR Hub             ─────────────►  API Gateway WebSocket API
PostgreSQL              ─────────────►  Amazon DynamoDB
RabbitMQ                ─────────────►  Amazon SQS
Demo: Docker + Nginx     ─────────────►  S3 + CloudFront ✓ (complete)

Workers (Email/SMS/WhatsApp)           Lambda functions + SQS triggers
```

**Migration Phases:**

| Phase | Change | VPS Dependency | AWS Dependency |
|-------|--------|-----------------|-----------------|
| 0 | **CI/CD Pipeline**: GitHub Actions | N/A | Deploy demo to S3+CloudFront |
| 1 | SQS Queues (email/sms/whatsapp) | Uses SQS | Create queues only |
| 2 | SQS Lambda consumers | VPS still runs | Add SQS-triggered Lambdas |
| 3 | DynamoDB tables | Direct cutover | Create tables, switch |
| 4 | API Gateway REST → Lambda | Direct cutover | Deploy API, switch |
| 5 | API Gateway WebSocket | Direct cutover | Deploy WebSocket, switch |
| 6 | Cutover & decommission | None | Full AWS |

---

## Phase 0: CI/CD Pipeline

**Goal:** GitHub Actions to deploy demo to S3 + CloudFront automatically.

### Story 1 - Demo Deployment Pipeline (Priority: P1)

Automated deployment of demo UI on every push to main.

**Acceptance Scenarios:**
1. **Given** push to `main` branch, **When** workflow completes, **Then** demo UI is live on CloudFront
2. **Given** manual trigger, **When** workflow runs, **Then** same deployment occurs
3. **Given** future projects, **When** added to workflow, **Then** each deploys independently

### Requirements

- **FR-001**: Workflow file at `.github/workflows/deploy.yml`
- **FR-002**: Trigger on push to `main` AND `workflow_dispatch` (manual)
- **FR-003**: Node.js 22 for building demo
- **FR-004**: AWS credentials via OIDC (no long-lived keys)
- **FR-005**: Invalidate CloudFront cache after S3 deploy

### GitHub Variables Required

| Variable | Description | Example |
|----------|-------------|---------|
| `API_BASE_URL` | Backend API endpoint | `https://api.notifyhub.dev` |
| `DEMO_S3_BUCKET` | S3 bucket name | `notifyhub-demo` |
| `DEMO_CF_DISTRIBUTION_ID` | CloudFront distribution ID | `E2XAMPLE123` |

---

## Phase 1: SQS Queues Infrastructure

**Goal:** Create SQS queues without changing application behavior.

### Story 1 - Create SQS Queues (Priority: P1)

Set up email, SMS, and WhatsApp queues in SQS us-east-1.

**Acceptance Scenarios:**
1. **Given** AWS credentials, **When** provisioning runs, **Then** three queues exist: `notifyhub-email-notifications`, `notifyhub-sms-notifications`, `notifyhub-whatsapp-notifications`
2. **Given** queues exist, **When** Dead Letter Queue configured, **Then** failed messages route to `notifyhub-dlq`

### Requirements

- **FR-006**: SQS queues must use FIFO naming convention: `notifyhub-{channel}-notifications.fifo`
- **FR-007**: Dead Letter Queue must be created for each main queue
- **FR-008**: IAM policy must follow least-privilege principle

### Key Entities

- **SQS Queues**: `notifyhub-email-notifications.fifo`, `notifyhub-sms-notifications.fifo`, `notifyhub-whatsapp-notifications.fifo`
- **Dead Letter Queues**: `notifyhub-email-dlq.fifo`, `notifyhub-sms-dlq.fifo`, `notifyhub-whatsapp-dlq.fifo`

---

## Phase 2: Lambda Consumers for Async Channels

**Goal:** Deploy Lambda functions that consume SQS queues, replacing worker services.

### Story 1 - Lambda Consumer Functions (Priority: P1)

Lambda functions process messages from SQS queues and call delivery providers. Lambda actualiza PostgreSQL directamente via EF Core.

**Acceptance Scenarios:**
1. **Given** message in SQS queue, **When** Lambda triggered, **Then** delivery provider called and PostgreSQL updated via EF Core
2. **Given** delivery fails 3 times, **When** message processed, **Then** moved to DLQ

### Requirements

- **FR-009**: Lambda must implement idempotency using `NotificationId + DeliveryId` as key
- **FR-010**: Retry behavior: 3 attempts with exponential backoff before DLQ
- **FR-011**: Lambda connects to PostgreSQL via EF Core (same pattern as existing workers)

### Key Entities

- **Lambda Functions**: `NotifyHub-EmailConsumer`, `NotifyHub-SmsConsumer`, `NotifyHub-WhatsAppConsumer`
- **Event Source Mappings**: SQS trigger for each function

---

## Phase 3: DynamoDB Migration

**Goal:** Replace PostgreSQL with DynamoDB via direct cutover.

### Story 1 - DynamoDB Tables (Priority: P1)

Create DynamoDB tables that mirror the PostgreSQL schema.

**Acceptance Scenarios:**
1. **Given** DynamoDB provisioned, **When** query by `recipientUserId`, **Then** return all notifications for that user with pagination
2. **Given** notifications exist, **When** query by `notificationId`, **Then** return notification with all delivery records

### Requirements

- **FR-012**: DynamoDB table `Notifications` with partition key `notificationId` (UUID) and GSI on `recipientUserId`
- **FR-013**: DynamoDB table `NotificationDeliveries` with partition key `deliveryId` (UUID) and GSI on `notificationId`
- **FR-014**: Global Secondary Index `NotificationsByUser` on `recipientUserId` + `createdAt` for listing
- **FR-015**: DynamoDB auto-scaling configured for read/write capacity

### Key Entities

- **Notifications Table**: `notificationId` (PK), `recipientUserId` (GSI), `title`, `body`, `status`, `isRead`, `readAt`, `metadata` (Map), `createdAt`, `updatedAt`
- **NotificationDeliveries Table**: `deliveryId` (PK), `notificationId` (GSI), `channel`, `status`, `recipient`, `retryCount`, `errorMessage`, `channelMetadata` (Map), `sentAt`

### Success Criteria

- **SC-001**: Query by `recipientUserId` returns results within 50ms at p99

---

## Phase 4: API Gateway REST → Lambda

**Goal:** Replace ASP.NET Core API with Lambda functions via API Gateway REST. Direct cutover a DynamoDB.

### Story 1 - REST API Lambda Functions (Priority: P1)

Each API endpoint becomes a separate Lambda function. Lambda conecta directo a DynamoDB.

**Acceptance Scenarios:**
1. **Given** Lambda function deployed, **When** HTTP request hits API Gateway, **Then** Lambda executes and returns proper HTTP response
2. **Given** Lambda function, **When** request validation fails, **Then** return 400 with validation errors

### Story 2 - Endpoint Parity (Priority: P1)

All existing endpoints must work identically.

| Method | Path | Lambda Function |
|--------|------|-----------------|
| POST | `/notifications` | `NotifyHub-CreateNotification` |
| GET | `/notifications/{id}` | `NotifyHub-GetNotification` |
| GET | `/notifications` | `NotifyHub-ListNotifications` |
| GET | `/notifications/unread-count` | `NotifyHub-GetUnreadCount` |
| PATCH | `/notifications/{id}/read` | `NotifyHub-MarkAsRead` |
| PATCH | `/notifications/read-all` | `NotifyHub-MarkAllAsRead` |

**Acceptance Scenarios:**
1. **Given** all Lambdas deployed, **When** API Gateway configured, **Then** all 6 endpoints respond correctly
2. **Given** `/notifications` list endpoint, **When** called with `userId`, `page`, `pageSize`, `unreadOnly`, **Then** return paginated results

### Requirements

- **FR-016**: API Gateway REST API with Lambda proxy integration for each endpoint
- **FR-017**: Lambda functions must use same request/response DTOs as current API
- **FR-018**: API Gateway must have CORS configured for demo domain
- **FR-019**: Lambda reads/writes DynamoDB directly (no PostgreSQL)

### Key Entities

- **Lambda Functions**: `NotifyHub-CreateNotification`, `NotifyHub-GetNotification`, `NotifyHub-ListNotifications`, `NotifyHub-GetUnreadCount`, `NotifyHub-MarkAsRead`, `NotifyHub-MarkAllAsRead`
- **API Gateway**: `NotifyHub-REST-API`

---

## Phase 5: SignalR → API Gateway WebSocket

**Goal:** Replace SignalR real-time notifications with API Gateway WebSocket API.

### Story 1 - WebSocket API (Priority: P1)

WebSocket endpoint for real-time notification delivery.

**Acceptance Scenarios:**
1. **Given** client connects to WebSocket, **When** `JoinUserGroup` message sent with `userId`, **Then** connection added to user-specific group
2. **Given** notification created, **When** broadcast called, **Then** all connections in user group receive `NewNotification` event

### Story 2 - Client Reconnection (Priority: P2)

Handle client disconnection and reconnection gracefully.

**Acceptance Scenarios:**
1. **Given** client disconnects, **When** Lambda receives disconnect, **Then** client removed from all groups
2. **Given** client reconnects, **When** `JoinUserGroup` called, **Then** client re-joins groups

### Requirements

- **FR-020**: API Gateway WebSocket API with route `$connect`, `$disconnect`, `JoinUserGroup`, `LeaveUserGroup`
- **FR-021**: WebSocket connection URL: `wss://{api-id}.execute-api.us-east-1.amazonaws.com/production`
- **FR-022**: Connection table (DynamoDB) to track `connectionId` → `userId` mapping
- **FR-023**: Broadcast must use `PostToConnection` API call

### Key Entities

- **WebSocket API**: `NotifyHub-WebSocket-API`
- **Connections Table**: `connectionId` (PK), `userId`, `connectedAt`

---

## Phase 6: Cutover & Decommission

**Goal:** Complete migration and decommission VPS components.

### Story 1 - DNS Cutover (Priority: P1)

Point domain to AWS infrastructure.

**Acceptance Scenarios:**
1. **Given** all AWS components verified, **When** DNS TTL expires, **Then** all traffic routes through AWS
2. **Given** issues detected post-cutover, **When** rollback needed, **Then** DNS can be reverted within 5 minutes

### Story 2 - VPS Decommission (Priority: P2)

Shutdown VPS components after verification period.

**Acceptance Scenarios:**
1. **Given** 7-day verification period passed, **When** no issues reported, **Then** VPS can be shutdown
2. **Given** shutdown, **When** audit needed, **Then** all data migrated and backed up

### Requirements

- **FR-024**: Route53 hosted zone configured with API, WebSocket, and CloudFront endpoints
- **FR-025**: Lambda functions must NOT have VPC attachment after PostgreSQL cutover (cost optimization)

---

## Assumptions

- **A-001**: Demo UI already migrated to S3 + CloudFront
- **A-002**: Free tier usage: Lambda (1M), SQS (1M), DynamoDB (25GB), CloudFront (1TB)
- **A-003**: AWS credentials via OIDC Role (no hardcoded keys)
- **A-004**: All Lambda functions in `us-east-1`
- **A-005**: No user authentication; recipients identified by `recipientUserId` UUID only
- **A-006**: Idempotency via `NotificationId + DeliveryId` deduplication key
- **A-007**: GitHub repo public: `hectorrosario22/notify-hub`

## Edge Cases

- **EC-001**: DynamoDB throttling → exponential backoff with jitter
- **EC-002**: SQS visibility timeout → Lambda must complete within 15 min
- **EC-003**: WebSocket connection limit → 500 connections/second per account
- **EC-004**: Lambda cold starts → consider provisioned concurrency
- **EC-005**: DynamoDB eventual consistency → reads may return stale data within 1 sec

## Cost Estimates (Monthly, ~$15-30/month)

| Service | Free Tier | Overage |
|---------|-----------|---------|
| Lambda | 1M requests | $0.20/1K |
| API Gateway REST | 1M calls | $3.50/1K |
| SQS | 1M requests | $0.40/1K |
| DynamoDB | 25GB, 25 RCU/WCU | $1.25/GB |
| CloudFront | 1TB | $0.085/GB |
| GitHub Actions | 2000 min/month | Free (public repo) |

---

## Branch Strategy

All work on `migrate/aws-infrastructure` branch:

```
migrate/aws-infrastructure
├── phase/0-cicd-pipeline
├── phase/1-sqs-queues
├── phase/2-lambda-consumers
├── phase/3-dynamodb-migration
├── phase/4-apigateway-rest
├── phase/5-apigateway-websocket
└── phase/6-cutover (PR to main)
```

### Commit Convention

```
aws(0): feat: add GitHub Actions workflow for demo deployment
aws(1): feat: create SQS queues for email, sms, whatsapp
aws(2): feat: add Lambda consumers for async channels
aws(3): feat: add DynamoDB tables
aws(4): feat: deploy REST API Gateway with Lambda functions
aws(5): feat: add WebSocket API for real-time notifications
aws(6): feat: complete cutover to AWS
```