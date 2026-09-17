# PubTracker

PubTracker is a literature-monitoring SaaS. A user pastes a search URL from a scientific database
(currently PubMed and PEDro), and the system is designed to poll it on a schedule, deduplicate new
records, and email a digest of what's new.

## Repo layout

- **`api/`** — .NET 10 / ASP.NET Core Web API (backend). Two projects: `RecordData` (domain models)
  and `RecordService` (the API, layered Controller → BusinessLogic → DataAccess, raw Npgsql/SQL
  against Postgres — no ORM). Literature sources (PubMed, PEDro) are pluggable providers under
  `DataAccess/ExternalSources/`.
- **`web/`** — React + TypeScript frontend, built with Vite, authenticating against Keycloak via
  `oidc-client-ts`.
- **`database/`** — `schema/init.sql` is the source of truth for the Postgres schema, auto-applied
  on container first boot.
- **`docker/`** — `docker-compose.yml` wiring up Postgres, Keycloak, the API, and the web frontend
  for local full-stack runs.
- **`ai/system-architecture-net10.md`** — target architecture doc (scheduler, email digest
  pipeline, revised schema); parts of it are still unimplemented, see `CLAUDE.md`.
- **`CLAUDE.md`** — contributor/agent guidelines and a more detailed architecture breakdown.

## Quickstart (full stack via Docker)

```bash
cd docker
cp .env.example.example .env   # fill in Postgres, Keycloak, email/NCBI values
docker compose up --build
```

This starts Postgres (with `database/schema/init.sql` applied), Keycloak, the API (`:8080`), and
the web app (`:5173`).

## Backend development (`api/`)

```bash
dotnet restore api/RecordService/RecordService.csproj
dotnet build api/RecordService/RecordService.csproj
dotnet run --project api/RecordService     # needs ConnectionStrings__DefaultConnection configured
```

Tests (xunit + Testcontainers.PostgreSql):

```bash
dotnet test api/test/Test.csproj
```

## Frontend development (`web/`)

```bash
cd web
npm install
npm run dev       # Vite dev server
npm run build      # tsc + production build
npm run lint       # oxlint
```

See `web/README.md` for Vite/ESLint template notes.

## Further reading

- `CLAUDE.md` — layering conventions, DTO patterns, current vs. target schema, auth status.
- `ai/system-architecture-net10.md` — full target architecture.
- `api/DTO_IMPLEMENTATION.md` — DTO conventions for new entities.
