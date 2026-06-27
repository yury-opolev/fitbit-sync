# Phase 7 plan — Hardening + verification tooling

> Status: **IN PROGRESS.** This is the first commit of Phase 7 (plan before production code),
> grounded in the real signatures of the code Phase 7 extends. Baseline at start: build 0
> warnings (warnings-as-errors), full suite **154** green (Domain 17, Security 10,
> Application 35, Persistence 22, Providers.Fitbit 35, Host 35).

## Authoritative scope (IMPLEMENTATION_PLAN §9 Phase 7, restated in PHASE6_COMPLETE)

> **Phase 7 — Hardening + verification tooling.** First red test:
> `VerifyChain_DetectsTamperedAuditEntry` / `VerifyIntegrity_DetectsForgedSample`.
> Chain/signature verification CLI verbs, key-rotation path (`PRAGMA rekey` + signing-key
> roll), config validators, security headers, docs (README + ops runbook), final review pass.

PHASE6_COMPLETE "what remains for Phase 7" adds: **DPAPI-wrapped key-file `IKeyProvider` for
Windows** (InMemory stays dev/test fallback).

### Deliverables

1. **Verification behavior + CLI `verify` verb.** Audit hash-chain check (reuse the existing
   `IAuditTrail.VerifyChainAsync`) **plus** a new signed-sample integrity check that re-computes
   each `metric_samples` row signature via `IRecordSigner.Verify` and reports forgeries.
2. **Key rotation.** SQLite3MC `PRAGMA rekey` (re-encrypt the DB file with a new passphrase)
   **and** HMAC signing-key roll (re-sign every `metric_samples` row under the new key id),
   done atomically with auditing.
3. **DPAPI-wrapped key-file `IKeyProvider` for Windows** — the real provider; InMemory stays as
   dev/test fallback. Windows-specific: the actual `ProtectedData` call sits behind an
   `IKeyProtector` seam so the wrap/unwrap codec is unit-tested without a real DPAPI call, and
   the whole suite still builds/runs on this host.
4. **Config validators** — fail-fast on missing/malformed `HostStorageOptions` /
   `FitbitOAuthOptions` at startup.
5. **Security headers / equivalent hardening** per the plan.
6. **Docs** — update README + write an ops runbook (run, rotate keys, verify integrity, recover).

### Explicitly OUT OF SCOPE for Phase 7

The deferred **HTTP API** slice: force-sync/status/metrics/audit/health endpoints, API-key
middleware, and the `PostSync_RequiresApiKey_AndEnqueuesForceSync` test. The user chose Phase 7
hardening, not the API slice. IMPLEMENTATION_PLAN §6 lists it; PHASE6_COMPLETE deferred it. It is
**not** built here. (Consequence: "security headers / hardening" in §6.2 targets an HTTP surface
that does not exist in this phase — handled below.)

## Grounding — real signatures Phase 7 extends (verified by reading the code)

- `IAuditTrail` (Domain) already exposes `Task<bool> VerifyChainAsync(CancellationToken)`;
  `AuditTrail` (Persistence) implements it by ordering `audit_entries` by `Sequence` and
  recomputing each `EntryHash` via `AuditEntryHasher`. The named test
  `VerifyChain_DetectsTamperedAuditEntry` is a thin alias over behavior the existing
  `AuditTrail_VerifyChain_ReturnsFalse_When*Tampered` tests already cover — Phase 7 surfaces it
  through the new `IIntegrityVerifier` + CLI verb.
- `IRecordSigner` (Security): `byte[] Sign<TRecord>(TRecord)` /
  `bool Verify<TRecord>(TRecord, ReadOnlySpan<byte>)`. `HmacRecordSigner` signs
  `CanonicalJson.ToUtf8Bytes(record)` with `HMACSHA256` under `IKeyProvider.GetSigningKey()`.
- `MetricRepository.UpsertAsync` signs each `MetricSample` (the domain record, via
  `MetricSampleMapping.ToDomain(row)`) and stores `Signature` + `SignatureKeyId`. So the integrity
  verifier re-derives the domain sample from each row and calls `recordSigner.Verify(sample, row.Signature)`.
- `IKeyProvider` (Security): `string SigningKeyId`, `ReadOnlyMemory<byte> GetColumnEncryptionKey()`,
  `ReadOnlyMemory<byte> GetSigningKey()`. `InMemoryKeyProvider` validates 32-byte keys and derives
  `SigningKeyId` = `SHA256(signingKey)[..8]` hex.
- `EncryptedSqliteConnectionFactory` (Persistence) already has `CreateOpenConnection()`,
  `CreateOpenConnectionWithKey(string)`, `CreateOpenConnectionWithoutKey()`, and builds the
  connection string with `Password=` (Microsoft.Data.Sqlite emits `PRAGMA key`). `PRAGMA rekey`
  will be issued on an open connection to re-encrypt.
- `FitbitSyncDbContext.GuardAuditAppendOnly` blocks Modified/Deleted `AuditEntryRow` in
  `SaveChanges[Async]`. Key rotation re-signs `metric_samples` (mutable) and appends an audit
  entry — it never edits audit rows, so the guard is satisfied.
