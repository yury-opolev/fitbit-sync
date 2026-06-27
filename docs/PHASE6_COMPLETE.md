# PHASE6-COMPLETE — Composition root: runnable host + loopback OAuth + CLI

> Status: **COMPLETE.** Build 0 warnings (warnings-as-errors), full suite green
> (**154** tests, up from the Phase 5 baseline of 119), all work committed and pushed to
> `origin/main`. The `fitbitsync` console executable runs end-to-end (`help`/`login`/`run`).

## What Phase 6 delivered

Phase 6 built the **`FitbitSync.Host`** project — the composition root that makes the system
runnable for the first time. Phase 5 ended with a fully unit-tested sync engine and
`SyncScheduler` `BackgroundService` but **nothing hosting them**. Phase 6 supplies that host,
the one-time loopback OAuth login flow, and a CLI to drive both. It implements the re-scoped
§9 Phase 6 (host + loopback OAuth + CLI); the aspirational HTTP API slice is deferred (see
"Scope decision" below).

The flow now possible: `fitbitsync login` opens the browser to Fitbit's consent page, catches
the loopback redirect, verifies anti-CSRF state, and persists encrypted tokens;
`fitbitsync run` boots the Generic Host and the 15-minute `SyncScheduler`.

## Chunks (each: build 0 warnings → full suite green → commit → push)

| Chunk | Commit subject |
|-------|----------------|
| plan | docs: Phase 6 plan — composition root, chunk breakdown 6a–6h, grounded in real signatures |
| 6a | Scaffold `FitbitSync.Host` (Exe console) + `Host.Tests`, add to `.slnx`, smoke test |
| 6b | Hand-rolled `CommandLineParser` (login/run/help verbs, unknown→error) + `CliVerb`/`ParsedCliCommand` + `Program` dispatch |
| 6c | `LoopbackRedirectParser` (pure) + `OAuthCallbackResult`; parses code/state/error, URL-decoding, missing-param failures |
| 6d | `OAuthStateValidator` (pure anti-CSRF, `FixedTimeEquals`, length/null-safe) |
| 6e | `ILoopbackOAuthListener`/`IBrowserLauncher` ports + `HttpListener` and `Process.Start` thin shells |
| 6f | `AddFitbitSyncHost` composition root (key/cipher/signer + EncryptedSqlite factory seams; `AddPersistence`+`AddFitbitProvider`+`AddSyncEngine`; manual `FitbitOAuthOptions` binding; fail-fast keys) + `HostStorageOptions` + DI tests |
| 6g | `LoginCommand` + `RunCommand` + `FitbitSyncHostFactory` (UserSecrets+env config) + `Program` dispatch with clean fail-fast |
| 6h | `appsettings.json` (non-secret shape) + `PHASE6-COMPLETE` + final verification |

## Test count

154 total (up from 119): Domain 17, Security 10, Application 35, Persistence 22,
Providers.Fitbit 35, **Host 35 (new)**. The 35 Host tests are all pure units —
`CommandLineParser` (11), `LoopbackRedirectParser` (7), `OAuthStateValidator` (9),
`AddFitbitSyncHost` DI smoke (7), plus the 6a assembly-reachability smoke (1).

## Key design decisions (and deviations from the aspirational plan)

- **Scope decision — host + loopback OAuth + CLI; HTTP API deferred.** The session
  instruction re-scoped Phase 6 to the runnable composition root and is silent on the
  programmatic HTTP API (force-sync/status/metrics/audit/health endpoints, API-key
  middleware, `WebApplicationFactory` tests) that IMPLEMENTATION_PLAN §6/§9 also lists. Per
  Phase 5's "trust the code over the aspirational plan" lesson, those endpoints are deferred
  to a later phase. They can be added to this same `FitbitSync.Host` project (ASP.NET Core
  10.0.8 runtime is present) without touching the inner layers. The §9 first red test
  `PostSync_RequiresApiKey_AndEnqueuesForceSync` belongs to that deferred slice.
- **Project named `FitbitSync.Host`, not `FitbitSync.Api`.** No HTTP endpoints ship this
  phase; `Host` reflects the actual responsibility (composition root + worker). The deferred
  API can live here later.
- **BCL `HttpListener` for the loopback catcher**, not ASP.NET/Kestrel. Smallest surface for
  a one-shot `http://127.0.0.1:<port>/callback` redirect; no cert/Kestrel needed.
- **Pure units vs thin shell** (the explicit Phase 6 instruction): the redirect→result
  translation (`LoopbackRedirectParser`), anti-CSRF compare (`OAuthStateValidator`), and verb
  dispatch (`CommandLineParser`) are pure and fully tested. The browser launch
  (`SystemBrowserLauncher`), socket accept loop (`HttpListenerLoopbackOAuthListener`), host
  build (`FitbitSyncHostFactory`), and the `Login`/`Run` orchestration glue are untested thin
  shells — every branch worth testing delegates to a pure unit.
