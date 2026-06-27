# Migrate Fitbit-sync to the Google Health API — design

**Date:** 2026-06-27
**Status:** Approved; proceeding to implementation (PoC already proved the path end-to-end).
**Source of truth:** GitHub clone `github.com/yury-opolev/fitbit-sync`.

## Why

The legacy Fitbit Web API is turned down **September 2026** and new Fitbit app registration is
closed; the replacement is the **Google Health API** (`health.googleapis.com`, REST v4, Google
OAuth 2.0). A live proof-of-concept on 2026-06-27 confirmed the full path works: Google OAuth →
access token (+ refresh token) → `GET /v4/users/me/dataTypes/steps/dataPoints` returned real
**Charge 6** step data.

## Verified facts (from the live API + docs)

- **OAuth:** Google OAuth 2.0. Authorize `https://accounts.google.com/o/oauth2/v2/auth` with
  `response_type=code`, `access_type=offline`, `prompt=consent`, `state`, and **no**
  `include_granted_scopes` (it would merge unrelated Gmail scopes and the Health API rejects a
  token carrying mail scopes — 403 `DISALLOWED_OAUTH_SCOPES`). Token exchange/refresh at
  `https://oauth2.googleapis.com/token` with `client_id`+`client_secret` (a confidential "web"
  client). Refresh tokens are **7-day** in Testing mode, long-lived once the app is **Published**.
- **Credentials:** a Google "web" OAuth client (`client_id`, `client_secret`, redirect
  `https://localhost:7654/callback`). Loaded from User Secrets / env — never committed.
- **Read scopes (all Restricted, prefix `https://www.googleapis.com/auth/googlehealth`):**
  `.activity_and_fitness.readonly`, `.health_metrics_and_measurements.readonly`, `.sleep.readonly`.
- **Read endpoint:** `GET https://health.googleapis.com/v4/users/me/dataTypes/{dataType}/dataPoints`
  `?pageSize=&filter=<AIP-160>` → `{ dataPoints: [ { dataSource{recordingMethod, device{displayName},
  platform}, <dataType>:{ … } } ], nextPageToken }`. Filters are per-dataType time ranges (interval
  `*.interval.civil_start_time`, daily summary `*.date`, sleep `sleep.interval.civil_end_time`).
- **`dataType` ids** are the `DataPoint` union field names: `steps` (verified), `heartRate`, `sleep`,
  `oxygenSaturation`/`dailyOxygenSaturation`, `heartRateVariability`/`dailyHeartRateVariability`,
  `activeZoneMinutes`, `distance`, `vo2Max`/`dailyVo2Max`/`runVo2Max`, `dailyRespiratoryRate`,
  `dailyRestingHeartRate`, temperature types, etc. Daily-summary ids are not directly listable —
  use the sample/interval type's `list`, or the `dataPoints:dailyRollUp` method, per metric.

## Approach (recommended, agreed)

Add a `FitbitSync.Providers.GoogleHealth` adapter behind the existing `IHealthDataProvider` port and
a provider-neutral OAuth abstraction; make **Google the active provider**; leave the Fitbit code
**dormant** (compiles, tested, a reference). Domain / Application / Persistence stay untouched. No
dual-library re-consent machinery (there are no existing Fitbit users to migrate).

## Components

### 1. Provider-neutral OAuth (`Slice A`)
- New Domain port `IAuthorizationService` (`Begin() → AuthorizationSession`,
  `CompleteAsync(session, returnedState, code, ct) → OAuthToken`) and a neutral
  `AuthorizationSession(Uri AuthorizeUrl, string State, string CodeVerifier)`.
- `FitbitAuthorizationService` implements it (rename `FitbitAuthorizationSession` → reuse the neutral
  type). `BeginLoginCommand` / `CompleteLoginCommand` + DI depend on `IAuthorizationService`. The
  existing `login --begin` / `login --complete` machinery is unchanged.

