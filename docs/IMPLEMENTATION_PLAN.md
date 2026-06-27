# Fitbit-sync — Implementation Plan

> Status: **APPROVED — build in progress.** All six open decisions (Appendix C) are
> settled. The phased TDD build follows §9.

## 0. Purpose & scope

`Fitbit-sync` is a self-contained .NET 10 service that lives in its own top-level
subfolder of this repo. It:

- Periodically syncs a user's own Fitbit health data into a local SQLite database.
- Exposes a programmatic API (local HTTP, loopback-bound, TLS) including **force-sync
  on demand**.
- Treats encryption (at rest + in transit), auditability, resilience, anti-spoofing,
  loose coupling, and TDD as **first-class, non-negotiable constraints**.

### Target platform & toolchain

| Item | Decision |
|------|----------|
| SDK | .NET 10 (10.0.300 confirmed installed) |
| Target framework | `net10.0` for all projects |
| Language | C# (latest), nullable enabled, implicit usings on |
| OS posture | Cross-platform code; Windows is the primary dev/run host. Key-protection has an OS-specific seam (see §2/§4). |

### Guiding principles (apply everywhere)

- **No code comments anywhere except in test projects.** Production code must read
  cleanly through intention-revealing names, small methods, and types that model the
  domain. XML doc comments are also omitted in production code.
- **Semantically correct, intention-revealing naming.** Verb-phrase methods, `is/has/can/should`
  booleans, no abbreviations.
- **Loose coupling via interfaces + DI.** Every cross-cutting capability (provider,
  clock, token store, audit sink, rate limiter, crypto) sits behind an interface
  registered in the DI container. Concrete types are `sealed` and `internal` where
  possible; only the contracts are public across project boundaries.
- **TDD.** A failing test precedes each unit of behavior. The phased build order in §9
  is expressed as "which red test first".
- **C# style** per the repo CLAUDE.md (this-qualified instance access, braces always,
  one type per file, file-scoped namespaces, `ConfigureAwait(false)` in library code,
  source-generated `[LoggerMessage]` logging, `var` when obvious, raw string literals,
  collection expressions).

---

## 1. Solution & project layout

A single solution `Fitbit-sync/FitbitSync.sln`. Projects are split to enforce a strict,
acyclic **dependency direction** that points inward toward the domain (Clean/Hexagonal
architecture). The Fitbit-specific code is an *outer* adapter so it can be replaced by a
Google Health adapter without touching the core.

```
Fitbit-sync/
├── FitbitSync.sln
├── Directory.Build.props          # shared TFM, nullable, langversion, analyzers
├── Directory.Packages.props       # central package version management (CPM)
├── .editorconfig                  # style rules (no-comments lint exception for tests)
├── docs/
│   └── IMPLEMENTATION_PLAN.md      # this file
├── src/
│   ├── FitbitSync.Domain/         # entities, value objects, enums, domain interfaces (PORTS)
│   ├── FitbitSync.Application/    # sync engine, use-cases, orchestration; depends on Domain only
│   ├── FitbitSync.Persistence/   # EF Core + SQLite + SQLite3MC encryption; implements repo/store ports
│   ├── FitbitSync.Providers.Fitbit/  # Fitbit Web API adapter (HTTP, OAuth, DTO→domain mapping)
│   ├── FitbitSync.Security/       # crypto primitives: AES-GCM column cipher, record signing, key access
│   └── FitbitSync.Api/            # ASP.NET Core minimal API host + DI composition root + background worker
└── tests/
    ├── FitbitSync.Domain.Tests/
    ├── FitbitSync.Application.Tests/
    ├── FitbitSync.Persistence.Tests/
    ├── FitbitSync.Providers.Fitbit.Tests/
    ├── FitbitSync.Security.Tests/
    └── FitbitSync.Api.Tests/      # integration tests via WebApplicationFactory
```

### Responsibilities & dependency direction

| Project | Responsibility | May depend on |
|---------|----------------|---------------|
| `Domain` | Pure domain: health-metric entities, `SyncCheckpoint`, `OAuthToken`, `AuditEntry`, enums, and **ports** (interfaces) like `IHealthDataProvider`, `ITokenStore`, `ISyncCheckpointStore`, `IAuditTrail`, `IMetricRepository`, `IClock`, `IRecordSigner`. No external packages. | — (BCL only) |
| `Application` | Orchestration/use-cases: `SyncEngine`, per-metric `ISyncJob`s, scheduling policy, rate-limit/backoff policy, force-sync command handler. Depends on **ports**, never on concretes. | `Domain` |
| `Persistence` | EF Core `DbContext`, entity configs, migrations, encrypted SQLite via SQLite3MC, implementations of `ITokenStore`/`ISyncCheckpointStore`/`IAuditTrail`/`IMetricRepository`. | `Domain` |
| `Providers.Fitbit` | `FitbitHealthDataProvider : IHealthDataProvider`, typed `HttpClient`, OAuth2 PKCE client, response→domain mappers, Fitbit rate-limit header parsing. | `Domain` |
| `Security` | `AesGcmColumnCipher`, `Hmac`/Ed25519 `IRecordSigner`, `IKeyProvider` with OS-specific protectors. | `Domain` |
| `Api` | Composition root. Minimal API endpoints, Kestrel/HTTPS, `BackgroundService` scheduler host, DI wiring of all adapters. The **only** project that references every other project. | all `src/*` |

