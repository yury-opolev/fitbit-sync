# PHASE 8 PLAN — Agent-facing CLI verbs (JSON + exit codes) + cross-platform key protection

> Status: **PLANNED.** Builds on PHASE7-COMPLETE (185 tests, build 0 warnings). Additive and
> hexagonal only — no inner-layer rewrites, no new runtime NuGet packages. The Application core
> stays the single source of truth so a future HTTP/IPC/MCP surface can wrap the SAME use-cases.

## 1. Goal

Two capabilities, grounded in the real repo (not the aspirational plan):

1. **Agent-facing CLI verbs** — `sync-once`, `backfill`, `query` — that emit a single documented
   **JSON envelope** on stdout and return **meaningful exit codes**, so an external agent/script can
   drive Fitbit-sync and parse results deterministically. Never print secrets/keys/tokens.
2. **Cross-platform (Linux) key protection** — a `PassphraseKeyProtector : IKeyProtector` using
   **pure BCL** crypto (PBKDF2 → AES-GCM) that slots into the existing Phase 7 seam
   (`DpapiProtectedKeyFileProvider` already composes any `IKeyProtector`), so the encrypted key
   file works on Linux/containers, not just Windows DPAPI.

**Scope refinement folded in (user, this session):** backfill must be **idempotent / gap-aware** —
re-running over already-held dates makes **zero** Fitbit API calls. Coverage/gap detection is built
**once** as a reusable, pure, unit-tested unit that BOTH gap-only backfill AND a coverage view
consume. Truthful coverage is derived from **stored `metric_samples` dates** (so an interior gap is
detected), not a checkpoint high-water mark.

## 2. Grounding — what already exists (confirmed by reading the code)

- **Host CLI** (`src/FitbitSync.Host`): `Program.Main` → `CommandLineParser.Parse(args)` returns a
  `ParsedCliCommand(CliVerb, Error?)` from the **first token only** (no flags today) →
  `RunHostCommandAsync(args, ExecuteAsync)` builds the Generic Host via `FitbitSyncHostFactory`,
  catches `InvalidOperationException`/`ArgumentException` as a clean startup-failure (exit 1). Each
  command (`VerifyCommand`, `RotateKeysCommand`, `RunCommand`) creates a DI scope, calls
  `ISchemaInitializer.Initialize()`, does its work, prints, returns an exit code. `verify` already
  uses exit codes 0/2/1.
- **Sync entry points** (`src/FitbitSync.Application`): `ISyncEngine.RunOnceAsync()` and
  `RunForceSyncAsync(ForceSyncCommand)`. `SyncEngine.BuildForceSyncPlan` already walks a `DateRange`
  **day-by-day**, but it **always fetches every day** (`SyncWorkKind.Incremental`, no gap-skip) and
  has no coverage reporting. `SyncRunResult(RunId, ItemsPlanned, ItemsCompleted, SamplesWritten,
  Outcome)`; `SyncRunOutcome { Completed, RateLimited, Cancelled, Faulted }`.
- **Read path** (`FitbitSync.Domain.IMetricRepository`): only `UpsertAsync` + `GetCheckpointAsync`.
  **No query-by-range, no coverage read** exists yet — both are new, additive port methods.
  `metric_samples` rows (`MetricSampleRow`: `Type`, `Timestamp`, `Value`, `Unit`, `Resolution`,
  `Source`) are the truthful "what we actually hold". `IntradayResolution.Daily` is the day grain.
- **Key seam** (Phase 7, `FitbitSync.Security`): `IKeyProtector { Protect, Unprotect }`;
  `DpapiProtectedKeyFileProvider(keyFilePath, IKeyProtector)` is **already protector-agnostic** —
  load-or-create a `ProtectedKeyFileCodec` payload wrapped by the injected protector, and it already
  `File.SetUnixFileMode(UserRead|UserWrite)` (chmod 600) on non-Windows. Host selection lives in
  `HostServiceCollectionExtensions.BuildKeyProvider`: Windows + `Storage:KeyFilePath` → DPAPI
  provider; else `InMemoryKeyProvider` from base64 keys.
