# Fitbit-sync — Setup guide

How to set up Fitbit-sync from scratch and start syncing your own Fitbit data into a local,
encrypted database. There are two authorization paths: a **desktop** flow (opens your browser
automatically) and a **headless / agent** flow (no desktop browser needed). Pick one.

> Driving this with an AI agent? See [AGENTS.md](../AGENTS.md) for the agent↔human collaboration
> script.

## 1. Prerequisites

- **.NET 10 SDK** (10.0.300 or newer) — <https://dotnet.microsoft.com/download/dotnet/10.0>.
  (A published self-contained binary needs no SDK; building from source does.)
- A **Fitbit account** and a registered Fitbit app (next step).

Build and test to confirm a healthy checkout:

```bash
dotnet build FitbitSync.slnx -c Debug   # 0 warnings (warnings-as-errors)
dotnet test  FitbitSync.slnx -c Debug   # all tests green
```

## 2. Register a Fitbit app (human, one-time)

1. Go to **<https://dev.fitbit.com/apps/new>** (sign in; manage existing apps at
   <https://dev.fitbit.com/apps>).
2. Fill in the form. The settings that matter:
   - **OAuth 2.0 Application Type:** **Personal** — required to read your intraday/personal metrics.
   - **Redirect URI:** must exactly match `Fitbit:RedirectUri`. Default:
     `http://127.0.0.1:7654/callback`.
   - **Default Access Type:** Read-Only is sufficient.
3. After saving, copy your **OAuth 2.0 Client ID**. A **Client Secret** is only needed for a
   confidential app and is optional here — PKCE is always used.

Fitbit's OAuth reference (grant types, scopes, endpoints):
<https://dev.fitbit.com/build/reference/web-api/authorization/>.

> ⚠️ Fitbit does **not** support the OAuth Device Authorization Grant ("device flow"). The only
> supported grants are Authorization Code (used here, with PKCE) and the deprecated Implicit grant —
> which is why headless setup uses the copy-paste flow in step 4b rather than a device code.

## 3. Configure secrets

`appsettings.json` holds only non-secret shape (redirect URI, scopes, DB path). Secrets come from
**.NET User Secrets** in development (`UserSecretsId=fitbitsync-host`) or **environment variables** at
runtime. The `.gitignore` blocks `*.db`, `*.key`, `secrets/`, and `appsettings.Development.json`.

Generate two 32-byte base64 keys (run twice, keep both):

- PowerShell: `[Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))`
- bash: `openssl rand -base64 32`

Then, from `src/FitbitSync.Host`:

```bash
dotnet user-secrets set "Fitbit:ClientId" "<your-fitbit-client-id>"
dotnet user-secrets set "Storage:DatabasePassphrase" "<a-strong-passphrase>"
dotnet user-secrets set "Storage:ColumnEncryptionKeyBase64" "<base64 key #1>"
dotnet user-secrets set "Storage:SigningKeyBase64" "<base64 key #2>"
```

| Key | Secret? | Purpose |
|-----|---------|---------|
| `Fitbit:ClientId` | secret | Your Fitbit app's client id. |
| `Fitbit:ClientSecret` | secret (optional) | Only for a confidential app; PKCE is always used. |
| `Fitbit:RedirectUri` | non-secret | Loopback callback; ships in `appsettings.json`. |
| `Fitbit:Scopes` | non-secret | OAuth scopes; ship in `appsettings.json`. |
| `Storage:DatabasePassphrase` | secret | Whole-file SQLite3MC encryption passphrase. |
| `Storage:ColumnEncryptionKeyBase64` | secret | Base64 of 32 bytes (AES-GCM token cipher). |
| `Storage:SigningKeyBase64` | secret | Base64 of 32 bytes (HMAC record signing). |
| `Storage:DatabasePath` | non-secret | Encrypted SQLite file path. |

**Runtime / environment-variable form:** replace `:` with `__` (e.g. `Storage__DatabasePassphrase`),
and array elements with an index (e.g. `Fitbit__Scopes__0=activity`). See `docs/OPS_RUNBOOK.md` for
the cross-platform key-file options (DPAPI on Windows, passphrase protector on Linux/containers).

## 4. Authorize

All commands below run from `src/FitbitSync.Host` (or use a published `fitbitsync` binary in place of
`dotnet run --`).

### 4a. Desktop flow (a browser is available)

```bash
dotnet run -- login        # opens your browser, captures the loopback redirect, stores encrypted tokens
```

### 4b. Headless / agent flow (no desktop browser)

```bash
# Step 1: print the authorize URL (JSON) and store a short-lived pending login.
dotnet run -- login --begin
```

Open the printed `data.authorizeUrl` in any browser, approve, and copy the URL you land on
(`http://127.0.0.1:7654/callback?code=…&state=…` — the page won't load, which is fine). Then:

```bash
# Step 2: finish the login from the pasted callback URL.
dotnet run -- login --complete --redirect "http://127.0.0.1:7654/callback?code=…&state=…"
```

Both headless commands emit a JSON envelope and a meaningful exit code (see [AGENTS.md](../AGENTS.md)
for the error-code table). The pending login expires ~15 minutes after `--begin`.

## 5. Run

```bash
dotnet run -- run          # starts the host + the 15-minute background sync scheduler (Ctrl+C to stop)
```

Or use the one-shot agent verbs (`sync-once`, `backfill`, `query`, `verify`) — see `README.md` and
`docs/OPS_RUNBOOK.md`.

## Troubleshooting

- **Startup fails naming a config key** — the host validates configuration and fails fast; the
  message names the exact missing/invalid key. Set it (User Secrets in dev, env var at runtime).
- **`authorization_denied`** on `--complete` — you declined consent or the callback carried an OAuth
  error. Re-run `login --begin`.
- **`authorization_expired` / `no_pending_authorization`** — the ~15-minute window lapsed or no
  `--begin` was run; start over with `login --begin`.
- **Redirect URI mismatch** — the Fitbit app's Redirect URI must exactly equal `Fitbit:RedirectUri`.

## Links

- Operations runbook: [OPS_RUNBOOK.md](OPS_RUNBOOK.md)
- Agent collaboration guide: [AGENTS.md](../AGENTS.md)
- Fitbit OAuth reference: <https://dev.fitbit.com/build/reference/web-api/authorization/>
- Register a Fitbit app: <https://dev.fitbit.com/apps/new>
- .NET 10 SDK: <https://dotnet.microsoft.com/download/dotnet/10.0>