Key rule: `Domain` and `Application` have **zero** knowledge of Fitbit, EF Core, ASP.NET,
or SQLite. Swapping providers or storage touches only the outer projects + composition root.

`Directory.Build.props` enables `TreatWarningsAsErrors`, nullable, and an analyzer that
forbids comments in `src/**` while `.editorconfig` relaxes it under `tests/**`.

---

## 2. Domain model, SQLite schema, encryption at rest, token security

### 2.1 Domain model (in `FitbitSync.Domain`)

Health metrics are modeled as a small, uniform set of records so new metric types and a
future provider don't explode the model. Each stored sample shares a common envelope.

**Core value objects / entities**

- `MetricType` (enum): `HeartRate`, `HeartRateIntraday`, `Sleep`, `Spo2`, `ActivitySummary`,
  `ActivityTimeSeries`, `ActiveZoneMinutes`, `BreathingRate`, `Hrv`, `SkinTemperature`,
  `CardioFitness` (VO2max).
- `IntradayResolution` (enum): `OneSecond`, `OneMinute`, `FiveMinute`, `FifteenMinute`, `None`.
- `MetricSample` (entity): the canonical stored unit —
  - `Id` (GUID, app-assigned, deterministic where possible for idempotency),
  - `MetricType`, `EffectiveDate` (the Fitbit "date" the data belongs to),
  - `PeriodStart`/`PeriodEnd` (UTC instants for intraday/range),
  - `Resolution`,
  - `Payload` (canonical JSON of the normalized metric value(s)),
  - `SourceProvider` (e.g. `"fitbit"`),
  - `Signature` + `SignatureKeyId` (anti-spoofing, see §7),
  - `RetrievedAtUtc`, `RowVersion`.
- `OAuthToken` (entity): `Provider`, `AccessTokenCipher`, `RefreshTokenCipher`,
  `ScopeCsv`, `AccessTokenExpiresAtUtc`, `ObtainedAtUtc`, `FitbitUserId`, `RowVersion`.
  Token material is **never** stored or logged in plaintext.
- `SyncCheckpoint` (entity): `Provider`, `MetricType`, `Resolution`, `LastSyncedThroughDate`,
  `NextDateToFetch`, `BackfillFloorDate`, `State` (`Idle|Running|Backfilling|Paused|Failed`),
  `UpdatedAtUtc`, `RowVersion`. Drives **resumable** sync (§5).
- `AuditEntry` (append-only): `Id`, `OccurredAtUtc`, `Category`
  (`Sync|ApiAccess|TokenRefresh|AuthGrant|RateLimit|Error`), `Action`, `Outcome`,
  `Actor`, `Detail` (JSON, secret-free), `PrevHash`, `EntryHash` (hash chain, see §7).
- `RateLimitSnapshot` (value object): `Limit`, `Remaining`, `ResetAtUtc`, `ObservedAtUtc`.

**Ports defined in Domain** (implemented in outer layers):
`IHealthDataProvider`, `ITokenStore`, `ISyncCheckpointStore`, `IMetricRepository`,
`IAuditTrail`, `IRecordSigner`, `IKeyProvider`, `IClock`, `IRateLimitGate`.

### 2.2 SQLite schema (EF Core code-first, in `FitbitSync.Persistence`)

Tables (snake_case via configured conventions):

| Table | Notes |
|-------|-------|
| `metric_samples` | PK `id`; indexes on (`metric_type`,`effective_date`), (`metric_type`,`resolution`,`period_start`). `payload` stored as TEXT (canonical JSON). `signature` BLOB. |
| `oauth_tokens` | One row per provider. `access_token_cipher`/`refresh_token_cipher` are BLOB (AES-GCM envelope: nonce ‖ ciphertext ‖ tag). No plaintext columns. |
| `sync_checkpoints` | PK (`provider`,`metric_type`,`resolution`). Drives resumability. |
| `audit_entries` | Append-only; `entry_hash`/`prev_hash` form a tamper-evident chain. Insert-only repository; no update/delete API surface. |
| `schema_metadata` | Tracks encryption scheme version + signing-key id for migrations/rotation. |
| `__EFMigrationsHistory` | EF Core migrations. |

Concurrency: every mutable entity has a `RowVersion` (`rowid`-backed or a GUID token)
for optimistic concurrency, preventing lost updates across the worker + API.

### 2.3 Encryption at rest — two complementary layers (defense in depth)

Research outcome drives a **belt-and-suspenders** approach:

1. **Whole-file encryption** of the SQLite database using **SQLite3MC**
   (`SQLite3MC.PCLRaw.bundle`) in **SQLCipher-compatible mode**. Rationale from research:
   - `SQLitePCLRaw.bundle_e_sqlcipher` is **deprecated** as of SQLitePCLRaw 3.0; the
     actively maintained successor is **SQLite3 Multiple Ciphers (SQLite3MC)** (MIT
     license; bundles current SQLite). It reads/writes SQLCipher-format databases.
   - We use the **`.Core` package split** to avoid native-bundle conflicts:
     `Microsoft.Data.Sqlite.Core` + `Microsoft.EntityFrameworkCore.Sqlite.Core` +
     `SQLite3MC.PCLRaw.bundle`, with `Batteries_V2.Init()` (or the SQLite3MC initializer)
     at composition-root startup.
   - The key is supplied via the connection string `Password=` keyword (Microsoft.Data.Sqlite
     emits `PRAGMA key`). Built with `SqliteConnectionStringBuilder` to avoid string injection.
   - **.NET 10 note:** the `Password` keyword path was broken on earlier runtimes (SQLite ≥3.48
     behavior change) and works again specifically on **.NET 10** — our target. This is called
     out as a risk to validate in Phase 0 (a spike test that opens, rekeys, and reopens an
     encrypted DB).
   - `PRAGMA rekey` supports key rotation without export/import.

