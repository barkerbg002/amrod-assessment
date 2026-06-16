# EF Core Migration Strategy — SadcOMS

## Migration History

| Migration | Purpose |
|-----------|---------|
| `20240601000000_InitialCreate` | Customers, Orders, OrderLineItems with composite index on Orders(CustomerId, Status, CreatedAt) |
| `20240602000000_AddOrderRowVersion` | Adds `RowVersion` (SQL Server `rowversion`) for optimistic concurrency |
| `20240603000000_AddOutbox` | OutboxMessages table + IdempotencyKeys table |
| `20240604000000_ScopeIdempotencyByRequestPath` | Composite unique index on IdempotencyKeys `(Key, RequestPath)` |

## Migration Strategy

We use **EF Core Code First** with explicit, incremental migrations checked into source control. Each migration represents a discrete, reviewable schema change. Migrations are applied via `dotnet ef database update` in development and through CI/CD pipelines in higher environments.

### Principles

1. **Additive-first**: New columns are nullable or have defaults; new tables don't break existing code.
2. **Backward-compatible**: Application code deployed before a migration must still function against the previous schema (expand-contract pattern).
3. **Separate deploy from schema apply**: Application binaries and database schema are versioned independently; migrations run as a dedicated pipeline step before traffic is routed to new app versions.
4. **Idempotent scripts for production**: Generate idempotent SQL scripts (`dotnet ef migrations script --idempotent`) for DBA review and manual/controlled application.

## Zero-Downtime Deployment Approach

### Phase 1 — Expand

- Add new columns as **nullable** or with **default values**
- Add new tables (Outbox, IdempotencyKeys) without modifying existing query paths
- Deploy migration while old application version still runs

Example: `AddOrderRowVersion` adds a concurrency token. Existing reads/writes continue; the new app version sends `RowVersion` on updates.

### Phase 2 — Dual-Write / Dual-Read (if needed)

- For breaking changes (column renames, type changes), introduce the new column alongside the old one
- Application writes to both; reads prefer new column with fallback

### Phase 3 — Contract

- Remove deprecated columns in a **subsequent** migration after all app instances use the new schema
- Never drop columns in the same release that introduces replacements

### Deployment Order

```
1. Apply migration (expand)
2. Deploy new application version
3. Verify health checks
4. (Later release) Apply contract migration
5. Deploy code that no longer references old schema
```

## Rollback Strategy

| Scenario | Action |
|----------|--------|
| Migration fails mid-apply | SQL Server rolls back the transaction; fix migration and re-run |
| Migration applied, new app has bugs | Roll back **application** to previous version (schema remains expanded — backward compatible) |
| Need to undo schema change | Apply a **new forward migration** that reverses the change (never `dotnet ef database update <PreviousMigration>` in production) |
| Disaster recovery | Restore database from point-in-time backup; replay Outbox unprocessed messages |

We maintain a `Down()` method in each migration for development rollback only. Production rollbacks use forward-compensating migrations.

## CI Validation

The GitHub Actions pipeline (`.github/workflows/ci.yml`) validates migrations:

1. **Build** the solution
2. **Generate idempotent script**: `dotnet ef migrations script --idempotent -o migration-script.sql`
3. **Spin up SQL Server** as a service container
4. **Apply migrations**: `dotnet ef database update` against `SadcOMS_CI` database
5. **Run integration tests** against the migrated schema
6. **Upload** the generated script as a build artifact for review

This ensures migrations compile, apply cleanly to a fresh database, and don't break the application.

## Local Commands

```bash
# Apply all pending migrations
dotnet ef database update \
  --project src/SadcOMS.Infrastructure \
  --startup-project src/SadcOMS.API

# Generate SQL script
dotnet ef migrations script --idempotent \
  --project src/SadcOMS.Infrastructure \
  --startup-project src/SadcOMS.API \
  --output migration-script.sql

# Add a new migration (development)
dotnet ef migrations add <MigrationName> \
  --project src/SadcOMS.Infrastructure \
  --startup-project src/SadcOMS.API
```

## Design-Time Factory

`SadcOmsDbContextFactory` implements `IDesignTimeDbContextFactory<SadcOmsDbContext>` so EF tools can create the DbContext without running the full application host.
