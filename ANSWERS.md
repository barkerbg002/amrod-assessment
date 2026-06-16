# SadcOMS — Technical Answers

## 1. System Architecture Structure (Layering, Why)

SadcOMS follows a layered layout with four backend projects plus a separate frontend:

- **Domain** — Pure business logic with zero infrastructure dependencies: entities, enums, `OrderTotalCalculator`, `SadcCurrencyValidator`, `OrderStatusTransitionValidator`. This layer is fully unit-testable without databases or HTTP.
- **Infrastructure** — EF Core `DbContext`, repositories, RabbitMQ publisher, Outbox persistence, and the mock FX provider. Implements data access and messaging concerns behind interfaces consumed by the API.
- **API** — Thin controllers, DTOs, application services, and middleware (correlation IDs). Orchestrates domain rules and infrastructure without embedding business logic in controllers.
- **Worker** — Separate deployable process hosting `OutboxPublisher` (polls DB → publishes to RabbitMQ) and `OrderCreatedConsumer` (simulates fulfillment). Decouples async processing from the request/response path.

This is a conventional layered split: Domain has no infrastructure references, Infrastructure implements persistence/messaging, API stays thin. The Worker is a separate process so RabbitMQ issues (slow consumer, poison message) don't take down order creation HTTP endpoints.

Trade-off: more projects and DTO mapping than a single-project API. Worth it here because the domain rules (SADC currency pairing, status transitions) are non-trivial and I wanted them testable without SQL or HTTP.

---

## 2. Scaling to 50k Orders/Minute Peak Writes

50k orders/min is ~833 sustained writes/sec — a single SQL Server primary won't hold that without architectural changes.

What the current codebase already does: short write transactions (Order + Outbox in one `SaveChanges`), async downstream work via outbox → RabbitMQ, and a separate Worker process.

What I'd add next, in order:

1. **Scale the outbox publisher** — multiple `OutboxPublisher` instances with `UPDLOCK/READPAST` polling (see SQL section; current EF query is single-instance only).
2. **Read replicas** — route GET endpoints to replicas via a read/write `DbContext` factory.
3. **Horizontal RabbitMQ consumers** — `prefetch` is already set to 10; add more worker replicas behind queue depth metrics.
4. **CQRS read model** (if list/report queries become hot) — project `OrderCreated` into a denormalized store; keep the write DB normalized.

Sharding by `CountryCode` or `CustomerId` is a later step if a single primary exhausts vertical scale.

---

## 3. Message Reliability (Outbox + At-Least-Once + Idempotent Consumers + DLQ)

**Transactional Outbox**: Order creation and `OutboxMessage` insert happen in a single EF Core `SaveChanges()` call within one database transaction. This guarantees that if the order exists, an outbox entry exists — no lost events due to crash between DB commit and message publish.

**At-least-once delivery**: The `OutboxPublisher` polls unprocessed rows, publishes to RabbitMQ, and marks `ProcessedOn` on success. If the publisher crashes after publishing but before marking processed, the message will be republished on restart. This is acceptable because consumers must be **idempotent**.

**Idempotent consumers**: The `OrderCreatedConsumer` should check a `ProcessedMessages` table (keyed by `OrderId` + event type) before performing fulfillment allocation. Duplicate deliveries are silently acknowledged. In this assessment, the consumer logs and simulates allocation — production would use an idempotency store.

**Retry with exponential backoff**: The OutboxPublisher retries failed publishes with `2^retry` second delays (configured up to 5 attempts), logging errors to the `Error` column.

**Dead-letter queue (DLQ)**: RabbitMQ queue is configured with `x-dead-letter-exchange` pointing to `order.created.dlq`. On failure the consumer **acks and republishes** the message with an incremented `x-retry-count` header (RabbitMQ does not update headers on `BasicNack(requeue:true)`). After three failed retries (`x-retry-count` reaches 3 on the fourth delivery), the message is `BasicNack`ed with `requeue: false` and routed to the DLQ.

**Ordering**: Outbox polling uses `ORDER BY OccurredOn` to maintain approximate FIFO. Strict ordering per customer would require partition keys.