2. **Application-level column encryption** for the highest-value secrets — the OAuth
   access/refresh tokens — using **`System.Security.Cryptography.AesGcm`** (AEAD) via an
   EF Core `ValueConverter`/the `ITokenStore`. Even if the DB file key is ever compromised
   or the file is opened unencrypted by mistake, tokens remain ciphertext. The AES-GCM
   **associated data** binds each ciphertext to its row identity (provider + column name)
   to prevent copy/paste swap attacks.

This means tokens are encrypted **twice** (column cipher inside an encrypted file); all
other health data is protected by the file-level cipher. We keep file-level encryption for
everything and reserve the heavier column cipher for token material to avoid losing
queryability on metric rows.

### 2.4 Key management (the seam that must stay clean)

`IKeyProvider` abstracts retrieval of (a) the SQLite file key and (b) the AES-GCM data
key, returning rotatable, versioned keys (`KeyId` + bytes). Implementations:

- **Windows (primary host):** wrap the key with **DPAPI** (`System.Security.Cryptography.ProtectedData`,
  `CurrentUser` scope) and store the wrapped blob in a permission-restricted key file under
  the app data directory.
- **Cross-platform fallback:** key file with OS file-permissions, or an env var / external
  KMS. Research flagged DPAPI is **Windows-only and throws elsewhere**, so the provider
  selects an implementation at composition time. ASP.NET Core **Data Protection** is the
  portable option for key-at-rest wrapping and is noted as an alternative.

Keys are **never** committed. Master key origin is config/secret-store (see §4.3). Rotation:
`schema_metadata` + `KeyId` on each ciphertext envelope let old and new keys coexist during
re-encryption.

---

## 3. Provider abstraction (isolating Fitbit; future Google Health swap)

The legacy Fitbit Web API is **deprecated September 2026** (migration to Google Health
APIs). The architecture therefore treats "where health data comes from" as a replaceable
adapter behind a domain port. Adding the Google provider later must touch **only** a new
`Providers.GoogleHealth` project + one composition-root line — no domain/application/persistence
changes.

### 3.1 The port (`FitbitSync.Domain`)

```
public interface IHealthDataProvider
{
    string ProviderKey { get; }                       // "fitbit", later "google-health"
    IReadOnlyCollection<MetricCapability> Capabilities { get; }
    Task<MetricFetchResult> FetchAsync(MetricFetchRequest request, CancellationToken ct);
}
```

- `MetricFetchRequest`: `MetricType`, `Resolution`, `DateRange` (or single date),
  pagination cursor. Provider-agnostic.
- `MetricFetchResult`: normalized `IReadOnlyList<MetricSample>` (already mapped to the
  canonical domain shape) **plus** a `RateLimitSnapshot?` and an optional continuation
  cursor. By returning **already-normalized `MetricSample`s**, the application layer never
  sees a Fitbit DTO.
- `MetricCapability`: declares which `MetricType`/`Resolution` combos and what **max
  historical window** a provider supports (Fitbit: HR 1y, Sleep 100d, BR/HRV/Temp/VO2max
  30d, SpO2 none). The sync engine reads capabilities to plan backfill windows generically,
  so per-metric limits live in data, not in engine `if`-statements.

### 3.2 The Fitbit adapter (`FitbitSync.Providers.Fitbit`)

- `FitbitHealthDataProvider : IHealthDataProvider` composes:
  - A typed `HttpClient` (`FitbitApiClient`) with base address `https://api.fitbit.com`,
    bearer-token handler, and a **rate-limit-aware `DelegatingHandler`** that reads
    `Fitbit-Rate-Limit-Remaining`/`-Reset` and feeds the `IRateLimitGate` (§5).
  - Per-metric **endpoint descriptors** mapping `MetricType`+`Resolution` to URL templates,
    e.g. `/1/user/-/activities/heart/date/{date}/{period}.json`,
    `/1.2/user/-/sleep/date/{date}.json`, `/1/user/-/spo2/date/{date}.json`,
    `/1/user/-/hrv/date/{date}.json`, AZM/intraday variants, etc.
  - **Mappers** translating Fitbit JSON DTOs → canonical `MetricSample` (stable, sorted,
    canonical-JSON payload so signatures are reproducible — see §7).
- Auth scopes needed are declared per capability: `activity, heartrate, sleep,
  oxygen_saturation, respiratory_rate, temperature, cardio_fitness, weight, nutrition,
  location, profile, settings` (HRV + AZM reuse `heartrate`/`activity`).
- DTOs, URL templates, and mappers are `internal`; only `FitbitHealthDataProvider` and a
  DI extension `AddFitbitProvider(...)` are public.

### 3.3 Why this swaps cleanly

