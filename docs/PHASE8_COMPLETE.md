# PHASE8-COMPLETE — Agent-facing CLI verbs (JSON + exit codes) + cross-platform key protection

> Status: **COMPLETE.** Build 0 warnings (warnings-as-errors), full suite green (**253** tests, up
> from the Phase 7 baseline of 185, +68), all work committed and pushed to `origin/main`. The three
> agent verbs — `sync-once`, `backfill`, `query` — emit a documented JSON envelope with meaningful
> exit codes, and a pure-BCL `PassphraseKeyProtector` slots Linux/container key-file protection into
> the Phase 7 `IKeyProtector` seam. Additive and hexagonal: no inner-layer rewrites, no new NuGet
> packages.

## What Phase 8 delivered

1. **Agent CLI verbs** over the same Application core as `run`:
   - `sync-once` — one incremental `RunOnceAsync` pass; does **not** start the scheduler.
   - `backfill --from --to [--metric]` — **idempotent, gap-aware** historical fill.
   - `query --metric --from --to` / `query --coverage` — read-only sample + coverage views.
   Each writes exactly one JSON envelope to **stdout** (logging routed to stderr in agent mode) and
   returns a meaningful exit code. Secrets/keys/tokens are never printed.

2. **Gap-aware idempotent backfill** (the scope refinement): backfill derives coverage from the dates
   actually present in `metric_samples` (not a checkpoint high-water mark, so **interior gaps** are
   detected), computes the missing dates, and fetches **only** those. A backfill over a fully-covered
   range makes **zero** Fitbit API calls. The result reports `alreadyCovered` / `fetched` /
   `stillMissing` per metric.

3. **Coverage visibility** via `query --coverage` — per-metric held date span (`heldFrom`/`heldTo`/
   `daysHeld`) and interior `gaps`, powered by the **same** coverage engine as backfill.

4. **Cross-platform key protection**: `PassphraseKeyProtector : IKeyProtector` (PBKDF2-HMAC-SHA256,
   600k iterations, random per-blob salt → AES-GCM, versioned self-describing blob) wraps the SAME
   `ProtectedKeyFileCodec` payload DPAPI wraps on Windows. Host selection is OS- and config-driven:
   Windows → DPAPI, non-Windows → passphrase protector (fail-fast if no master secret), in-memory
   stays the dev/test fallback.

## The JSON envelope (the documented contract)

```json
{ "schemaVersion": 1, "command": "sync-once|backfill|query", "ok": true,
  "exitCode": 0, "data": { /* command-specific */ }, "error": null }
```

`ok == (exitCode == 0)`. On usage/config failure `ok=false`, `data` is omitted, and `error` is
`{ "code", "message" }`. **Exit codes:** `0` success (incl. `query` empty results), `1`
usage/config/startup failure, `2` operation failed (`faulted`), `3` rate-limited before completion.

## Chunks (each: build 0 warnings → full suite green → commit → push)

| Chunk | Commit subject |
|-------|----------------|
| 8a | docs: Phase 8 plan (grounded in real code + scope refinement: gap-aware backfill, coverage engine) |
| 8b | `PassphraseKeyProtector` (Security) — production only |
| 8c | `PassphraseKeyProtector` tests (round-trip, tamper, wrong-secret, versioned-format, randomized) |
| 8d | Host protector selection + `KeyProtectorSecretResolver` + `Storage:KeyProtectorSecret/SecretFile` + validator |
| 8e | `CoverageGapCalculator` + `MetricCoverage` (Domain) — production only |
| 8f | `CoverageGapCalculator` tests (empty/full/interior/single-day/boundary/ordering/inverted) |
| 8g | `IMetricRepository.GetCoveredDatesAsync`/`QueryAsync` + impl + 5 Persistence integration tests |
| 8h | `BackfillCommand`/`BackfillResult`/`MetricBackfillReport` + `ISyncEngine.RunBackfillAsync` — production |
| 8i | Backfill tests (incl. zero-API-calls-over-covered-range, interior gap, rate-limit stillMissing) |
| 8j | `AgentResponse`/`AgentError`/`AgentJson`/`AgentExitCode` + `CliVerb`/`CliOptions` + parser flags + tests |
| 8k | `sync-once` shell + agent dispatch + stderr logging in agent mode + envelope tests |
| 8l | `backfill` shell + `AgentArguments` validation + tests |
| 8m | `query` shell (samples + coverage modes) + dispatch |
| 8n | README + OPS_RUNBOOK updates + `PHASE8-COMPLETE` + final verification |

## Test count

253 total (up from 185, +68): **Domain 28 (+11)**, **Security 28 (+10)**, **Application 41 (+6)**,
Providers.Fitbit 35, **Persistence 31 (+5)**, **Host 90 (+36)**.

New tests: `PassphraseKeyProtector` (10), `KeyProtectorSecretResolver` (6) + validator key-file case
(1), `CoverageGapCalculator` (11), metric-repository read path (5), gap-aware backfill (6), agent-verb
parser (11), `AgentResponse`/`AgentOutcome` envelope (6), `AgentArguments` validation (7).