- **Style/build gates:** `Directory.Build.props` sets `TreatWarningsAsErrors` + analyzers. One type
  per file, file-scoped namespaces, `this.`-qualified, braces always, `ConfigureAwait(false)` in
  src. Tests: xUnit + FluentAssertions + NSubstitute (and real encrypted SQLite fixtures in
  Persistence). Host thin shells carry explanatory comments and stay untested; testable logic lives
  in Application/Persistence/Domain/Security and in `CommandLineParser`.

## 3. The JSON envelope (documented contract)

One consistent shape for ALL agent verbs, serialized with `System.Text.Json`
(`JsonSerializerOptions { PropertyNamingPolicy = camelCase, WriteIndented = true }`, enums as camelCase
strings). Exactly one JSON document is written to **stdout**; nothing else pollutes stdout.

```json
{
  "schemaVersion": 1,
  "command": "sync-once | backfill | query",
  "ok": true,
  "exitCode": 0,
  "data": { /* command-specific payload, null on error */ },
  "error": null
}
```

On failure `ok=false`, `data=null`, and `error` is `{ "code": "<stable-token>", "message": "<human text>" }`.
`schemaVersion` lets agents pin the contract. The envelope NEVER contains secrets, keys, tokens,
passphrases, or file contents — only counts, dates, metric types, outcomes, and ids.

**Exit codes (meaningful, documented):**

| Code | Meaning |
|------|---------|
| 0 | Success. **`query` with empty results is still 0** (absence of data is not an error). |
| 1 | Startup/config/usage failure (bad flags, missing config, validation) — fail fast, JSON error. |
| 2 | Operation ran but the outcome is a real failure (sync `Faulted`). |
| 3 | Rate-limited before completion (sync/backfill `RateLimited`) — partial progress reported in `data`. |

`exitCode` is echoed inside the envelope AND returned as the process exit code.

### Per-command `data` payloads

- **`sync-once`** (does NOT start the scheduler; one `RunOnceAsync` pass):
  `{ runId, itemsPlanned, itemsCompleted, samplesWritten, outcome }`.
- **`backfill`** (gap-aware): validates `--from`/`--to` (parseable ISO `yyyy-MM-dd`, `from<=to`) and
  optional `--metric`. Reports the coverage delta:
  ```json
  { "runId": "...", "requestedRange": { "from": "2024-01-01", "to": "2024-01-31" },
    "metrics": [
      { "metric": "heartRate",
        "alreadyCovered": { "count": 20, "dates": ["..."] },
        "fetched":        { "count": 8,  "dates": ["..."] },
        "stillMissing":   { "count": 3,  "dates": ["..."] },
        "samplesWritten": 8 } ],
    "outcome": "completed | rateLimited | faulted" }
  ```
  Covered dates cost **zero** provider calls. `stillMissing` is non-empty only if the run stopped
  early (rate-limit/fault) before filling every gap.
- **`query`** has two modes (read-only, never triggers sync):
  - `--metric X --from D --to D` → `{ "metric": "...", "range": {...}, "count": N, "samples": [ { timestamp, value, unit, resolution } ] }`. Empty → `count:0, samples:[]`, exit 0.
  - `--coverage` (optionally `--metric`/range) → per-metric coverage view:
    `{ "coverage": [ { metric, heldFrom, heldTo, daysHeld, gapCount, gaps: ["..."] } ] }`.

`query` is the chosen surface for **coverage visibility** (mode flag `--coverage`), avoiding a 4th
verb while keeping the read-only contract crisp.

## 4. Coverage / gap engine (built once, shared)

Pure logic in **Domain** so both Application (backfill) and the read path can use it with no
infra dependency.

- **`CoverageGapCalculator`** (`FitbitSync.Domain`, static, pure): given a set of present
  `DateOnly`s and a `DateRange`, returns:
  - `MissingDates(present, range)` → ordered `IReadOnlyList<DateOnly>` of dates in range NOT present.
  - `CoverageOf(present, range)` → a `MetricCoverage` value object (`HeldFrom?`, `HeldTo?`,
    `DaysHeld`, `Gaps`). Inverted range is rejected upstream by `DateRange`'s own guard (start<=end),
    so the calculator trusts its `DateRange` input.
- **`MetricCoverage`** value object (`FitbitSync.Domain`) — the shape the coverage view serializes
  from.
