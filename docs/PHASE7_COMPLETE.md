# PHASE7-COMPLETE — Hardening + verification tooling

> Status: **COMPLETE.** Build 0 warnings (warnings-as-errors), full suite green (**185** tests,
> up from the Phase 6 baseline of 154), all work committed and pushed to `origin/main`. The two
> named first red tests — `VerifyChain_DetectsTamperedAuditEntry` and
> `VerifyIntegrity_DetectsForgedSample` — pass. The `fitbitsync` CLI gains `verify` and
> `rotate-keys` verbs alongside `login`/`run`/`help`.

## What Phase 7 delivered

Phase 7 hardened the system that Phase 6 made runnable, adding the verification and key-management
tooling the plan (§7, §9) calls for, plus fail-fast config validation and the real Windows key
provider. It implements §9 Phase 7 (verification CLI verbs, key rotation, config validators, docs);
the deferred HTTP API slice stays out of scope (see "Scope decision").

New operator capabilities:
- `fitbitsync verify` — detects any out-of-band tampering of the audit chain or signed samples.
- `fitbitsync rotate-keys` — rolls the HMAC signing key and re-encrypts the DB file, atomically
  and audited.
- DPAPI-wrapped key file on Windows as the real `IKeyProvider` (in-memory stays dev/test fallback).
- Startup fails fast on missing/malformed storage or OAuth configuration.

## Chunks (each: build 0 warnings → full suite green → commit → push)

| Chunk | Commit subject |
|-------|----------------|
| plan  | docs: Phase 7 plan — hardening + verification tooling, chunk breakdown 7a–7j grounded in real signatures |
| 7b    | `IIntegrityVerifier` + `IntegrityReport` (Domain) + `IntegrityVerifier` (Persistence) + DI; named red tests green |
| 7c    | CLI `verify` verb (`CliVerb.Verify` + parser + `VerifyCommand` + dispatch) + parser tests |
| 7d    | `KeyRotationService` (re-sign samples + `schema_metadata` + audit, then `PRAGMA rekey`) + `KeyRotationResult` + factory `Rekey` + integration test |
| 7e    | CLI `rotate-keys` verb + `RotateKeysCommand` + `StorageKeyDecoder` extracted/shared + tests |
| 7f    | `IKeyProtector` seam + `DpapiKeyProtector` (Windows-guarded) + `ProtectedKeyFileCodec` + `KeyMaterial` + ProtectedData ref; codec tests use a fake protector |
| 7g    | `DpapiProtectedKeyFileProvider : IKeyProvider` + host selection (Windows key-file vs in-memory) + tests |
| 7h    | `HostConfigurationValidator` (fail-fast storage + OAuth) wired eagerly into `AddFitbitSyncHost` + tests |
| 7i    | Root `README.md` + `docs/OPS_RUNBOOK.md` (run, verify, rotate, recover) + hardening/API boundary note |
| 7j    | `PHASE7-COMPLETE` + final verification |

## Test count

185 total (up from 154, +31): Domain 17, **Security 18 (+8)**, Application 35,
**Persistence 26 (+4)**, Providers.Fitbit 35, **Host 54 (+19)**.

New tests: integrity verifier (3, incl. the two named), key rotation (1), protected-key-file codec
(5), DPAPI key-file provider (3), `verify`/`rotate-keys` parser + usage (5), `StorageKeyDecoder`
(4), config validators (9).

## Key design decisions (and deviations from the aspirational plan)

- **The two named red tests already had behavioral siblings.** `IAuditTrail.VerifyChainAsync` and
  its tamper tests existed from Phase 2. Phase 7 surfaces them through a new combined
  `IIntegrityVerifier` (chain check **+** per-row signature re-verification) and the `verify` CLI
  verb. `VerifyChain_DetectsTamperedAuditEntry` and `VerifyIntegrity_DetectsForgedSample` are the
  named tests on the new verifier; the forged-sample test mutates a `metric_samples.value` via raw
  SQL (bypassing the signing path) and asserts the stale HMAC no longer matches the canonical bytes.
- **Integrity verifier re-derives the signed payload from the row.** `IntegrityVerifier` maps each
  `MetricSampleRow` back to the domain `MetricSample` (`MetricSampleMapping.ToDomain`) and calls
  `IRecordSigner.Verify(sample, row.Signature)` — exactly what `MetricRepository` signed at write
  time — so verification is deterministic and reuses the canonical-JSON + HMAC seam unchanged.
- **Key rotation is atomic then rekeys.** `KeyRotationService` re-signs all samples, updates the
  `signing_key_id` metadata, and appends the `key-rotation` audit entry in **one** `SaveChanges`
  (the append-only guard is satisfied — audit rows are added, never modified), then issues
  `PRAGMA rekey` on the open connection. Ordering is deliberate: the DB is opened with the
  **current** passphrase, so rekey must come last. The integration test proves the old passphrase
  can no longer open the file and integrity verifies under the new key.