## Key design decisions (and why)

- **Backfill coverage is derived from stored samples, not checkpoints.** `RunBackfillAsync` calls the
  new `IMetricRepository.GetCoveredDatesAsync` (DISTINCT UTC dates present in `metric_samples`) and
  `CoverageGapCalculator.MissingDates`, so a hole in the **middle** of a previously-synced range is
  re-fetched. The checkpoint high-water mark would have masked it. The zero-API-call guarantee for
  covered dates is asserted directly against the recording provider (`provider.FetchedDates` empty).
- **One coverage engine, two consumers.** `CoverageGapCalculator` (pure Domain) is used by backfill
  (gap-only fetch via `MissingDates`) and by `query --coverage` (held span + interior gaps via
  `CoverageOf`). Built once, unit-tested directly across every edge case.
- **Read path filters by metric in SQL, windows by date client-side.** `DateTimeOffset` range
  comparisons (`>=`/`<`) don't translate on the SQLite provider (equality, used by the upsert, does),
  so `MetricRepository` filters `Type == metric` in the database and applies the `DateOnly` window in
  memory. Per-metric row counts in a personal DB are modest; correctness over premature optimization.
- **`query` is the coverage surface, not a 4th verb.** A `--coverage` mode on the read-only `query`
  verb keeps the verb count at three while documenting a crisp shape. Empty results are exit 0 — the
  absence of data is not an error.
- **`PassphraseKeyProtector` reuses the Phase 7 codec untouched.** The protector wraps the exact bytes
  `ProtectedKeyFileCodec` emits, so `DpapiProtectedKeyFileProvider` (already protector-agnostic, and
  already chmod-600 on Unix) composes it with **zero** changes. The versioned blob (magic `FBSP` +
  version + iterations + salt + nonce + tag + ciphertext) is self-describing and fails loudly on
  tamper/wrong-secret/bad-format. Pure BCL (`Rfc2898DeriveBytes.Pbkdf2` + `AesGcm`) — no NuGet, and it
  builds/tests on this Windows host.
- **Agent mode routes logging to stderr.** `FitbitSyncHostFactory.Create(args, agentMode: true)`
  clears providers and sends framework logs to stderr, so the agent verbs emit **exactly one** JSON
  document on stdout. Smoke-verified end-to-end (`sync-once` faulted with no token → exit 2, clean
  JSON stdout; `backfill` from>to → exit 1 usage error; `query` empty → exit 0 count 0).
- **Validation is fail-fast and pure.** `CommandLineParser` strictly parses ISO `yyyy-MM-dd` and
  case-insensitive metric names; `AgentArguments.RequireRange`/`RequireMetric` enforce required flags
  and `from<=to`, throwing `AgentCommandException("usage", …)` which the host renders as an exit-1 JSON
  error. All directly unit-tested; the command shells stay thin/untested (they serialize already-tested
  Application/Persistence results), matching the Phase 7 verify/rotate precedent.

## Cross-platform key protection — operator notes

- Set `Storage:KeyFilePath` to use a wrapped key file (replaces the base64 column/signing keys).
  Windows wraps with DPAPI (`CurrentUser`, no secret needed); non-Windows wraps with the passphrase
  protector and **requires** `Storage:KeyProtectorSecret` (env) or `Storage:KeyProtectorSecretFile`
  (mounted file). Missing master secret on non-Windows → fail-fast at startup.
- The key file is created owner-only (chmod 600 on Unix). Back it up with its master secret; losing
  either makes the column/signing material unrecoverable (OPS_RUNBOOK §4/§5).

## Scope — what stayed OUT (deferred, unchanged)

Per the plan's out-of-scope list, all still deferred and able to wrap the SAME Application core later:
**HTTP API** (endpoints, API-key middleware, `ProblemDetails`, HTTPS/loopback, `WebApplicationFactory`
tests, security headers, `PostSync_RequiresApiKey_AndEnqueuesForceSync`), **IPC/named-pipe**,
**MCP server**, **stdio command-loop daemon** (the Phase 8 verbs are one-shot), **Windows service /
systemd unit** packaging, and the **Google Health provider stub**. We delivered CLI + JSON only.

## Environment notes / gotchas for future sessions

- **Build/test loop:** `dotnet build FitbitSync.slnx -c Debug` (0 warnings) and
  `dotnet test FitbitSync.slnx -c Debug`. Run an agent verb:
  `dotnet run --project src/FitbitSync.Host -- <sync-once|backfill ...|query ...>`.
- **Parse agent output:** redirect stdout to a file (`1> out.json`); logs are on stderr. The envelope
  is `{ schemaVersion, command, ok, exitCode, data, error }`.
- **Backfill is safe to re-run.** After a rate-limit (exit 3) re-run the same range to finish the
  `stillMissing` dates; already-held dates cost no API calls.
- **Linux key file** needs a master secret (`Storage:KeyProtectorSecret`/`SecretFile`); PBKDF2 is
  600k iterations (`PassphraseKeyProtector.Iterations`) — increase as hardware improves; the iteration
  count is stored in the blob so old files still open.
