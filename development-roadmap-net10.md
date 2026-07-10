# Scientific Search Alert SaaS: Development Roadmap

This roadmap sequences implementation against `system-architecture-net10.md`. Phases are ordered by dependency, not strictly by calendar time — some can run in parallel once their prerequisites land (noted where relevant).

---

## Phase 0 — Foundations & Risk Retirement
**Goal:** de-risk the two things that could invalidate the whole architecture before writing product code.

- [ ] **Legal/ToS review** of automated replay of Scopus and IEEE Xplore search URLs, including institutional proxy routing. This can block the Scopus/IEEE integrations entirely — resolve direction before building against them.
- [ ] **Spike: session-URL expiry behavior.** Manually test how long a pasted Scopus/IEEE search URL stays valid when replayed server-side days/weeks later. This determines whether "paste once, monitor forever" is viable as-is or needs a re-auth/re-paste flow.
- [ ] **Spike: Pedro.org.au scrape feasibility.** Confirm Playwright can reliably extract the target DOM structure and survive a basic anti-bot check before committing to it as the sole strategy.
- [ ] Repo scaffolding: solution structure (`Api`, `Worker`, `Domain`, `Infrastructure` projects), CI pipeline skeleton, local docker-compose for Postgres + Keycloak.

**Exit criteria:** legal direction confirmed (or scope reduced to PubMed-only if Scopus/IEEE are blocked); scraper/session risks quantified, not necessarily solved.

---

## Phase 1 — Identity & Access
**Goal:** users can register, verify email, and log in; API can validate tokens.

- [ ] Keycloak realm `science-alerts-saas` provisioned (email-as-username, verify-email enabled).
- [ ] OIDC Authorization Code Flow + PKCE wired up in the frontend shell.
- [ ] ASP.NET Core 10 JWT bearer validation middleware against Keycloak's `.well-known/openid-configuration`.
- [ ] `User` table + sync mechanism (create/update local `User` row on first authenticated request or via Keycloak event listener).
- [ ] Minimal authenticated "who am I" endpoint to prove the pipeline end-to-end.

**Exit criteria:** a real user can register through Keycloak, verify their email, log in, and hit a protected endpoint successfully.

---

## Phase 2 — Data Model & Persistence Layer
**Goal:** the finalized schema is live and testable in isolation from the worker/scraping logic.

- [ ] Migrations for `Source`, `Record`, `SourceRecord`, `SearchQuery`, `SearchQueryRecord`, `UserSearchQuery`, `User`.
- [ ] Seed `Source` rows (PubMed, IEEE Xplore, Scopus, Pedro.org.au).
- [ ] Composite PK + indexes confirmed in migration (`SearchQueryRecord (searchQueryId, recordId)`, `idx_searchqueryrecord_query_seen`).
- [ ] EF Core (or chosen ORM) entity configs using `DateTimeOffset` for all `TIMESTAMPTZ` columns.
- [ ] Repository/unit-of-work layer with integration tests against a real Postgres instance (Testcontainers recommended over mocks, given the FK/constraint-heavy schema).

**Exit criteria:** CRUD + the digest delta query (`SearchQueryRecord` filtered by `firstSeenAt`) are covered by integration tests and pass.

*Can run in parallel with Phase 1.*

---

## Phase 3 — URL Parsing & Search Submission
**Goal:** a user can paste a search URL and the system stores it correctly as a `SearchQuery`.

- [ ] Parser Factory: one parser per `Source`, each extracting the relevant query parameter (`term`, `queryText`, `searchString`, Pedro form fields) and validating it belongs to the expected domain/host.
- [ ] `POST /search-queries` endpoint: paste URL → detect source → parse → dedupe against existing `SearchQuery.targetUrl` for that source → create or attach `UserSearchQuery`.
- [ ] Validation & error handling for malformed/unrecognized URLs (clear user-facing error, not a 500).
- [ ] Basic frontend paste-and-submit form, with source auto-detection feedback ("Detected: PubMed search").

**Exit criteria:** a logged-in user can paste a real PubMed URL and see it saved as an active subscription.

---

## Phase 4 — Extraction Engines (per source, incrementally)
**Goal:** each source can independently fetch current results for a stored `SearchQuery`.

Build and validate one source at a time — don't parallelize this phase across sources until PubMed (the simplest, lowest-risk integration) proves the pattern end-to-end.

- [ ] **PubMed:** `esearch.fcgi` / `esummary.fcgi` client via `HttpClientFactory`, mapped into `Record` shape.
- [ ] **IEEE Xplore:** Metadata API client with developer key management (secrets in Keycloak-adjacent vault or `dotnet user-secrets` / cloud secret manager, never in config files committed to source control).
- [ ] **Scopus:** API client + proxy routing — contingent on Phase 0 legal review outcome.
- [ ] **Pedro.org.au:** Playwright scraper, isolated per-run browser context, DOM extraction into `Record` shape.
- [ ] Polly-based retry/backoff policy applied uniformly across all HTTP-based extractors (rate-limit handling from day one, not bolted on later).

