# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

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
No test project exists yet in `api/`.

### Full stack via Docker
```bash
cd docker
docker compose up --build     # builds api/ (via api/Dockerfile) + postgres, applies database/schema/init.sql on first boot
```
Requires `docker/.env` (copy from `docker/.env.example.example`) with `POSTGRES_USER`, `POSTGRES_PASSWORD`, `POSTGRES_DB`, `POSTGRES_PORT`, `ASPNETCORE_ENVIRONMENT`. The API reads its connection string from `ConnectionStrings__DefaultConnection`, which `docker-compose.yml` assembles from those same Postgres env vars. `Program.cs` throws at startup if this connection string is missing.

## Architecture (`api/`)

Two-project .NET solution:
- **`RecordData`** — plain domain model classes (`User`, `Source`, `SearchQuery`, `LiteratureRecord`, `SourceSearchResult`), namespace `RecordData`. No dependencies on the web project.
- **`RecordService`** — the ASP.NET Core Web API, referencing `RecordData`.

`RecordService` follows a strict layered flow: **Controller → BusinessLogic (Service) → DataAccess**, each layer defined by an interface (`I*Service`, `I*DataAccess`) with a single implementation, wired up manually in `Program.cs` (no assembly scanning). When adding a new resource, add all three layers plus register them in `Program.cs`.

- **Controllers** (`Controllers/`) are thin: call the service, catch exceptions, return DTOs via mapping extensions. They don't touch `DataAccess` or `RecordData` models directly.
- **BusinessLogic** (`BusinessLogic/`) holds orchestration/business rules and calls `DataAccess`.
- **DataAccess** (`DataAccess/`) talks to Postgres directly via raw `Npgsql` `NpgsqlCommand`/parameterized SQL (no ORM/EF Core). Table/column names in SQL are `snake_case` (see `database/schema/init.sql`); C# model properties are `PascalCase` — data access classes are responsible for that mapping manually. Connection strings are injected as plain strings into constructors, not via `DbContext`.
- **DTOs** (`DTOs/`) + **Extensions** (`Extensions/`, e.g. `UserMappingExtensions.cs`) — API request/response shapes are always DTOs, never raw `RecordData` models. Mapping between DTOs and domain models is hand-written extension methods (`ToResponseDto()`, `ToUserModel()`, `UpdateFromDto()`), not AutoMapper. See `api/DTO_IMPLEMENTATION.md` for the full convention when adding DTOs for a new entity.
- **ErrorHandling** (`ErrorHandling/IErrorHandler.cs`) — a `DefaultErrorHandler` provides consistent `{ message }`-shaped JSON error responses (`BadRequest`, `NotFound`, `InternalServerError`, etc.); prefer it over ad hoc `StatusCode(...)` results in new controller code.
- **External literature sources** (`DataAccess/ExternalSources/`) — a strategy/factory pattern for pluggable source providers:
  - `ILiteratureSourceProvider` — `CanHandle(url)`, `SearchAsync(url, lastRunDate)`, `RefreshAsync(url)`.
  - `LiteratureSourceFactory` — picks the right provider for a URL from the registered set.
  - `SourceDetector` — a separate, simpler URL→`SourceType` sniffer used for factory error messages (currently substring-matches `"pubmed"`/`"pedro"` in the URL; note this logic is duplicated, not shared, with each provider's own `CanHandle`).
  - `PubMedProvider`, `PedroProvider` — one per source. New sources are added by implementing this interface and registering the provider as a singleton in `Program.cs`'s `LiteratureSourceFactory` setup. Current provider implementations are scaffolded/placeholder (the actual NCBI/PEDro fetch-and-parse logic is not yet implemented) — check current state in the file before assuming a source is functional.
- Auth is scaffolded but mostly inactive: `Program.cs` defines `AdminOnly` / `UserOrAdmin` authorization policies (role-based), but most `[Authorize]` attributes on controller actions are currently commented out. The target design uses Keycloak/OIDC JWTs (see `ai/system-architecture-net10.md` §4) — this is not yet wired up.

## Database

Single source of truth for schema is `database/schema/init.sql`, auto-applied to Postgres on container first-boot via the `docker-entrypoint-initdb.d` mount in `docker-compose.yml`. Current schema is the *old* flatter design (`source_records` table, no `search_query_records` junction, no `last_digest_sent_at`-driven delta computation). `ai/system-architecture-net10.md` §5 documents a revised schema (adds `SearchQueryRecord` junction table with `firstSeenAt`, drops raw record-count counters) that has **not** been migrated into `init.sql` yet — when doing schema-dependent work, check which shape is actually live rather than trusting the architecture doc.