- The engine plans work from `Capabilities` + `SyncCheckpoint`, not Fitbit specifics.
- Rate-limit semantics are surfaced as a provider-agnostic `RateLimitSnapshot`; a Google
  adapter supplies its own snapshot (or a null-object "unlimited" gate).
- `SourceProvider` on every `MetricSample` records provenance, so a mixed-history database
  (Fitbit rows + later Google rows) remains coherent and auditable.

---

## 4. OAuth2 flow, token refresh/rotation, secrets out of source

### 4.1 Flow — Authorization Code Grant with **PKCE** (research-confirmed)

- **Authorize URL:** `https://www.fitbit.com/oauth2/authorize` with `response_type=code`,
  `client_id`, `scope`, `code_challenge`, `code_challenge_method=S256`.
- **Token URL:** `https://api.fitbit.com/oauth2/token` with
  `grant_type=authorization_code`, `code`, `code_verifier`, `client_id`.
- **PKCE:** code verifier 43–128 chars; challenge = base64url(SHA-256(verifier)) without
  padding. Generated with a CSPRNG. For a **personal** app the client secret is optional;
  PKCE is used regardless and the secret (if configured) is kept out of source (§4.3).
- The interactive authorize step is a **one-time bootstrap**: a `dotnet run -- authorize`
  CLI verb (or a loopback `/oauth/callback` endpoint bound to localhost) captures the code,
  exchanges it, and persists the encrypted token. The long-running service thereafter only
  refreshes.

### 4.2 Token storage & refresh/rotation (resilience-critical)

- Tokens are persisted by `ITokenStore` → `oauth_tokens`, with access/refresh values
  AES-GCM-encrypted (§2.3) inside the encrypted DB file.
- **Access token lifetime 8h** (`expires_in: 28800`); expiry → **HTTP 401 `expired_token`**.
  A token handler refreshes **proactively** when within a skew window (e.g. < 5 min to
  expiry) and **reactively** on a 401.
- **Refresh-token rotation caveats** (from research, designed for explicitly):
  - Refresh tokens are **single-use**; each refresh returns a **new** access+refresh pair.
    The new pair must be **persisted atomically** (transaction + optimistic concurrency)
    *before* the old one is discarded, or the integration is bricked and needs re-auth.
  - **Concurrent refreshes can 409.** A process-wide **single-flight** async lock
    (`SemaphoreSlim`/`ITokenRefreshCoordinator`) ensures only one refresh is in flight;
    others await its result.
  - Identical refresh requests within ~2 min return the same response (idempotency aid) —
    used as a safety net, not the primary mechanism.
  - Every refresh writes a `TokenRefresh` audit entry (success/failure, no secret material).
- Refresh failures escalate through retry/backoff (§5); terminal failure sets token state
  to "needs re-auth", pauses affected checkpoints, and surfaces via the API health endpoint.

### 4.3 Keeping secrets out of source

- **No secrets in the repo, ever.** `client_id`, optional `client_secret`, redirect URI,
  and the master encryption key come from layered configuration:
  - **Development:** **.NET User Secrets** (`Microsoft.Extensions.Configuration.UserSecrets`),
    which stores values outside the project tree.
  - **Runtime/production:** environment variables and/or an OS secret store; the
    `IKeyProvider` resolves the master key (DPAPI-wrapped key file on Windows, §2.4).
- `appsettings.json` holds only **non-secret** shape (URLs, scope list, schedule, paths).
  `appsettings.Development.json` and any `*.secret.*`, `*.db`, key files, and User-Secrets
  are **git-ignored** via a project `.gitignore`.
- A startup **configuration validator** (`IValidateOptions<T>`) fails fast if required
  secrets are missing, so the service never silently runs unconfigured.

---

## 5. Sync engine — scheduling, resumable/incremental sync, rate limits, retry

Lives in `FitbitSync.Application`; hosted by a `BackgroundService` in `FitbitSync.Api`.
The engine is provider-agnostic and entirely interface-driven for testability.

### 5.1 Scheduling

- A `SyncScheduler` `BackgroundService` wakes every **15 minutes** for incremental
  ("recent") sync using `PeriodicTimer` and an injected `IClock` (so tests control time).
  Backfill is interleaved continuously, consuming only leftover budget (§5.6).
- Each tick enumerates enabled `MetricType`/`Resolution` capabilities and asks the
  `SyncPlanner` what work is due, based on `SyncCheckpoint`s. No work is hard-coded by date.
- A global **concurrency limiter** bounds simultaneous in-flight requests (Fitbit is
  per-user metered; we keep concurrency low, e.g. 1–2, and let the rate gate pace us).

### 5.2 Incremental + resumable sync (survives restarts)

- **`SyncCheckpoint`** per (provider, metric, resolution) is the durable cursor:
  - `NextDateToFetch` walks **forward** for incremental "catch up to today".
  - `BackfillFloorDate` / `NextBackfillDate` bound and track **backward** historical fill,
    walking **one day at a time** from the latest known day toward the provider capability
    floor (HR 1y, Sleep 100d, BR/HRV/Temp/VO2max 30d, SpO2 none).
  - `State` (`Idle|Running|Backfilling|Paused|Failed`) makes restarts safe: on startup the
    engine resets any `Running` left dangling by a crash back to a resumable state and
    continues from the persisted cursor — **no reliance on in-memory progress**.
