# System Architecture - Real-time Event Analytics Engine

## Overview

This project is a real-time event analytics platform composed of a .NET 10 backend, a React/Vite frontend, PostgreSQL for durable storage, Redis for caching and live counters, and SignalR for live updates.

The current architecture is already aligned with the implementation and is suitable for the next iteration of work because it cleanly separates:

- API exposure and transport concerns
- command/query handling
- background processing and persistence
- caching and live data distribution
- frontend visualization and real-time updates

## Current High-Level Architecture

```text
┌────────────────────────────────────────────────────────────────────┐
│                         PRESENTATION LAYER                        │
│  ┌──────────────────────┐  ┌───────────────────────────────────┐  │
│  │   REST Endpoints      │  │   SignalR Hub /eventHub          │  │
│  │   /api/events         │  │   Live event broadcasts         │  │
│  │   /api/dashboard      │  │   CORS + performance middleware │  │
│  └──────────────────────┘  └───────────────────────────────────┘  │
└──────────────────────────────┬─────────────────────────────────────┘
                               │
┌──────────────────────────────▼─────────────────────────────────────┐
│                        APPLICATION LAYER                           │
│  ┌──────────────────────────┐  ┌────────────────────────────────┐ │
│  │ LogEventCommandHandler   │  │ FetchDashboardDataQueryHandler│ │
│  │ + validation             │  │ + cache-aside logic          │ │
│  └──────────────────────────┘  └────────────────────────────────┘ │
└──────────────────────────────┬─────────────────────────────────────┘
                               │
┌──────────────────────────────▼─────────────────────────────────────┐
│                      INFRASTRUCTURE LAYER                          │
│  ┌──────────────────────┐  ┌───────────────────────────────────┐   │
│  │ In-Memory Queue      │  │ Background Worker                │   │
│  │ IEventWriteQueue     │  │ EventWriteBackgroundService      │   │
│  └──────────┬───────────┘  └─────────────────┬──────────────────┘   │
│             │                                │                      │
│  ┌──────────▼───────────┐        ┌──────────▼────────────────────┐  │
│  │ PostgreSQL Repository │        │ Redis Cache Service          │  │
│  │ EventWriteRepository │        │ Dashboard + event caching   │  │
│  └──────────────────────┘        └──────────────────────────────┘  │
└────────────────────────────────────────────────────────────────────┘
```

## Runtime Components

### 1. Presentation Layer

- REST endpoints in the backend for event ingestion and dashboard retrieval.
- SignalR hub for live event streaming to the frontend.
- Middleware for request timing and basic observability.
- CORS policy configured for the Vite/React frontend.

### 2. Application Layer

- Command and query handlers encapsulate business flow.
- FluentValidation is used for input validation.
- The handlers are intentionally lightweight and delegate persistence and cross-cutting concerns to infrastructure services.

### 3. Infrastructure Layer

- In-memory channel-based queue for fast ingestion.
- Background service that consumes queued events, batches them, and flushes them to PostgreSQL.
- Repository layer using Dapper and Npgsql for efficient SQL execution.
- Redis-based cache for dashboard snapshots and recent event data.
- Database initializer for bootstrapping the persistent store.

## Request Flow

### Event Ingestion Flow (POST /api/events)

1. The client sends an event payload to the API.
2. The endpoint validates the request.
3. The command handler enriches the timestamp if needed and enqueues the event.
4. The HTTP request returns 202 Accepted immediately.
5. The background worker dequeues events, batches them, and writes them to PostgreSQL.
6. Redis is updated for counters and short-lived event cache.
7. SignalR broadcasts the event to connected frontend clients.

### Dashboard Query Flow (GET /api/dashboard)

1. The dashboard endpoint validates the window parameter.
2. The query handler checks Redis first.
3. If the cache misses, it reads aggregated data from PostgreSQL.
4. The result is cached and returned to the client.
5. The frontend renders the metrics and charts using the returned data.

## Live Realtime Flow

The frontend uses SignalR to subscribe to live events.

- The backend publishes to the hub through the background worker.
- The React client listens for event messages and updates the live list immediately.
- This keeps the experience responsive without blocking the ingestion API.

## Data Stores

### PostgreSQL

- Durable source of truth for analytics events.
- Stores event rows with event type, timestamp, payload, and source.
- Used for dashboard aggregations and historical reporting.

### Redis

- Used for fast cached dashboard responses.
- Stores recent event values and live counters.
- Helps reduce load on PostgreSQL during dashboard-heavy traffic.

## Current Design Decisions

### Why this design fits the project

- Fast write path: the API does not wait on database writes.
- Resilient processing: event persistence occurs asynchronously in the background.
- Clear separation of concerns: handlers, infrastructure, and endpoints are isolated.
- Good fit for dashboards and live monitoring workloads.

### Current runtime characteristics

- Ingest endpoint returns quickly with 202 Accepted.
- Persistence uses a batch strategy of 100 events or a 5-second timeout.
- Dashboard reads benefit from Redis-backed caching.
- The system is stateless at the API layer and can be scaled horizontally.

## Frontend Structure

The frontend is built with React + Vite and uses:

- Dashboard for high-level metrics and charts
- EventLogger for event submission input
- RealtimePanel for live feed visualization
- custom hooks for dashboard and SignalR communication

## Deployment Model

The project is containerized with Docker Compose.

Services included:

- PostgreSQL
- Redis
- Backend API
- Frontend UI

Typical environment variables:

- Redis__ConnectionString
- ConnectionStrings__EventStore
- ASPNETCORE_ENVIRONMENT

## Evolution Path

The current architecture is solid, but the following additions would improve it for new work:

- Add structured observability with OpenTelemetry and distributed tracing.
- Introduce retries and dead-letter handling for background processing failures.
- Add authentication and tenant isolation if multi-user access is required.
- Replace the in-memory queue with Kafka or RabbitMQ for distributed event pipelines.
- Add event schema versioning and contract validation.
- Add automated integration tests for ingestion, dashboard aggregation, and SignalR delivery.

## Summary

Architecture Type: Event-driven, async ingestion, cache-aside, realtime broadcast

Technology Stack:
- .NET 10
- PostgreSQL
- Redis
- SignalR
- React + Vite
- Serilog

Scalability: Horizontal for API instances, shared Redis/PostgreSQL backing services

Resilience: Async background processing, cache fallback, non-blocking API response path
