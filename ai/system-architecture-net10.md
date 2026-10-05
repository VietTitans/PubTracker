# Scientific Search Alert SaaS: Architecture and specification

## 1. Executive summary
This document specifies the architecture of a multi-tenant, public SaaS platform that automates literature monitoring across PubMed, IEEE Xplore, Scopus, and Pedro.org.au.

Users interact with the platform by performing their desired search query natively on a scientific website, copying the final browser search URL, and pasting it into this platform. On a weekly schedule, the system processes these URLs, extracts new matching records via API extraction or headless scraping, deduplicates findings against historical runs, and sends a consolidated email digest with new article counts, titles, and descriptions.

The stack is C# 14 on .NET 10 (LTS), with Keycloak for identity federation and OIDC access control.

## 2. Platform strategy and extraction mechanics

The backend has a parser factory that breaks pasted browser URLs into structured query data and maps each one to its provider:

| Target Platform | Strategy | Integration Protocol |
| :--- | :--- | :--- |
| PubMed | Native API | Extract `term` query parameter $\rightarrow$ Bind to public NCBI Entrez API (`esearch.fcgi` / `esummary.fcgi`). |
| IEEE Xplore | Native API | Extract `queryText` parameter $\rightarrow$ Query IEEE Xplore Metadata API via premium developer keys. |
| Scopus | Native API + Proxy | Extract `searchString` key $\rightarrow$ Query Elsevier Scopus API (Requires developer keys combined with institutional network proxy routing). |
| Pedro.org.au | Headless Scraper | Extract form field arrays from URL $\rightarrow$ Instantiate isolated Playwright for .NET or external micro-scrapers to read raw DOM segments. |

> **Note (ToS/legal review required):** replaying user-authenticated or session-scoped search URLs against Scopus/IEEE Xplore on a recurring schedule, and routing traffic through institutional proxies, may conflict with those providers' terms of service and the institution's network-use policy. This needs explicit legal sign-off before implementation, and pasted URLs from these platforms may carry session tokens that expire, breaking the "paste once, monitor forever" assumption.

> **Note (PEDro Fair Use conflict, confirmed):** PEDro's published Fair Use policy (`pedro.org.au/fair-use/`) explicitly states "Any form of systematic, bulk, or automated downloading or the use of spiders or robots is not permitted," and that commercial use requires written approval from the PEDro Partnership. The current `PedroProvider` headless-scraping implementation directly conflicts with this. It is not a hypothetical risk. `robots.txt` being permissive does not override these terms. No self-serve API or bulk-licensing option is advertised on their site; the stated path is to contact the PEDro Partnership directly for written approval. Decision (as of this investigation): accepted as a dev-time risk given current low request volume. It must be resolved (obtain written approval, or drop PEDro as a source) before any production launch or scale-up.

## 3. High-level architecture and technical stack

Back-end polling runs asynchronously, so it never blocks the front end or user requests.

```
                  ┌──────────────────────────────────────────────┐
                  │              Identity Provider               │
                  │        Keycloak IdP (Realm: science-alerts)  │
                  └──────┬────────────────────────────────┬──────┘
                         │                                │
            (Redirect for Auth / OIDC)          (JWT Signature Keys)
                         │                                │
                         ▼                                ▼
[ Angular/React/Blazor ] ──(Pastes Search URL)──> [ .NET 10 Web API ] ──> [ Relational DB ]
(Frontend Application)                           (ASP.NET Core 10)       (PostgreSQL / SQL Server)
                                                                                  ▲
                                                                           (Polls Queries)
                                                                                  │
┌───────────────────────────┐                                                     │
│   Quartz.NET / Hangfire   │──(Triggers Job)──> [ .NET 10 Background Worker ] ───┘
│    (Weekly Cron Timer)    │                    (IHostedService Broker)
└───────────────────────────┘                     │
                                                  ├─> Queries Native APIs
                                                  ├─> Executes Playwright Scrapers
                                                  │
                                                  ▼ (Generates Diff Data)
                                        [ Email Transaction Service ]
                                        (Resend / Postmark SMTP Pipeline)
                                                  │
                                                  ▼ (Weekly Email Digest)
                                               [ User ]
```

## 4. Identity and access management (Keycloak integration)

A Keycloak instance handles authentication, registration, and credentials, separate from the transactional database.

### Realm profile configuration
* Realm identifier: `science-alerts-saas`, isolated from the default master realm.
* Self-service settings:
  * User registration: enabled.
  * Email as username: enabled (simplifies sign-in).
  * Verify email: enabled. The system depends on this: email reachability must be confirmed before costly background tasks run.

