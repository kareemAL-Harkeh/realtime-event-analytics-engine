# Real-time Event Analytics Engine

<div align="left">
  <a href="https://analytics-ex.duckdns.org/" target="_blank">
    <img src="https://img.shields.io/badge/Live_Demo-Interactive_Dashboard-0f766e?style=for-the-badge&logo=react&logoColor=white" alt="Live Demo" />
  </a>
  <a href="https://github.com/kareemAL-Harkeh/realtime-event-analytics-engine" target="_blank">
    <img src="https://img.shields.io/badge/View_Source-GitHub-181717?style=for-the-badge&logo=github&logoColor=white" alt="View source on GitHub" />
  </a>
  <a href="https://www.linkedin.com/in/kareem-al-harkeh/" target="_blank">
    <img src="https://img.shields.io/badge/Connect-LinkedIn-0A66C2?style=for-the-badge&logo=linkedin&logoColor=white" alt="Connect on LinkedIn" />
  </a>
  <a href="https://kareemalharkeh.vercel.app/" target="_blank">
    <img src="https://img.shields.io/badge/Portfolio-Kareem_Alharkeh-0f766e?style=for-the-badge&logo=vercel&logoColor=white" alt="Open portfolio" />
  </a>
  <img src="https://img.shields.io/badge/.NET_10-Backend-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" alt=".NET 10" />
  <img src="https://img.shields.io/badge/React_19-Frontend-149ECA?style=for-the-badge&logo=react&logoColor=white" alt="React 19" />
  <img src="https://img.shields.io/badge/CI-GitHub_Actions-2088FF?style=for-the-badge&logo=github-actions&logoColor=white" alt="GitHub Actions" />
</div>

## Overview

Real-time Event Analytics Engine is a production-oriented telemetry pipeline and live monitoring dashboard. It accepts high-frequency events, acknowledges them without waiting for database I/O, persists them asynchronously in batches, and streams successfully persisted events to connected dashboard clients.

The project demonstrates practical backend engineering decisions for event-heavy systems: write-behind processing, bounded in-memory queues, batch persistence, cache-aside reads, role-based API key authorization, rate limiting, structured logging, and automated quality checks.

## What It Solves

Operational systems need visibility into what is happening now, which services are producing events, and whether failures are increasing. This project provides:

- A fast event ingestion endpoint for services, devices, or applications.
- A dashboard endpoint for aggregated event metrics.
- A live event feed powered by SignalR.
- Durable PostgreSQL storage with Redis acceleration for read-heavy dashboards.

## Architecture

```text
Client
  |
  | POST /api/events
  v
ASP.NET Core API
  | validate -> authorize -> rate limit
  v
Bounded in-memory channel
  |
  v
Background worker
  | batch up to 100 events or flush every 2 seconds
  +--> PostgreSQL (durable storage)
  +--> Redis (cache and counters)
  +--> SignalR /eventHub (live broadcast after successful persistence)
```

### Event ingestion flow

1. The client sends an event to `POST /api/events`.
2. FluentValidation checks the payload.
3. The handler enriches a missing timestamp and places the event in the bounded queue.
4. The API returns `202 Accepted` without waiting for PostgreSQL, Redis, or SignalR.
5. The background worker flushes a batch when it reaches 100 events or the 2-second timer fires.
6. After a successful database write, Redis is updated and SignalR broadcasts the event.

### Dashboard flow

1. The client calls `GET /api/dashboard?windowMinutes=...`.
2. The query handler checks Redis first.
3. On a cache miss, aggregated data is read from PostgreSQL and cached.
4. Locks are scoped per time-window value so unrelated dashboard windows do not block each other.

## Key Engineering Decisions

- **Asynchronous write-behind processing:** keeps the ingestion path responsive under burst traffic.
- **Bounded queue with immediate rejection:** applies back-pressure without making callers wait indefinitely.
- **Batch writes:** reduces database round trips and improves throughput.
- **`ON CONFLICT DO NOTHING`:** prevents one duplicate event ID from failing an entire batch.
- **Post-persistence side effects:** prevents Redis and SignalR from showing events that were not stored successfully.
- **Independent flush timer:** ensures low-traffic batches do not remain in memory indefinitely.
- **Role-based API keys:** keeps service ingestion separate from dashboard read access.
- **Global exception handling:** returns a consistent error response and correlates failures with a trace ID.
- **Environment-aware seeding:** sample data is restricted to development environments.

## Features

### Backend

- Minimal REST APIs built with ASP.NET Core and .NET 10.
- `POST /api/events` for asynchronous event ingestion.
- `GET /api/dashboard` for aggregated metrics.
- SignalR hub for live event delivery.
- FluentValidation for command and query validation.
- Dapper and Npgsql for efficient PostgreSQL access.
- Redis cache-aside strategy and live counters.
- Token-bucket rate limiting for ingestion.
- Fixed-window rate limiting for dashboard reads.
- Serilog console and rolling-file logging.
- Global exception handling with traceable error responses.

### Frontend

