# Scientific Search Alert SaaS: Architecture & Specification

## 1. Executive Summary
This document defines the complete technical architecture and system specification for a multi-tenant, public SaaS platform designed to automate literature monitoring across major scientific databases (PubMed, IEEE Xplore, Scopus, and Pedro.org.au).

Users interact with the platform by performing their desired search query natively on a scientific website, copying the final browser search URL, and pasting it into this platform. On a weekly schedule, the system processes these URLs, extracts new matching records via API extraction or headless scraping, deduplicates findings against historical runs, and dispatches a consolidated email digest featuring new article counts, titles, and descriptions.

The implementation stack is built natively on **Modern C# 14 and .NET 10 (Long-Term Support)**, leveraging **Keycloak** for full identity federation and OIDC-compliant access control.

---

## 2. Platform Strategy & Extraction Mechanics

The backend features a **Parser Factory Engine** designed to deconstruct pasted browser URLs into structured, queryable data payloads mapped to respective service providers:

| Target Platform | Strategy | Integration Protocol |
| :--- | :--- | :--- |
| **PubMed** | Native API | Extract `term` query parameter $\rightarrow$ Bind to public NCBI Entrez API (`esearch.fcgi` / `esummary.fcgi`). |
| **IEEE Xplore** | Native API | Extract `queryText` parameter $\rightarrow$ Query IEEE Xplore Metadata API via premium developer keys. |
| **Scopus** | Native API + Proxy | Extract `searchString` key $\rightarrow$ Query Elsevier Scopus API (Requires developer keys combined with institutional network proxy routing). |
| **Pedro.org.au** | Headless Scraper | Extract form field arrays from URL $\rightarrow$ Instantiate isolated **Playwright for .NET** or external micro-scrapers to read raw DOM segments. |

> **Note — ToS/legal review required:** replaying user-authenticated or session-scoped search URLs against Scopus/IEEE Xplore on a recurring schedule, and routing traffic through institutional proxies, may conflict with those providers' terms of service and the institution's network-use policy. This needs explicit legal sign-off before implementation, and pasted URLs from these platforms may carry session tokens that expire, breaking the "paste once, monitor forever" assumption.

---

## 3. High-Level Architecture & Technical Stack

The architecture is explicitly designed to handle intensive, asynchronous back-end polling patterns without blocking front-end operations or user request lifecycles.

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

---

## 4. Identity & Access Management (Keycloak Integration)

Authentication, registration, and credential security are fully decoupled from the transactional database and offloaded to an enterprise **Keycloak** identity provider instance.

### Realm Profile Configuration
* **Realm Identifier:** `science-alerts-saas` (Completely isolated from administrative default master domains).
* **Self-Service Configuration Attributes:**
  * **User Registration:** Enabled.
  * **Email as Username:** Enabled (Streamlines client sign-in patterns).
  * **Verify Email:** Enabled. This is an explicit architectural dependency—ensuring email reachability is required before executing costly background tasks.

### Token Verification Flow (.NET 10 Pipeline)
1. The frontend client initializes OIDC Authorization Code Flow with PKCE via Keycloak's login interface.
2. Upon verification, Keycloak passes cryptographically signed **JSON Web Tokens (JWTs)** back to the browser context.
3. Every protected API request transmits this token via the standard HTTP pipeline header: `Authorization: Bearer <JWT>`.
4. The ASP.NET Core 10 backend processes and authorizes requests middleware-style using standard metadata validators mapping directly back to Keycloak's `.well-known/openid-configuration` endpoints.

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

---

## 5. Database Schema & ER Model

The relational persistence tier tracks sources, individual unique queries, historical record indexing, and multi-tenant user tracking relationships.

**Revision note:** the previous version of this schema linked `SearchQuery` to `Record` only indirectly, through `Source`. Since `Source` is coarse-grained (one row per platform), that path could not express "which records belong to which specific saved search," and the weekly digest job had no way to compute a per-search delta. This revision introduces an explicit `SearchQueryRecord` junction table between `SearchQuery` and `Record`, and replaces the `currentRecordCount` / `previousRecordCount` counters with a timestamp-based delta mechanism (`firstSeenAt`), which is both simpler and less prone to drifting out of sync with reality. The undefined `subscriptioners` field has been removed and replaced with an explicit `lastDigestSentAt` tracking column.

### Entity Relationship Layout

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
                                   ├───────────────────┤
                                   │ id (PK)           │
                                   │ name              │
                                   │ userName          │
                                   │ email             │
                                   └───────────────────┘
```

### Relational Table Implementations (SQL Specification)

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
```