- **Read-path port additions** (`IMetricRepository`, additive):
  - `Task<IReadOnlyList<DateOnly>> GetCoveredDatesAsync(MetricType, DateRange, ct)` — DISTINCT UTC
    dates present in `metric_samples` for that metric within range (truthful coverage source).
  - `Task<IReadOnlyList<MetricSample>> QueryAsync(MetricType, DateRange, ct)` — ordered samples for
    `query` sample mode.
  Implemented in `MetricRepository` via EF over `MetricSamples` (project `Timestamp.UtcDateTime.Date`
  → `DateOnly`, `Distinct`, `OrderBy`). Backfill computes gaps from `GetCoveredDatesAsync`, NOT from
  the checkpoint, so an **interior** gap is fetched.

## 5. Gap-aware backfill (Application)

New additive method on `ISyncEngine`: `Task<BackfillResult> RunBackfillAsync(BackfillCommand, ct)`.

- `BackfillCommand(Guid RunId, IReadOnlyList<MetricType>? Metrics, DateRange Range)`.
- For each target metric: `covered = repo.GetCoveredDatesAsync(metric, range)`; `missing =
  CoverageGapCalculator.MissingDates(covered, range)`. If `missing` is empty → **no provider call**
  for that metric (this is the asserted zero-call behavior). Otherwise fetch ONLY missing dates
  (reusing the existing resilience pipeline + rate gate + `UpsertAsync` + checkpoint advance with
  `SyncWorkKind.Backfill`). Stop early on rate-limit/fault, recording `stillMissing`.
- `BackfillResult(RunId, IReadOnlyList<MetricBackfillReport>, SyncRunOutcome)` where
  `MetricBackfillReport(Metric, AlreadyCovered, Fetched, StillMissing, SamplesWritten)` — the host
  maps this straight into the envelope `data`. Result types live in Application; the host shell just
  serializes.

This is genuinely new behavior (the existing force-sync path fetches unconditionally), so it earns
its own tests including the **zero-API-call-on-covered-range** assertion against the NSubstitute /
recording provider.

## 6. Linux crypto design (`PassphraseKeyProtector : IKeyProtector`, Security)

Pure BCL, no NuGet. Wraps the SAME `ProtectedKeyFileCodec` payload the Phase 7 codec emits.

- **Master secret** supplied by the container/operator: from `Storage:KeyProtectorSecret` (env/User
  Secrets) and/or a mounted secret-file path `Storage:KeyProtectorSecretFile` (file contents read,
  trimmed). Fail-fast with a clear message if Linux key-file protection is configured but no master
  secret is supplied.
- **KDF:** `Rfc2898DeriveBytes` (PBKDF2-HMAC-SHA256), **random per-file 16-byte salt**, high
  iteration count (≥ 600_000, a const), derive a 32-byte wrapping key.
- **AEAD:** `AesGcm` with a **random 12-byte nonce**, 16-byte tag, to wrap the codec payload.
- **Versioned blob layout** (self-describing, like the codec): `magic("FBSP") ‖ version(1) ‖
  iterations(int32 BE) ‖ salt(16) ‖ nonce(12) ‖ tag(16) ‖ ciphertext`. `Unprotect` re-derives the
  key from the stored salt+iterations and AES-GCM-decrypts; a flipped byte fails the GCM tag;
  wrong secret derives a wrong key → tag failure. `chmod 600` on the key file is already handled by
  `DpapiProtectedKeyFileProvider.RestrictToOwner` on Unix.
- **Tests (Security, no OS dependency):** wrap/unwrap round-trip; flipped-byte tamper throws;
  wrong-secret rejection; versioned-format (bad magic / unsupported version / truncated) rejection;
  salt/nonce randomized per call (two protects of same input differ).

### Host protector selection (config-driven, fail-fast)

`BuildKeyProvider` extended: when `Storage:KeyFilePath` is set, pick the protector by OS —
Windows → `DpapiKeyProtector` (unchanged); Linux/Unix → `PassphraseKeyProtector` built from the
master secret (throw a named `InvalidOperationException` if absent). `InMemoryKeyProvider` stays the
dev/test fallback when no key file is configured. `HostConfigurationValidator.ValidateStorage`
extended to require the master secret when a key file is configured on a non-Windows OS.

## 7. Chunk breakdown (each: build 0 warnings → FULL suite green, count only ↑ from 185 → commit → push)

