# Headless, agent-collaborative login — design

**Date:** 2026-06-27
**Status:** Approved (proceeding to implementation by user request)
**Working copy / source of truth:** the GitHub clone `github.com/yury-opolev/fitbit-sync`.

## Problem

Fitbit-sync is highly automatable for *operation* (the `sync-once` / `backfill` / `query`
agent verbs emit JSON + meaningful exit codes), but the *one-time setup* requires a desktop
browser: `login` opens the system browser and blocks on a loopback `HttpListener`. An AI agent
on a headless box can't complete it. Fitbit does **not** support the OAuth 2.0 Device
Authorization Grant (confirmed at <https://dev.fitbit.com/build/reference/web-api/authorization/>:
the only models are Authorization Code Grant, Authorization Code Grant + PKCE, and Implicit
Grant). So device flow is impossible; the goal is to make the **authorization-code (PKCE) flow
work headlessly** via an agent↔human handoff.

## Goal

An agent can clone, build, configure, and operate the project unattended; the only human steps
are (1) registering the Fitbit app and (2) approving the consent screen in a browser. The agent
drives the OAuth token exchange through two discrete, non-interactive commands with the human
interaction happening in chat between them.

## Non-goals / cut scope

- `import-tokens` verb (YAGNI — superseded by the manual flow).
- Any change to the existing desktop `login` (browser + loopback) — it stays as-is.
- Automating Fitbit app registration — remains a guided human step.

## CLI surface (additive)

| Command | Mode | Output |
|---|---|---|
| `login` | Interactive (existing) | Human text; browser + loopback. **Unchanged.** |
| `login --begin` | Headless step 1 | JSON envelope → stdout |
| `login --complete --redirect "<url>"` | Headless step 2 | JSON envelope → stdout |

The headless verbs reuse the existing agent envelope
`{ schemaVersion, command, ok, exitCode, data, error }` (logs to stderr) and **never** contain
tokens or secrets.

- `login --begin` → `data: { authorizeUrl, state, expiresAtUtc }`
- `login --complete` → `data: { authorized: true }`

`--begin` and `--complete` are mutually exclusive; `--complete` requires `--redirect <url>`.

## Data flow

```
agent: login --begin
  → FitbitAuthorizationService.Begin()            (EXISTING: PKCE + state + authorize URL)
  → persist {state, codeVerifier, authorizeUrl, expiresAtUtc} as the single pending row
  → emit { authorizeUrl, state, expiresAtUtc }
agent → human: "open this URL, approve, paste the redirect URL back"
human → agent: "http://127.0.0.1:7654/callback?code=…&state=…"
agent: login --complete --redirect "<that url>"
  → load pending row → reject if expired
  → LoopbackRedirectParser.Parse(url)             (EXISTING: code / state / provider-error)
  → FitbitAuthorizationService.CompleteAsync(session, returnedState, code)
                                                  (EXISTING: state check, exchange, persist+audit)
  → delete pending row → emit { authorized: true }
```

Most logic is reuse (`Begin`, `CompleteAsync`, `LoopbackRedirectParser`, `OAuthStateValidator`
already exist and are tested). New code: the pending-authorization store, two thin command
shells, parser changes, and envelope shaping.

## New persistence unit

- Port `IPendingAuthorizationStore` (same layering as `ITokenStore`):
  - `SaveAsync(pending, ct)` — replaces any existing row (only one login in flight at a time).
  - `GetAsync(ct)` — returns the pending authorization or `null`.
  - `DeleteAsync(ct)` — clears it.
- EF Core implementation against a new `pending_authorizations` table in the existing encrypted
  SQLite database; registered in the schema initializer.
- The `code_verifier` is transient (single-use, deleted on success). Whole-file SQLite3MC
  encryption covers it at rest — **no extra column cipher** (deliberate: TTL + single-use make it
  unnecessary).
- TTL ≈ 15 minutes, computed with `TimeProvider`. Expired pending rows are rejected on
  `--complete` with a clear error.

## Host orchestration (thin shells + testable units)

Follows the existing convention: thin untested IO shells; logic in pure, tested units.

- `BeginLoginCommand` / `CompleteLoginCommand`: thin shells that resolve services, persist/emit.
- `CommandLineParser`: parse `login` + optional `--begin` | `--complete` + `--redirect <url>`;
  validate mutual exclusion and that `--complete` requires `--redirect`. Parsing and
  envelope-mapping are the tested units; stdin/stdout stays in the shell.
- `Program`: route interactive `login` through the existing host path; route `login --begin` /
  `login --complete` through the agent-mode path (JSON envelope + exit codes).
- Implementation note: `CompleteAsync` takes a `FitbitAuthorizationSession`; the persisted row is
  rehydrated into one (persist `authorizeUrl` too, or add a verifier+state overload — decide in
  implementation).

## Error handling → exit codes

| Condition | exit | error.code |
|---|---|---|
| begin / complete success | 0 | — |
| bad flags (`--complete` without `--redirect`) / config | 1 | `usage` / `startup` |
| no pending session | 2 | `no_pending_authorization` |
| pending session expired | 2 | `authorization_expired` |
| `--redirect` not a valid absolute URL | 2 | `invalid_redirect` |
| redirect carried an OAuth error (e.g. `access_denied`) | 2 | `authorization_denied` |
| state mismatch (CSRF) | 2 | `state_mismatch` |
| token exchange failed | 2 | `token_exchange_failed` |

> Implementation note: Fitbit's token endpoint surfaces every non-success (including HTTP 429) as a
> single `FitbitAuthenticationException`, so there is no distinct rate-limited path on `--complete`;
> all exchange failures map to `token_exchange_failed` (exit 2). The earlier draft's `rate_limited`
> (exit 3) row was dropped, and `invalid_redirect` was added.

The envelope never emits tokens, the verifier, or any secret. The pasted redirect URL (which
carries the single-use auth code) is consumed immediately and not logged.

## Documentation (with links)

- `AGENTS.md` (repo root): agent operating manual — what's automatable vs. the two human steps,
  the full bootstrap command sequence, and the headless-login collaboration script.
- `docs/SETUP.md`: step-by-step human/agent setup guide with links:
  - Register app → <https://dev.fitbit.com/apps/new> (manage: <https://dev.fitbit.com/apps>)
  - OAuth reference → <https://dev.fitbit.com/build/reference/web-api/authorization/>
  - Authorization Code + PKCE tutorial, scopes reference, .NET 10 SDK download.
  - ⚠️ Legacy Fitbit Web API deprecation (Sept 2026) + Google Health migration note.
- `README.md`: link to both; document the headless `login --begin` / `login --complete` verbs.

## Testing (TDD, 0 warnings, warnings-as-errors)

- `FitbitSync.Persistence.Tests`: pending-auth store save/replace/get/delete + encrypted
  round-trip + expiry semantics.
- `FitbitSync.Host.Tests`: parser cases (begin / complete / redirect, validation errors);
  envelope mapping; completer flow mapping state-mismatch / expired / denied → correct exit codes
  (with fakes).
- Reuse existing auth/parse tests.

## Security considerations

- `code_verifier` persisted only between `--begin` and `--complete`, in the whole-file-encrypted
  DB, single active row, deleted on success, TTL ≈ 15 min, rejected if expired.
- `state` validated (anti-CSRF) on complete — unchanged guarantee.
- Envelope and logs never contain tokens, verifier, or secrets.
