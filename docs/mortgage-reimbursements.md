# Recurring mortgage reimbursements

German UI: **Kostenerstattungen**. Hungarian UI: **Költségtérítések**. These are expense-coverage claims, distinct from received expense refunds; see [money-back terminology](ui/money-back-terminology.md).

The first two reimbursement deliverables add optional estimates to expense schedules and manual external-submission tracking. They do not create incoming transactions, transfers, payment allocations, or changes to account balances or financial aggregates. Received-payment reconciliation now links existing bank movements and tracks partially/fully received statuses; see the settlement section below.

## Workflow and privacy

Enable reimbursement tracking in the shared transaction/recurring editor. Choose the provider, fixed amount or percentage, optional cap, same-currency destination account, estimated payment delay (0–365 days), and optional submission reminder (0–365 days after recording). Estimates cannot exceed the recorded expense. Amounts retain four decimal places. The percentage uses the actual recorded item total, not the template total.

Saving a rule or reaching a scheduled date does not create a claim. Recording an occurrence creates the expense, occurrence receipt and one claim in the same database transaction. Opt-in automatic expense recording uses this same path. A skipped occurrence creates neither an expense nor a claim. Retrying an occurrence returns the previous transaction; household locking and a unique claim transaction index prevent duplicate claims. Failed postings roll back all three records. No historical occurrences are backfilled.

Claims snapshot provider, amount, currency, destination account and timing. Changes to a rule affect future recordings only. Editing the source payment does not silently rewrite the snapshot: the UI asks the user to review the changed payment. Voided payments and payments changed to income suppress expected amounts and reminders and prohibit submission while keeping history available for cancellation.

Claims are private to the recurring-rule creator, with additional server-side checks for visibility of both the source transaction and destination account. Modifications require current permission to modify the source account. Household administrators do not gain access to another member's private claim. Household departure and account ownership/visibility changes do not copy or reveal claims.

## Manual submission and reminders

- **Needs submission:** potential reimbursement only; expected and outstanding amounts are zero.
- **Submitted:** the user explicitly confirms external submission, supplies a date from the original payment date through today (UTC), and optionally a reference of up to 160 characters. Expected date is submission date plus the snapshotted delay.
- **Rejected:** only from Submitted; expected/outstanding amounts return to zero. A new explicitly confirmed submission can reopen it.
- **Cancelled:** terminal; financial transactions and append-only claim events remain intact.

Every state change records the actor, status, submission date/reference and timestamp. Optimistic version checks reject stale updates. Scanning a receipt does not change claim status. There is no provider upload or automated receipt submission.

Dashboard and transaction detail cards show original/potential/expected amounts separately. Currency totals are never combined. In-app reminders appear when an unsubmitted claim reaches its reminder date or a submitted claim passes its expected date. They send no email or push notification, and never advance payment status. Received/linked and outstanding totals now reflect active payment matches; full receipt stops the overdue reminder.

## API and deployment

Recurring save/list contracts expose optional `reimbursement` configuration. `GET /api/v1/households/{householdId}/reimbursements` lists visible active claims; `transactionId` includes that payment's cancelled history too. `PUT /api/v1/households/{householdId}/reimbursements/{claimId}/status` requires the current version and explicit receipt consent for Submitted.

Apply migration `20261117220912_AddMortgageReimbursements` before deploying API, worker and frontend. It creates configuration, claim and audit-event tables without rewriting financial records. Normal application startup does not run migrations. Review the deployment SQL and preserve claim history before any rollback; migration Down removes these new tables.

Verification: the existing transaction and PostgreSQL test executables include reimbursement cases. The collaboration browser suite includes `reimbursements.mjs`, covering settings, server/client consent, retry idempotency, unchanged account balances, dashboard/details and translated mobile/desktop layouts. Local PostgreSQL tests create and drop isolated databases; use only a dedicated test server.

## Creating a schedule

New recurring first offers Recurring expense, Recurring income, or Expense with reimbursement. These are form presets over the existing recurring model. The reimbursement preset selects Expense and enables claim tracking; it never represents automatic incoming income. The ordinary transaction editor can still enable tracking for an expense.

Reimbursement settings group provider/destination, calculation/amount with an exact-decimal potential estimate, and payment/submission reminder timing. The optional cap is expandable, and reminders have an explicit on/off checkbox. Shared InfoPopover controls hold optional explanations; external submission and its financial consequence remain visible. EN/DE/HU, responsive layouts and dark mode use the shared Tailwind components.

## Manual claims on ordinary expenses

An ordinary new expense can enable Track reimbursement using the same settings. The expense, aggregates and claim save in one database transaction; failed reimbursement validation rolls all of them back. Receipt scanning does not silently enable tracking. Once a scanned/imported expense is recorded, it can use the same action in transaction details.

For an existing posted, unarchived expense, Track reimbursement opens a separate claim form based on the saved expense snapshot. Unsaved transaction edits must be saved first. POST /api/v1/households/{householdId}/reimbursements accepts the source transaction ID, expected transaction timestamp and reimbursement configuration. Permissions are checked on the source account, and the destination must be visible, active and in the same currency. The claim belongs to the creator and has no recurring rule. Income, voided/archived expenses and inaccessible records cannot create claims.

