# Phase 6 — Composition root: runnable host + loopback OAuth + CLI

> Status: **IN PROGRESS.** Builds on Phase 5 (`119` tests green). Additive only; one type per
> file; hard gate after every chunk (build 0 warnings + full suite green + commit + push).

## Authoritative scope (this session's instruction, reconciled with the repo plan)

Phase 5 delivered the provider-agnostic sync engine but **nothing hosts it**. Phase 6 builds
the **composition root** that makes the system runnable:

1. **Host** — a console Generic Host (`Microsoft.Extensions.Hosting`) that registers
   `AddPersistence()` + `AddFitbitProvider()` + `AddSyncEngine()` (which already registers the
   `SyncScheduler` `BackgroundService`) plus the **security/persistence-factory services that
   `AddPersistence` deliberately leaves to the composition root** (`IKeyProvider`,
   `IColumnCipher`, `IRecordSigner`, `EncryptedSqliteConnectionFactory`,
   `EncryptedDbContextFactory`). Binds `FitbitOAuthOptions` + storage/crypto config from
   **User Secrets (dev)** and **environment variables (runtime)**. No secrets in source.
2. **Loopback OAuth flow** — `FitbitAuthorizationService.Begin()` yields the authorize URL +
   `State` + `CodeVerifier`. Open the system browser to that URL, run a tiny local
   `HttpListener` on `127.0.0.1` to catch the redirect, parse `code`/`state`/`error` from the
   query, verify the returned state matches (anti-CSRF), then `CompleteAsync(...)` to
   exchange + persist tokens.
3. **CLI** — a `login` verb (runs the loopback flow once) and a `run` verb (starts the host +
   scheduler). Verb dispatch + arg parsing are a pure testable unit.

### Divergence from the aspirational IMPLEMENTATION_PLAN §6

The aspirational plan's Phase 6 also lists a full programmatic **HTTP API**
(force-sync/status/metrics/audit/health endpoints, API-key middleware, `ProblemDetails`,
`WebApplicationFactory` tests). The session instruction re-scopes Phase 6 to the **runnable
host + loopback OAuth + CLI** and is silent on those endpoints; Phase 5's completion notes
explicitly warned the aspirational plan diverges from real code ("trust the code"). The HTTP
API is therefore **deferred** to a later phase (it can be added to this same `FitbitSync.Host`
project or a sibling without touching inner layers). The named §9 first red test
(`PostSync_RequiresApiKey_AndEnqueuesForceSync`) belongs to that deferred HTTP slice.

## Real signatures this phase wires together (verified in code, not the plan)

- `FitbitAuthorizationService` (public, `internal` ctor; resolved from DI scope):
  `FitbitAuthorizationSession Begin()`,
  `Task<OAuthToken> CompleteAsync(FitbitAuthorizationSession session, string returnedState, string code, CancellationToken ct = default)`.
  `CompleteAsync` already validates state (throws `FitbitAuthenticationException` on mismatch),
  persists via `ITokenStore.SaveAsync`, and writes an `AuthGrant` audit entry.
- `FitbitAuthorizationSession(Uri AuthorizeUrl, string State, string CodeVerifier)`.
- `FitbitOAuthOptions` { `ClientId`, `ClientSecret?`, `RedirectUri` (Uri), `Scopes`,
  `RefreshSkew`, `TokenEndpoint`, `AuthorizationEndpoint` }. `AddFitbitProvider` validates
  `ClientId` non-empty + `RedirectUri` absolute via `ValidateOnStart`.
- `AddFitbitProvider(Action<FitbitProviderOptions>?, Action<FitbitOAuthOptions>?)` — host passes
  `configureOAuth` that binds the `Fitbit` config section.
- `AddPersistence()` registers `DbContext` (scoped, via `EncryptedDbContextFactory.Create()`),
  `IClock`, `AuditEntryHasher`, `IMetricRepository`, `ITokenStore`, `ISyncCheckpointStore`,
  `IAuditTrail`, `ISchemaInitializer` — but **not** the factory/keys/cipher/signer.
- `AddSyncEngine(Action<SyncOptions>?)` registers gate/planner/resilience/queue/cycle-runner,
  scoped `ISyncEngine`, and the hosted `SyncScheduler`.
