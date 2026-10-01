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

PubTracker is a literature-monitoring SaaS: users paste a search URL from a scientific database (PubMed, PEDro, later IEEE/Scopus), the system polls it on a schedule, deduplicates new records, and emails a digest of what's new, optionally with an AI-generated summary. Full target architecture is documented in `ai/system-architecture-net10.md` — it's aspirational in places (e.g. Quartz/Hangfire, Playwright scraping), so check the actual code before trusting a design detail from it; the core loop (auth, scheduled polling, email digest, AI summaries) described below is implemented and running.

The backend lives in `api/` — **.NET 10 / ASP.NET Core** Web API. The frontend lives in `web/` — **React 19 / TypeScript / Vite**. (An earlier Python/FastAPI prototype previously lived at `app/`; it has been removed.)

## Commands

### Web app (`web/`)
```bash
cd web
npm install       # install deps
npm run dev       # Vite dev server (localhost:5173 by default)
npm run build     # production build
```
Auth in the browser is handled by `oidc-client-ts` against Keycloak (`src/auth/`); the API's CORS policy (`Cors:WebAppOrigin`, see `Program.cs`) must match whatever origin the dev server runs on.

### .NET API (`api/`)
```bash
dotnet restore api/RecordService/RecordService.csproj   # restore deps
dotnet build api/RecordService/RecordService.csproj      # build
dotnet run --project api/RecordService                   # run locally (needs config below)
```
`Program.cs` throws at startup if required config is missing. Required: `ConnectionStrings__DefaultConnection` (Postgres), `Email:ApiKey`/`Email:FromAddress`/`Email:FromName` (Brevo — digest emails). Optional, with fallbacks: `Keycloak:Authority`/`Keycloak:MetadataAddress`/`Keycloak:Audience` (defaults point at a local Keycloak on `:8081`; in `Development` a `DebugAuthenticationHandler` also lets unauthenticated requests through as a fake user when no `Authorization` header is sent), `Llm:BaseUrl`/`Llm:ApiKey`/`Llm:Model` (AI digest summaries — any OpenAI-compatible chat-completions endpoint; missing key falls back to `NullSummaryGenerator`, i.e. no summaries, not a startup failure), `Cors:WebAppOrigin` (default `http://localhost:5173`), `Scheduler:PollIntervalHours` (default 168), `Scheduler:UserPurgeIntervalHours` (default 24), `Ncbi:ApiKey`/`Ncbi:ContactEmail` (PubMed rate limits).

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
docker compose up --build     # proxy (Caddy) + db (postgres) + keycloak + api + web
```
Five services, all behind the `proxy` (Caddy, port 80): `web` (React build), `api` (.NET, builds from `api/Dockerfile`, applies pending EF migrations on startup), `db` (Postgres), and `keycloak` (dev-mode, realm auto-imported from `docker/keycloak/realm-export.json`, served under `/auth` so Caddy can forward `/auth/*` to it unmodified). Requires `docker/.env` (copy from `docker/.env.example.example`) — Postgres creds plus `EMAIL_*`, `NCBI_*`, `LLM_*`, `SCHEDULER_POLL_INTERVAL_HOURS`, and the `WEB_*`/`KEYCLOAK_*` origin vars `docker-compose.yml` uses to wire CORS, Keycloak's browser-facing vs. in-network URLs, and the web build's `VITE_*` args — see `docker-compose.yml` for the full mapping.

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
  - `PubMedProvider`, `PedroProvider` — one per source, both implemented (real NCBI/PEDro fetch-and-parse, not placeholders). New sources are added by implementing this interface and registering the provider as a singleton in `Program.cs`'s `LiteratureSourceFactory` setup. A new provider needs its own per-call timeout and circuit breaker (see `PubMedHttpClientExtensions.AddPubMedHttpClient` for an HTTP-based source, `PedroProvider.CircuitBreakerPipeline` for a non-HTTP one) — without one, a hung/dead source can hold `RecordPollingService`'s global poll-cycle lock for a long time on every poll cycle.
- **Auth** is wired up and enforced (most controller actions carry `[Authorize]`, some `[Authorize(Policy = "AdminOnly")]`): real Keycloak/OIDC JWTs are validated by `ConfigureKeycloakBearer` in `Program.cs`, resolving the JWT's `sub` (a Keycloak UUID) to an internal `users` row via `UsersService.GetOrProvisionByKeycloakSubAsync` (auto-provisions on first login). In `Development` only, a `Smart` policy scheme (`Authentication/DebugAuthenticationHandler.cs`) falls back to a fake debug identity for requests with no `Authorization` header, so Swagger/Postman testing doesn't need a live Keycloak login; a request that does carry a bearer token is always routed to real JWT validation, even in `Development`.
- **Background workers** (`Workers/`, both `IHostedService`s registered in `Program.cs`): `RecordPollingBackgroundService` runs `RecordPollingService` (`BusinessLogic/RecordPollingService/`) on an interval (`Scheduler:PollIntervalHours`, default weekly) — polls every search query via the source providers, dedupes new records, and triggers digest emails through `IDigestService`. `UserPurgeBackgroundService` runs on `Scheduler:UserPurgeIntervalHours` (default daily) and hard-deletes users past their soft-delete grace period (`UsersDataAccess.DeletionGracePeriodDays`).
- **Digest & email** (`BusinessLogic/DigestService/`, `DataAccess/Email/`): `DigestService` builds per-user digest content from new records — `RecordTopicClassifier`/`CategoryKeywordMatcher`/`DigestCategories` group records by topic, `PubMedDigestMessageBuilder`/`PedroDigestMessageBuilder` format the per-source sections, `DigestMessageFormatter` assembles the final message. `IEmailSender` is implemented by `BrevoEmailSender` (`DataAccess/Email/`, uses the Brevo transactional email API) — swap providers by implementing `IEmailSender`, not by touching `DigestService`.
- **AI summaries** (`DataAccess/Summarization/`): `ISummaryGenerator` is implemented by `ChatCompletionsSummaryGenerator`, which speaks the OpenAI-compatible chat-completions wire format (works against OpenAI, Azure OpenAI, Groq, local Ollama, OpenRouter, etc. — vendor is a config change via `Llm:BaseUrl`/`Llm:Model`, not a code change). This is additive: if `Llm:ApiKey` isn't configured, `Program.cs` wires up `NullSummaryGenerator` instead (digests still send, just without AI summaries) rather than failing startup.

## Database

Single source of truth for schema is the EF Core migrations in `api/RecordService/Migrations/` (generated `.cs` files via `dotnet ef migrations add`), applied via `Database.Migrate()` at API startup (see `Program.cs`) — runs on every start, against any environment, not just first boot. Adding a schema change means changing the entities in `DataAccess/Entities/` and/or the Fluent API config in `PubTrackerDbContext.OnModelCreating`, then generating a new migration — never editing an already-shipped one. (Schema was previously managed via hand-written DbUp SQL scripts; that's been fully replaced by EF Core migrations generated fresh from the current entity model, with no historical migration carried over.) `ai/system-architecture-net10.md` §5 documents further schema evolution (e.g. dropping raw record-count counters) not yet reflected in the migrations — when doing schema-dependent work, check the actual migration files rather than trusting the architecture doc.

## Agent skills

### Issue tracker

Issues live in GitHub Issues for VietTitans/PubTracker (`gh` CLI). See `docs/agents/issue-tracker.md`.

### Triage labels

Default five-label vocabulary (`needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`). See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: one `CONTEXT.md` + `docs/adr/` at repo root. See `docs/agents/domain.md`.
