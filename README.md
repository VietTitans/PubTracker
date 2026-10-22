# PubTracker

Literature-monitoring SaaS: paste a search URL from a scientific database (PubMed, PEDro,
later IEEE/Scopus), and PubTracker polls it on a schedule, deduplicates new records, and
emails a digest of what's new.

> **Disclaimer:** PEDro scraping is disabled by default and requires PEDro's explicit permission.
> See [Third-Party Data Sources](#disclaimer-third-party-data-sources) below.

## Disclaimer: Third-Party Data Sources

PubTracker is a portfolio and demonstration project. It is not affiliated with, endorsed by, or sponsored by any of the databases it connects to.

**PEDro (Physiotherapy Evidence Database).** The PEDro provider retrieves and parses pages from the PEDro website. PEDro does not offer a public API for this purpose, and its terms of use may restrict automated access. Before you run this system against PEDro, you must contact PEDro (https://pedro.org.au) and obtain their explicit permission to access their website programmatically. The author does not grant, and cannot grant, that permission on PEDro's behalf. Do not deploy the PEDro provider in a production or public setting without it. Respect any rate limits, robots.txt directives, and usage conditions PEDro specifies.

**PubMed / NCBI.** The PubMed provider uses NCBI's official E-utilities API. You must follow NCBI's usage policies, including rate limits and the use of a registered API key and contact email (`Ncbi:ApiKey`, `Ncbi:ContactEmail`).

**Your responsibility.** Anyone who runs, deploys, or modifies this software is solely responsible for complying with the terms of service, copyright rules, and applicable laws of every source they query. The software is provided "as is", without warranty, and the author accepts no liability for misuse. Records and abstracts remain the property of their respective publishers and databases.

## Quick start (Docker)

```bash
cp docker/.env.example.example docker/.env   # fill in email/NCBI keys as needed
cd docker
docker compose up --build
```

Open `http://localhost`. A Caddy reverse proxy (`docker/caddy/Caddyfile`) is the single
entrypoint in front of the web app, API, and Keycloak, routing by path:

| Path | Routed to |
|---|---|
| `/` | `web` (React/Vite SPA) |
| `/api/*` | `api` (.NET Web API) |
| `/auth/*` | `keycloak` (OIDC provider) |

## Local development (without Docker)

```bash
# API: needs Postgres plus Email config (see below)
dotnet run --project api/RecordService

# Web
cd web && npm install && npm run dev    # http://localhost:5173
```

In `Development`, requests with no `Authorization` header run as a fake debug user, so
Swagger/Postman work without a live Keycloak login.

## Configuration (API)

Set via environment variables (`Section__Key`) or `appsettings`.

| Key | Required | Purpose |
|---|---|---|
| `ConnectionStrings__DefaultConnection` | yes | Postgres |
| `Email:ApiKey`, `Email:FromAddress`, `Email:FromName` | yes | Brevo digest emails |
| `Keycloak:Authority`, `Keycloak:MetadataAddress`, `Keycloak:Audience` | no | OIDC (default: local Keycloak `:8081`) |
| `Llm:BaseUrl`, `Llm:ApiKey`, `Llm:Model` | no | AI digest summaries (any OpenAI-compatible endpoint); no key = no summaries |
| `Llm:MaxAuthorIntentionsPerPoll` | no | Default 50. Max records per poll that get an AI author intention (new ones first, then older records missing one) |
| `Ncbi:ApiKey`, `Ncbi:ContactEmail` | no | PubMed rate limits |
| `Sources:<Name>:Enabled` | no | Per-source toggle, override in `docker/docker-compose.yml`; defaults live in `Sources/SourceCatalog.cs` (`PubMed` on, `Pedro` off). Enable a scraped source only after obtaining its owner's permission (see [Disclaimer](#disclaimer-third-party-data-sources)) |
| `Llm:SummaryChunkSize`, `Llm:SummaryAbstractChars` | no | Digest summary: abstracts per LLM call (default 40) and characters kept per abstract (default 1000). A bigger batch is summarized in chunks, then combined |
| `Cors:WebAppOrigin` | no | Default `http://localhost:5173` |
| `Scheduler:PollIntervalHours` | no | Default 168 (weekly) |
| `Scheduler:UserPurgeIntervalHours` | no | Default 24 |
| `Scheduler:DigestOutboxIntervalSeconds` | no | Default 30 |

## How it works

Background workers in the API: one polls every search query via the source providers
(PubMed, PEDro), dedupes new records, and queues digests in a `digest_outbox` table; one
sends queued digests by email (with retry/backoff); one hard-deletes users past their
soft-delete grace period.

## Architecture

- `api/`: .NET 10 / ASP.NET Core Web API (`RecordService`), backed by Postgres via EF Core
  (`Npgsql.EntityFrameworkCore.PostgreSQL`). See `CLAUDE.md` for the layered Controller → BusinessLogic → DataAccess
  convention and how to add a new resource.
- `web/`: React/Vite SPA, served by its own Caddy instance in production images.
- `api/RecordService/Migrations/`: EF Core migrations, source of truth for the Postgres
  schema. Applied automatically via `Database.Migrate()` on every API startup (see `Program.cs`).
- `ai/system-architecture-net10.md`: target end-state architecture (scheduler, email
  digest pipeline, full Keycloak realm setup); check it before assuming a described piece
  is already implemented.

## Tests

```bash
dotnet test api/test/Test.csproj
```