### 2. Google OAuth (`Slice B`) — `FitbitSync.Providers.GoogleHealth`
- `GoogleOAuthOptions` (ClientId, ClientSecret, RedirectUri, Scopes; AuthorizationEndpoint,
  TokenEndpoint defaulted).
- `GoogleAuthorizeUrlBuilder` — builds the authorize URL with `access_type=offline`,
  `prompt=consent`, `state`, PKCE challenge, **no** `include_granted_scopes`.
- `GoogleTokenClient` — `ExchangeCodeAsync` / `RefreshAsync` against the Google token endpoint
  (`client_secret` + form body; Google response includes `refresh_token`, `expires_in`).
- `GoogleAuthorizationService : IAuthorizationService` (Begin/CompleteAsync; persist via `ITokenStore`,
  audit via `IAuditTrail` — mirrors the Fitbit service).
- `GoogleRefreshingAccessTokenSource : IAccessTokenSource` (reuses `BearerTokenHandler`).

### 3. Google Health data provider (`Slice C`) — same project
- `GoogleHealthApiClient` — typed HttpClient (base `https://health.googleapis.com`, bearer via
  `BearerTokenHandler`); `ListDataPointsAsync(dataType, filter, ct)` with `nextPageToken` paging.
- `GoogleHealthDataTypeCatalog` — `MetricType` → (`dataType` id, read strategy: list vs dailyRollUp,
  filter member). Mirrors `FitbitEndpointCatalog`.
- DTOs for the `dataPoints` response + the per-type payloads, and per-metric **mappers** →
  `MetricSample` (value + timestamp + source). Metrics: HeartRate, Steps, Sleep, SpO2,
  BreathingRate, Hrv, Temperature, VO2Max, ActiveZoneMinutes (the 9 the Fitbit provider exposes).
- `GoogleHealthDataProvider : IHealthDataProvider` (`Source => "google"`, `Capabilities`, `FetchAsync`).

### 4. Composition root + config (`Slice D`)
- `HostServiceCollectionExtensions` registers the Google provider + Google auth (instead of Fitbit).
- New `Google:` config section (`ClientId`, `ClientSecret`, `RedirectUri`=`https://localhost:7654/callback`,
  `Scopes`) bound like `Fitbit:`; `HostConfigurationValidator` validates it. Secrets via User
  Secrets / env. `appsettings.json` carries only non-secret shape.

### 5. Docs (`Slice E`)
Update `AGENTS.md`, `docs/SETUP.md`, `README.md` for Google Cloud registration + the Google login
flow; note **publish-to-Production** for long-lived tokens; keep Fitbit steps as a historical note.

## Error handling
- Reuse the headless-login exit/error-code contract. Map Google token-exchange failures (HTTP
  non-2xx from the token endpoint) to `token_exchange_failed`; Health API non-2xx to the existing
  provider error/rate-limit handling.

## Testing (TDD, 0 warnings, warnings-as-errors)
- `GoogleAuthorizeUrlBuilder` (offline/consent/PKCE/state, no scope-merge), `GoogleTokenClient`
  request/response shaping (against a mock handler), `GoogleAuthorizationService` flow.
- `GoogleHealthDataTypeCatalog` and each metric **mapper** against captured/representative sample
  JSON (the verified `steps` shape + reference schemas). `GoogleHealthDataProvider.FetchAsync`.
- The existing Fitbit + headless-login suites stay green throughout.

## Honest unknowns (resolved during implementation)
- Exact per-metric `dataType` id + read strategy (list vs `dailyRollUp`) + filter member + payload
  field names — taken from the reference `DataPoint` type schemas, not guessed; flagged per metric
  if no clean equivalent exists.
- Whether some Fitbit metrics (e.g., skin temperature) map cleanly to a Google daily-summary type.

## Operational (user, not code)
Publish the OAuth consent app to **In Production** for long-lived refresh tokens (unverified apps
keep a 100-user cap, so a 1-user personal app avoids the third-party security review). Optionally use
a **dedicated** OAuth client rather than the Gmail-shared one to avoid the scope-merge footgun.
