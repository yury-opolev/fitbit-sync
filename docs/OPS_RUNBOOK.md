# Fitbit-sync — Operations Runbook

Operational procedures for running, verifying, rotating keys, and recovering Fitbit-sync. All
commands run from `src/FitbitSync.Host` unless noted; substitute a published binary
(`fitbitsync <verb>`) for `dotnet run --` in production.

## 0. Prerequisites

- .NET 10 SDK (10.0.300+) for building; the published self-contained binary needs no SDK.
- Configuration supplied via User Secrets (dev) or environment variables (runtime). Startup
  validates configuration and **fails fast** with a named error if anything required is missing
  or malformed — read the message; it names the exact key.

Environment-variable form of a config key replaces `:` with `__`, e.g.
`Storage__DatabasePassphrase`, `Storage__SigningKeyBase64`, `Fitbit__ClientId`.

Generate a 32-byte base64 key:
```
[Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))   # PowerShell
openssl rand -base64 32                                                                          # bash
```

## 1. First-time setup & login

1. Register a Fitbit app (personal); set its redirect URI to match `Fitbit:RedirectUri`
   (default `http://127.0.0.1:7654/callback`).
2. Configure secrets (see README "First run").
3. Bootstrap OAuth — opens the browser, captures the loopback redirect, verifies anti-CSRF
   state, and persists **encrypted** tokens:
   ```
   dotnet run -- login
   ```
   Re-run `login` only if tokens are revoked or the refresh chain breaks (needs re-auth).

## 2. Run the service

```
dotnet run -- run
```

Starts the Generic Host and the 15-minute `SyncScheduler`. Incremental sync is prioritized;
backfill consumes leftover hourly rate-limit budget and resumes across restarts from durable
`SyncCheckpoint`s. Stop with Ctrl+C (graceful shutdown).

## 2a. Agent verbs (JSON + exit codes)

For scripted/agent automation use the one-shot agent verbs. Each writes a single JSON envelope to
**stdout** (logs go to stderr) and returns a meaningful exit code. They share the same Application
core as `run`, so they cannot violate rate limits or corrupt checkpoints.

```
dotnet run -- sync-once
dotnet run -- backfill --from 2024-01-01 --to 2024-01-31 --metric heartRate
dotnet run -- query --metric heartRate --from 2024-01-01 --to 2024-01-07
dotnet run -- query --coverage
```

Envelope: `{ schemaVersion, command, ok, exitCode, data, error }`. Exit codes: **0** success
(including `query` with empty results), **1** usage/config failure (fail-fast JSON error), **2**
operation failed (`faulted`), **3** rate-limited before completion. The envelope never contains
secrets/keys/tokens.

- **`sync-once`** runs ONE incremental pass and exits; it does **not** start the scheduler.
- **`backfill`** is **idempotent and gap-aware**: it computes the dates missing from
  `metric_samples` across `--from`..`--to` and fetches **only** those — re-running over an
  already-held range makes **zero** Fitbit API calls. The `data` reports `alreadyCovered`,
  `fetched`, and `stillMissing` per metric. Re-run safely after a rate-limit pause (exit 3) to
  finish `stillMissing` dates. Omit `--metric` to backfill all metrics.
- **`query`** is read-only. Sample mode (`--metric` + range) returns stored samples; an empty range
  is exit 0 with `count: 0`. Coverage mode (`--coverage`) reports the held date span and gaps per
  metric — answering "what data do we have, and for which dates?".

Capture stdout to parse the JSON (stderr carries logs):

```
dotnet run -- query --coverage 1> coverage.json 2> /dev/null
```

## 3. Verify integrity (tamper detection)

```
dotnet run -- verify
```

Runs two checks and prints a report:
- **Audit chain** — recomputes the SHA-256 hash chain over `audit_entries`; any retroactive
  edit/delete breaks the chain.
- **Sample signatures** — re-verifies the HMAC-SHA256 signature of every `metric_samples` row
  against its canonical bytes; any out-of-band value change is reported as a forgery.

Exit codes: **0** = integrity OK; **2** = tampering detected (chain broken or ≥1 forged sample);
**1** = startup/config failure. Wire `verify` into a scheduled health check (e.g. nightly); a
non-zero exit should alert.

If verify fails, see §5 (Recovery).

## 4. Rotate keys

Rotating rolls the **HMAC signing key** (re-signs every sample) and re-encrypts the **whole DB
file** with a new passphrase (`PRAGMA rekey`), atomically, writing a `key-rotation` audit entry.

1. Generate the new key material and set the `New*` keys alongside the **current** ones (the
   current keys are still needed to open the DB during rotation):
   ```
   dotnet user-secrets set "Storage:NewSigningKeyBase64" "<base64 32 bytes>"
   dotnet user-secrets set "Storage:NewColumnEncryptionKeyBase64" "<base64 32 bytes>"   # if rotating the column key too
   dotnet user-secrets set "Storage:NewDatabasePassphrase" "<a new strong passphrase>"   # omit to keep the file passphrase
   ```
2. Back up the database file first (see §5.1).
3. Run:
   ```
   dotnet run -- rotate-keys
   ```
   It prints the re-signed count, the new signing-key id, and whether the file was re-encrypted.
