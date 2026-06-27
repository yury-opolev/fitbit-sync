# Fitbit-sync

A self-contained .NET 10 service that periodically syncs your own Fitbit health data into a
local, **encrypted** SQLite database, with auditability, anti-spoofing, and resumable sync as
first-class constraints. Built TDD, phased (see `docs/IMPLEMENTATION_PLAN.md`).

> Security posture: encryption at rest (whole-file SQLite3MC cipher **plus** AES-GCM on OAuth
> token columns), a tamper-evident audit hash-chain, HMAC-signed metric rows, OS-protected
> rotatable keys, and fail-fast configuration validation. No secrets are ever committed.

> **Authentication & data source (2026):** this app authenticates with **Google OAuth 2.0** and reads
> data via the **Google Health API** (`health.googleapis.com`) — the replacement for the legacy Fitbit
> Web API, which is deprecated September 2026. Your Fitbit/Pixel device data is read through Google.
> Mapped metrics: heart rate, steps, sleep, SpO₂, HRV, active-zone-minutes, VO₂max. (Breathing rate and
> skin temperature have no intraday-listable Google equivalent yet and are not advertised.)

**New here?** Step-by-step setup with links → [docs/SETUP.md](docs/SETUP.md). Driving this with an AI
agent → [AGENTS.md](AGENTS.md).

## Architecture

Clean/Hexagonal: dependencies point inward to the domain. The Fitbit integration is an outer
adapter so a future Google Health provider can replace it without touching the core.

| Project | Responsibility |
|---------|----------------|
| `FitbitSync.Domain` | Entities, value objects, enums, and ports (interfaces). BCL only. |
| `FitbitSync.Application` | Sync engine, planner, scheduler, resilience. Depends on Domain. |
| `FitbitSync.Persistence` | EF Core + encrypted SQLite (SQLite3MC), stores, audit chain, integrity & key-rotation services. |
| `FitbitSync.Providers.Fitbit` | Fitbit Web API adapter: typed HttpClient, OAuth2 PKCE, mappers, rate-limit handling. |
| `FitbitSync.Security` | AES-GCM column cipher, HMAC record signer, key providers, DPAPI key protection, canonical JSON. |
| `FitbitSync.Host` | Composition root: console CLI + Generic Host + `SyncScheduler` background worker. |

`Domain` and `Application` have zero knowledge of Fitbit, EF Core, or SQLite.

## CLI

```
fitbitsync <command>

Operator commands:
  login         One-time loopback OAuth flow (desktop browser); persists encrypted tokens.
  login --begin / login --complete --redirect <url>
                Headless two-step OAuth for agents/servers (JSON envelope). See AGENTS.md.
  run           Start the host and the 15-minute background sync scheduler.
  verify        Verify the audit hash-chain and stored-sample signatures, then exit.
  rotate-keys   Roll the signing key and re-encrypt the database, then exit.
  help          Show help.

Agent commands (emit a single JSON envelope on stdout; meaningful exit codes):
  sync-once     Run ONE incremental sync pass (does NOT start the scheduler), then exit.
  backfill --from <yyyy-MM-dd> --to <yyyy-MM-dd> [--metric <name>]
                Gap-aware historical fill — fetches ONLY dates not already held.
  query --metric <name> --from <yyyy-MM-dd> --to <yyyy-MM-dd>
                Read stored samples in range as JSON (read-only).
  query --coverage [--metric <name>] [--from <d> --to <d>]
                Report the held date span and any gaps per metric, as JSON.
```

Run a verb: `dotnet run --project src/FitbitSync.Host -- <verb>`.

### Agent JSON contract

The agent verbs (`sync-once`, `backfill`, `query`) are designed to be driven by scripts/agents.
Each writes exactly **one** JSON document to **stdout** (all logging goes to stderr) and returns a
meaningful process exit code. The envelope is stable and versioned:

```json
{
  "schemaVersion": 1,
  "command": "sync-once",
  "ok": true,
  "exitCode": 0,
  "data": { "...command-specific..." },
  "error": null
}
```

On a usage/config failure `ok=false`, `data` is omitted, and `error` is
`{ "code": "...", "message": "..." }`. The envelope **never** contains secrets, keys, tokens, or
passphrases — only counts, dates, metric types, ids, and outcomes.

| Exit code | Meaning |
|-----------|---------|
| `0` | Success. **`query` with empty results is still `0`** (absence of data is not an error). |
| `1` | Usage/config/startup failure (bad flags, missing config) — fail fast, JSON error. |
| `2` | The operation ran but failed (sync/backfill `faulted`). `data` carries partial progress. |
| `3` | Rate-limited before completion. `data` reports what was done and what is still missing. |

**Backfill is idempotent and gap-aware:** it derives coverage from the dates actually present in
`metric_samples`, computes the missing dates, and fetches **only** those — re-running over an
already-held range makes **zero** Fitbit API calls. The result reports `alreadyCovered`, `fetched`,
and `stillMissing` per metric so the coverage delta is explicit. `query --coverage` answers "what
data do we have, and for which dates?" using the same coverage engine.