**Note on polling implementation**: `OutboxRepository` uses a plain EF `Where(ProcessedOn == null).OrderBy(OccurredOn).Take(n)` query — fine for a single publisher instance. The SQL section below shows the `UPDLOCK/READPAST` pattern I'd use for multi-instance publishers in production.

---

## 4. API Versioning Strategy

**Recommendation: URL path versioning** (`/api/v1/orders`) as the primary strategy, with optional `Accept-Version` header support for clients that prefer headers.

**Why URL versioning**: Explicit, visible in logs, Swagger, and browser dev tools. Easy to route at the reverse proxy level (nginx/Azure APIM). Most SADC enterprise integrations (ERP systems, payment gateways) expect stable URL paths.

**Header versioning** (`api-version: 2024-06-01`): Useful for mobile apps that don't want URL changes, but harder to test in browser/Swagger without custom headers.

**Deprecation policy**:
1. New version released alongside old (v1 + v2 coexist)
2. `Sunset` and `Deprecation` response headers on v1 endpoints
3. Minimum 6-month deprecation window with documented migration guide
4. v1 removed only after telemetry confirms <1% traffic

For this assessment, endpoints are unversioned (`/api/orders`) as a single initial release. Versioning would be introduced before the first breaking change (e.g., changing `TotalAmount` precision or status enum values).

---

## 5. Security with Microsoft Entra / JWT

### Implemented in this solution

The API uses **JWT Bearer authentication** (`Microsoft.AspNetCore.Authentication.JwtBearer`) with an `Auth` configuration section:

```json
"Auth": {
  "DevBypass": false,
  "Authority": "",
  "Issuer": "sadcoms-dev",
  "Audience": "sadcoms-api",
  "SigningKey": "<symmetric-key-min-32-chars>"
}
```

**Local / dev validation** uses the symmetric `SigningKey` with `Issuer` and `Audience`. **Production Entra** is a config swap only: set `Authority` to `https://login.microsoftonline.com/{tenantId}/v2.0` and `Audience` to the Entra app registration API URI; remove reliance on `SigningKey` (signing keys are resolved from OIDC metadata). No controller or middleware rewrite is required.

**Authorization policies** are registered in `AuthServiceExtensions` for the Entra scopes we expect (`OrdersWrite`, `OrdersRead`, `CustomersWrite`, `Admin`). Controllers currently use plain `[Authorize]` on write endpoints — any authenticated caller passes. Tightening to `[Authorize(Policy = "OrdersWrite")]` etc. is a one-line change per action once Entra issues scoped tokens in production.

Mutating endpoints (`POST /api/customers`, `POST /api/orders`, `PUT /api/orders/{id}/status`) are protected with `[Authorize]`. GET list/detail endpoints remain anonymous (add `[Authorize]` on those actions to lock down reads).

**Dev bypass:** When `Auth:DevBypass` is `true` (default in `appsettings.Development.json` and Docker Compose), `DevBypassAuthenticationHandler` authenticates every request as `sub=dev-user` with the expected scope claims — no bearer token needed. Disabled in production (`DevBypass: false` in base `appsettings.json`). Explicit config toggle for the demo, not an accidental hole.

**Dev token endpoint:** `POST /api/auth/token` (Development or when `DevBypass` is on) mints a JWT signed with `SigningKey`, returning `{ accessToken, expiresIn }`. Use this to test the real bearer path with `DevBypass` turned off.

### Production Entra plan (SPA + API)

**Auth flow (Authorization Code + PKCE for SPA, Client Credentials for services)**:
1. React SPA redirects to Microsoft Entra ID login
2. User authenticates; Entra returns authorization code
3. SPA exchanges code for access token (PKCE prevents interception)
4. API validates JWT via the configured `Auth:Authority` / `Auth:Audience` (optionally `Microsoft.Identity.Web` for richer Entra integration)

**Token validation middleware** (production Entra — equivalent to current `AddJwtBearer` with `Authority` set):
```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = configuration["Auth:Authority"];
        options.Audience = configuration["Auth:Audience"];
    });
```

**Scopes/roles** (to be issued by Entra; policies above are ready when we wire them to controllers):
- `orders.read` — GET customers/orders/reports
- `orders.write` — POST/PUT orders
- `customers.write` — POST customers
- `SadcOMS.Admin` — app role for status overrides and reports

