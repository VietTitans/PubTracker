# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Behavioral guidelines

These bias toward caution over speed. For trivial tasks, use judgment.

**1. Think before coding.** Don't assume. Don't hide confusion. Surface tradeoffs.
- State assumptions explicitly. If uncertain, ask.
- If multiple interpretations exist, present them — don't pick silently.
- If a simpler approach exists, say so. Push back when warranted.
- If something is unclear, stop. Name what's confusing. Ask.

**2. Simplicity first.** Minimum code that solves the problem. Nothing speculative.
- No features beyond what was asked.
- No abstractions for single-use code.
- No "flexibility" or "configurability" that wasn't requested.
- No error handling for impossible scenarios.
- If you write 200 lines and it could be 50, rewrite it.
- Ask: "Would a senior engineer say this is overcomplicated?" If yes, simplify.

**3. Surgical changes.** Touch only what you must. Clean up only your own mess.
- Don't "improve" adjacent code, comments, or formatting.
- Don't refactor things that aren't broken.
- Match existing style, even if you'd do it differently.
- If you notice unrelated dead code, mention it — don't delete it.
- Remove imports/variables/functions that YOUR changes made unused; don't remove pre-existing dead code unless asked.
- Test: every changed line should trace directly to the user's request.

**4. Goal-driven execution.** Define success criteria. Loop until verified.
- Transform tasks into verifiable goals: "Add validation" → "Write tests for invalid inputs, then make them pass"; "Fix the bug" → "Write a test that reproduces it, then make it pass"; "Refactor X" → "Ensure tests pass before and after."
- For multi-step tasks, state a brief plan: `1. [Step] → verify: [check]`, `2. [Step] → verify: [check]`, ...
- Strong success criteria let you loop independently; weak criteria ("make it work") require constant clarification.

These are working if: fewer unnecessary changes in diffs, fewer rewrites due to overcomplication, and clarifying questions come before implementation rather than after mistakes.

## Project overview

PubTracker is a literature-monitoring SaaS: users paste a search URL from a scientific database (PubMed, PEDro, later IEEE/Scopus), the system polls it on a schedule, deduplicates new records, and emails a digest of what's new. Full target architecture (Keycloak auth, Quartz/Hangfire scheduler, Playwright scraping, email digest pipeline) is documented in `ai/system-architecture-net10.md` — read it when working on anything beyond basic CRUD, since large parts of that design (background worker, scheduling, email dispatch, auth) are not implemented yet.

The backend lives in `api/` — **.NET 10 / ASP.NET Core** Web API. (An earlier Python/FastAPI prototype previously lived at `app/`; it has been removed.)

## Commands

### .NET API (`api/`)
```bash
dotnet restore api/RecordService/RecordService.csproj   # restore deps
dotnet build api/RecordService/RecordService.csproj      # build
dotnet run --project api/RecordService                   # run locally (needs DefaultConnection configured, see below)
```
Tests live in `api/test/Test.csproj` (xunit; `Testcontainers.PostgreSql`-based end-to-end tests):
```bash
dotnet test api/test/Test.csproj
```

EF Core migrations (from `api/RecordService/`, requires the `dotnet-ef` global tool):
```bash
dotnet ef migrations add <Name>   # add a migration after changing DataAccess/Entities or PubTrackerDbContext
dotnet ef database update         # apply pending migrations to ConnectionStrings__DefaultConnection (env var; must be set and reachable)
```
`Program.cs` also applies pending migrations automatically at API startup (`Database.Migrate()`), same as any other environment.

### Full stack via Docker
```bash
cd docker
docker compose up --build     # builds api/ (via api/Dockerfile) + postgres; API applies pending migrations from api/RecordService/Migrations/ on startup
```
Requires `docker/.env` (copy from `docker/.env.example.example`) with `POSTGRES_USER`, `POSTGRES_PASSWORD`, `POSTGRES_DB`, `POSTGRES_PORT`, `ASPNETCORE_ENVIRONMENT`. The API reads its connection string from `ConnectionStrings__DefaultConnection`, which `docker-compose.yml` assembles from those same Postgres env vars. `Program.cs` throws at startup if this connection string is missing.

## Architecture (`api/`)

Two-project .NET solution:
- **`RecordData`** — plain domain model classes (`User`, `Source`, `SearchQuery`, `LiteratureRecord`, `SourceSearchResult`), namespace `RecordData`. No dependencies on the web project.
- **`RecordService`** — the ASP.NET Core Web API, referencing `RecordData`.

`RecordService` follows a strict layered flow: **Controller → BusinessLogic (Service) → DataAccess**, each layer defined by an interface (`I*Service`, `I*DataAccess`) with a single implementation, wired up manually in `Program.cs` (no assembly scanning). When adding a new resource, add all three layers plus register them in `Program.cs`.