- Each unit of work is a small, **idempotent** transaction: fetch a date/window → map →
  upsert `MetricSample`s (deterministic ids so re-runs don't duplicate) → advance checkpoint
  → write audit entry, all committed together. If the process dies mid-way, the next run
  re-attempts from the last committed checkpoint.
- **Idempotent upsert** keys on (`provider`, `metric_type`, `resolution`, `effective_date`,
  `period_start`) so re-fetching a day overwrites rather than duplicates, and re-signs.

### 5.3 Rate-limit handling (Fitbit: 150 req/hour/user)

Driven by research-confirmed facts:

- A shared **`IRateLimitGate`** (token-bucket style) gates every provider call. After each
  response, the Fitbit rate-limit `DelegatingHandler` updates the gate from
  **`Fitbit-Rate-Limit-Limit`**, **`Fitbit-Rate-Limit-Remaining`**, and
  **`Fitbit-Rate-Limit-Reset`** (the latter is **seconds until reset**, *not* a timestamp —
  converted to an absolute `ResetAtUtc` via `IClock`).
- Because Fitbit states these headers are **approximate/async**, the gate **also** keeps its
  own conservative client-side counter and treats whichever (header vs local) is more
  restrictive as truth, leaving headroom (e.g. stop at ~90% of quota).
- On **HTTP 429**, the engine parses `-Reset`, **pauses** affected checkpoints until
  `ResetAtUtc`, writes a `RateLimit` audit entry, and resumes automatically. `Retry-After`
  is **not documented** by Fitbit, so the design relies on `-Reset` (but reads `Retry-After`
  opportunistically if present).

### 5.4 Retry / backoff

- Transient faults (network, 5xx, 429) are handled with **Polly v8 `ResiliencePipeline`**:
  - **Retry** with exponential backoff **+ jitter**, bounded attempts.
  - **429** is special-cased: wait until `ResetAtUtc` rather than a blind backoff.
  - **Circuit breaker** opens on sustained provider failure to stop hammering; the engine
    pauses and surfaces unhealthy status.
  - **Timeout** per request.
- Retry policy is injected (`IAsyncPolicy`/pipeline) so tests assert behavior with a fake
  clock and a fake handler — no real waiting.
- **401 `expired_token`** is handled by the token layer (refresh + single retry), distinct
  from transient retry, so we don't burn rate-limit budget on auth errors.

### 5.5 Force-sync on demand

- The API (`POST /sync`) enqueues a `ForceSyncCommand` (optional metric/date-range filter)
  onto the same engine via an in-process queue/`Channel`. It shares the rate gate and
  checkpoints with scheduled sync, so a manual trigger can't violate limits or corrupt
  cursors. The command returns a `syncRunId` for status polling and is fully audited.

### 5.6 Shared hourly budget — freshness first, backfill with the remainder

Incremental (15-min) sync and historical backfill **share the single 150-req/hour/user
budget**. A `SyncBudgetPlanner` enforces strict priority:

1. **Freshness first.** Every cycle, the planner first satisfies all *incremental* work
   (advance each metric's `NextDateToFetch` up to today). This is reserved budget — backfill
   never starves current data.
2. **Backfill with the remainder.** Only the **leftover** hourly allowance (observed via the
   `IRateLimitGate`'s remaining count, minus a safety reserve for the next incremental cycle)
   is spent walking `NextBackfillDate` **backwards one day at a time**.
3. **Hit-limit → pause → resume.** When the gate signals exhaustion or Fitbit returns
   **429**, backfill **pauses** (checkpoint `State = Paused`, `ResumeAtUtc = ResetAtUtc`,
   ~1h) and resumes automatically next window from the persisted `NextBackfillDate` — fully
   resumable across restarts.
4. **Eventually complete.** Backfill continues window after window until every metric reaches
   its `BackfillFloorDate` (provider cap), then idles.

All four behaviors are unit-tested with a fake clock + fake gate so no real waiting occurs.

---

## 6. Programmatic API surface + transport security

`FitbitSync.Api` is an ASP.NET Core **minimal API**. It is a **local programmatic API**
(loopback-bound by default), not a public web service.

### 6.1 Endpoints (versioned under `/v1`)

| Method & path | Purpose |
|---------------|---------|
| `POST /v1/sync` | **Force-sync on demand.** Optional body filters by metric(s) and date range. Returns `202 Accepted` + `syncRunId`. |
| `GET /v1/sync/runs/{id}` | Status of a sync run (queued/running/completed/failed, counts). |
| `GET /v1/sync/status` | Per-metric checkpoint state, last success, rate-limit snapshot, paused flags. |
| `GET /v1/metrics/{type}` | Query stored samples by type + date range + resolution (paged). Read path for consumers. |
| `GET /v1/metrics/{type}/{date}` | Single-day fetch. |
| `GET /v1/audit` | Read the append-only audit trail (paged, filter by category/time). |
| `GET /v1/health` | Liveness/readiness: DB reachable, token valid/needs-reauth, circuit state. |
| `POST /v1/oauth/authorize` + `GET /v1/oauth/callback` | One-time bootstrap of OAuth (loopback only); can alternatively be a CLI verb. |

DTOs are explicit response/request contracts (no leaking EF entities). Validation via
`MiniValidation`/data annotations; consistent `ProblemDetails` error payloads.

### 6.2 Transport security (in transit)

- **HTTPS/TLS only.** Kestrel configured with HTTPS endpoints; HTTP either disabled or
  `UseHttpsRedirection` + HSTS. Dev uses the .NET dev certificate; production uses a
  configured cert (path/store via config, never committed).
- **Loopback binding by default** (`https://127.0.0.1:<port>`); exposure beyond localhost is
  an explicit opt-in config change.
- **Authentication for the local API:** a required API key (or mTLS as an upgrade path)
  enforced by middleware, so other local processes can't trigger syncs or read health data
  unauthenticated. The key is a secret resolved like all others (§4.3). Every authenticated
  call writes an `ApiAccess` audit entry; failures write `ApiAccess`/`Error`.
- Standard hardening: rate-limiting middleware on the API itself, request size limits,
  security headers, and no secrets in logs.

---

## 7. Auditability & anti-spoofing (concrete mechanisms)

### 7.1 Append-only audit trail (tamper-evident hash chain)

- `audit_entries` is **insert-only**: the `IAuditTrail` port exposes `AppendAsync` and
  read queries — **no update/delete** is offered anywhere in code, and EF is configured to
  block modification/removal of audit entities (guarded in `SaveChanges`).
- Each entry stores `PrevHash` (the previous entry's `EntryHash`) and its own
  `EntryHash = SHA-256(canonical(entry-without-hash) ‖ PrevHash)`, forming a
  **blockchain-style chain**. Any retroactive edit/deletion breaks the chain and is
  detectable by a `VerifyChainAsync` routine (exposed via a CLI verb and exercised in tests).
- Optionally the latest chain head hash is periodically written to a separate location
  (or signed, see 7.2) so even wholesale table replacement is detectable.
- Audited categories: **Sync** (start/finish, per-metric counts, windows), **TokenRefresh**,
  **AuthGrant**, **ApiAccess** (who/when/what endpoint), **RateLimit** (429s, pauses), and
  **Error**. Detail payloads are JSON and **scrubbed of secrets/token material**.

### 7.2 Anti-spoofing — authenticity & integrity of stored records

- Every `MetricSample` is **signed** at write time by `IRecordSigner` over a **canonical
  serialization** of its meaningful fields (type, dates, resolution, provider, canonical
  payload). Stored as `signature` + `signature_key_id`.
  - Default scheme: **HMAC-SHA256** with a dedicated signing key from `IKeyProvider`
    (fast, symmetric, fits the local single-writer model).
  - Upgrade path: **Ed25519** asymmetric signatures (`System.Security.Cryptography` /
    `NSec`) if external verifiability is ever needed — the `IRecordSigner` seam makes this a
    drop-in.
- **Canonical JSON** (sorted keys, fixed culture/number formatting) guarantees the signed
  bytes are reproducible, so verification is deterministic across runs and providers.
- A `VerifyIntegrityAsync` routine (CLI verb + API `health` signal + tests) re-computes and
  checks signatures, detecting any out-of-band tampering with `metric_samples`.
- **Provenance:** `SourceProvider` + `RetrievedAtUtc` + the signing key id record *where*
  and *when* each row came from, so authenticity is attributable per provider — important
  across the Fitbit→Google migration.
- Defense in depth combines with §2.3 (encrypted at rest) and §2.4 (key protection): an
  attacker would need both the encryption key *and* the signing key to forge undetectably.

---

## 8. TDD approach

Tests **drive** development: each behavior starts as a failing (red) test, then minimal code
to green, then refactor. **Comments are allowed only in tests** — used there to document
intent, given/when/then, and edge cases.

### 8.1 Frameworks & doubles

- **xUnit** as the test runner; **FluentAssertions** for readable assertions; **NSubstitute**
  for interface fakes/mocks of ports.
- **No live network, ever, in automated tests.** External HTTP is faked at the
  `HttpMessageHandler` boundary:
  - A hand-rolled `FakeHttpMessageHandler` (and/or **`WireMock.Net`**) returns canned Fitbit
    JSON fixtures and **simulated rate-limit headers / 429 / 401** responses, so retry,
    backoff, refresh, and rate-gate logic are tested deterministically.
- **Time is faked** via `IClock` (custom) and/or `Microsoft.Extensions.Time.Testing`
  `FakeTimeProvider`, so backoff/scheduling tests run instantly and deterministically.
- Test data builders produce canonical fixtures (real Fitbit response samples, secret-free).

### 8.2 Unit vs integration split

**Unit tests** (fast, no I/O) cover pure/logic-heavy units in `Domain`, `Application`,
`Security`:
- Crypto: AES-GCM round-trip, associated-data binding rejects swapped rows, key-id
  envelope handling, HMAC/Ed25519 sign+verify, canonical-JSON stability.
- Sync planning: checkpoint advancement, backfill window clamping to capability limits,
  resume-after-crash transitions.
- Rate gate: header parsing (`-Reset` seconds→`ResetAtUtc`), conservative local counter,
  429 pause/resume, "most restrictive wins".
- Retry/backoff policy: attempt counts, jitter bounds, 429 special-case, circuit breaker —
  with fake clock.
- OAuth: PKCE verifier/challenge correctness, proactive vs reactive refresh, **single-flight**
  refresh under concurrency, atomic persistence of rotated tokens.
- Mappers: Fitbit DTO → `MetricSample` for every metric type.
- Audit: hash-chain append + `VerifyChain` detects tampering.

**Integration tests** (real components, still no external network):
- **Persistence**: spin up a **real encrypted SQLite file** via SQLite3MC in a temp dir;
  verify migrations, encrypted round-trip, **that the raw file is unreadable without the
  key** (open without `Password` ⇒ failure), token column ciphertext, optimistic concurrency,
  and append-only audit enforcement (update/delete blocked).
- **Provider**: `FitbitHealthDataProvider` against `WireMock.Net` serving fixtures, asserting
  end-to-end fetch→map→rate-snapshot, 429 handling, and token refresh on 401.
- **API**: `Microsoft.AspNetCore.Mvc.Testing` `WebApplicationFactory` boots the host with a
  fake provider + temp DB; tests `POST /v1/sync` force-sync, status polling, auth (API key
  required ⇒ 401 without it), HTTPS behavior, audit entries written, and `ProblemDetails`.
- A **full resumability test**: start a sync, kill mid-run (simulate), restart, assert it
  resumes from the last checkpoint with no duplicates and a clean audit chain.

### 8.3 Coverage & CI hygiene

- Coverage via **coverlet**; the repo's due-diligence rules (TDD, review) are honored by
  keeping every phase green before moving on. Analyzers enforce style and the "no comments
  in `src/**`" rule.

---

## 9. Phased, incremental build order (red test first each phase)

Each phase is shippable and ends green. The **first failing test** of each phase is named.

**Phase 0 — Skeleton + encryption spike.**
First red test: `EncryptedDatabase_CannotBeOpenedWithoutKey`.
Create solution, projects, CPM, `Directory.Build.props`, analyzers. Prove SQLite3MC
whole-file encryption works on **.NET 10** (the `Password`-keyword caveat) with a real
temp-file round-trip and a negative "no key ⇒ fail" test. Establish `IClock`. **De-risks the
single biggest technical unknown first.**

**Phase 1 — Security core.**
First red test: `AesGcmColumnCipher_RoundTrips_AndRejectsTamperedAssociatedData`.
Implement `Security`: AES-GCM column cipher, `IKeyProvider` (Windows DPAPI + cross-platform
file fallback), `IRecordSigner` (HMAC-SHA256) + canonical JSON. All unit-tested.

**Phase 2 — Domain + persistence.**
First red test: `MetricRepository_UpsertIsIdempotent_AndSignsRows`.
Define domain entities/ports. Implement EF Core `DbContext`, migrations, encrypted store,
`ITokenStore` (encrypted token columns), `ISyncCheckpointStore`, append-only `IAuditTrail`
with hash chain. Integration-tested against real encrypted SQLite.

**Phase 3 — Provider abstraction + Fitbit adapter (offline).**
First red test: `FitbitProvider_MapsHeartRateResponse_ToCanonicalSamples`.
Define `IHealthDataProvider`, capabilities, `MetricFetchRequest/Result`. Build
`FitbitHealthDataProvider`, typed client, endpoint descriptors, all metric mappers, and the
rate-limit header handler — tested entirely against `WireMock.Net` fixtures (incl. 429/401).

**Phase 4 — OAuth2 PKCE + token refresh.**
First red test: `TokenService_RefreshesRotatedTokens_AtomicallyAndSingleFlight`.
PKCE authorize/exchange, proactive/reactive refresh, single-flight coordination, atomic
rotation persistence, refresh auditing. Bootstrap CLI verb.

**Phase 5 — Sync engine + resilience.**
First red test: `SyncEngine_ResumesFromCheckpoint_AfterSimulatedCrash`.
`SyncPlanner`, per-metric jobs, idempotent transactional sync unit, `IRateLimitGate`, Polly
retry/backoff/circuit-breaker/timeout, 429 pause-until-reset, scheduler `BackgroundService`.
Resumability + rate-limit tests.

**Phase 6 — Programmatic API + transport security.**
First red test: `PostSync_RequiresApiKey_AndEnqueuesForceSync`.
Minimal API endpoints (force-sync, status, metrics, audit, health), HTTPS/loopback, API-key
auth middleware, `ProblemDetails`, API-access auditing. `WebApplicationFactory` integration
tests.

**Phase 7 — Hardening + verification tooling.**
First red test: `VerifyChain_DetectsTamperedAuditEntry` / `VerifyIntegrity_DetectsForgedSample`.
Chain/signature verification CLI verbs, key-rotation path (`PRAGMA rekey` + signing-key
roll), config validators, security headers, docs (README + ops runbook), final review pass.

**Phase 8 (optional, post-MVP) — Google Health provider stub.**
First red test: `GoogleHealthProvider_AdvertisesCapabilities`.
Prove the abstraction by scaffolding a second `IHealthDataProvider` with no changes to
Domain/Application/Persistence — validating the Sept-2026 migration story.

---

## 10. Proposed NuGet packages (with one-line justification)

Versions are managed centrally via `Directory.Packages.props` (Central Package Management).
Pinned versions to be finalized at scaffold time against the restored .NET 10 feed.

### Runtime / production

| Package | Why |
|---------|-----|
| `Microsoft.EntityFrameworkCore.Sqlite.Core` | EF Core 10 ORM for SQLite **without** the bundled native lib, so we can supply the encryption-capable provider. |
| `Microsoft.Data.Sqlite.Core` | ADO.NET SQLite provider, `.Core` variant to avoid native-bundle conflicts with SQLite3MC. |
| `SQLite3MC.PCLRaw.bundle` | Actively maintained, MIT-licensed SQLite **Multiple Ciphers** native bundle giving whole-file encryption (SQLCipher-compatible); replaces the deprecated `bundle_e_sqlcipher`. |
| `Microsoft.Extensions.Hosting` | Generic Host: DI, configuration, logging, `BackgroundService` for the scheduler/worker. |
| `Microsoft.AspNetCore.App` (framework ref) | Minimal API + Kestrel HTTPS for the programmatic API. |
| `Polly` (v8) | Resilience pipelines: retry+jitter, circuit breaker, timeout; 429-aware backoff. |
| `Microsoft.Extensions.Http.Polly` / `Microsoft.Extensions.Http.Resilience` | Wire Polly pipelines into the typed `HttpClient` for the Fitbit adapter. |
| `Microsoft.Extensions.Configuration.UserSecrets` | Keeps `client_id`/secret/keys out of source during development. |
| `System.Security.Cryptography.ProtectedData` | DPAPI key-wrapping on Windows (the primary host) for the master key. |
| `Microsoft.Extensions.Options.DataAnnotations` | Fail-fast validation of required config/secrets at startup. |
| `MiniValidation` *(or built-in data annotations)* | Lightweight request validation for minimal-API DTOs. |

*Built-ins used (no package):* `System.Security.Cryptography.AesGcm` (token column AEAD),
`System.Text.Json` (canonical serialization), `System.Threading.Channels` (force-sync queue),
`System.Net.Http` `DelegatingHandler` (rate-limit/bearer handlers), `TimeProvider`/`PeriodicTimer`.

*Optional upgrade:* `NSec.Cryptography` — only if Ed25519 record signing is chosen over HMAC.

### Test-only

| Package | Why |
|---------|-----|
| `xUnit` (+ `xunit.runner.visualstudio`) | Test framework + IDE/CLI runner. |
| `Microsoft.NET.Test.Sdk` | Test host/discovery for `dotnet test`. |
| `FluentAssertions` | Expressive, readable assertions. |
| `NSubstitute` | Simple fakes/mocks for ports. |
| `WireMock.Net` | In-process fake HTTP server for Fitbit fixtures, 429/401, rate-limit headers. |
| `Microsoft.AspNetCore.Mvc.Testing` | `WebApplicationFactory` for in-memory API integration tests. |
| `Microsoft.Extensions.Time.Testing` | `FakeTimeProvider` for deterministic time/backoff/scheduling tests. |
| `coverlet.collector` | Code coverage in CI. |

---

## Appendix A — Cross-cutting conventions

- **Logging:** source-generated `[LoggerMessage]` on `partial` classes; structured params,
  never string-interpolated templates; **secrets/tokens never logged**.
- **Async:** `Async` suffix; `ConfigureAwait(false)` in all `src` library code; `ValueTask`
  on hot synchronous-completion paths.
- **Style/no-comments:** analyzer + `.editorconfig` forbid comments in `src/**`, allow them
  in `tests/**`. One type per file; file-scoped namespaces; `sealed` + `readonly` by default.
- **Determinism:** all time via `IClock`/`TimeProvider`; all randomness (PKCE, GUIDs where
  applicable) via injected CSPRNG seams so tests are repeatable.

## Appendix B — Risks & how the plan mitigates them

| Risk | Mitigation |
|------|------------|
| SQLite3MC `Password` keyword behavior on .NET 10 | **Phase 0 spike test** proves it before anything depends on it. |
| Refresh-token rotation bricking auth (single-use, 409 on concurrency) | Single-flight refresh + atomic transactional persist-before-discard + audit (Phase 4 tests). |
| Fitbit rate-limit headers are approximate | Conservative client-side counter; "most restrictive wins"; pause-until-`Reset` on 429. |
| Fitbit API deprecation Sept 2026 | Provider behind `IHealthDataProvider`; Phase 8 stub proves swap-ability. |
| Key compromise | Defense-in-depth: file encryption + token column AES-GCM + record signing; OS-protected, rotatable keys. |
| Cross-platform key protection (DPAPI Windows-only) | `IKeyProvider` selects DPAPI on Windows, file/KMS fallback elsewhere. |

## Appendix C — Decisions (settled)

All six decisions are now **DECIDED** and drive the build:

1. **Encryption strategy** — **DECIDED: both layers.** Whole-file via SQLite3MC **plus**
   AES-GCM on the OAuth token columns.
2. **Record signing** — **DECIDED: HMAC-SHA256** (symmetric). `IRecordSigner` keeps Ed25519
   as a future drop-in.
3. **Local API auth** — **DECIDED: API key** over loopback HTTPS.
4. **API hosting shape** — **DECIDED: single combined process** — ASP.NET Core minimal API +
   `BackgroundService` worker in one host.
5. **Sync cadence & backfill** — **DECIDED:** incremental sync runs **every 15 minutes**.
   Backfill walks **backwards day-by-day** from the latest known day, running until the
   Fitbit hourly rate limit is hit, then **pausing (~1h)** and resuming the next window until
   all history (within provider caps) is filled — fully resumable via durable
   `SyncCheckpoint`s. **Fresh/incremental sync is always prioritized**; backfill consumes
   only the **remaining** hourly budget (see §5.6).
6. **OAuth bootstrap UX** — **DECIDED: loopback `/oauth/callback`** redirect.