### Token verification flow (.NET 10 pipeline)
1. The frontend client initializes OIDC Authorization Code Flow with PKCE via Keycloak's login interface.
2. Upon verification, Keycloak passes cryptographically signed JSON Web Tokens (JWTs) back to the browser context.
3. Every protected API request transmits this token via the standard HTTP pipeline header: `Authorization: Bearer <JWT>`.
4. The ASP.NET Core 10 backend validates and authorizes requests in middleware, using metadata from Keycloak's `.well-known/openid-configuration` endpoint.

```csharp
// Program.cs Token Validation Pattern (Modern ASP.NET Core 10 Minimal APIs)
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = "https://<keycloak-domain>/realms/science-alerts-saas";
        options.Audience = "science-alerts-api";
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = true,
            ValidateIssuer = true,
            ClockSkew = TimeSpan.FromMinutes(5)
        };
    });
```

## 5. Database schema and ER model

The database tracks sources, unique queries, records, and which users follow which queries.

**Revision note:** the previous version of this schema linked `SearchQuery` to `Record` only indirectly, through `Source`. Since `Source` is coarse-grained (one row per platform), that path could not express "which records belong to which specific saved search," and the weekly digest job had no way to compute a per-search delta. This revision introduces an explicit `SearchQueryRecord` junction table between `SearchQuery` and `Record`, and replaces the `currentRecordCount` / `previousRecordCount` counters with a timestamp-based delta mechanism (`firstSeenAt`), which is simpler and less prone to drift. The undefined `subscriptioners` field has been removed and replaced with an explicit `lastDigestSentAt` tracking column.

**Revision note (per-user digest tracking):** `SearchQuery.lastDigestSentAt` above was a single watermark shared by every subscriber of a query: a failed send to any one subscriber blocked retry for all of them, and there was no way to combine a user's multiple subscribed searches (potentially across different sources) into one email, since "has this query's digest gone out" wasn't a question that could be asked per-user. This revision adds `UserSearchQueryDigest`, moving the watermark onto the `(userId, searchQueryId)` pair it actually describes. `SearchQuery.lastDigestSentAt` is kept only for backward read compatibility (existing API/frontend consumers); it is no longer written to, and should eventually be removed once those consumers are migrated to the per-user data.

### Entity relationship layout

```
┌───────────────┐          ┌───────────────┐
│    Source     │1        n│  SearchQuery  │
├───────────────┤          ├───────────────┤
│ id (PK)       ├─────────>│ id (PK)       │
│ name          │          │ sourceId (FK) │
│ baseUrl       │          │ targetUrl     │
└──────┬────────┘          │ lastDigestSentAt (TIMESTAMPTZ, nullable)
       │                   └───┬───────┬───┘
       │1                      │1      │1
       ▼n                      │       │n
┌───────────────┐              │  ┌────┴──────────────┐
│ SourceRecord  │              │  │  UserSearchQuery   │
├───────────────┤              │  ├────────────────────┤
│ id (PK)       │              │  │ userId (FK)         │
│ recordId (FK) │              │  │ searchQueryId (FK)  │
│ sourceId (FK) │              │  │ PK (userId, searchQueryId)
└──────▲────────┘              │n └─────────▲──────────┘
       │n                      │            │n
┌──────┴────────┐    ┌─────────▼──────────┐ │
│    Record     │1  n│ SearchQueryRecord  │ │
├───────────────┤◄───┤────────────────────┤ │
│ id (PK)       │    │ searchQueryId (FK) │ │
│ doi           │    │ recordId (FK)      │ │
│ title         │    │ firstSeenAt        │ │
│ description   │    │ PK (searchQueryId, recordId)
└───────────────┘    └────────────────────┘ │
                                             │1
                                   ┌─────────┴─────────┐
                                   │       User        │
                                   ├──────────┬────────┤
                                   │ id (PK)  │        │
                                   │ name     │        │
                                   │ userName │        │
                                   │ email    │        │
                                   └──────────┴────────┘
                                             │1
                                             │n
                          ┌──────────────────┴──────────────────┐
                          │        UserSearchQueryDigest         │
                          ├───────────────────────────────────────┤
                          │ userId (FK)                           │
                          │ searchQueryId (FK)                    │
                          │ lastDigestSentAt (TIMESTAMPTZ, nullable)
                          │ PK (userId, searchQueryId)             │
                          └────────────────────────────────────────┘
```
`UserSearchQueryDigest` is the digest-delta watermark, keyed per (user, search query) rather than per search query alone (see the revision note above). It has an implicit FK to `SearchQuery(id)` too, alongside `User(id)`; omitted from the diagram above only for layout space.

