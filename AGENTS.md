# AGENTS.md — operating Fitbit-sync with an AI agent

This file tells an AI coding/automation agent how to set up and run Fitbit-sync **in collaboration
with a human**. Almost everything is automatable; exactly **two steps require a human**, because
Fitbit's OAuth has no device flow (the only grant types Fitbit supports are Authorization Code —
which this app uses with PKCE — and the deprecated Implicit grant; see
<https://dev.fitbit.com/build/reference/web-api/authorization/>).

## What the agent can do unattended

- Clone, `dotnet build`, `dotnet test`.
- Generate the encryption/signing keys and set configuration (User Secrets or environment variables).
- Drive the OAuth token exchange via two non-interactive commands (`login --begin` / `login --complete`).
- Operate the service: `sync-once`, `backfill`, `query`, `verify`, `rotate-keys` — each emits a single
  JSON envelope on **stdout** (logs go to stderr) with a meaningful exit code, and **never prints
  secrets**.

## What needs a human (one-time)

1. **Register a Fitbit app** at <https://dev.fitbit.com/apps/new> (manage existing apps at
   <https://dev.fitbit.com/apps>). The human must:
   - Choose OAuth 2.0 Application Type **Personal** (required to read intraday/personal data).
   - Set **Redirect URI** to exactly match `Fitbit:RedirectUri` (default
     `http://127.0.0.1:7654/callback`).
   - Copy the **OAuth 2.0 Client ID** back to the agent (and Client Secret only if using a
     confidential app — optional; PKCE is always used).
2. **Approve the consent screen** in a browser (the second half of the headless login below).

After this one-time bootstrap, tokens are stored encrypted and auto-refresh; the agent runs
indefinitely without further human help (re-auth is only needed if the grant is revoked).

## Bootstrap sequence (agent)

```bash
git clone git@github.com:yury-opolev/fitbit-sync.git
cd fitbit-sync
dotnet build FitbitSync.slnx -c Debug      # 0 warnings (warnings-as-errors)
dotnet test  FitbitSync.slnx -c Debug      # all green
```

Configure (development uses .NET User Secrets, `UserSecretsId=fitbitsync-host`; runtime uses
environment variables — replace `:` with `__`, and array elements with `__0`, `__1`, …):

```bash
cd src/FitbitSync.Host
dotnet user-secrets set "Fitbit:ClientId" "<client-id-from-human>"
dotnet user-secrets set "Storage:DatabasePassphrase" "<strong-passphrase>"
dotnet user-secrets set "Storage:ColumnEncryptionKeyBase64" "<base64 of 32 random bytes>"
dotnet user-secrets set "Storage:SigningKeyBase64" "<base64 of 32 random bytes>"
```

Generate a 32-byte base64 key:
- PowerShell: `[Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))`
- bash: `openssl rand -base64 32`

`Fitbit:RedirectUri` and `Fitbit:Scopes` already ship in `appsettings.json`; override via config only
if needed.

## Headless login: the agent↔human handoff

Run from `src/FitbitSync.Host` (or substitute the published `fitbitsync` binary for `dotnet run --`).

**Step 1 — agent starts the login and gets the URL:**

```bash
dotnet run -- login --begin
```

stdout (example):

```json
{
  "schemaVersion": 1,
  "command": "login-begin",
  "ok": true,
  "exitCode": 0,
  "data": {
    "authorizeUrl": "https://www.fitbit.com/oauth2/authorize?response_type=code&client_id=...&state=...",
    "state": "…",
    "expiresAtUtc": "2026-06-27T11:18:12Z"
  }
}
```

The agent parses `data.authorizeUrl` and asks the human: **"Open this URL, approve access, then paste
back the full URL you land on."** The pending login (PKCE verifier + state) is stored encrypted with a
~15-minute TTL.

**Step 2 — human approves, agent completes:**

The human's browser lands on `http://127.0.0.1:7654/callback?code=…&state=…`. (Nothing is listening
there in headless mode, so the browser shows a connection error — that's expected; the human copies
the URL from the **address bar**.) The agent then runs:

```bash
dotnet run -- login --complete --redirect "http://127.0.0.1:7654/callback?code=…&state=…"
```

stdout on success: `{ "command": "login-complete", "ok": true, "exitCode": 0, "data": { "authorized": true } }`.

### `login --complete` exit codes / error codes

| exit | error.code | Meaning |
|------|------------|---------|
| 0 | — | Authorized; tokens stored. |
| 1 | `usage` / `startup` | Bad flags (e.g. missing `--redirect`) or missing/invalid configuration. |
| 2 | `no_pending_authorization` | No pending login — run `login --begin` first. |
| 2 | `authorization_expired` | The pending login's TTL lapsed — run `login --begin` again. |
| 2 | `invalid_redirect` | `--redirect` was not a valid absolute callback URL. |
| 2 | `authorization_denied` | The callback carried an OAuth error (e.g. `access_denied`). |
| 2 | `state_mismatch` | Returned state didn't match the pending login (possible CSRF). |
| 2 | `token_exchange_failed` | Fitbit rejected the code→token exchange. |

## Operating verbs (after login)

```bash
dotnet run -- sync-once                                           # one incremental pass
dotnet run -- backfill --from 2024-01-01 --to 2024-01-31          # gap-aware; re-runs are free
dotnet run -- query --metric heartRate --from 2024-01-01 --to 2024-01-07
dotnet run -- query --coverage                                   # what dates are held + gaps
dotnet run -- verify                                             # audit-chain + signature check
```

Exit codes for agent verbs: `0` success (an empty `query` is still `0`), `1` usage/config, `2`
operation failed, `3` rate-limited. See `README.md` and `docs/OPS_RUNBOOK.md` for the full contract.

## Links

- Setup guide (humans): [docs/SETUP.md](docs/SETUP.md)
- Operations runbook: [docs/OPS_RUNBOOK.md](docs/OPS_RUNBOOK.md)
- Fitbit OAuth reference: <https://dev.fitbit.com/build/reference/web-api/authorization/>
- Register a Fitbit app: <https://dev.fitbit.com/apps/new>
- ⚠️ The legacy Fitbit Web API is being deprecated in **September 2026**; migration moves to the
  Google Health API. This app's provider layer is isolated to ease that future swap.