All timestamp columns use `TIMESTAMPTZ` (`TIMESTAMP WITH TIME ZONE`) rather than plain `TIMESTAMP`, following a "universal UTC" storage strategy: values are normalized to UTC internally on write regardless of the writing session's time zone, and converted for display only at read time. This keeps cross-timezone comparisons (e.g. the digest cutoff filter below) unambiguous regardless of where the application server, database, or a future admin client happens to be running.

---

## 6. Asynchronous Background Engine & Lifecycle Workflows

The automated monitoring loop functions outside the Web API execution layer using distributed scheduling models (Quartz.NET or Hangfire) operating on a 7-day chronometer loop.

### Complete Lifecycle Protocol
1. **Trigger Phase:** The central cron event kicks off the processing queue.
2. **Batch Query Processing:** The worker selects all unique records from the `SearchQuery` table.
3. **Execution Routing (The Parser Factory):** For every target query:
   * **API Handlers (PubMed/IEEE):** The engine reads the `targetUrl`. Using **C# 14 extension blocks**, it separates raw parameter blocks instantly without garbage heap allocations. It routes structured REST data payloads safely via `HttpClientFactory`.
   * **Scraper Handlers (Pedro):** The engine boots a headless configuration context utilizing **Playwright for .NET**, loads the specific query page, executes virtual scrolling or form processing, and pulls back raw DOM nodes to process.
4. **Analysis & Update Mechanics:**
   * For each record returned by a query, upsert into `Record` keyed on `doi` (`INSERT ... ON CONFLICT (doi) DO NOTHING`), ensuring global deduplication across all searches and users.
   * Insert the `(searchQueryId, recordId)` pair into `SearchQueryRecord` (`ON CONFLICT (searchQueryId, recordId) DO NOTHING`). This is the moment `firstSeenAt` is stamped for that query — regardless of whether the underlying `Record` was brand new to the system or already existed from a different search.
   * Bridge new source associations through `SourceRecord` as before.
5. **Digest Assembly & Email Dispatch:**
   * For each `SearchQuery`, compute the delta as every row in `SearchQueryRecord` where `firstSeenAt >= lastDigestSentAt` (falling back to a fixed lookback window, e.g. 7 days, if `lastDigestSentAt` is null on first run):
     ```sql
     SELECT r.id, r.doi, r.title, r.description, sqr.firstSeenAt
     FROM SearchQueryRecord sqr
     JOIN Record r ON r.id = sqr.recordId
     WHERE sqr.searchQueryId = @searchQueryId
       AND sqr.firstSeenAt >= @lastDigestSentAt
     ORDER BY sqr.firstSeenAt DESC;
     ```
   * The worker joins `UserSearchQuery` with `"User"` to pull the complete roster of emails subscribed to that `searchQueryId`.
   * The delta set is transformed via an automated HTML styling script into an elegant email digest template and dispatched asynchronously through the transmission vendor (e.g., Postmark or Resend API).
   * On confirmed successful dispatch, `SearchQuery.lastDigestSentAt` is updated to the current run's timestamp. Using an explicit column here (rather than deriving the cutoff purely from the cron interval) makes the system robust to missed or delayed runs, retries, and manual backfills.

---

## 7. Scaling & Optimization Blueprint

* **Allocation-Free Text Tokenization:** The background parsing architecture takes deliberate advantage of **C# 14 implicit span conversions** and specialized .NET 10 string normalization helper functions for tokenizing incoming pasted URL strings.
* **JSON Serialization Handling:** Domain responses utilize .NET 10 `JsonSourceGenerationOptionsAttribute` parameters to catch and cleanly drop circular metadata linkages natively, maintaining low overhead loops when tracking thousands of references concurrently.
* **Rate Limit Resiliency (Throttling HTTP Clients):** Scientific databases impose aggressive IP and application key quotas. The background processing architecture must leverage .NET's native `HttpClientFactory` combined with resilient scheduling algorithms (e.g., **Polly**) to enforce rate-limiting parameters and backoff on 429/503 responses.
* **Polyglot Scraper Architecture Option:** While Playwright for .NET is robust, if platforms change their client-side anti-bot validation structures (e.g., Cloudflare, CAPTCHAs), individual scrapers can be abstracted away into tiny, isolated Python Docker microservices. The .NET Worker calls these microservices via internal gRPC/HTTP parameters, allowing targeted updates to scrapers without modifying the master architecture.

---

## 8. Open Items / Follow-Ups

* **Legal/ToS review** of automated replay of Scopus/IEEE Xplore search URLs, including institutional proxy routing (see Section 2 note).
* **Session-URL expiry handling:** define behavior when a pasted search URL's embedded session token expires and the query can no longer be replayed server-side.
* **Anti-bot resilience plan** for the Pedro.org.au Playwright scraper beyond the Section 7 fallback idea (detection, alerting, and graceful degradation when scraping breaks mid-run).