# PR 3 — Monthly Recurring Personal Money

See [ADR-031](decisions/ADR-031-recurring-personal-money-allowances.md), [Personal Money Foundation](personal-money.md) and [Shared Spending](personal-money-reconciliation.md).

## What it does

A household Owner can arrange a **monthly entitlement grant** for an active household member (including the Owner). This is **not** a bank transfer, recurring transaction, funding return or settlement. One grant increments available entitlement; attributed private funding does not change. Existing unspent entitlement persists indefinitely, including negative balances.

A rule is defined by member, currency, positive amount, first date, optional inclusive end date and status. A rule's recipient, currency and start anchor do not change once saved; stop it and create a new rule to change those. The Owner can edit amount/end date, pause/resume or permanently stop the rule. The recipient can see **only their own schedule** and its grant receipts.

## Monthly behavior

- All dates are UTC date-only, processed by `Averio.Worker` approximately once a minute, without an exact-time SLA.
- Recurrence is anchored to the first date: Jan 31 → Feb 28 → Mar 31 → Apr 30. Leap years are handled by DateOnly.AddMonths.
- The worker issues one grant per due date in chronological order. Backlogs after downtime run during subsequent batches; failed jobs do not starve later rules.
- Pausing freezes due processing, but does not skip anything. Resuming replays overdue months. Stopping is permanent; completed rules have no next due date.
- Amount edits affect future **unposted** occurrences only; previously granted ledger entries never change.
- Schedules never create bank transactions or transfers and never touch categorized cash-flow aggregates.
- Existing funded allowances stay funded only as explicitly recorded through transfers; this schedule does not automatically fund them.

## API

- `GET /api/v1/households/{householdId}/personal-money/schedules`: Owner sees rule definitions for the household, other active members only their own schedules.
- `POST /api/v1/households/{householdId}/personal-money/schedules`: Owner only. Requires RecipientMemberId, Currency, Amount, StartsOn, EndsOn(optional), OperationId.
- `PUT /api/v1/households/{householdId}/personal-money/schedules/{scheduleId}`: Owner only. Amount, EndsOn, IsPaused, IsStopped, ExpectedVersion. Returns 204 on success.
- `GET /api/v1/households/{householdId}/personal-money/schedules/{scheduleId}/history`: Owner or recipient; **only scheduled grant receipts**, no private spending or other wallet events.

Amounts are decimal strings to prevent floating-point serialization errors. All actions recheck household membership, Owner role, currency and concurrency. The create operation ID allows retry after a timeout without creating a second schedule. Stale optimistic versions return a conflict, and deleted/departed membership is never posted to.

## Database and recovery

New `recurring_allowance_rules` table stores status, current version and due cursor. The existing `personal_money_entries` table gains a nullable `recurring_allowance_rule_id` foreign key. PostgreSQL uniquely constrains `(recurring_allowance_rule_id, occurred_on)` when the rule ID is non-null. The successful grant and rule advance are committed together under the existing household row lock; duplicate worker retries cannot credit an entitlement twice.

An invalid rule is marked `Needs review` and will not fire until the owner corrects it; transient failures are retried. Pausing or stopping never deletes issued grants, and old-household history remains bound to the original membership. No automatic operation is processed for departed recipients or archived households.

## Deployment

Apply the new EF migration before starting the updated API and worker. Deploy the updated **Averio.Worker** alongside the API, configured with `ConnectionStrings__AverioDatabase`; no public worker HTTP port is needed. The worker dependency graph verification must resolve both the existing `RecurringHandler` and `RecurringAllowanceHandler`.

Run:
```bash
dotnet restore Averio.slnx
dotnet build Averio.slnx -c Release
dotnet run --project tests/Averio.Transactions.Tests -c Release --no-build
dotnet run --project tests/Averio.Persistence.Tests -c Release --no-build
cd apps/averio-web
npm ci
npm run build
npm run lint
```

Use a disposable PostgreSQL database via `AVERIO_TEST_CONNECTION_STRING` for the persistence suite. CI must also validate the generated OpenAPI client and browser flows.

## Troubleshooting: scheduled allowances return HTTP 500 or 503

The PR 3 API **does not run EF migrations when it starts**. Running a newer API
against an older PR 2 database can fail for both `GET .../personal-money/schedules`
and `POST .../personal-money/schedules`, because the
`recurring_allowance_rules` table and the new ledger columns do not yet exist.

If PostgreSQL reports `42P01: relation "recurring_allowance_rules" does not exist`,
the API now returns RFC 7807 **503** with
`code: recurring_allowance_database_update_required` rather than a generic
500. The frontend shows the localized database-readiness message and disables
creating new schedules until the list endpoint is healthy. This targeted error
mapping is not a substitute for applying the actual migration.

For a **local development database**, from the repository root in PowerShell:

```powershell
# Use the exact PostgreSQL connection used by your locally running Averio API.
# Do not commit or paste a real connection string or password into chat/logs.
$env:AVERIO_MIGRATION_CONNECTION_STRING = $env:ConnectionStrings__AverioDatabase
if ([string]::IsNullOrWhiteSpace($env:AVERIO_MIGRATION_CONNECTION_STRING)) {
    throw 'Set AVERIO_MIGRATION_CONNECTION_STRING to your local API PostgreSQL connection string first.'
}
dotnet tool install --global dotnet-ef --version 10.0.11
dotnet ef migrations list --project src/Averio.Infrastructure --startup-project apps/Averio.Api
dotnet ef database update --project src/Averio.Infrastructure --startup-project apps/Averio.Api
```

Check that `20261020154408_AddRecurringPersonalMoney` is in the applied list
(its exact identifier is shown by `dotnet ef migrations list`). Restart the
API and worker as necessary, and use the Retry action on the monthly allowances
card. The initial `GET schedules` should then return HTTP 200 and an array
(possibly empty); `POST schedules` should subsequently succeed with a
valid Owner and active recipient.

**Production / shared database:** back up and review pending migrations, then
apply through the deployment's controlled migration process **before deploying
the new API/worker**. Do not turn on automatic startup migrations in production.
If the API continues returning 500 **after verifying the schema**, inspect the
server logs using the problem response's `traceId`: an HTTP 500 can have other
causes and is not necessarily a migration problem.

## Manual QA

Set a rule for the owner and a partner; verify active Owner-only controls and the partner's read-only upcoming grant. Check a rule starting tomorrow, then a due occurrence and a catch-up after the worker is temporarily stopped. Change the amount after the first grant: previous history stays unchanged, future grants use the new amount. Pause/resume and stop; confirm no additional grants while paused/stopped. Repeat POST with same OperationId; verify one rule. Confirm accounts, transfers, transactions and categorized statistics are unchanged.