- React 19 with TypeScript.
- Vite development and production build tooling.
- Tailwind CSS for responsive styling.
- Recharts for event distribution visualizations.
- Framer Motion for interface transitions.
- SignalR client for live event updates.
- Nginx container for production static hosting.

## Technology Stack

| Area | Technologies |
| --- | --- |
| Backend | C#, .NET 10, ASP.NET Core Minimal APIs |
| Application patterns | CQRS-style handlers, layered architecture, dependency inversion |
| Validation | FluentValidation |
| Persistence | PostgreSQL, Dapper, Npgsql |
| Caching | Redis, StackExchange.Redis |
| Realtime | SignalR |
| Observability | Serilog, performance logging middleware |
| Frontend | React 19, TypeScript, Vite, Tailwind CSS |
| Visualization | Recharts, Framer Motion, Lucide React |
| Testing | xUnit, Microsoft.NET.Test.Sdk, Coverlet, NetArchTest |
| Delivery | Docker, Docker Compose, Nginx, GitHub Actions |

## Project Structure

```text
backend/
  Core/
    Commands/
    Constants/
    Interfaces/
    Queries/
    Validation/
  Infrastructure/
    Cache/
    Constants/
    Data/
    Extensions/
    Logging/
  Presentation/
    Authentication/
    Endpoints/
    Extensions/
    Hubs/
    Middleware/
    Responses/

Tests/
  Architecture/
  Core/
  Infrastructure/
  Integration/
  TestDoubles/

frontend/
  src/
    components/
    hooks/
    lib/
    api.ts
    types.ts

.github/workflows/ci.yml
docker-compose.yml
```

## API Contract

### Authentication

Protected REST endpoints use the `X-Api-Key` header:

```http
X-Api-Key: <api-key>
```

Available roles:

| Role | Endpoint |
| --- | --- |
| `ingestion-client` | `POST /api/events` |
| `dashboard-client` | `GET /api/dashboard` |

Missing or invalid keys return `401 Unauthorized`. A valid key with the wrong role returns `403 Forbidden`.

### `POST /api/events`

```bash
curl -X POST "http://localhost:5261/api/events" \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: <ingestion-api-key>" \
  -d '{
    "eventType": "payment_failed",
    "payload": "{\"transactionId\":\"txn-456\",\"reason\":\"timeout\"}",
    "source": "payment-service"
  }'
```

The endpoint returns `202 Accepted` when the event is successfully queued. `timestamp` is optional; when omitted, the backend assigns the current UTC time.

### `GET /api/dashboard`

```bash
curl "http://localhost:5261/api/dashboard?windowMinutes=30" \
  -H "X-Api-Key: <dashboard-api-key>"
```

`windowMinutes` must be between 1 minute and 43,200 minutes (30 days). The response contains total events, event counts by type, and the recent success rate.

### SignalR

The hub is available at:

```text
/eventHub
```

Clients listen for the `ReceiveEvent` message. The current hub endpoint is intentionally unauthenticated; REST API key authorization does not automatically secure SignalR browser connections.

## Running Locally

### Prerequisites

- Docker Desktop
- .NET 10 SDK
- Node.js 20 or newer
- npm

### Run the complete stack with Docker

From the repository root:

```bash
docker compose up --build
```

The Compose stack contains PostgreSQL, Redis, the backend API, and the frontend served by Nginx. Connection strings, API keys, and ports can be supplied through environment variables or an untracked `.env` file.

### Run the backend directly

Start PostgreSQL and Redis first, then run:

```bash
cd backend
dotnet restore
dotnet run
```

### Run the frontend directly

```bash
cd frontend
npm install
npm run dev
```

The Vite development proxy forwards `/api` and `/eventHub` to the backend at `http://localhost:5261`.

## Configuration

Important backend configuration keys include:

```text
ConnectionStrings__EventStore
Redis__ConnectionString
ASPNETCORE_ENVIRONMENT
ApiKeys__0__Key
ApiKeys__0__Name
ApiKeys__0__Role
ApiKeys__1__Key
ApiKeys__1__Name
ApiKeys__1__Role
```

For Docker Compose, the commonly used variables are:

```text
INGESTION_API_KEY
DASHBOARD_API_KEY
EVENT_STORE_CONNECTION
REDIS_CONNECTION_STRING
ASPNETCORE_ENVIRONMENT
```

Do not commit real keys or passwords. Use `.env` locally and your deployment platform's secret store in production.

## Testing and CI

The backend test project covers core logic, validators, queue behavior, architecture rules, HTTP endpoint authorization, validation, rate limiting, and dashboard responses. Integration tests use `WebApplicationFactory` with in-memory fakes for PostgreSQL and Redis, keeping the pipeline tests fast and deterministic.

Run all backend tests:

```bash
dotnet test Tests/Tests.csproj
```

Run frontend quality checks:

```bash
cd frontend
npm run lint
npm run build
```

GitHub Actions runs the backend test project and frontend lint/build checks on every `push` and `pull_request` through [`.github/workflows/ci.yml`](.github/workflows/ci.yml).