- **Controllers** (`Controllers/`) are thin: call the service, catch exceptions, return DTOs via mapping extensions. They don't touch `DataAccess` or `RecordData` models directly.
- **BusinessLogic** (`BusinessLogic/`) holds orchestration/business rules and calls `DataAccess`.
- **DataAccess** (`DataAccess/`) talks to Postgres via **EF Core** (`Npgsql.EntityFrameworkCore.PostgreSQL`), through the single `PubTrackerDbContext` (`DataAccess/PubTrackerDbContext.cs`), injected into each `I*DataAccess` implementation's constructor and registered per-request via `AddDbContext` in `Program.cs`. EF entity classes live in `DataAccess/Entities/` (`UserEntity`, `SourceEntity`, `SearchQueryEntity`, `RecordEntity`, and the join-table entities `UserSearchQueryEntity`, `UserSearchQueryDigestEntity`, `SourceRecordEntity`, `SearchQueryRecordEntity`) — these are separate from the `RecordData` model classes (`User`, `Source`, `SearchQuery`, `LiteratureRecord`) returned by the DataAccess layer, since the entities mirror raw column nullability/shape 1:1 while the `RecordData` models carry computed/derived fields (e.g. `SearchQuery.RecordCount`, `LastFetchedAt`) that aren't columns. Data access classes map between the two by hand (no AutoMapper), same convention as DTO↔model mapping. Table/column names are configured `snake_case` via Fluent API in `PubTrackerDbContext.OnModelCreating` (including `id`, to match raw SQL used elsewhere) — there are no `[Column]`/`[Table]` attributes on the entities. Plain CRUD goes through LINQ (`ExecuteUpdateAsync`/`ExecuteDeleteAsync` for updates/deletes); Postgres-specific upserts (`ON CONFLICT ... RETURNING`) and other statements EF's LINQ provider can't express go through `Database.SqlQuery<T>`/`ExecuteSqlInterpolatedAsync` raw SQL inside an explicit `Database.BeginTransactionAsync()` transaction — never `.SingleAsync()`/`.FirstAsync()` etc. directly on a `SqlQuery<T>` built from non-SELECT SQL (EF must compose those with a `LIMIT`, which fails on `INSERT ... RETURNING`; materialize with `.ToListAsync()` first and take the single element in memory instead).
- **DTOs** (`DTOs/`) + **Extensions** (`Extensions/`, e.g. `UserMappingExtensions.cs`) — API request/response shapes are always DTOs, never raw `RecordData` models. Mapping between DTOs and domain models is hand-written extension methods (`ToResponseDto()`, `ToUserModel()`, `UpdateFromDto()`), not AutoMapper. See `api/DTO_IMPLEMENTATION.md` for the full convention when adding DTOs for a new entity.
- **ErrorHandling** (`ErrorHandling/IErrorHandler.cs`) — a `DefaultErrorHandler` provides consistent `{ message }`-shaped JSON error responses (`BadRequest`, `NotFound`, `InternalServerError`, etc.); prefer it over ad hoc `StatusCode(...)` results in new controller code.
- **External literature sources** (`DataAccess/ExternalSources/`) — a strategy/factory pattern for pluggable source providers:
  - `ILiteratureSourceProvider` — `CanHandle(url)`, `SearchAsync(url, lastRunDate)`, `RefreshAsync(url)`.
  - `LiteratureSourceFactory` — picks the right provider for a URL from the registered set.
  - `SourceDetector` — a separate, simpler URL→`SourceType` sniffer used for factory error messages (currently substring-matches `"pubmed"`/`"pedro"` in the URL; note this logic is duplicated, not shared, with each provider's own `CanHandle`).
  - `PubMedProvider`, `PedroProvider` — one per source. New sources are added by implementing this interface and registering the provider as a singleton in `Program.cs`'s `LiteratureSourceFactory` setup. Current provider implementations are scaffolded/placeholder (the actual NCBI/PEDro fetch-and-parse logic is not yet implemented) — check current state in the file before assuming a source is functional.
- Auth is scaffolded but mostly inactive: `Program.cs` defines `AdminOnly` / `UserOrAdmin` authorization policies (role-based), but most `[Authorize]` attributes on controller actions are currently commented out. The target design uses Keycloak/OIDC JWTs (see `ai/system-architecture-net10.md` §4) — this is not yet wired up.

## Database

Single source of truth for schema is the EF Core migrations in `api/RecordService/Migrations/` (generated `.cs` files via `dotnet ef migrations add`), applied via `Database.Migrate()` at API startup (see `Program.cs`) — runs on every start, against any environment, not just first boot. Adding a schema change means changing the entities in `DataAccess/Entities/` and/or the Fluent API config in `PubTrackerDbContext.OnModelCreating`, then generating a new migration — never editing an already-shipped one. (Schema was previously managed via hand-written DbUp SQL scripts; that's been fully replaced by EF Core migrations generated fresh from the current entity model, with no historical migration carried over.) `ai/system-architecture-net10.md` §5 documents further schema evolution (e.g. dropping raw record-count counters) not yet reflected in the migrations — when doing schema-dependent work, check the actual migration files rather than trusting the architecture doc.
