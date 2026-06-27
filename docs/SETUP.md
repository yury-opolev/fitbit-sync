# Fitbit-sync — Setup guide (Google Health API)

How to set up Fitbit-sync from scratch and start syncing your Fitbit/Pixel data into a local,
encrypted database. As of 2026 this app authenticates with **Google OAuth 2.0** and reads data via
the **Google Health API** (`health.googleapis.com`) — the replacement for the deprecated Fitbit Web
API. Authorization uses the **headless** `login --begin` / `login --complete` flow (no desktop
browser-loopback needed).

> Driving this with an AI agent? See [AGENTS.md](../AGENTS.md) for the agent↔human collaboration
> script.

## 1. Prerequisites

- **.NET 10 SDK** (10.0.300+) — <https://dotnet.microsoft.com/download/dotnet/10.0>.
- A **Google account** whose Fitbit data you want to read.

```bash
dotnet build FitbitSync.slnx -c Debug   # 0 warnings (warnings-as-errors)
dotnet test  FitbitSync.slnx -c Debug   # all tests green
```

## 2. Register a Google Cloud OAuth client (human, one-time)

Reference: <https://developers.google.com/health/setup>.

1. Open the Google Health **setup page** and use **"Enable the API and get an OAuth 2.0 Client ID"**.
   Create (or pick) a Google Cloud project you administer.
2. When asked "Where are you calling from?", choose **Web Server**.
3. Set an **Authorized redirect URI** and remember it — it must match `Google:RedirectUri`. This app
   ships with `https://localhost:7654/callback`; use that (or change both to match).
4. Copy the **OAuth 2.0 Client ID** and **Client Secret** (and download the Credentials JSON).
5. **OAuth consent screen:** User type **External**, publishing status **Testing**. On the Audience
   page, add **your own email** under **Test users**.
6. **Data Access → Add or remove scopes:** search "Google Health API" and add the read scopes for the
   metrics you want — at minimum:
   - `.../auth/googlehealth.activity_and_fitness.readonly` (steps, active-zone-minutes, VO₂max)
   - `.../auth/googlehealth.health_metrics_and_measurements.readonly` (heart rate, SpO₂, HRV)
   - `.../auth/googlehealth.sleep.readonly` (sleep)
7. **Publish to "In Production"** when you're ready to run unattended (see step 5 caveat below).

> ⚠️ **7-day token caveat.** In **Testing** publishing mode Google refresh tokens **expire after 7
> days** (you'd re-authorize weekly). Publishing the app to **In Production** makes refresh tokens
> long-lived. Unverified apps keep a 100-user cap in production too, so as a personal (1-user) app you
> get long-lived tokens without the third-party security review.

> ⚠️ **Don't reuse a Gmail-scoped client carelessly.** If the same Google Cloud client has also been
> granted Gmail scopes, do not request `include_granted_scopes` — the Health API rejects any token
> carrying mail scopes (`403 DISALLOWED_OAUTH_SCOPES`). This app never sends `include_granted_scopes`;
> a dedicated client for health is cleanest.

## 3. Configure secrets

`appsettings.json` holds non-secret shape only (redirect URI, scopes, db path). Secrets come from
**.NET User Secrets** (dev, `UserSecretsId=fitbitsync-host`) or **environment variables** (runtime).

Generate two 32-byte base64 keys (run twice):
- PowerShell: `[Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))`
- bash: `openssl rand -base64 32`

From `src/FitbitSync.Host`:

```bash
dotnet user-secrets set "Google:ClientId" "<your-google-oauth-client-id>"
dotnet user-secrets set "Google:ClientSecret" "<your-google-oauth-client-secret>"
dotnet user-secrets set "Storage:DatabasePassphrase" "<a-strong-passphrase>"
dotnet user-secrets set "Storage:ColumnEncryptionKeyBase64" "<base64 key #1>"
dotnet user-secrets set "Storage:SigningKeyBase64" "<base64 key #2>"
```

| Key | Secret? | Purpose |
|-----|---------|---------|
| `Google:ClientId` | secret | Google Cloud OAuth (web) client id. |
| `Google:ClientSecret` | secret | Client secret for the confidential web client. |
| `Google:RedirectUri` | non-secret | Must match the client's redirect (ships as `https://localhost:7654/callback`). |
| `Google:Scopes` | non-secret | Google Health read scopes (ship in `appsettings.json`). |
| `Storage:DatabasePassphrase` | secret | Whole-file SQLite3MC encryption passphrase. |
| `Storage:ColumnEncryptionKeyBase64` | secret | Base64 of 32 bytes (AES-GCM token cipher). |
| `Storage:SigningKeyBase64` | secret | Base64 of 32 bytes (HMAC record signing). |

Env-var form: `:` → `__`, array elements indexed (e.g. `Google__Scopes__0=...`). Cross-platform key-file
options are in `docs/OPS_RUNBOOK.md`.

## 4. Authorize (headless flow)

From `src/FitbitSync.Host`:

```bash
dotnet run -- login --begin
```

Open the printed `data.authorizeUrl`, sign in, approve the Health permissions, and copy the URL you
land on (`https://localhost:7654/callback?code=…&state=…` — the page won't load, which is fine). Then:

```bash
dotnet run -- login --complete --redirect "https://localhost:7654/callback?code=…&state=…"
```

(`login` with no flags just prints this guidance — there is no desktop-loopback login for Google.)

## 5. Run

```bash
dotnet run -- run          # host + 15-minute background sync scheduler (Ctrl+C to stop)
```

Or the one-shot agent verbs (`sync-once`, `backfill`, `query`, `verify`) — see `README.md`.

## Troubleshooting

- **`403 DISALLOWED_OAUTH_SCOPES` (mail scopes)** — the token carries Gmail scopes from a shared
  client; re-authorize (this app doesn't request `include_granted_scopes`), or use a dedicated client.
- **Weekly re-authorization** — you're in Testing mode (7-day refresh tokens); publish to In
  Production.
- **Scope/access error on the consent screen** — add the Health read scopes on the Data Access page
  and yourself as a Test user.
- **Startup fails naming a `Google:` key** — set it via User Secrets / env var.

## Links

- Google Health setup: <https://developers.google.com/health/setup>
- Migrate from Fitbit Web API: <https://developers.google.com/health/migration>
- Scopes: <https://developers.google.com/health/scopes>
- Agent guide: [AGENTS.md](../AGENTS.md) · Ops runbook: [OPS_RUNBOOK.md](OPS_RUNBOOK.md)
