# PubTracker

Literature-monitoring SaaS: paste a search URL from a scientific database (PubMed, PEDro,
later IEEE/Scopus), and PubTracker polls it on a schedule, deduplicates new records, and
emails a digest of what's new.

## Quick start (Docker)

```bash
cp docker/.env.example.example docker/.env   # fill in email/NCBI keys as needed
cd docker
docker compose up --build
```

Open `http://localhost` — a Caddy reverse proxy (`docker/caddy/Caddyfile`) is the single
entrypoint in front of the web app, API, and Keycloak, routing by path:

| Path | Routed to |
|---|---|
| `/` | `web` (React/Vite SPA) |
| `/api/*` | `api` (.NET Web API) |
| `/auth/*` | `keycloak` (OIDC provider) |

## Architecture

- `api/` — .NET 10 / ASP.NET Core Web API (`RecordService`), backed by Postgres via raw
  `Npgsql` (no ORM). See `CLAUDE.md` for the layered Controller → BusinessLogic → DataAccess
  convention and how to add a new resource.
- `web/` — React/Vite SPA, served by its own Caddy instance in production images.
- `api/RecordService/Migrations/` — ordered SQL migrations, source of truth for the Postgres
  schema. Applied automatically via DbUp on every API startup (see `Program.cs`).
- `ai/system-architecture-net10.md` — target end-state architecture (scheduler, email
  digest pipeline, full Keycloak realm setup); check it before assuming a described piece
  is already implemented.

## Tests

```bash
dotnet test api/test/Test.csproj
```