4. **Promote the new values to the active keys**, then remove the `New*` entries:
   ```
   dotnet user-secrets set "Storage:SigningKeyBase64" "<the new signing key>"
   dotnet user-secrets set "Storage:ColumnEncryptionKeyBase64" "<the new column key>"     # if rotated
   dotnet user-secrets set "Storage:DatabasePassphrase" "<the new passphrase>"            # if rotated
   dotnet user-secrets remove "Storage:NewSigningKeyBase64"
   dotnet user-secrets remove "Storage:NewColumnEncryptionKeyBase64"
   dotnet user-secrets remove "Storage:NewDatabasePassphrase"
   ```
5. Confirm with `dotnet run -- verify` (expect exit 0) and a normal `run`.

> Order matters: rotation opens the DB with the **current** passphrase, then rekeys. If you
> promote the new passphrase before rotating, the DB won't open. If rotation is interrupted after
> `PRAGMA rekey` commits, the file is already on the new passphrase — promote the new values and
> re-run `verify`.

### Cross-platform key file (DPAPI on Windows, passphrase on Linux)

When `Storage:KeyFilePath` is set, the column + signing keys live in a permission-restricted
(owner-only / chmod 600) key file created on first use — the base64 key settings are not used.

- **Windows:** the file is DPAPI-wrapped (`CurrentUser`) — decryptable only by the same Windows user
  on the same machine. No master secret is needed.
- **Linux/containers:** the file is wrapped with a pure-BCL passphrase protector
  (PBKDF2-HMAC-SHA256, random per-file salt → AES-GCM). Supply the **master secret** via
  `Storage:KeyProtectorSecret` (env var) **or** `Storage:KeyProtectorSecretFile` (a mounted secret
  file whose contents are the secret). Startup **fails fast** if a key file is configured on
  non-Windows without a master secret. Keep the master secret in your orchestrator's secret store
  (e.g. a Kubernetes/Docker secret mounted read-only); rotating it requires re-creating the key file
  from known key material, so back the key file up alongside its secret.

Treat the key file as a secret: back it up to a protected location. Losing it (or, on Windows, the
user profile; on Linux, the master secret) means the column-key and signing material cannot be
recovered (see §5.3). The DB passphrase is still configured separately.

## 5. Recovery

### 5.1 Backups

The database is a single file at `Storage:DatabasePath` (plus transient `-wal`/`-shm`). Back it
up while the service is **stopped** (or after a clean shutdown) by copying all three. The backup is
encrypted at rest; its passphrase/keys are required to read it — store those separately in your
secret store. Test-restore periodically by pointing a `verify` run at a copy.

### 5.2 Integrity check failed (tampering detected)

1. Do **not** keep writing to the suspect database. Stop the service.
2. Preserve the current file (copy it aside) for forensics — the audit chain pinpoints where the
   break is (first sequence whose recomputed hash mismatches).
3. Restore the most recent backup that passes `verify`.
4. Re-run sync (`run`); resumable checkpoints re-fetch any gap from Fitbit (within provider
   history caps). Investigate how the tamper occurred (host compromise, key leak) before trusting
   the host again; if a key may be compromised, rotate (§4) after restoring.

### 5.3 Lost or rotated-away keys / passphrase

There is **no backdoor** — by design the data is unreadable without the keys.
- Lost **DB passphrase**: the file cannot be decrypted; restore from a backup whose passphrase you
  hold, or re-bootstrap an empty DB (`login` + `run` re-syncs current data; historical backfill
  re-runs within Fitbit caps).
- Lost **signing key**: existing signatures can no longer be verified; rotate to a new signing key
  (§4), which re-signs all rows, then `verify`. (Past tamper-evidence for the old-key period is
  lost, so confirm the data is trustworthy first — ideally restore a verified backup.)
- Lost **DPAPI key file** (Windows): unrecoverable column/signing material; restore the key-file
  backup, or restore a DB backup and re-key.

### 5.4 OAuth needs re-auth

If refresh fails terminally (revoked grant, broken single-use refresh chain), affected checkpoints
pause and the token is marked needs-reauth. Recover by re-running `dotnet run -- login`.

## 6. Quick reference

| Goal | Command | Healthy result |
|------|---------|----------------|
| Bootstrap OAuth | `dotnet run -- login` | "Authorization complete. Tokens stored." |
| Start syncing | `dotnet run -- run` | Host runs; scheduler ticks every 15 min |
| One sync pass (agent) | `dotnet run -- sync-once` | JSON envelope; exit 0 on `completed` |
| Gap-aware backfill (agent) | `dotnet run -- backfill --from D --to D` | JSON coverage delta; covered dates = 0 API calls |
| Read samples / coverage (agent) | `dotnet run -- query --metric M --from D --to D` / `query --coverage` | JSON; exit 0 even when empty |
| Check tamper-evidence | `dotnet run -- verify` | "Integrity OK." (exit 0) |
| Rotate keys | `dotnet run -- rotate-keys` | Re-signed count + new key id printed (exit 0) |
| Build / test | `dotnet build` / `dotnet test FitbitSync.slnx -c Debug` | 0 warnings; all tests pass |
