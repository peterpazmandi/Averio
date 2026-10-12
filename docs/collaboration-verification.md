# Collaboration verification and polish

Phase three verifies the merged household settings and invitation cutover through
the actual browser, cookie authentication, CSRF middleware, controllers, handlers,
and PostgreSQL. It does not change the membership or retained-history policy in
[ADR-024](decisions/ADR-024-invitation-cutover-and-retained-history.md).

## Automated verification

`tests/Averio.Collaboration.Host` starts the existing API with
`WebApplicationFactory<Program>` and Kestrel. It creates a uniquely named database,
applies the **entire migration chain** with `MigrateAsync`, seeds real Identity
passwords and financial records, and removes the database on normal shutdown.
Only email senders and application time are replaced. Cookie authentication, CSRF,
authorization, rate limiting, persistence and frontend API requests remain real.
The loopback control server and captured inbox exist only in this test executable;
the production API has no fixture endpoints or authentication bypasses.

`tests/collaboration/run.mjs` uses Playwright with installed Google Chrome and
separate owner, new partner and existing member browser contexts. It verifies:

- Owner invitation controls, delivery failure/retry and revoked/expired/rotated links.
  Opening a replacement link in the same tab captures the new hash credential and
  resets the prior review and consent, even when the page stays mounted.
- Wrong-email rejection and mandatory CSRF, registration and email verification.
- Invitation-first onboarding, explicit consent, immediate destination membership
  and Member permissions, and no empty previous-households card.
- Private accounts, transactions and activities remain inaccessible, including
  guessed IDs, search, financial projections, dashboard and write attempts. Transfers
  expose only the permitted leg; a hidden counterparty and note stay redacted, and
  a private account cannot be used as a filter or write target.
- A holder and another member see different tracked balances when private entries
  exist. Household reports exclude private entries and keep currencies separate.
- Existing accounts are copied with reviewed opening balances, without old ledger
  entries; household-owned accounts stay behind and cash flow does not gain income
  from copied balances. Source access closes, personal history remains readable,
  and another member cannot fetch that snapshot.
- Simultaneous repeated acceptances create no duplicate account copies.
- EN/DE/HU household and balance states on desktop/light and mobile/dark, without
  overflow, and same-tab logout/login with a different identity.

The existing PostgreSQL suite additionally verifies concurrent first acceptance,
stale departure/account versions, owner succession, preserved source cash flow,
retained-history immutability, transaction/transfer/category writes racing with
acceptance, invitation cooldown and successful-owner resend attribution. Browser
coverage complements these cases; it does not replace them.

Frontend regressions use real TanStack Query observers to verify complete private
query/mutation cleanup, cancellation and delayed responses, reattachment-free
identity refresh, and CSRF requests resolving across identity changes in both
orders. Old actions are cancelled instead of being given the new identity's token;
new actions share the replacement request. An initial anonymous session check
does not cancel public actions such as email verification; expiration of a known
session still cancels them. Financial cache removal does not rely
on an enumerated list of features.

## Running locally

Use a disposable local PostgreSQL 17 server with permission to create/drop databases,
.NET 10, Node 24 or later, and installed Google Chrome. Never use production
credentials. API, frontend and control ports 5180, 5173 and 5181 must be free.

```sh
export AVERIO_TEST_CONNECTION_STRING='Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=postgres'
dotnet restore Averio.slnx
dotnet build Averio.slnx --configuration Release --no-restore
cd apps/averio-web
npm ci
npm run build
npm run test:collaboration
```

PowerShell users can set the connection with
`$env:AVERIO_TEST_CONNECTION_STRING = 'Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=postgres'`.
The script starts/stops the servers itself. CI runs the same script after frontend
build/lint and before the generated-client consistency check. Failure screenshots,
Playwright traces and server logs are saved in `test-results/collaboration` and
uploaded on failure. These fixtures contain synthetic addresses/tokens only.
After a forced process termination, remove only the abandoned `averio_browser_*`
test database identified in the test server logs.

## Balance wording

The dashboard says “Household balances visible to you”; account cards say “Tracked
balance.” Both explain that opening balances and visible recorded entries determine
the displayed amount. Another member can see a different amount. These are not
bank-confirmed balances, and original currencies are never combined. The wording
is general and does not reveal whether a particular account contains hidden data.

## Deployment and provider smoke record

This phase adds no migration or API contract. A deployment must still explicitly
apply every pending migration through `20261009165339_AddHouseholdDepartureAndHistory`
before starting the cutover-enabled API; startup does not migrate automatically.
Follow the [invitation deployment guide](household-invitations.md).

The automated captured inbox proves the application's authentication and acceptance
flow. Actual Brevo delivery, Gmail/Outlook rendering and production HTTPS/cookie
configuration need a separate staging smoke with authorized test recipients. Do not
record these checks as passed based on the simulated sender or Chrome email layout.
Record this evidence for the exact release candidate:

| Check | Evidence to record |
| --- | --- |
| Release | Commit SHA, environment, frontend/API versions, CI run URL |
| Database | Starting migration, complete applied chain, final migration ID |
| New member | Invite → registration → verification → consent → correct active household |
| Existing member | Reviewed copies, old ledger absent in destination, source/snapshot retained |
| Privacy | Two real sessions; private IDs, activity, dashboard and statistics checks |
| Session | Logout/login as another user; reload; no prior private/history data |
| Email | Brevo event/message reference; Gmail light/dark and Outlook web/app captures; logo, link and expired link |
| Locale/layout | EN/DE/HU on desktop/mobile, light/dark, readable wrapping and button spacing |

Leave a check explicitly **not run** until its evidence exists. This implementation
and PR do not deploy, migrate a shared database, or send provider email.

## Refresh and development startup regression

The browser suite also checks authenticated cold loads and browser reloads on `/app`, `/app/accounts`, `/app/statistics`, and `/`, plus signed-out redirects to login. It delays the real `/auth/me` response to exercise startup overlap. After the production checks it restarts the frontend as a Vite development server on the same test origin and repeats these scenarios with React StrictMode enabled.

The current-user query is shared by the auth observer and imperative route guards. It deliberately does not consume the observer abort signal: a temporary observer unmount must not cancel a guard's pending session lookup. Explicit logout/session-cache cleanup still cancels query waiters and discards late results. The session-isolation regression tests verify both behaviors.