### Relational tables (SQL specification)

```sql
-- Represents monitored platforms (e.g., PubMed, IEEE, Scopus)
CREATE TABLE Source (
    id SERIAL PRIMARY KEY,
    name TEXT NOT NULL,
    baseUrl TEXT NOT NULL
);

-- Internal cache for unique scientific literature papers found across searches
CREATE TABLE Record (
    id SERIAL PRIMARY KEY,
    doi TEXT UNIQUE,
    title TEXT NOT NULL,
    description TEXT
);

-- Many-to-Many bridge identifying which record is tracked under which provider platform
CREATE TABLE SourceRecord (
    id SERIAL PRIMARY KEY,
    recordId INTEGER NOT NULL REFERENCES Record(id) ON DELETE CASCADE,
    sourceId INTEGER NOT NULL REFERENCES Source(id) ON DELETE CASCADE
);

-- Represents a unique target search URL shared among one or multiple subscribers
CREATE TABLE SearchQuery (
    id SERIAL PRIMARY KEY,
    sourceId INTEGER NOT NULL REFERENCES Source(id) ON DELETE CASCADE,
    targetUrl TEXT NOT NULL,
    lastDigestSentAt TIMESTAMPTZ  -- last time a digest was successfully dispatched for this query
);

-- Bridges a SearchQuery to the specific Records it has returned, with first-seen tracking.
-- This is the mechanism that makes per-search delta computation ("what's new since last digest")
-- a pure query instead of requiring mutable counters.
CREATE TABLE SearchQueryRecord (
    searchQueryId INTEGER NOT NULL REFERENCES SearchQuery(id) ON DELETE CASCADE,
    recordId INTEGER NOT NULL REFERENCES Record(id) ON DELETE CASCADE,
    firstSeenAt TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (searchQueryId, recordId)
);

-- Index to support the weekly digest delta query efficiently as history grows
CREATE INDEX idx_searchqueryrecord_query_seen
    ON SearchQueryRecord (searchQueryId, firstSeenAt);

-- Master identity alignment platform mapping back to Keycloak identities
CREATE TABLE "User" (
    id SERIAL PRIMARY KEY,
    name TEXT NOT NULL,
    userName TEXT NOT NULL UNIQUE,
    email TEXT NOT NULL UNIQUE
);

-- Many-to-Many resolution bridging distinct users to their configured SearchQuery monitoring targets
CREATE TABLE UserSearchQuery (
    userId INTEGER NOT NULL REFERENCES "User"(id) ON DELETE CASCADE,
    searchQueryId INTEGER NOT NULL REFERENCES SearchQuery(id) ON DELETE CASCADE,
    PRIMARY KEY (userId, searchQueryId)
);

-- Per-(user, searchQuery) digest watermark; supersedes SearchQuery.lastDigestSentAt as the
-- source of truth for "what has this specific user already been sent for this query". Seeded
-- to now() when a user subscribes (so they only get records seen after subscribing, not the
-- query's entire historical backlog), and advanced only when that user's combined digest email
-- actually sends successfully; a failed send blocks retry for this (user, query) pair alone,
-- never for any other subscriber of the same query or any other query.
CREATE TABLE UserSearchQueryDigest (
    userId INTEGER NOT NULL REFERENCES "User"(id) ON DELETE CASCADE,
    searchQueryId INTEGER NOT NULL REFERENCES SearchQuery(id) ON DELETE CASCADE,
    lastDigestSentAt TIMESTAMPTZ,
    PRIMARY KEY (userId, searchQueryId)
);
```

All timestamp columns use `TIMESTAMPTZ` (`TIMESTAMP WITH TIME ZONE`) rather than plain `TIMESTAMP`, following a "universal UTC" storage strategy: values are normalized to UTC internally on write regardless of the writing session's time zone, and converted for display only at read time. This keeps cross-timezone comparisons (e.g. the digest cutoff filter below) unambiguous regardless of where the application server, database, or a future admin client happens to be running.

## 6. Asynchronous background engine and lifecycle workflows

The monitoring loop runs outside the Web API, scheduled by Quartz.NET or Hangfire on a 7-day cycle.

### Lifecycle
1. Trigger: the cron event starts the processing queue.
2. Batch: the worker selects all unique records from the `SearchQuery` table.
3. Routing (parser factory): for every target query:
   * API handlers (PubMed/IEEE): the engine reads the `targetUrl` and uses C# 14 extension blocks to split out the parameters without heap allocations. It sends the REST requests through `HttpClientFactory`.
   * Scraper handlers (Pedro): the engine starts headless Playwright for .NET, loads the query page, scrolls or processes forms as needed, and reads the raw DOM nodes.
