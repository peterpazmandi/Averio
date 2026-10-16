# ADR-026 — Full transaction templates for recurring schedules

**Status:** Accepted
**Date:** 2026-10-16
**Supersedes:** ADR-025. Its ownership, privacy, calendar, lifecycle, atomic posting, worker fairness and idempotency decisions remain in force as restated below.

## Decision

Create recurring schedules through the ordinary transaction popup using a recurring checkbox. Both modes share account, income/expense type, item descriptions/categories/exact amounts, labels, merchant, note, activity and transaction visibility. The recurring overview remains the maintenance entry point; its create action opens the same popup with recurrence enabled. Schedule name and weekly/monthly/yearly timing supplement the transaction fields.

Saving creates only a schedule, never an immediate standalone transaction. Confirmation is the default, including the first due occurrence. Explicit automatic mode allows the worker to record due occurrences without manual confirmation. Each manual confirmation can override each item's amount for that occurrence only; no proportional rounding or category allocation is inferred from a changed total.

Templates contain 1–100 ordered items and up to 20 labels. Each recorded occurrence uses the template's merchant, note, labels and creator-owned activity. Every posting rechecks all references and permissions through the existing transaction handler, within the same transaction as aggregates, occurrence receipt and advancement. An archived category, invalid label or closed activity prevents posting without consuming the date. Private activity assignment belongs to the schedule creator, including worker execution.

Schedules remain private to their creator. Posted transactions follow account/transaction visibility. Active membership and rule ownership authorize suspension; posting permission and reference eligibility are required for reactivation. Schedule edits affect unprocessed occurrences only. Account, kind, cadence and first date stay fixed after creation. Stop is permanent; pause retains missed dates. UTC anchored dates, inclusive end dates and month-end/leap-day handling are unchanged. A unique receipt and household serialization prevent double posting; voiding never reopens an occurrence.

The bounded worker continues fair date/ID traversal with retry after wrap. No bank payments, transfers, new runtime dependency or startup migration is introduced. The 100 unfinished schedules per member and 24 recent visible receipts limits remain.

## Persistence and compatibility

ExtendRecurringTemplates adds ordered item and label tables plus merchant/note/activity fields. It backfills every existing schedule into one item using its original name/category/amount, preserving its version, timing, status and receipts. Activity and label foreign keys retain household/member boundaries; category eligibility is validated in the application. Exact totals remain stored on the schedule for the overview.

Legacy single-item requests remain supported. Requests with item templates must supply matching total/first category; the server validates these instead of trusting duplicate summary fields. Legacy requests cannot overwrite an itemized schedule without supplying its items. Deploy the migration before the API/frontend/worker. Downgrading the migration loses the newly introduced template details; use a backup rather than downgrading a live environment.

## Consequences

One form prevents creation and maintenance from drifting apart. The ordinary transaction popup now uses shared Tailwind cards, form controls and the accessible dialog. Editing an already posted transaction changes that entry only; future occurrences are changed from Recurring transactions. No revenue gate is added to core financial correctness.