- **`PRAGMA rekey` uses a quoted literal, not a parameter.** `PRAGMA` statements don't accept bound
  parameters in SQLite, so the new passphrase is escaped (`'' ` doubling) via a private
  `QuoteLiteral` on the connection factory. The passphrase is operator-supplied config, not
  untrusted input, and the doubling prevents quote-breakouts regardless.
- **DPAPI sits behind `IKeyProtector`; the provider is protector-agnostic.** `DpapiKeyProtector`
  (`[SupportedOSPlatform("windows")]`, `ProtectedData`/`CurrentUser` + fixed entropy) is the only
  Windows-specific type and is never touched by tests. `DpapiProtectedKeyFileProvider` composes any
  `IKeyProtector` + `ProtectedKeyFileCodec`, so load/create/reload logic is unit-tested on this host
  with an in-memory XOR fake — no real DPAPI call in the suite, and the whole solution builds and
  runs cross-platform. `System.Security.Cryptography.ProtectedData` 10.0.0 was confirmed present in
  the offline NuGet cache before being referenced (it was already declared in CPM but unused).
- **`ProtectedKeyFileCodec` is a versioned, self-describing payload.** Magic header (`FBSK`) +
  format version + the two 32-byte keys; deserialize rejects wrong length, bad magic, and
  unsupported version, so a truncated/garbled wrapped file fails loudly rather than yielding junk
  keys.
- **Config validators are eager and pure.** `HostConfigurationValidator.ValidateStorage/ValidateOAuth`
  are plain static methods called inside `AddFitbitSyncHost` (storage right after binding; OAuth by
  running the same map callback into a throwaway options object), so misconfiguration throws a named
  `InvalidOperationException` at composition time — before the host or scheduler start. They're
  fully unit-tested directly; the existing `Program` already converts startup
  `InvalidOperationException`/`ArgumentException` into a clean message. Chose pure validators over
  `IValidateOptions<T>` because the host maps `FitbitOAuthOptions` by hand (its `Uri`/list members
  don't round-trip the binder) and registers `IKeyProvider` from config, so there's no single
  bound-options pipeline to hook — a direct check is simpler and equally fail-fast.
- **`StorageKeyDecoder` extracted to remove duplication.** The base64-key decode/fail-fast logic
  the composition root had inline is now one shared, tested static used by both the host wiring and
  `rotate-keys`.

## Scope decision — HTTP API slice remains OUT of scope

Per the session instruction (the user chose Phase 7 hardening, not the API slice) and PHASE6_COMPLETE,
the deferred HTTP API — force-sync/status/metrics/audit/health endpoints, API-key middleware,
`ProblemDetails`, HTTPS/loopback binding, `WebApplicationFactory` tests, and the
`PostSync_RequiresApiKey_AndEnqueuesForceSync` test — is **not** built in Phase 7. It can be added to
`FitbitSync.Host` later without touching the inner layers.

### Consequence: "security headers" hardening

IMPLEMENTATION_PLAN §6.2 lists HTTP security headers under hardening, but **there is no HTTP surface
in this phase** (console + `BackgroundService` + a one-shot `HttpListener` used only during `login`),
so there is nothing to attach response headers to. Phase 7 delivers the **process-level** hardening
that does apply: fail-fast config validation, the integrity/chain verifier as a tamper-detection
control, atomic audited key rotation, DPAPI key-at-rest wrapping, owner-restricted key files, and the
standing "no secrets in source/logs" posture. HTTP security headers land with the deferred API slice.

## Environment notes / gotchas for future sessions

- **Build/test loop:** `dotnet build FitbitSync.slnx -c Debug` (0 warnings, warnings-as-errors) and
  `dotnet test FitbitSync.slnx -c Debug`. Run a verb: `dotnet run --project src/FitbitSync.Host -- <login|run|verify|rotate-keys|help>`.
- **`ProtectedData` is offline-cached** (10.0.0 under `~/.nuget/packages`) and now referenced by
  `FitbitSync.Security`. The DPAPI call is Windows-only and guarded; non-Windows hosts use the
  in-memory key provider.
- **`PRAGMA rekey` re-encrypts in place** — back up the DB file before `rotate-keys` (runbook §4/§5).
  After rekey the file is on the new passphrase; promote `Storage:DatabasePassphrase` to match.
- **Verify exit codes:** 0 = OK, 2 = tampering detected, 1 = startup/config failure. Suitable for a
  scheduled health check.
- **Key-rotation input** comes from `Storage:New*` config keys (`NewSigningKeyBase64`,
  `NewColumnEncryptionKeyBase64`, `NewDatabasePassphrase`); promote them to the active keys after a
  successful rotation and remove the `New*` entries.

## What remains for later phases

- **Deferred HTTP API** (§6/§9): force-sync/status/metrics/audit/health endpoints, API-key auth
  middleware, `ProblemDetails`, HTTPS/loopback binding, `WebApplicationFactory` tests, HTTP security
  headers, and the `PostSync_RequiresApiKey_AndEnqueuesForceSync` test.
- **Phase 8 (optional):** Google Health provider stub to prove the `IHealthDataProvider` swap ahead
  of the Sept-2026 Fitbit deprecation.
