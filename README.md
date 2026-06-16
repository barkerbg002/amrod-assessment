# SadcOMS — SADC Order Management System

A full-stack order management system for Southern African Development Community (SADC) countries.

## Architecture

```
src/
├── SadcOMS.Domain/          # Entities, enums, domain rules (SADC validation, status transitions)
├── SadcOMS.Infrastructure/  # EF Core, repositories, Outbox, RabbitMQ, FX provider
├── SadcOMS.API/             # ASP.NET Core 8 REST API
├── SadcOMS.Worker/          # Outbox publisher + OrderCreated consumer
└── SadcOMS.Tests/           # xUnit unit + integration tests
web/                         # React + TypeScript (Vite)
```

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js 20+](https://nodejs.org/)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (for containerized setup)

## Quick Start (Docker Compose)

> **Apple Silicon (M1/M2/M3):** The default `sqlserver` service uses **Azure SQL Edge** (`:latest`), which has a native `arm64` image. The tag `:2.2.0` does not exist in MCR (valid tags: `latest`, `2.0.0`, `1.0.x`). For full SQL Server 2022 via Rosetta emulation:
> ```bash
> docker compose -f docker-compose.yml -f docker-compose.sqlserver.yml up -d sqlserver
> ```

```bash
# Start infrastructure + services
docker compose up -d sqlserver rabbitmq

# Wait for SQL Server to become healthy (about 45 seconds)
sleep 45
docker compose run --rm migrate

# Start API, Worker, and Web
docker compose up -d api worker web
```

| Service   | URL                                      |
|-----------|------------------------------------------|
| Web UI    | http://localhost:3000                    |
| API       | http://localhost:8080                    |
| Swagger   | http://localhost:8080/swagger            |
| RabbitMQ  | http://localhost:15672 (guest/guest)     |

## Local Development (without Docker)

### 1. Start SQL Server and RabbitMQ

```bash
docker compose up -d sqlserver rabbitmq
```

### 2. Apply EF Core Migrations

```bash
dotnet tool install --global dotnet-ef --version 8.0.11
dotnet ef database update \
  --project src/SadcOMS.Infrastructure \
  --startup-project src/SadcOMS.API
```

### 3. Run the API

```bash
cd src/SadcOMS.API
dotnet run
# Swagger: http://localhost:5000/swagger (or port shown in console)
```

### 4. Run the Worker

```bash
cd src/SadcOMS.Worker
dotnet run
```

### 5. Run the Web Frontend

```bash
cd web
npm install
npm run dev
# http://localhost:5173
```

## Running Tests

```bash
dotnet test src/SadcOMS.Tests/SadcOMS.Tests.csproj
```

## Sample API Requests

With Docker Compose or local Development, `Auth:DevBypass` is on by default — the curls below work **without** an `Authorization` header. For the bearer-token path, see [Authentication](#authentication).

### Create a Customer

```bash
curl -X POST http://localhost:8080/api/customers \
  -H "Content-Type: application/json" \
  -d '{"name":"Acme Trading","email":"orders@acme.co.za","countryCode":"ZA"}'
```

### Create an Order

```bash
curl -X POST http://localhost:8080/api/orders \
  -H "Content-Type: application/json" \
  -d '{
    "customerId": "<CUSTOMER_ID>",
    "currencyCode": "ZAR",
    "lineItems": [
      {"productSku": "WIDGET-001", "quantity": 2, "unitPrice": 149.99}
    ]
  }'
```

### Update Order Status (with Idempotency-Key)

```bash
curl -X PUT http://localhost:8080/api/orders/<ORDER_ID>/status \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: $(uuidgen)" \
  -d '{"status":"Paid","rowVersion":"<BASE64_ROW_VERSION>"}'
```

### ZAR Conversion Report

```bash
curl http://localhost:8080/api/reports/orders/zar
```

A Postman collection is available at `docs/SadcOMS.postman_collection.json`.

## Authentication

Write endpoints (`POST /api/customers`, `POST /api/orders`, `PUT /api/orders/{id}/status`) require authentication. GET endpoints are anonymous.

### Dev bypass (default for local + Docker)

`Auth:DevBypass` defaults to **true** in `appsettings.Development.json` and Docker Compose (`Auth__DevBypass=true`). When enabled, a dev authentication handler signs every request in as `dev-user` — no bearer token needed. The React UI and Docker smoke tests work unchanged.

Set `Auth:DevBypass` to **false** in production (`appsettings.json` default).

### JWT bearer (real path)

JWT validation is config-driven via the `Auth` section:

| Setting | Purpose |
|---------|---------|
| `SigningKey` | Symmetric key for local/dev JWT validation (min 32 chars) |
| `Issuer` / `Audience` | Validated on every token |
| `Authority` | Set to Entra OIDC URL in production (config swap — no code change) |

**Get a dev token** (Development or when `DevBypass` is on):

```bash
curl -X POST http://localhost:8080/api/auth/token
# { "accessToken": "...", "expiresIn": 3600 }
```

Use it on write requests:

```bash
curl -X POST http://localhost:8080/api/customers \
  -H "Authorization: Bearer <accessToken>" \
  -H "Content-Type: application/json" \
  -d '{"name":"Acme","email":"a@acme.co.za","countryCode":"ZA"}'
```

### CORS

CORS is currently `AllowAnyOrigin` so the Vite dev server and Docker nginx proxy work without extra setup. When wiring real Entra auth, restrict CORS to the SPA origin (e.g. `https://app.example.com`) and send the bearer token from the SPA after the Entra login flow.

## Key Features

- **JWT on write endpoints** — `[Authorize]` on POST/PUT; dev bypass on by default for Docker/demo (`Auth:DevBypass`)
- **SADC currency validation** with CMA (Common Monetary Area) logic
- **Transactional Outbox** pattern for reliable OrderCreated events
- **RabbitMQ** publishing with DLQ and retry
- **Optimistic concurrency** via SQL Server ROWVERSION
- **Idempotency-Key** support on status updates (scoped to request path)
- **Mock FX provider** with in-memory caching (15 min TTL)
- **React frontend** with customer/order CRUD, filters, and status transitions

## Documentation

- [ANSWERS.md](ANSWERS.md) — Architecture decisions, scaling, security, SQL section
- [MIGRATIONS.md](MIGRATIONS.md) — EF Core migration strategy and CI validation

## Configuration

Connection strings and RabbitMQ settings are in `appsettings.json` for both API and Worker. Override via environment variables using the standard ASP.NET Core `__` convention:

```bash
ConnectionStrings__DefaultConnection="Server=..."
RabbitMQ__HostName=rabbitmq
FxCache__TtlMinutes=15
Auth__DevBypass=true
Auth__SigningKey="sadcoms-dev-signing-key-min-32-chars!!"
```
