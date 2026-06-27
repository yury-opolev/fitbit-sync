# Phase 5 — Sync engine + resilience (build plan)

> Status: **in progress.** Grounded in `docs/IMPLEMENTATION_PLAN.md` §5 (sync engine),
> §5.6 (shared budget), §8.2 (resumability test), §9 (Phase 5 scope) and the *actual*
> Phase 0–4 codebase (which simplified several Domain shapes vs. the original aspirational
> model — this plan follows the code that exists, not the prose).

## 1. Authoritative scope (from IMPLEMENTATION_PLAN §9, Phase 5)

> First red test: `SyncEngine_ResumesFromCheckpoint_AfterSimulatedCrash`.
> `SyncPlanner`, per-metric jobs, idempotent transactional sync unit, `IRateLimitGate`, Polly
> retry/backoff/circuit-breaker/timeout, 429 pause-until-reset, scheduler `BackgroundService`.
> Resumability + rate-limit tests.

Plus the explicitly deferred item from Phase 3 (carried into Phase 5 by the task brief):
**the Fitbit Sleep mapper** (Phase 3 shipped every metric *except* Sleep; capabilities and
the endpoint catalog currently assert Sleep is unsupported).

Phase 6 (NOT now) builds the Api host, the loopback OAuth listener and the CLI. Phase 5
therefore stops at the Application layer: a `BackgroundService` scheduler type is created and
unit-tested in isolation, but it is **not** hosted by a composition root here.

## 2. Grounding facts discovered in the codebase