**Additional hardening**: HTTPS only, CORS restricted to known origins in production (currently `AllowAnyOrigin` for the assessment demo — tighten to the SPA origin when Entra is enabled), rate limiting via ASP.NET Core middleware, and POPIA-compliant audit logging of data access (see §9).

---

## 6. Testing Strategy / Test Pyramid

```
        ╱  E2E (few)  ╲         Not implemented — would use Playwright against docker-compose
       ╱ Integration    ╲       WebApplicationFactory + InMemory EF: idempotency, auth, 409s
      ╱   Unit (many)    ╲      Domain + Worker: totals, SADC rules, retries, FX rounding
```

**59 tests total** (run `dotnet test src/SadcOMS.Tests/SadcOMS.Tests.csproj`):

| Layer | Count | What |
|-------|------:|------|
| Domain | 42 | `OrderTotalCalculator`, `SadcCurrencyValidator`, status transitions, FX rounding |
| Integration | 7 | HTTP pipeline: idempotency, stale rowversion, auth bypass on/off + bearer token |
| Worker | 10 | `MessageRetryPolicy` unit tests |

Domain tests cover the main SADC pairings (ZA/BW/ZW, CMA cross-rates, invalid combos) — not every country in the validator dictionary.

**Integration tests** use InMemory EF via `WebApplicationFactory` for speed. CI also spins up SQL Server to apply migrations and generate an idempotent script, but assertions don't run against that database.

**E2E**: not implemented — manual smoke test via Docker Compose is how I verified the full path.

**CI gate** (`.github/workflows/ci.yml`): build, domain unit tests, migration script generation + `database update` against SQL Server, integration tests, web build.

---

## 7. Observability

### Implemented

- **Serilog** in the API (`Enrich.FromLogContext()`, request logging middleware) and console output in the Worker.
- **Correlation IDs** — `CorrelationIdMiddleware` reads or generates `X-Correlation-Id`, adds it to the log scope and response headers for HTTP requests.

### Not implemented (next steps)

- **End-to-end correlation** — the ID does not yet flow into RabbitMQ message headers or Worker logs. I'd add it in `RabbitMqPublisher.PublishAsync` and log it in `OrderCreatedConsumer`.
- **Structured log shipping** — Seq / Elasticsearch / Azure Monitor (JSON formatter instead of plain console).
- **OpenTelemetry** — spans for HTTP → EF → outbox publish → consumer.
- **Metrics** — e.g. `orders_created_total`, `outbox_lag_seconds`, DLQ depth, p99 latency. Alert on outbox lag > 60s or DLQ depth > 0.

---

## 8. Performance

### Implemented

- Composite index on `Orders(CustomerId, Status, CreatedAt)` — matches the list filter/sort path (see SQL section).
- **FX rate caching** — `MockFxRateProvider` uses `IMemoryCache`, 15-minute TTL (`FxCache:TtlMinutes`).
- **RabbitMQ backpressure** — `BasicQos(prefetchCount: 10)` on the consumer; outbox publisher batch size 20, poll interval 5s.
- **API reads** — pagination capped at 100; `AsNoTracking()` on repository queries; EF connection pooling by default.

### Not implemented (documented as future work)

- Customer-by-ID cache, ZAR report result cache.
- Response compression middleware.
- Read replicas / materialized views for reporting at scale.

---

## 9. Data Retention & POPIA Compliance

**POPIA** (Protection of Personal Information Act, South Africa) requires lawful processing, data minimization, and data subject rights.

**Data minimization**: Store only `Name`, `Email`, `CountryCode` for customers — no ID numbers, phone, or address unless required. Order line items store SKU references, not product descriptions with PII.

**Retention policies**:
- Active orders: indefinite while customer relationship exists
- Completed/cancelled orders: 7 years (tax/regulatory), then anonymize
- Outbox messages: purge 30 days after `ProcessedOn`
- Idempotency keys: purge after 72 hours
- Audit logs: 3 years

**Right to erasure**: Implement `DELETE /api/customers/{id}` (admin-only) that anonymizes customer PII (`Name → "REDACTED"`, `Email → hash`) while retaining order financial records in anonymized form for regulatory retention.