- `EncryptedSqliteConnectionFactory(string databasePath, string encryptionKey)` (SQLite file
  passphrase); `InMemoryKeyProvider(ReadOnlySpan<byte> columnKey32, ReadOnlySpan<byte> signingKey32)`;
  `AesGcmColumnCipher(byte[] columnKey)`; `HmacRecordSigner(IKeyProvider)`.
- `SchemaInitializer.Initialize()` does `EnsureCreated` + metadata upsert (called once at startup).

## Environment constraints (verified)

- **`System.CommandLine` is NOT in the offline NuGet cache** → CLI parsing is **hand-rolled**
  (a pure `CommandLineParser` unit), no package added.
- `Microsoft.Extensions.Hosting` 10.0.0, `…Configuration.UserSecrets` 10.0.0,
  `…Configuration.EnvironmentVariables`, `…Configuration.Binder` are all cached → host buildable.
- Loopback uses BCL `System.Net.HttpListener` (no ASP.NET runtime dependency).

## Project layout (new, additive)

```
src/FitbitSync.Host/              # console composition root (OutputType=Exe, Microsoft.NET.Sdk)
tests/FitbitSync.Host.Tests/      # pure-unit tests (InternalsVisibleTo Host)
```

## Design decisions

- **BCL HttpListener over ASP.NET** for the one-shot loopback redirect catcher — smallest
  surface, no Kestrel/cert needed for a personal `http://127.0.0.1:<port>/` redirect.
- **Pure units vs thin shell** (per instruction): `LoopbackRedirectParser` (query → result)
  and `OAuthStateValidator` (anti-CSRF compare) are pure + fully tested; the browser launch and
  the `HttpListener` accept loop are an untested thin shell behind `ILoopbackOAuthListener`.
- **Keys/passphrase come from config, never generated ephemerally** (an ephemeral key would
  make the encrypted DB unreadable next run). `HostStorageOptions` binds a DB path (non-secret),
  DB passphrase, and base64 32-byte column + signing keys (secret → User Secrets/env). A missing
  secret fails fast.
- **`InMemoryKeyProvider` as the Phase 6 `IKeyProvider`.** DPAPI-wrapped key files are Phase 7
  hardening; the seam is unchanged.
- **One host project hosts both the worker now and the deferred HTTP API later** — named
  `FitbitSync.Host` to reflect that (not `Api`, since no endpoints ship this phase).

## Chunk breakdown (each ends green + committed + pushed)

| Chunk | Deliverable |
|-------|-------------|
| 6a | Scaffold `FitbitSync.Host` (Exe) + `FitbitSync.Host.Tests`; add both to `.slnx`; minimal `Program` returns 0. Suite stays 119 green. |
| 6b | `CliVerb` enum + `ParsedCliCommand` record + `CommandLineParser` (pure) + tests; `Program` dispatches. |
| 6c | `OAuthCallbackResult` record + `LoopbackRedirectParser` (pure) + tests. |
| 6d | `OAuthStateValidator` (pure anti-CSRF compare) + tests. |
| 6e | `ILoopbackOAuthListener` + `HttpListenerLoopbackOAuthListener` + `BrowserLauncher` (thin untested shell). |
| 6f | `HostStorageOptions` + `HostServiceCollectionExtensions.AddFitbitSyncHost` (composition root wiring) + DI smoke tests. |
| 6g | `LoginCommand` + `RunCommand` (thin shells) wired into `Program` dispatch. |
| 6h | `appsettings.json` (non-secret shape), `.gitignore` for db/secrets, `PHASE6_COMPLETE.md`; final full-suite verification. |

## Test strategy

- **Pure units** (fast, no I/O): `CommandLineParser` (verbs, unknown, help, options),
  `LoopbackRedirectParser` (code, error, missing params, URL-decoding), `OAuthStateValidator`
  (match/mismatch/empty), `AddFitbitSyncHost` DI smoke (registrations present incl. hosted
  `SyncScheduler`; resolvable non-DB singletons).
- **Untested thin shell** (documented): browser launch + `HttpListener` accept loop + the
  `LoginCommand`/`RunCommand` orchestration glue (logic delegates to the pure units above).
- **No live network, no real browser, no real sockets in automated tests.**
- Gate every chunk: `dotnet build FitbitSync.slnx -c Debug` (0 warnings) +
  `dotnet test FitbitSync.slnx -c Debug` (count only goes UP from 119).