- Host CLI: `CommandLineParser.Parse` → `ParsedCliCommand(CliVerb)`; `Program.Main` switches on
  `CliVerb`. Verbs `login`/`run`/`help` exist. Phase 7 adds `verify` and `rotate-keys` the same way
  (new `CliVerb` values, parser cases, `*Command.ExecuteAsync(IHost, ct)` thin shells, dispatch).
- `System.Security.Cryptography.ProtectedData` **10.0.0 is present in the offline NuGet cache**
  (confirmed under `~/.nuget/packages`) and already declared in `Directory.Packages.props`. The
  DPAPI provider can reference it directly; the call still sits behind `IKeyProtector` so unit
  tests never invoke real DPAPI and the suite runs on non-Windows CI.

## Security headers / hardening — boundary note

§6.2 "security headers" presumes an ASP.NET HTTP surface. **There is no HTTP surface in this phase**
(the API slice is deferred; the host is a console + `BackgroundService` + one-shot `HttpListener`
loopback used only during `login`). So there is nothing to attach HTTP response headers to. Phase 7
delivers the equivalent **process-level hardening** that does apply: fail-fast config validators
(no silent unconfigured run), key material never logged/committed, DPAPI-at-rest key wrapping, the
integrity/chain verifier as a tamper-detection control, and atomic audited key rotation. This
boundary is documented in PHASE7_COMPLETE; HTTP security headers land with the deferred API slice.

## Chunk plan (hard gate after each: build 0 warnings → full suite green, count only goes UP from 154 → commit → push). One type per file, additive only.

- **7a (this commit)** — `docs/PHASE7_PLAN.md`.
- **7b — Integrity verifier + the two named red tests.** `IntegrityReport` (Domain record:
  `IsAuditChainIntact`, `ForgedSampleCount`, `IsValid`), `IIntegrityVerifier` (Domain port:
  `Task<IntegrityReport> VerifyAsync(ct)`), `IntegrityVerifier` (Persistence: runs
  `VerifyChainAsync` + re-verifies every `metric_samples` signature via `IRecordSigner`), DI
  registration. Integration tests in Persistence.Tests including the named
  `VerifyChain_DetectsTamperedAuditEntry` and `VerifyIntegrity_DetectsForgedSample` (raw-SQL
  forge a sample value, assert detection).
- **7c — CLI `verify` verb.** `CliVerb.Verify`, parser case + usage text, `VerifyCommand` thin
  shell (resolve `IIntegrityVerifier`, init schema, print report, exit 0 intact / 2 tampered),
  `Program` dispatch. Parser unit tests.
- **7d — Key rotation service.** `KeyRotationResult` + `KeyRotationService` (Persistence):
  re-sign all `metric_samples` under a new `IKeyProvider`, update `schema_metadata` signing-key
  id, issue `PRAGMA rekey` for the new DB passphrase, append a `TokenRefresh`/`KeyRotation` audit
  entry — atomically. Integration tests: rotate → old passphrase fails to open, new passphrase
  opens, integrity still valid under the new key, audit chain intact.
- **7e — CLI `rotate-keys` verb.** `CliVerb.RotateKeys`, parser, `RotateKeysCommand`, dispatch,
  parser tests.
- **7f — DPAPI key-protector seam + codec.** `IKeyProtector` (Security: `byte[] Protect(byte[])`
  / `byte[] Unprotect(byte[])`), `DpapiKeyProtector` (Windows-guarded thin shell over
  `ProtectedData`), `ProtectedKeyFile` codec (serialize/deserialize the wrapped key blob +
  key-id). Unit tests use a fake in-memory `IKeyProtector` to prove wrap/unwrap round-trips and
  reject tampered blobs — no real DPAPI dependency in tests.
- **7g — DPAPI key-file `IKeyProvider`.** `DpapiProtectedKeyFileProvider : IKeyProvider`
  (composes `IKeyProtector` + `ProtectedKeyFile`; reads/creates a permission-restricted key file).
  Host selection: use it on Windows when a key-file path is configured, else `InMemoryKeyProvider`
  fallback. Unit tests with the fake protector + temp file.
- **7h — Config validators.** `IValidateOptions<HostStorageOptions>` +
  `IValidateOptions<FitbitOAuthOptions>` (or equivalent fail-fast), wired so startup throws a
  named, actionable error on missing/malformed config. Unit tests.
- **7i — Docs.** README (root) + `docs/OPS_RUNBOOK.md` (run, rotate keys, verify integrity,
  recover from tamper/lost-key). Hardening boundary note included.
- **7j — Close-out.** `docs/PHASE7_COMPLETE.md` + final full-suite verification.

## Test-style conventions to follow (observed in the repo)

- xUnit + FluentAssertions; comments allowed only in tests (given/when/then intent headers).
- Integration tests use `EncryptedDatabaseFixture` (real encrypted temp-file SQLite, fresh
  `DbContext` per op). Pure units use plain xUnit facts.
- One type per file; file-scoped namespaces; `this.`-qualified members; `ConfigureAwait(false)`
  in src; source-gen logging if any logging is added; no comments in `src/**`.

## Self-verification loop (every chunk)

```
dotnet build FitbitSync.slnx -c Debug   # must be 0 warnings (warnings-as-errors)
dotnet test  FitbitSync.slnx -c Debug   # total must be >= previous (strictly up when tests added)
git add -A && git commit -m "<chunk>" && git push
```