## Configuration

`appsettings.json` holds **non-secret shape only** (DB path, redirect URI, scope list). All
secrets come from **.NET User Secrets** (development, `UserSecretsId=fitbitsync-host`) or
**environment variables** (runtime). `.gitignore` blocks `*.db`, `*.key`, `secrets/`, and
`appsettings.Development.json`.

| Key | Secret? | Purpose |
|-----|---------|---------|
| `Google:ClientId` | secret | Your Google Cloud OAuth (web) client id. |
| `Google:ClientSecret` | secret | Client secret for the confidential web client. |
| `Google:RedirectUri` | non-secret | OAuth redirect registered on the client, e.g. `https://localhost:7654/callback`. |
| `Google:Scopes` | non-secret | Google Health read scopes (ships in `appsettings.json`: activity_and_fitness, health_metrics_and_measurements, sleep). |
| `Storage:DatabasePath` | non-secret | Encrypted SQLite file path. |
| `Storage:DatabasePassphrase` | secret | Whole-file SQLite3MC encryption passphrase. |
| `Storage:ColumnEncryptionKeyBase64` | secret | Base64 of 32 random bytes (AES-GCM token cipher). |
| `Storage:SigningKeyBase64` | secret | Base64 of 32 random bytes (HMAC record signing). |
| `Storage:KeyFilePath` | non-secret | Path to a wrapped key file. When set, replaces the base64 keys above. On **Windows** it is DPAPI-wrapped; on **Linux/Unix** it is wrapped with the passphrase protector (requires a master secret below). |
| `Storage:KeyProtectorSecret` | secret | **Non-Windows + key file:** master secret used to derive the key-file wrapping key (PBKDF2 → AES-GCM). |
| `Storage:KeyProtectorSecretFile` | non-secret (path) | **Non-Windows + key file:** path to a mounted file whose contents are the master secret (alternative to `KeyProtectorSecret`). |

Startup **fails fast** with a named error if required configuration is missing or malformed
(`HostConfigurationValidator`).

### First run (development)

```
cd src/FitbitSync.Host
dotnet user-secrets set "Google:ClientId" "<your-google-oauth-client-id>"
dotnet user-secrets set "Google:ClientSecret" "<your-google-oauth-client-secret>"
dotnet user-secrets set "Storage:DatabasePassphrase" "<a-strong-passphrase>"
dotnet user-secrets set "Storage:ColumnEncryptionKeyBase64" "<base64 of 32 random bytes>"
dotnet user-secrets set "Storage:SigningKeyBase64" "<base64 of 32 random bytes>"
dotnet run -- login --begin                              # prints the Google authorize URL (JSON)
# open it, approve, copy the redirect URL from the address bar, then:
dotnet run -- login --complete --redirect "<redirect-url>"
dotnet run -- run
```

The Google OAuth client's redirect URI must match `Google:RedirectUri`. To run unattended long-term,
publish the OAuth consent app to **"In Production"** (Testing mode refresh tokens expire after 7 days).
Full walkthrough: [docs/SETUP.md](docs/SETUP.md).

**Headless / agent setup (no desktop browser):** run `login --begin`, open the returned
`authorizeUrl`, approve, then `login --complete --redirect "<callback-url>"`. Both emit a JSON
envelope with meaningful exit codes. See [docs/SETUP.md](docs/SETUP.md) and [AGENTS.md](AGENTS.md).

## Build & test

```
dotnet build FitbitSync.slnx -c Debug   # 0 warnings (warnings-as-errors)
dotnet test  FitbitSync.slnx -c Debug
```

## Operations

See **`docs/OPS_RUNBOOK.md`** for running, verifying integrity, rotating keys, and recovery.

## Security model (summary)

- **At rest:** the whole DB file is encrypted (SQLite3MC, SQLCipher-compatible). OAuth tokens
  are additionally AES-GCM-encrypted at the column level, with associated data binding each
  ciphertext to its row identity — tokens are encrypted twice.
- **Integrity / anti-spoofing:** every `metric_samples` row is HMAC-SHA256 signed over a
  canonical serialization; the audit trail is an append-only SHA-256 hash chain. `verify`
  detects any out-of-band tampering of either.
- **Key protection:** keys can be wrapped in a permission-restricted (chmod 600 / owner-only)
  key file: **Windows** wraps with DPAPI (`CurrentUser`); **Linux/containers** wrap with a pure-BCL
  passphrase protector (PBKDF2-HMAC-SHA256 → AES-GCM) keyed by a container-supplied master secret.
  The in-memory provider (base64 keys) is the dev/test fallback. Keys are rotatable (`rotate-keys`)
  — re-signs all rows and `PRAGMA rekey`s the file, audited.
- **No HTTP surface yet:** automation is via the **agent CLI verbs** (`sync-once`/`backfill`/`query`,
  JSON + exit codes) over the same Application core. The programmatic HTTP API (force-sync/status/
  metrics endpoints + API-key middleware) and HTTP-specific hardening (security headers) remain
  deferred and can wrap the same core later.