- **The host owns the security/persistence-factory seams `AddPersistence` deliberately
  omits.** `AddPersistence` registers the stores but not `IKeyProvider`, `IColumnCipher`,
  `IRecordSigner`, `EncryptedSqliteConnectionFactory`, or `EncryptedDbContextFactory` — those
  need config-supplied keys/passphrase, so the composition root registers them.
- **Keys/passphrase come from config, never generated ephemerally.** An ephemeral key would
  make the encrypted DB unreadable on the next run. `HostStorageOptions` binds a DB path
  (non-secret) plus the DB passphrase and base64 32-byte column + signing keys (secret).
  Missing or non-base64 keys **fail fast** with a named, actionable error; `Program` catches
  startup `InvalidOperationException`/`ArgumentException` and prints a clean message instead
  of a stack trace.
- **`InMemoryKeyProvider` is the Phase 6 `IKeyProvider`.** DPAPI-wrapped key files are Phase 7
  hardening; the `IKeyProvider` seam is unchanged, so that swap touches only the host.
- **`FitbitOAuthOptions` bound by hand.** Its `Uri RedirectUri` and `IReadOnlyList<string>
  Scopes` members don't round-trip through the default configuration binder, so
  `AddFitbitSyncHost` maps them explicitly from the `Fitbit` section into the
  `configureOAuth` callback that `AddFitbitProvider` already exposes.
- **`FitbitAuthorizationService.CompleteAsync` already does the canonical state-check +
  persist + audit.** `LoginCommand` pre-checks state with `OAuthStateValidator` for an early,
  clean rejection, then calls `CompleteAsync`, which re-validates (defense in depth),
  exchanges the code, persists tokens via `ITokenStore`, and writes the `AuthGrant` audit
  entry.

## Environment notes / gotchas for future sessions

- **`System.CommandLine` is NOT in the offline NuGet cache** → CLI parsing is hand-rolled
  (`CommandLineParser`, a pure static). No package was added.
- **Namespace collision:** the project namespace `FitbitSync.Host` shadows
  `Microsoft.Extensions.Hosting.Host`, so `FitbitSyncHostFactory` calls
  `Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(...)` fully qualified.
- **Transitive project references:** `Host.Tests` references only `FitbitSync.Host` but uses
  types from Application/Domain/Persistence/Providers/Security — they resolve transitively
  (default `.NET` transitive project-reference behavior), so no extra `<ProjectReference>`s
  were needed.
- **`appsettings.json` ships non-secret shape only** (DB path, redirect URI, scopes) and is
  copied to output (`CopyToOutputDirectory=PreserveNewest`). All secrets stay in User Secrets
  (dev, `UserSecretsId=fitbitsync-host`) / environment variables (runtime); `.gitignore`
  already blocks `*.db`, `*.key`, `secrets/`, `appsettings.Development.json`.
- **Build/test loop:** `dotnet build FitbitSync.slnx -c Debug` (0 warnings, warnings-as-errors)
  and `dotnet test FitbitSync.slnx -c Debug`. Run the app: `dotnet run --project
  src/FitbitSync.Host -- <help|login|run>`.

## How to configure and run (dev)

```
cd src/FitbitSync.Host
dotnet user-secrets set "Fitbit:ClientId" "<your-fitbit-client-id>"
dotnet user-secrets set "Storage:DatabasePassphrase" "<a-strong-passphrase>"
dotnet user-secrets set "Storage:ColumnEncryptionKeyBase64" "<base64 of 32 random bytes>"
dotnet user-secrets set "Storage:SigningKeyBase64" "<base64 of 32 random bytes>"
# optional for a confidential app: dotnet user-secrets set "Fitbit:ClientSecret" "<secret>"
dotnet run -- login    # one-time browser consent + token persist
dotnet run -- run      # start host + 15-min sync scheduler
```

The Fitbit app's redirect URI must match `Fitbit:RedirectUri`
(`http://127.0.0.1:7654/callback` by default).

## What remains for later phases

- **Deferred HTTP API:** force-sync/status/metrics/audit/health endpoints, API-key auth
  middleware, `ProblemDetails`, HTTPS/loopback binding, `WebApplicationFactory` integration
  tests (the original §6/§9 Phase 6 first red test
  `PostSync_RequiresApiKey_AndEnqueuesForceSync`). Lands in `FitbitSync.Host` or a sibling.
- **Phase 7:** chain/signature verification CLI verbs, key rotation (`PRAGMA rekey` +
  signing-key roll), DPAPI-wrapped key-file `IKeyProvider` for Windows, config validators,
  security headers, README + ops runbook.
