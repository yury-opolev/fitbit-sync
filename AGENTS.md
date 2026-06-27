# AGENTS.md — operating Fitbit-sync with an AI agent

This file tells an AI agent how to set up and run Fitbit-sync **in collaboration with a human**. The
app authenticates with **Google OAuth 2.0** and reads Fitbit/Pixel data via the **Google Health API**
(`health.googleapis.com`) — the replacement for the deprecated Fitbit Web API. Almost everything is
automatable; exactly **two steps require a human**.

## What the agent can do unattended

- Clone, `dotnet build`, `dotnet test`.
- Generate the encryption/signing keys and set configuration (User Secrets or environment variables).
- Drive the OAuth token exchange via two non-interactive commands (`login --begin` / `login --complete`).
- Operate the service: `sync-once`, `backfill`, `query`, `verify`, `rotate-keys` — each emits a single
  JSON envelope on **stdout** (logs to stderr), a meaningful exit code, and **never prints secrets**.

## What needs a human (one-time)

1. **Register a Google Cloud OAuth client** at <https://developers.google.com/health/setup>:
   choose **Web Server**, set the redirect URI to match `Google:RedirectUri`
   (`https://localhost:7654/callback`), copy the **Client ID + Client Secret**, set the OAuth consent
   screen to **External / Testing**, add the human's email as a **Test user**, and add the Google
   Health **read scopes**. For unattended running, **publish to "In Production"** (long-lived refresh
   tokens; Testing mode tokens expire after 7 days).
2. **Approve the consent screen** in a browser (the second half of the headless login below).

After this one-time bootstrap, tokens are stored encrypted and auto-refresh; the agent runs
indefinitely (re-auth only if the grant is revoked).

## Bootstrap sequence (agent)

```bash
git clone git@github.com:yury-opolev/fitbit-sync.git && cd fitbit-sync
dotnet build FitbitSync.slnx -c Debug && dotnet test FitbitSync.slnx -c Debug

cd src/FitbitSync.Host
dotnet user-secrets set "Google:ClientId" "<client-id-from-human>"
dotnet user-secrets set "Google:ClientSecret" "<client-secret-from-human>"
dotnet user-secrets set "Storage:DatabasePassphrase" "<strong-passphrase>"
dotnet user-secrets set "Storage:ColumnEncryptionKeyBase64" "<base64 of 32 random bytes>"
dotnet user-secrets set "Storage:SigningKeyBase64" "<base64 of 32 random bytes>"
```

`Google:RedirectUri` and `Google:Scopes` ship in `appsettings.json`. Generate a key:
`[Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))`
(PowerShell) or `openssl rand -base64 32` (bash).

## Headless login: the agent↔human handoff

```bash
dotnet run -- login --begin
```

stdout (example): `{ "command":"login-begin","ok":true,"exitCode":0,"data":{ "authorizeUrl":"https://accounts.google.com/o/oauth2/v2/auth?...","state":"…","expiresAtUtc":"…" } }`

The agent relays `data.authorizeUrl` to the human: **"open this, approve, then paste back the URL you
land on."** The browser lands on `https://localhost:7654/callback?code=…&state=…` (it won't load —
that's expected; copy it from the address bar). Then:

```bash
dotnet run -- login --complete --redirect "https://localhost:7654/callback?code=…&state=…"
```

stdout on success: `{ "command":"login-complete","ok":true,"exitCode":0,"data":{ "authorized":true } }`.

> ⚠️ The app deliberately does **not** request `include_granted_scopes`. If the Google client is shared
> with a Gmail integration, merging those scopes makes the Health API reject the token
> (`403 DISALLOWED_OAUTH_SCOPES`). A dedicated client for health is cleanest.

### `login --complete` exit / error codes

| exit | error.code | Meaning |
|------|------------|---------|
| 0 | — | Authorized; tokens stored. |
| 1 | `usage` / `startup` | Bad flags or missing/invalid configuration. |
| 2 | `no_pending_authorization` / `authorization_expired` | Run `login --begin` (again). |
| 2 | `invalid_redirect` / `authorization_denied` / `state_mismatch` | Bad/denied/forged callback URL. |
| 2 | `token_exchange_failed` | Google rejected the code→token exchange. |

## Operating verbs (after login)

```bash
dotnet run -- sync-once
dotnet run -- backfill --from 2026-06-01 --to 2026-06-27 --metric heartRate
dotnet run -- query --metric heartRate --from 2026-06-20 --to 2026-06-27
dotnet run -- query --coverage
dotnet run -- verify
```

Exit codes: `0` success (empty `query` is still `0`), `1` usage/config, `2` operation failed, `3`
rate-limited.

**Mapped metrics:** heart rate, steps, sleep, SpO₂, HRV, active-zone-minutes, VO₂max. (Breathing rate
and skin temperature have no intraday-listable Google equivalent yet and are not advertised in the
provider's capabilities.)

## Links

- Setup guide (humans): [docs/SETUP.md](docs/SETUP.md) · Ops: [docs/OPS_RUNBOOK.md](docs/OPS_RUNBOOK.md)
- Google Health: setup <https://developers.google.com/health/setup> · migration
  <https://developers.google.com/health/migration> · scopes <https://developers.google.com/health/scopes>