There is one claim per source payment, including recurring and cancelled history. An identical retry by the same creator returns its original claim, while different configuration or another creator receives a generic conflict without claim details. The existing mandatory submission confirmation, reminders, history and account visibility rules apply. Claim creation and submission never create incoming transactions or change financial totals.

Apply 20261118130622_AddManualReimbursementClaims after the original reimbursement migration. It permits a null recurring-rule association and adds the private creation fingerprint for duplicate protection. Rollback refuses to proceed while manual claims exist, preserving history for deliberate resolution.

## Transaction overview and edit action

The existing-expense tracking action appears in a compact reimbursement card above the edit fields. New transaction settings use the same heading weight as the other form sections. Reimbursement cap is an optional per-claim ceiling applied after fixed/percentage calculation; for example, 20% of HUF 150,000 estimates HUF 30,000, but a HUF 25,000 cap limits it to HUF 25,000. The shared information popover beside the cap field explains this; the estimate can never exceed the source expense.

Transaction responses include a nullable reimbursement status. The list loads statuses in one batch for the current transaction page and applies the same creator/source/destination visibility checks as claim details. Badges include needs submission, submitted, rejected and cancelled history; a shared expense never reveals another member's private claim through its badge. Claim creation and status changes refresh transaction queries so overview badges stay current.

## Editing an existing claim

Edit reimbursement is available for visible, modifiable claims on posted expenses until the claim is cancelled. It reuses the reimbursement form to change provider, the occurrence estimate, destination account, payment delay and submission-reminder setting. The saved occurrence estimate opens as a fixed amount; percentage/cap calculations can be applied against the original payment snapshot. Changes affect only this claim, never its recurring rule or original transaction.

PUT /api/v1/households/{householdId}/reimbursements/{claimId} requires the current claim version and rechecks source permissions, status and destination currency/visibility. Submitted date/reference and status remain intact; changing the delay recalculates the estimated payment date from the saved submission date. Reminder changes remain anchored to claim creation. Cancelled claims cannot be edited.

Each edit appends before/after snapshots to claim history with provider, amount, destination and timing; old events are not rewritten or falsely backfilled. Apply 20261118140924_ReimbursementEditHistory before deploying. It adds nullable event snapshot fields and stable ordering for new events, preserving legacy history. Rolling it back discards those added audit details, so preserve them first.

The follow-up form uses two matching panels with aligned desktop inputs and a stacked mobile layout.

The shared provider information icon explains who pays the reimbursement (for example, an employer, insurer, fund or retailer). Expected payment has its own information icon explaining the delay from confirmed external receipt submission and that an estimated date never records received money. Both helpers are available in English, German and Hungarian.

Manual creation and individual claim edits reject a fixed estimate above the original recorded expense, even if an optional cap would reduce it. The form shows the expense limit beside the estimate and validates exact amounts to four decimal places; an estimate equal to the expense is allowed. Backend validation enforces the same rule. Recurring configuration remains a template: its generated claim is always capped at the actual recorded payment.

## Providers and received refunds

Reimbursements are not limited to health funds. The receipt instructions, provider help and external-submission confirmation use general wording for the organisation or person paying the money back. Submission may happen through any process that provider accepts; adding or scanning a receipt in Averio never submits it externally.

Received merchant refunds now use the item-refund workflow in [Item refunds](item-refunds.md) and [ADR-048](decisions/ADR-048-item-refunds-reduce-expenses.md). Select one original receipt item and either record received money or explicitly reclassify an existing credit. The item's expense and category totals decrease; the returned amount is a Refund, never income. Multiple item refunds are supported, with exact item-level caps and retained history. Expected third-party claims and their external-submission confirmation remain separate from received refunds.

## Receive and reconcile payments

After confirming external submission, choose **Link received payment**. Search for a recorded Income credit in the destination account and currency, select it, and confirm that it was received for this claim. Missing income must first be recorded under Transactions. The full credit must fit the remaining claim and current net expense. Several credits can settle one claim; a pooled credit cannot yet be divided across claims.

The claim shows received and outstanding amounts, **Partially received** or **Fully received**, and retained payment history. Linking changes claim coverage only: the expense, income, bank balances, activity statistics and Personal Money remain unchanged. Reimbursements do not use merchant-refund expense reduction.

Use **Unlink payment** to correct a match. It reopens the outstanding amount while preserving the Income credit and the old link in history. Unlink first before cancelling a claim, editing/voiding linked transactions, or refunding the source expense. The estimate cannot be reduced below received money, and the destination cannot change while receipts remain linked. Import undo protects historical claims and links even after unlinking.

A lost response can be retried with the same operation; the UI retains the original request. Before closing and starting a different match, refresh and inspect saved history. The claim remains private to its creator and is hidden when any referenced source/destination/credit becomes inaccessible. See ADR-052 for concurrency, migration, privacy and accounting rules.

Apply migration AddReimbursementPayments using the epoch-2 deployment images. Existing claims start with no matches; no credits are guessed or automatically backfilled. Preserve a backup; downgrade refuses to discard payment history.