Production and tests split across turns for logic-heavy chunks, with a build checkpoint between.

| Chunk | Content |
|-------|---------|
| 8a | **This plan doc** — commit first, before any production code. |
| 8b | `PassphraseKeyProtector` (Security) — production file only. Build checkpoint. |
| 8c | `PassphraseKeyProtector` tests (round-trip, tamper, wrong-secret, versioned-format, randomized). Suite green. |
| 8d | Host protector selection + `Storage:KeyProtectorSecret`/`SecretFile` options + validator extension + tests. |
| 8e | `CoverageGapCalculator` + `MetricCoverage` (Domain) — production. Build checkpoint. |
| 8f | `CoverageGapCalculator` tests (empty/full/interior gap/single-day/from==to/ordering). Suite green. |
| 8g | `IMetricRepository` read methods (`GetCoveredDatesAsync`, `QueryAsync`) + `MetricRepository` impl + Persistence integration tests (incl. interior-gap dates, ordering, empty). |
| 8h | `BackfillCommand` + `BackfillResult` + `MetricBackfillReport` + `ISyncEngine.RunBackfillAsync` (Application) — production. Build checkpoint. |
| 8i | Backfill tests: gap-only fetch, **zero provider calls over fully-covered range**, interior gap fetched, rate-limit stop records stillMissing. Suite green. |
| 8j | `AgentResponse` envelope + `AgentError` (Host) + `System.Text.Json` options + `CliVerb` additions + `CommandLineParser` flag parsing (`--from`/`--to`/`--metric`/`--coverage`) + parser tests. |
| 8k | `sync-once` command shell + Program dispatch + smoke. Suite green. |
| 8l | `backfill` command shell (validate flags, map `BackfillResult` → envelope) + dispatch + parser/validation tests. Suite green. |
| 8m | `query` command shell (samples + coverage modes) + dispatch + tests. Suite green. |
| 8n | README + OPS_RUNBOOK updates (3 verbs, JSON envelope, exit codes, Linux key protection + master-secret config) + `PHASE8_COMPLETE.md` + final full-suite verification. |

## 8. Test strategy

- **Pure units** (Domain `CoverageGapCalculator`; Security `PassphraseKeyProtector`): direct xUnit +
  FluentAssertions, no infra. Named edge cases per the refinement: empty store (all missing), full
  coverage (no-op), interior gap, single-day, boundary `from==to`, ordering; crypto tamper/wrong-
  secret/versioned-format.
- **Application backfill:** reuse `RecordingHealthDataProvider`/`InMemorySyncDoubles`; assert
  `provider.FetchedDates` is **empty** when the recording repo already holds the range, and equals
  exactly the gap dates otherwise. Rate-limit stop path asserts `stillMissing`.
- **Persistence read path:** real encrypted SQLite via `EncryptedDatabaseFixture`; seed samples,
  assert covered dates / query results / interior gaps.
- **Host:** `CommandLineParser` flag parsing is the testable core (verb + flags → `ParsedCliCommand`
  with typed options or error). Command shells stay thin/untested (they serialize already-tested
  Application/Persistence results), matching the Phase 7 verify/rotate precedent.
- Hard gate after every chunk: `dotnet build FitbitSync.slnx -c Debug` (0 warnings) +
  `dotnet test FitbitSync.slnx -c Debug` (total only increases from 185).

## 9. Explicitly OUT OF SCOPE (deferred future work)

These stay deferred and are noted so a later phase can wrap the SAME Application core:

- **HTTP API** (force-sync/status/metrics/audit/health endpoints, API-key middleware,
  `ProblemDetails`, HTTPS/loopback binding, `WebApplicationFactory` tests, HTTP security headers,
  the `PostSync_RequiresApiKey_AndEnqueuesForceSync` test).
- **IPC / named-pipe / socket** control channel.
- **MCP server** surface.
- **stdio command-loop daemon** (long-running interactive verb loop) — Phase 8 verbs are one-shot.
- **Windows service / systemd unit** packaging.
- **Google Health provider stub** (the original aspirational Phase 8) — unaffected; the
  `IHealthDataProvider` seam is untouched.

We deliver **CLI + JSON only**, kept hexagonal so the above can layer on without touching
Domain/Application/Persistence.