| Area | Reality in code (Phase 0–4) |
|------|------------------------------|
| Application project | **Does not exist yet.** Phase 5 creates `src/FitbitSync.Application` + `tests/FitbitSync.Application.Tests`. |
| `MetricSample` | Flat record `(MetricType Type, DateTimeOffset Timestamp, double Value, string Unit, IntradayResolution Resolution, string Source)`. |
| `SyncCheckpoint` | `(MetricType Metric, DateTimeOffset? NewestSynced, DateTimeOffset? OldestBackfilled)` with `AdvanceForward(to)` / `ExtendBackfill(to)`. |
| Checkpoint persistence | `ISyncCheckpointStore.GetAsync/SaveAsync` (Persistence: `SyncCheckpointStore`, upsert + RowVersion). |
| Metric persistence | `IMetricRepository.UpsertAsync` (signs + idempotent upsert keyed on Source+Type+Resolution+Timestamp). |
| Audit | `IAuditTrail.AppendAsync(string action)` / `VerifyChainAsync()`. Existing PascalCase actions: `TokenRefresh`, `AuthGrant`. |
| Provider port | `IHealthDataProvider.FetchAsync(MetricFetchRequest, ct)` → `MetricFetchResult(IReadOnlyList<MetricSample> Samples, RateLimitSnapshot? RateLimit)`. Loops the date range internally, one HTTP GET per day. |
| Rate-limit signalling | Provider returns `RateLimitSnapshot?` on success; **throws** `FitbitRateLimitedException` (carries snapshot) on 429 and `FitbitAuthenticationException` on terminal 401. |
| `RateLimitSnapshot` | `(int Remaining, int Limit, int ResetSeconds, DateTimeOffset ObservedAt)` with `IsExhausted`, `ResetsAt`. |
| Capabilities | `MetricCapability(MetricType Metric, IntradayResolution Resolution)` — **no** per-metric backfill window (the original plan's HR-1y/Sleep-100d data is not modelled). |
| House style | one-type-per-file, file-scoped ns, `this.` access, `ArgumentNullException.ThrowIfNull`, `ct = default` + `ConfigureAwait(false)`, System usings first then project, sealed+readonly, source-gen `[LoggerMessage]`. Provider/Persistence expose internals to tests via `InternalsVisibleTo`. |
| Baseline | Build 0 warnings; **77** tests green (Domain 14, Security 10, Providers.Fitbit 31, Persistence 22). |

## 3. Design decisions (and deviations from the aspirational plan)

1. **`IRateLimitGate` lives in `FitbitSync.Application`, not Domain.** The original plan listed
   it as a Domain port, but it was never created in Phases 0–4 and it is consumed *only* by the
   Application engine. Putting it in Application keeps Domain free of orchestration concerns and
   avoids speculative public surface. Implementation: `TokenBucketRateLimitGate` (singleton, clock-driven).
2. **Provider-neutral exceptions in Domain.** The engine must catch "rate limited" / "auth failed"
   without referencing `FitbitSync.Providers.Fitbit` (clean architecture: Application → Domain only).
   Add `ProviderRateLimitedException(RateLimitSnapshot?)` and `ProviderAuthenticationException(string)`
   to Domain; make the existing `FitbitRateLimitedException` / `FitbitAuthenticationException` derive
   from them. This is backward-compatible — existing provider tests that catch the Fitbit types and
   read `.RateLimit` still pass (the property moves to the base).
3. **Uniform backfill floor via `SyncOptions`, not per-metric capability windows.** Because
   `MetricCapability` carries no window, the planner clamps backfill to `today − SyncOptions.BackfillWindow`
   (default 30 days). Per-metric caps can be reintroduced later by enriching `MetricCapability`; out of scope now.
4. **Work granularity = one day per `SyncWorkItem`.** The provider already fetches per-day; emitting
   one item per day lets the engine consume rate budget precisely and checkpoint after every day
   (the foundation of resumability). Freshness-first ordering: the plan lists *all* Incremental items
   before *any* Backfill items, so backfill only runs on leftover budget (§5.6).
5. **Sleep maps to a scalar `MetricSample`.** The flat Domain model has no sleep entity; `MetricType.Sleep`
   already exists. The canonical scalar is **`minutesAsleep`** of the *main* sleep log
   (`isMainSleep == true`), Unit `"minutes"`, `Daily` resolution, timestamp = `dateOfSleep` at midnight UTC.
   Endpoint `sleep/date/{day}.json` (prefix-less, matching every existing sibling endpoint).
6. **New audit actions (PascalCase, matching `TokenRefresh`/`AuthGrant`):** `SyncStarted`,
   `SyncCompleted`, `RateLimited`, `ForceSync`. `SyncFailed` on an unexpected provider error.
7. **Resilience = Polly v8 `ResiliencePipeline` (Polly.Core).** A small factory wraps each provider
   call with bounded exponential-backoff-plus-jitter retry for *transient* faults (network/5xx/timeout).
   429 and 401 are **excluded** from retry — 429 is handled by the gate/pause logic, 401 by the token
   layer. Base delay is configurable so tests run with zero delay (no `FakeTimeProvider` gymnastics).
8. **Crash = cooperative cancellation.** The engine checkpoints after each completed day and honours the
   `CancellationToken` between items. A "crash" mid-run is simulated by cancelling the token; completed
   days are durable, and the next `RunOnceAsync` re-plans from the persisted checkpoints. Unexpected
   provider exceptions are audited (`SyncFailed`) and stop the run gracefully (the worker never dies);
   `ProviderRateLimitedException` pauses; both leave checkpoints intact for resumption.

## 4. Chunk breakdown (each: red test → green → build 0 warnings → full suite green → commit → push)

Small chunks (ideally one production file + its test) to respect the 8192-token output cap.

- **5a — Sleep DTOs + mapper.** `FitbitSleepResponse`, `FitbitSleepLog` DTOs + `SleepMapper`
  (main-sleep `minutesAsleep` → canonical sample) + `SleepMapperTests`. *(provider-internal; no wiring yet.)*
- **5b — Wire Sleep into the provider.** Endpoint catalog (`sleep/date/{day}.json`), capability,
  `FitbitHealthDataProvider` switch case. Update the two existing tests that assert Sleep is absent
  (`FitbitProviderCapabilitiesTests` → contains Sleep; `EndpointCatalogTests` → use a genuinely
  unsupported pair, e.g. SpO2 @ OneSecond, for the throws case). Add a provider fetch test for Sleep.
- **5c — Domain exceptions.** Add `ProviderRateLimitedException` + `ProviderAuthenticationException`
  to Domain; derive the Fitbit exceptions from them (move `RateLimit` to the base). Full provider
  suite must stay green.
- **5d — Application project + rate gate.** Create `FitbitSync.Application` (+ test project, slnx,
  `InternalsVisibleTo`). `IRateLimitGate` + `TokenBucketRateLimitGate` (consume / apply-snapshot /
  on-rate-limited / refill-after-reset, most-restrictive-wins) + `SyncOptions` + gate tests.
- **5e — Sync planner.** `SyncWorkItem`, `SyncWorkKind`, `SyncPlanner` (freshness-first incremental
  catch-up, then backward backfill clamped to the floor) + planner tests (pure, no I/O).
- **5f — Resilience pipeline.** `SyncResiliencePipelineFactory` (bounded retry + jitter, transient
  only) + tests (zero-delay: asserts retry count and that 429/auth are not retried).
- **5g — Sync engine + resumability (NAMED red test).** `ISyncEngine`, `SyncRunResult`, `SyncEngine`
  with `RunOnceAsync`. First red test: `SyncEngine_ResumesFromCheckpoint_AfterSimulatedCrash`
  (fake provider records dates; cancel mid-run; second run resumes from checkpoint, no duplicate
  fetches, audit chain valid).
- **5h — 429 pause/resume.** Engine handling of `ProviderRateLimitedException`: update gate, audit
  `RateLimited`, stop run; next run after reset resumes. Rate-gate-driven budget-exhaustion stop.
  Tests with a fake clock/`FakeTimeProvider`.
- **5i — Force-sync queue.** `ForceSyncCommand`, `SyncRunResult` run id, `IForceSyncQueue` +
  `ForceSyncQueue` (over `System.Threading.Channels`), `SyncEngine.RunForceSyncAsync` (targeted plan)
  + tests.
- **5j — Scheduler.** `ISyncCycleRunner` + `SyncCycleRunner` (drain force-sync queue → run scheduled),
  `SyncScheduler : BackgroundService` (`PeriodicTimer` on injected `TimeProvider`, 15-min cadence,
  catch/log per cycle) + tests (FakeTimeProvider advances → cycle runs; cycle runner ordering).
- **5k — DI.** `AddApplication` extension (gate/planner/engine/queue/cycle-runner/scheduler/options) +
  DI smoke test (ports faked, `ISyncEngine` resolves).
- **5l — Close-out.** `docs/PHASE5_COMPLETE.md` summary; final build 0 warnings + full suite green + push.

## 5. Test strategy

- **Frameworks:** xUnit + FluentAssertions + NSubstitute (ports), matching existing tests. No live
  network in Application tests — providers are faked at the `IHealthDataProvider` boundary (not HTTP).
  WireMock stays in the provider tests only.
- **Determinism:** all time via `IClock` / injected `TimeProvider`; resilience tests use zero base
  delay; scheduler tests advance a hand-controlled `TestClock`/`ManualTimeProvider` (note:
  `Microsoft.Extensions.Time.Testing` is **not available in this offline feed**, so Application tests
  use the repo's own `IClock` test double instead of `FakeTimeProvider`).
- **Doubles:** in-memory fakes for `IMetricRepository`, `ISyncCheckpointStore`, `IAuditTrail`,
  `IHealthDataProvider`, plus a real `TokenBucketRateLimitGate` where behaviour matters.
- **Resumability proof (named test):** fetched-date recording + cancel-mid-run + second run asserts
  continuation, no duplicates, and `VerifyChainAsync()` true.
- **Hard gate after every chunk:** `dotnet build` 0 warnings, **full** suite green (count only rises
  from 77), commit, push.

## 6. Risks / watch-list

- Editing the two "Sleep is unsupported" tests is intentional (delivering the deferred feature), not a
  regression — called out explicitly in 5b's commit.
- `BackgroundService` timing tests can be flaky; mitigated by injecting `TimeProvider` and asserting via
  a faked cycle runner rather than wall-clock waits.
- Polly v8 API surface (`Polly.Core`): use `ResiliencePipelineBuilder` with `AddRetry`; keep the predicate
  narrow (transient only) so 429/auth bypass retry.