4. Analysis and updates:
   * For each record returned by a query, upsert into `Record` keyed on `doi` (`INSERT ... ON CONFLICT (doi) DO NOTHING`), which deduplicates globally across all searches and users.
   * Insert the `(searchQueryId, recordId)` pair into `SearchQueryRecord` (`ON CONFLICT (searchQueryId, recordId) DO NOTHING`). This is the moment `firstSeenAt` is stamped for that query, regardless of whether the underlying `Record` was brand new to the system or already existed from a different search.
   * Bridge new source associations through `SourceRecord` as before.
5. Digest assembly and email dispatch: dispatch is per user, not per `SearchQuery`. A user subscribed to several queries (across one or many sources) gets exactly one email per run, with one section per query that has pending content for them, rather than one email per query.
   * For each `SearchQuery` just polled, compute the delta against `UserSearchQueryDigest` rather than a single query-wide watermark: read every subscriber's own `lastDigestSentAt` for that query (`null` counts as "never sent"; the delta then includes that query's full history for that subscriber, since a `null` watermark only occurs for a subscriber the seed-on-subscribe step hasn't reached, not for a deliberately-unbounded backlog request), then filter `SearchQueryRecord` rows per subscriber:
     ```sql
     SELECT usq.userId, r.id, r.doi, r.title, r.description, sqr.firstSeenAt
     FROM UserSearchQuery usq
     JOIN SearchQueryRecord sqr ON sqr.searchQueryId = usq.searchQueryId
     JOIN Record r ON r.id = sqr.recordId
     LEFT JOIN UserSearchQueryDigest uqd
         ON uqd.userId = usq.userId AND uqd.searchQueryId = usq.searchQueryId
     WHERE usq.searchQueryId = @searchQueryId
       AND sqr.firstSeenAt > COALESCE(uqd.lastDigestSentAt, '-infinity'::timestamptz)
     ORDER BY sqr.firstSeenAt DESC;
     ```
   * The worker groups every pending `(user, searchQuery)` delta computed this run by `userId`, so a user with pending content in three of their subscribed queries gets one email containing three sections, not three emails.
   * Each combined delta set is transformed via an automated HTML styling script into one email digest template (one section per query, still per-source-formatted) and dispatched asynchronously through the transmission vendor (e.g., Postmark or Resend API).
   * On confirmed successful dispatch, only the `UserSearchQueryDigest` rows for the `(user, searchQuery)` pairs actually included in that one email are advanced to the current run's cutoff timestamp, never the whole `SearchQuery`. A failed send therefore blocks retry only for that one user's pending queries, not for any other subscriber of the same query. Using an explicit per-pair column here (rather than deriving the cutoff purely from the cron interval) makes the system tolerate missed or delayed runs, retries, and manual backfills, without one subscriber's delivery trouble affecting anyone else's.
   * A subscriber's brand new subscription seeds its `UserSearchQueryDigest` row to `now()` at subscribe time, so they receive only records seen from that point forward, not the query's entire historical backlog, which may already have been sent to that query's other subscribers long ago.

## 7. Scaling and optimization

* Allocation-free tokenization: pasted URL strings are tokenized with C# 14 implicit span conversions and .NET 10 string normalization helpers.
* JSON serialization: responses use .NET 10 `JsonSourceGenerationOptionsAttribute` settings to drop circular references, keeping overhead low when tracking thousands of references.
* Rate-limit handling: scientific databases impose strict IP and API-key quotas. The worker should use `HttpClientFactory` with a resilience library such as Polly to enforce rate limits and back off on 429/503 responses.
* Polyglot scraper option: Playwright for .NET works, but if platforms change their anti-bot checks (e.g., Cloudflare, CAPTCHAs), individual scrapers can move into small, isolated Python Docker microservices. The .NET worker calls them over internal gRPC/HTTP, so scrapers can be updated without changing the main architecture.

## 8. Open items and follow-ups

* Legal/ToS review of automated replay of Scopus/IEEE Xplore search URLs, including institutional proxy routing (see Section 2 note).
* Session-URL expiry handling: define behavior when a pasted search URL's embedded session token expires and the query can no longer be replayed server-side.
* Anti-bot resilience plan for the Pedro.org.au Playwright scraper beyond the Section 7 fallback idea (detection, alerting, and graceful degradation when scraping breaks mid-run).