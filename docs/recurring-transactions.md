# Recurring transactions

Recurring income and expenses use confirmation by default. Open **Add transaction**, select **Recurring transaction**, fill in the normal transaction details, and choose a schedule name and daily, weekly, monthly or yearly repetition. Saving creates the schedule only; confirmation remains the default for the first due occurrence. The **New recurring** action opens the same form with recurrence enabled. Edit schedules from **Recurring transactions**. Each due occurrence can be reviewed with actual amounts for each item, recorded or permanently skipped. Pending items do not affect balances. Recent occurrences link to the normal transaction editor.

Automatic recording is an explicit option for fixed amounts. It records ledger entries; it never makes bank payments. Pause retains missed dates. Resume catches up oldest first. Stop is permanent and retains existing transactions and receipts. Editing items, labels, merchant, note or activity affects unprocessed occurrences only. To change account, type, cadence or first date, stop and replace the schedule.

Dates use UTC. The original date anchors every occurrence, including January 31 and leap-day schedules. End dates are inclusive. New schedules cannot start before today. Rules are private to their creator; emitted transactions use normal account and transaction visibility. Household departure does not copy rules. Every execution rechecks membership, account access/status and category eligibility. Active members can pause or stop their own schedules even after losing posting permission. Resuming always revalidates that permission. If account privacy changes, the editor allows the schedule visibility to be corrected explicitly.

Schedules repeat all transaction details: up to 100 ordered items with their own descriptions/categories/amounts, up to 20 labels, merchant, note and the creator’s private activity. Closed activities and invalid references block recording without consuming the due date. The feature supports 100 unfinished schedules per member, and the latest 24 visible occurrence receipts. Transfers, allocations, notifications and custom intervals are outside this slice.

## Deployment

1. Back up the intended database and review the generated SQL. EF tooling now requires an explicit **AVERIO_MIGRATION_CONNECTION_STRING**; it never falls back to application user secrets. For scaffold/script-only commands use a localhost placeholder. For applying migrations set it deliberately to the intended target.
2. Generate/review the script from AddSavedTransactionViews to AddDailyRecurringFrequency, then apply AddRecurringTransactions, ExtendRecurringTemplates and AddDailyRecurringFrequency before running the new API or worker. ExtendRecurringTemplates preserves existing schedules by backfilling their single item; do not remove or reapply the earlier migration. AddDailyRecurringFrequency permits the new daily option without altering existing schedules. There are no startup migrations.
3. Deploy the API and frontend together, then run the separate **apps/Averio.Worker** executable with **ConnectionStrings__AverioDatabase** configured to the same database. The worker needs no web listener. Use a process supervisor to restart it after failure.
4. Check one manual confirmation and one opt-in automatic schedule in staging. Confirm a single transaction, correct balance/report changes and advancement. Stop the worker and verify manual review still works; restart and verify missed automatic dates catch up.

Example PowerShell for generating SQL without a live database:

```powershell
$env:AVERIO_MIGRATION_CONNECTION_STRING = 'Host=localhost;Port=55439;Database=averio_migration;Username=postgres;Password=placeholder'
dotnet ef migrations script 20261013210249_AddSavedTransactionViews 20261016214223_AddDailyRecurringFrequency --project src/Averio.Infrastructure --startup-project apps/Averio.Api --output recurring.sql
```

Publish the worker with `dotnet publish apps/Averio.Worker -c Release -o artifacts/worker`, then run `dotnet artifacts/worker/Averio.Worker.dll` in a supervised service with runtime configuration injected. Do not put connection strings in the repository or command history.

The worker checks at startup and approximately every minute, up to 100 candidate rules per pass and one due occurrence per rule. Larger backlogs drain over subsequent passes. Multiple worker instances and simultaneous manual confirmation are serialized by the existing household lock and receipt key. With the worker offline, automatic dates remain pending until it returns. No exact-time delivery guarantee is made.

Expected validation/access failures mark a schedule **Needs review** without consuming the occurrence. Correct the account/category, edit and save the schedule to clear the blocked state. Infrastructure failures are retried on a later traversal. The worker keeps an in-memory cursor across batches, moves past failed rules and wraps after reaching the end; restarting resets the cursor. This prevents a permanently failing first batch from starving later schedules. Monitor worker errors and process availability. All amounts stay in account currency; no conversion is introduced.

## Verification

The transaction suite covers anchored calendar dates and lifecycle invariants. The PostgreSQL suite covers migration/model consistency, exact-money posting and aggregates, concurrent retries/workers, private schedules, stale versions, skip/void behavior and archived-account failures. The real-API browser suite covers creation, confirmation, history links, pause, stale drafts, skip, CSRF and EN/DE/HU desktop/mobile light/dark views.

Run the repository's existing verification workflow. PostgreSQL and browser tests create and clean up dedicated local test databases; never point **AVERIO_TEST_CONNECTION_STRING** at a shared environment.

See [ADR-027](decisions/ADR-027-daily-recurring-frequency.md) for daily scheduling and [ADR-026](decisions/ADR-026-recurring-transaction-templates.md) for the stable domain decisions.
