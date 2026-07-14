# Real-time Event Analytics Engine

A modern, production-oriented event-driven analytics platform built with ASP.NET Core and React. The system is designed to ingest high-volume events, process them asynchronously, persist them efficiently, cache dashboard data for low-latency reads, and broadcast live updates to the frontend in real time.

This project demonstrates a practical implementation of an observability-style architecture suitable for monitoring systems, SaaS products, payment platforms, e-commerce services, security telemetry, and internal business operations.

## Why this project exists

In modern systems, operational visibility is critical. Teams need to answer questions such as:

- What is happening right now?
- Are there spikes in errors or warnings?
- Which services are generating the most events?
- Is the system healthy or degrading?
- How fast can we detect and respond to incidents?

This project provides a foundation for answering those questions through a real-time analytics dashboard and an event ingestion pipeline designed for performance and scalability.

## Key capabilities

- High-throughput event ingestion via ASP.NET Core endpoints
- Asynchronous background processing using a memory channel and background worker
- Durable persistence to PostgreSQL through batch inserts
- Fast dashboard reads using Redis cache-aside pattern
- Live updates to the UI through SignalR
- Clean separation of concerns using a layered architecture
- Modern frontend dashboard with charts and real-time metrics
- Docker-based deployment for local and containerized environments

## Architecture overview

The system follows a layered and event-driven design:

- Presentation layer: API endpoints, SignalR hub, middleware
- Application layer: command/query handlers and validation
- Infrastructure layer: PostgreSQL repository, Redis cache, background processing, logging

### Request flow

1. A client sends an event to the API.
2. The request is validated and accepted quickly.
3. The event is placed into an in-memory queue.
4. A background service processes the queue and writes events in batches to PostgreSQL.
5. Redis is updated for cache acceleration and live counters.
6. SignalR broadcasts new events to connected clients in real time.
7. The dashboard reads aggregated metrics from cache first, falling back to the database when needed.

## Main features

### Backend

- REST API for event submission
- REST API for dashboard metrics
- Validation middleware using FluentValidation
- Structured logging with Serilog
- Background service for non-blocking persistence
- Redis-backed caching strategy
- SignalR real-time communication

### Frontend

- Modern React + TypeScript interface
- Vite-based fast development experience
- Rich analytics cards and charts
- Real-time dashboard updates
- Responsive UI for desktop and tablet experiences

## Tech stack

### Frontend

- React
- TypeScript
- Vite
- Tailwind CSS
- Framer Motion
- Recharts
- Lucide React
- SignalR client

### Backend

- ASP.NET Core
- C#
- .NET 10
- FluentValidation
- Serilog
- SignalR
- Dapper
- Npgsql
- StackExchange.Redis
- Bogus

### Data & infrastructure

- PostgreSQL
- Redis
- Docker
- Docker Compose
- Nginx

## Project structure

```text
backend/
  1.Core/
    Commands/
    Interfaces/
    Queries/
    Validation/
  2.Infrastructure/
    Cache/
    Constants/
    Data/
    Extensions/
    Logging/
  3.Presentation/
    Endpoints/
    Hubs/
    Middleware/
    Responses/

frontend/
  src/
    components/
    hooks/
    lib/
    api.ts
    types.ts

Dockerfile files
docker-compose.yml
README.md
```

## Getting started

### Prerequisites

- Docker Desktop
- .NET 10 SDK (optional if you want to run the backend directly)
- Node.js 20+ and npm (for frontend development)

### Run with Docker

From the project root:

```bash
docker compose up --build
```

This brings up:

- PostgreSQL
- Redis
- Backend API
- Frontend UI

### Access the application

- Frontend: http://localhost:5174
- Backend API: http://localhost:5261

## API endpoints

### POST /api/events

Accepts a new event payload and returns a fast acknowledgement.

Example request:

```json
{
  "eventType": "error",
  "payload": "{\"message\":\"Payment failed\"}",
  "source": "payment-service"
}
```

### GET /api/dashboard

Returns dashboard metrics for a selected time window.

Example:

```bash
curl "http://localhost:5261/api/dashboard?windowMinutes=15"
```

### SignalR hub

Real-time event updates are broadcast through:

```text
/eventHub
```

## Configuration

The backend uses configuration values such as:

- Redis connection string
- PostgreSQL connection string
- CORS origins
- environment-specific settings

Typical configuration keys include:

- Redis__ConnectionString
- ConnectionStrings__EventStore

## Development workflow

### Backend

```bash
cd backend
dotnet restore
dotnet build
dotnet run
```

### Frontend

```bash
cd frontend
npm install
npm run dev
```

## Database behavior

On startup, the application can:

- create the required table if it does not exist
- initialize the database connection
- seed sample data when the database is empty

This makes local development and testing straightforward.

## Performance characteristics

The architecture is designed to optimize both write and read paths:

- Writes are fast because persistence is deferred to a background worker.
- Reads are fast because dashboard metrics are cached in Redis.
- Batch inserts reduce the number of database round trips.
- Live updates avoid full-page refreshes through SignalR.

## Testing & quality engineering

This project is designed with a modern testing mindset, not just as a demo, but as a production-oriented system where reliability and regression safety are first-class concerns.

### Testing approach

- Unit tests for core business logic, command handlers, validators, and queue behavior
- Behavioral tests for cache-aside logic and background processing flows
- Regression tests to protect dashboard aggregation and real-time event processing paths
- Quality gates that help maintain stability as the platform evolves

### Current test foundation

- xUnit as the main test framework
- FluentAssertions for expressive and readable assertions
- Coverlet for code coverage collection
- Test suites organized around core and infrastructure responsibilities

### Recommended next steps

- Add integration tests for API endpoints and database interactions
- Introduce container-based tests for PostgreSQL and Redis behavior
- Add smoke and end-to-end tests for Docker Compose deployments
- Establish CI pipelines with automatic build, test, and coverage reporting

### Test command

```bash
cd tests
dotnet test
```

## Summary

Real-time Event Analytics Engine is a practical and modern example of how to build an event-driven analytics system that is fast, responsive, scalable, and visually helpful. It blends backend reliability, real-time communication, and modern UI development into a single cohesive platform.
