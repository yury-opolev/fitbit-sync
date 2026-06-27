# PHASE5-COMPLETE — Sync engine + resilience

> Status: **COMPLETE.** Build 0 warnings, full suite green (**119** tests, up from the
> Phase 4 baseline of 77), all work committed and pushed to `origin/main`.

## What Phase 5 delivered

Phase 5 built the **`FitbitSync.Application`** layer — the provider-agnostic sync engine and
its resilience machinery — plus the Fitbit **Sleep** mapper that Phase 3 had deferred. It
implements IMPLEMENTATION_PLAN §5 (scheduling, resumable/incremental sync, rate limits,
retry, force-sync, shared budget) and the §9 Phase 5 scope. The named first red test,
`SyncEngine_ResumesFromCheckpoint_AfterSimulatedCrash`, is green.

Phase 6 (Api host, loopback OAuth listener, CLI) is intentionally **not** started; the
`SyncScheduler` `BackgroundService` is implemented and unit-tested in isolation, ready for a
composition root to host.

## Chunks (each: red test → green → build 0 warnings → full suite green → commit → push)

| Chunk | Commit | Summary |
|-------|--------|---------|
| 5a | `fdf5a51` | Fitbit Sleep DTOs (`FitbitSleepResponse`/`FitbitSleepLog`) + `SleepMapper` (main-sleep `minutesAsleep` → canonical sample; naps excluded) + mapper tests. |
| 5b | `1034493` | Wired Sleep into the endpoint catalog (`sleep/date/{day}.json`), capabilities, and provider fetch; updated the two tests that asserted Sleep was unsupported; added a Sleep fetch test. |
| 5c | `c3e0494` | Domain provider-neutral exceptions `ProviderRateLimitedException`/`ProviderAuthenticationException`; Fitbit exceptions now derive from them (snapshot moved to base) so the engine catches them without a provider dependency. |
| 5d | `c36ba23` | New `FitbitSync.Application` project; `IRateLimitGate` + `TokenBucketRateLimitGate` (conservative local counter, most-restrictive-wins, 429 pause-until-reset, auto-refill) + `SyncOptions`. |
| 5e | `d42adc5` | `SyncPlanner` — freshness-first incremental catch-up then backward backfill clamped to a floor — + `SyncWorkItem`/`SyncWorkKind`. |
| 5f | `40da310` | `SyncResiliencePipelineProvider` — Polly v8 bounded retry + jitter for transient faults only; 429/auth bypass retry. |
| 5g | `fd517e8` | `SyncEngine.RunOnceAsync` (plan → gate → resilient fetch → upsert → checkpoint-per-day → audit) + **named red test** `SyncEngine_ResumesFromCheckpoint_AfterSimulatedCrash` + in-memory port doubles. |
| 5h | `6bea5fe` | Engine 429 pause-until-reset + `RateLimited` audit, budget-exhaustion stop, auto-resume-after-window tests. |
| 5i | `cdf62cb` | `ForceSyncCommand` + `ForceSyncQueue` (unbounded `Channel`) + `SyncEngine.RunForceSyncAsync` (targeted plan, shared gate/checkpoints, `ForceSync` audit). |
| 5j | `60c9665` | `SyncCycleRunner` (drain force-sync then one scheduled pass) + `SyncScheduler : BackgroundService` (`PeriodicTimer` on injected `TimeProvider`, swallow+log per-cycle faults). |
| 5k | `e5fb170` | `AddSyncEngine` DI extension (gate/planner/resilience/queue/cycle-runner/scheduler singletons, scoped engine, hosted scheduler); `SyncCycleRunner` resolves a scoped engine per cycle; DI smoke tests. |

## Key design decisions (and deviations from the original aspirational plan)

- **`IRateLimitGate` lives in Application, not Domain.** It is consumed only by the engine;
  keeping it out of Domain avoids speculative public surface (the original plan listed it as a
  Domain port but it was never created).
- **Provider-neutral exceptions in Domain.** Clean architecture: Application → Domain only.
  The engine catches `ProviderRateLimitedException`/`ProviderAuthenticationException`; the
  Fitbit adapter's exceptions derive from them. Existing provider tests still pass unchanged.
- **Uniform backfill floor via `SyncOptions.BackfillWindow`** (default 30 days). The simplified
  `MetricCapability` carries no per-metric window, so the planner clamps uniformly; per-metric
  caps can be reintroduced later by enriching `MetricCapability`.
- **One day per `SyncWorkItem`** — matches the provider's per-day fetch and enables a durable
  checkpoint after every completed day (the basis of resumability).
- **Crash = cooperative cancellation.** Checkpoints persist per day ignoring the token, so a
  cancelled run loses no completed work; the next run re-plans from the persisted cursors.
  Unexpected provider errors are audited (`SyncFailed`) and stop the run gracefully.
- **Sleep → scalar sample.** The flat Domain model has no sleep entity; the canonical scalar is
  the main sleep log's `minutesAsleep` (unit `minutes`, `Daily` resolution, midnight-UTC).
- **New audit actions** (PascalCase, matching `TokenRefresh`/`AuthGrant`): `SyncStarted`,
  `SyncCompleted`, `RateLimited`, `ForceSync`, `SyncFailed`.

## Environment notes / gotchas for future sessions

- **Offline NuGet feed:** `Microsoft.Extensions.Time.Testing` (`FakeTimeProvider`) is **not
  resolvable** in this environment. Application tests use the repo's own `IClock` test double
  (`TestClock`) and a hand-rolled `ManualTimeProvider` (captures `PeriodicTimer`'s timer and
  fires ticks on demand) instead — no real waiting.
- **EF + BackgroundService:** the scheduler is a singleton but the engine depends on scoped EF
  stores, so `SyncCycleRunner` opens a DI scope per cycle and resolves `ISyncEngine` from it.
- **Build/test loop:** `dotnet build FitbitSync.slnx -c Debug` (0 warnings, warnings-as-errors)
  and `dotnet test FitbitSync.slnx -c Debug`. Final count: 119 (Domain 17, Security 10,
  Application 35, Persistence 22, Providers.Fitbit 35).

## What remains for later phases

- **Phase 6:** Api minimal-API host (force-sync/status/metrics/audit/health endpoints), HTTPS +
  loopback, API-key auth, loopback OAuth callback, CLI verbs, composition root that calls
  `AddPersistence()` + `AddFitbitProvider()` + `AddSyncEngine()` and hosts `SyncScheduler`.
- **Phase 7:** chain/signature verification CLI verbs, key rotation, config validators, docs.