**Exit criteria:** for each enabled source, given a `SearchQuery`, the engine returns a list of candidate records with `doi`/`title`/`description` populated (or best-effort equivalents where a source lacks a DOI).

---

## Phase 5 — Background Worker & Delta Detection
**Goal:** the weekly loop runs end-to-end and correctly identifies "what's new."

- [ ] Quartz.NET or Hangfire job registered on a 7-day schedule (configurable for testing — don't hardcode to real-time waits during dev).
- [ ] Worker iterates all active `SearchQuery` rows, routes to the correct Phase 4 extractor.
- [ ] Upsert into `Record` (`ON CONFLICT (doi) DO NOTHING`) and `SearchQueryRecord` (`ON CONFLICT (searchQueryId, recordId) DO NOTHING`), correctly stamping `firstSeenAt` only on first insertion.
- [ ] Bridge `SourceRecord` associations.
- [ ] Delta query implemented exactly as specified (`firstSeenAt >= lastDigestSentAt`, falling back to a lookback window when null).
- [ ] Idempotency check: running the job twice in a row on the same data should not duplicate `SearchQueryRecord` rows or double-count deltas.

**Exit criteria:** manually triggering the job twice with a mocked "week apart" data change produces exactly the correct new-record set on the second run.

---

## Phase 6 — Digest Assembly & Email Dispatch
**Goal:** users actually receive the email.

- [ ] HTML digest template (per-`SearchQuery` section: source name, new count, title/description list).
- [ ] Roster resolution: `UserSearchQuery` → `User` join per `searchQueryId`.
- [ ] Integration with Postmark/Resend (or chosen vendor), including sender domain verification and bounce/complaint webhook handling.
- [ ] `lastDigestSentAt` updated only on confirmed successful dispatch — failed sends should not silently advance the cutoff and lose that week's records.
- [ ] Empty-state handling: no digest sent (or a lighter "nothing new this week" variant, per product decision) when the delta is empty.

**Exit criteria:** a test user with a seeded delta receives a correctly formatted digest email, and a re-run without new data does not re-send stale content.

---

## Phase 7 — Frontend Completion
**Goal:** full self-service UX beyond the paste-and-submit flow from Phase 3.

- [ ] Dashboard: list of active `SearchQuery` subscriptions per user, with last-checked / last-digest-sent status.
- [ ] Unsubscribe / delete search query flow.
- [ ] Account settings (delegates to Keycloak account console or embeds it).
- [ ] Digest history/preview (optional — view past digest content in-app, sourced from `SearchQueryRecord` history rather than re-sending email).

*Can run in parallel with Phases 4–6 once Phase 3's API contract is stable.*

---

## Phase 8 — Hardening, Scaling & Observability
**Goal:** production-readiness beyond the happy path.

- [ ] Structured logging + correlation IDs across API and Worker (trace a single `SearchQuery` through parse → extract → digest).
- [ ] Metrics/alerting on extractor failure rates per source (early warning for anti-bot changes on Pedro.org.au or API contract changes on PubMed/IEEE/Scopus).
- [ ] Rate-limit tuning per source based on observed real quotas, not assumed defaults.
- [ ] Load test the worker loop against a realistic subscription volume (hundreds/thousands of `SearchQuery` rows) to validate the `idx_searchqueryrecord_query_seen` index actually keeps the delta query fast.
- [ ] Fallback plan execution readiness: Python micro-scraper extraction path for Pedro.org.au, gated behind a feature flag, in case Playwright breaks against a client-side anti-bot change.
- [ ] Backup/restore and disaster-recovery runbook for the Postgres instance.

---

## Phase 9 — Launch
- [ ] Staged rollout: PubMed-only (lowest legal/technical risk) → add IEEE → add Scopus (post legal sign-off) → add Pedro.org.au.
- [ ] Terms of Service / Privacy Policy covering data retention (`Record` cache), email handling, and third-party data source usage.
- [ ] Monitoring dashboards live before first real weekly cron fire against production data.

---

## Suggested Sequencing Summary

| Phase | Depends on | Can parallelize with |
|---|---|---|
| 0 — Foundations & Risk | — | — |
| 1 — Identity | 0 | 2 |
| 2 — Data Model | 0 | 1 |
| 3 — URL Parsing | 1, 2 | — |
| 4 — Extraction Engines | 3 | 7 (once API contract stable) |
| 5 — Worker & Delta | 2, 4 (PubMed at minimum) | — |
| 6 — Digest & Email | 5 | — |
| 7 — Frontend Completion | 3 | 4, 5, 6 |
| 8 — Hardening | 4–6 | 7 |
| 9 — Launch | all | — |

The critical path runs **0 → 1/2 → 3 → 4 (PubMed) → 5 → 6 → 9**, with IEEE/Scopus/Pedro extractors, full frontend, and hardening filled in around that spine as capacity allows.