**Encryption**: TLS 1.2+ in transit (HTTPS, encrypted SQL connections). SQL Server TDE for encryption at rest. RabbitMQ TLS + user credentials via secrets manager (Azure Key Vault / Docker secrets).

**Access control**: Role-based access via Entra ID; audit log every PII access with correlation ID, user identity, timestamp.

---

## 10. When GraphQL Would Make Sense

**GraphQL is ideal for** the ZAR reporting dashboard and order detail views where clients need **nested, flexible queries**:

```graphql
query OrderDashboard($customerId: UUID!) {
  customer(id: $customerId) {
    name
    orders(status: PAID, first: 10) {
      totalAmount
      amountInZar
      lineItems { productSku quantity unitPrice }
    }
  }
}
```

This avoids over-fetching (REST returns full order objects when only summary needed) and under-fetching (REST requires multiple round-trips for customer + orders + line items + ZAR conversion).

**REST remains better for**:
- **Writes** (order creation, status updates) — explicit, cacheable, idempotent with standard HTTP semantics
- **Simple CRUD** with stable contracts
- **Webhook/callback integrations** from payment providers expecting REST
- **Caching** at CDN/proxy level (GraphQL POST requests aren't cacheable by default)

**Hybrid approach**: REST for commands (POST/PUT), GraphQL gateway for read-heavy reporting and the React dashboard. The GraphQL layer queries read replicas/projections, not the write database.

---

## FX Conversion — Rounding & Caching (Implementation Notes)

**Rounding**: `FxConversionHelper.ConvertToZar` uses `Math.Round(..., 2, MidpointRounding.AwayFromZero)` — half away from zero (10.005 → 10.01).

**Caching**: `MockFxRateProvider` caches each currency pair in `IMemoryCache` with configurable TTL (default 15 min via `FxCache:TtlMinutes`). Cache key: `fx:{fromCurrency}:ZAR`. A small random jitter (±2%) simulates live rate movement while demonstrating cache hits within the TTL window.

---

## SQL Section

### Pagination Query (OFFSET/FETCH with index-aligned ORDER BY)

```sql
DECLARE @CustomerId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';
DECLARE @Status       INT            = 1; -- Paid
DECLARE @Page         INT            = 1;
DECLARE @PageSize     INT            = 20;

SELECT
    o.Id,
    o.CustomerId,
    o.Status,
    o.CreatedAt,
    o.CurrencyCode,
    o.TotalAmount
FROM dbo.Orders o
WHERE o.CustomerId = @CustomerId
  AND o.Status     = @Status
ORDER BY o.CreatedAt DESC, o.Id DESC  -- CreatedAt matches index leading columns; Id breaks ties
OFFSET (@Page - 1) * @PageSize ROWS
FETCH NEXT @PageSize ROWS ONLY;
```

### Top Spenders Last 90 Days

```sql
WITH CustomerSpend AS (
    SELECT
        c.Id           AS CustomerId,
        c.Name,
        c.CountryCode,
        SUM(o.TotalAmount) AS TotalSpend,
        COUNT(o.Id)        AS OrderCount
    FROM dbo.Customers c
    INNER JOIN dbo.Orders o ON o.CustomerId = c.Id
    WHERE o.CreatedAt >= DATEADD(DAY, -90, SYSUTCDATETIME())
      AND o.Status NOT IN (3) -- Exclude Cancelled
    GROUP BY c.Id, c.Name, c.CountryCode
),
Ranked AS (
    SELECT
        *,
        RANK() OVER (ORDER BY TotalSpend DESC) AS SpendRank
    FROM CustomerSpend
)
SELECT TOP 20
    CustomerId, Name, CountryCode, TotalSpend, OrderCount, SpendRank
FROM Ranked
ORDER BY SpendRank;
```

### Index Strategy for Orders (CustomerId/Status/CreatedAt)

```sql
-- Composite index matching filter + sort
CREATE NONCLUSTERED INDEX IX_Orders_CustomerId_Status_CreatedAt
ON dbo.Orders (CustomerId, Status, CreatedAt DESC)
INCLUDE (CurrencyCode, TotalAmount);
```

### Covering Index — Removing Key Lookups

If the execution plan shows **Key Lookup** (bookmark lookup to clustered index) for a frequent query:

```sql
-- Before: index seek + key lookup for TotalAmount, CurrencyCode
-- After: covering index includes all SELECT columns

CREATE NONCLUSTERED INDEX IX_Orders_Covering_List
ON dbo.Orders (CustomerId, Status, CreatedAt DESC)
INCLUDE (Id, CurrencyCode, TotalAmount, RowVersion);

-- Verify with:
SET STATISTICS IO ON;
-- Re-run query; logical reads on Orders should drop; Key Lookup operator gone
```

### Optimistic Concurrency with ROWVERSION

```sql
DECLARE @OrderId      UNIQUEIDENTIFIER = '...';
DECLARE @NewStatus    INT              = 1; -- Paid
DECLARE @OriginalRowVersion BINARY(8) = 0x0000000000000001; -- from client (rowversion sent as Base64 from API, compared as BINARY(8))

UPDATE dbo.Orders
SET    Status = @NewStatus
WHERE  Id = @OrderId
  AND  RowVersion = @OriginalRowVersion;

IF @@ROWCOUNT = 0
    THROW 50001, 'Concurrency conflict: order was modified by another transaction.', 1;
```

### Deadlock Scenario + Mitigation

**Scenario**: Transaction A updates Order status (locks Order row), then reads Customer. Transaction B updates Customer email (locks Customer row), then reads the same Order. Classic cycle → deadlock.

**Mitigation**:
1. **Consistent access order**: Always lock Customer before Order (or vice versa) across all procedures
2. **Smaller transactions**: Update only the Order row; don't hold locks while publishing to RabbitMQ
3. **Retry logic**: Catch SQL error 1205 (deadlock victim) and retry with jitter:

```csharp
for (int attempt = 0; attempt < 3; attempt++)
{
    try { await _context.SaveChangesAsync(); break; }
    catch (DbUpdateException ex) when (IsDeadlock(ex) && attempt < 2)
    {
        await Task.Delay(Random.Shared.Next(50, 200));
    }
}
```

4. **READ COMMITTED SNAPSHOT ISOLATION** (RCSI) for read queries to reduce reader/writer blocking

### Window Function — Running Totals per Customer

```sql
SELECT
    o.CustomerId,
    o.Id          AS OrderId,
    o.CreatedAt,
    o.TotalAmount,
    SUM(o.TotalAmount) OVER (
        PARTITION BY o.CustomerId
        ORDER BY o.CreatedAt
        ROWS UNBOUNDED PRECEDING
    ) AS RunningTotal
FROM dbo.Orders o
WHERE o.Status <> 3 -- Not Cancelled
ORDER BY o.CustomerId, o.CreatedAt;
```

### Partitioning Strategy (Orders by CreatedAt Month)

```sql
-- Partition function: monthly ranges
CREATE PARTITION FUNCTION PF_Orders_CreatedAt_Monthly (DATETIME2)
AS RANGE RIGHT FOR VALUES (
    '2024-01-01', '2024-02-01', '2024-03-01', '2024-04-01',
    '2024-05-01', '2024-06-01', '2024-07-01', '2024-08-01',
    '2024-09-01', '2024-10-01', '2024-11-01', '2024-12-01',
    '2025-01-01'
);

CREATE PARTITION SCHEME PS_Orders_CreatedAt_Monthly
AS PARTITION PF_Orders_CreatedAt_Monthly ALL TO ([PRIMARY]);
-- Production: map partitions to separate filegroups for I/O isolation

-- Rebuild clustered index on partition scheme
CREATE CLUSTERED INDEX CIX_Orders_CreatedAt
ON dbo.Orders (CreatedAt, Id)
ON PS_Orders_CreatedAt_Monthly(CreatedAt);
```

Benefits: partition elimination on date-range queries, efficient archival (switch out old partitions), reduced index maintenance scope.

### Outbox Pattern DB Design + Polling Query

Schema matches the `AddOutbox` migration. The polling query below is the **production pattern** for multiple publisher instances. The running code uses a simpler EF LINQ query (see §3).

```sql
CREATE TABLE dbo.OutboxMessages (
    Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    Type        NVARCHAR(100)    NOT NULL,
    Payload     NVARCHAR(MAX)    NOT NULL,
    OccurredOn  DATETIME2        NOT NULL,
    ProcessedOn DATETIME2        NULL,
    Error       NVARCHAR(MAX)    NULL
);

CREATE NONCLUSTERED INDEX IX_OutboxMessages_Unprocessed
ON dbo.OutboxMessages (OccurredOn)
WHERE ProcessedOn IS NULL;

-- Polling query with UPDLOCK + READPAST (skip locked rows for multi-instance publishers)
BEGIN TRANSACTION;

SELECT TOP 20
    Id, Type, Payload, OccurredOn
FROM dbo.OutboxMessages WITH (UPDLOCK, READPAST, ROWLOCK)
WHERE ProcessedOn IS NULL
ORDER BY OccurredOn;

-- After successful publish:
UPDATE dbo.OutboxMessages
SET ProcessedOn = SYSUTCDATETIME(), Error = NULL
WHERE Id = @MessageId;

COMMIT;
```

`UPDLOCK` prevents two publishers from selecting the same row. `READPAST` skips rows already locked by another instance. `ROWLOCK` avoids page-level contention.

### Stored Procedure — Transaction Report

```sql
CREATE OR ALTER PROCEDURE dbo.sp_GetCustomerTransactionReport
    @CustomerId UNIQUEIDENTIFIER,
    @FromDate   DATETIME2,
    @ToDate     DATETIME2,
    @Status     INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        c.Name                              AS CustomerName,
        c.CountryCode,
        o.Id                                AS OrderId,
        o.CreatedAt,
        o.Status,
        o.CurrencyCode,
        o.TotalAmount,
        li.ProductSku,
        li.Quantity,
        li.UnitPrice,
        li.Quantity * li.UnitPrice          AS LineTotal
    FROM dbo.Customers c
    INNER JOIN dbo.Orders o
        ON o.CustomerId = c.Id
    INNER JOIN dbo.OrderLineItems li
        ON li.OrderId = o.Id
    WHERE c.Id = @CustomerId
      AND o.CreatedAt >= @FromDate
      AND o.CreatedAt <  @ToDate
      AND (@Status IS NULL OR o.Status = @Status)
    ORDER BY o.CreatedAt DESC, li.ProductSku;

    -- Summary aggregation
    SELECT
        COUNT(DISTINCT o.Id)                AS OrderCount,
        SUM(o.TotalAmount)                  AS GrandTotal,
        o.CurrencyCode
    FROM dbo.Orders o
    WHERE o.CustomerId = @CustomerId
      AND o.CreatedAt >= @FromDate
      AND o.CreatedAt <  @ToDate
      AND (@Status IS NULL OR o.Status = @Status)
    GROUP BY o.CurrencyCode;
END;
```

---

## Assumptions & Tradeoffs

| Assumption | Rationale |
|------------|-----------|
| SQL Server as primary database | Assessment spec; Azure SQL or PostgreSQL possible with EF provider swap |
| Mock FX provider with hardcoded rates | Real integration would call SARB/Reuters with circuit breaker |
| Single SQL Server instance | Fine for assessment; §2 outlines scale-out if needed |
| JWT auth with dev bypass | `Microsoft.AspNetCore.Authentication.JwtBearer` + `Auth:DevBypass` for demo; Entra via `Auth:Authority` config swap (§5) |
| InMemory DB for integration tests | Fast feedback; CI separately validates migrations against SQL Server |
| Outbox polling via EF (not CDC) | Simpler for assessment; `UPDLOCK/READPAST` SQL in docs for multi-instance publishers |
| CMA currencies ~1:1 with ZAR in mock FX | Mock rates; production needs real NAD/LSL/SZL quotes |
| Idempotency keys kept indefinitely | Production would TTL-purge after ~72h; scoped to `(Idempotency-Key, RequestPath)` |
| Inline styles in React UI | Assessment scope; production would use a design system |
| ZAR report loads up to 10,000 orders | Would paginate or pre-aggregate in production |
| Zimbabwe accepts ZWL or USD | Per spec; dollarization edge cases not modeled |
| Worker and API share one database | Standard for outbox; read replicas for reporting at scale |
