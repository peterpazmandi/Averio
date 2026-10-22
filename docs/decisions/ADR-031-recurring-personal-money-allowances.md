# ADR-031 — Monthly Recurring Personal Money

**Status:** Accepted for PR 3 implementation
**Date:** 2026-10-20
**Builds on:** ADR-029, ADR-030; complements but does not replace ADR-026/027/028 for recurring bank transactions.

## Decision

A monthly Personal Money schedule is an **owner-approved promise to grant spending entitlement**, never a payment, virtual bank account, private-balance proxy or recurring transfer. It creates a single signed, append-only `Grant` event in the recipient membership's immutable-currency wallet for each due month. Granting never changes attributed private funding, account balances, transfers, categorized expenses or income aggregates. Existing manual grants/funding are not replayed or automatically matched.

The household Owner alone may create/edit/pause/resume/stop a schedule for an active member, including themselves. The recipient may inspect their own schedule and grant receipts, but cannot edit; a different member cannot inspect a recipient's schedule, wallet, private transactions or linked spending. The Owner may inspect their own configured schedule definitions and scheduled grant **receipts**, which the Owner already authorized, but never the recipient's private purchases or full wallet history.

## Calendar and lifecycle

- **Monthly only** in PR 3. A schedule specifies first due date, positive amount and currency, and optional inclusive last date.
- Dates are UTC calendar dates (not a promise about exact clock time). Each month's due date is computed from the **original anchor**, so Jan 31 → Feb 28 → Mar 31; leap February is Feb 29.
- New schedules begin today or later. Edits change the amount and optional end date **only for future unposted occurrences**. Recipient, currency and anchor stay immutable.
- Pausing preserves the current due occurrence. Resuming processes missed dates sequentially; pausing is not skipping or cancelling an entitlement.
- Stopping is irreversible, preserves issued grants and removes the next due occurrence. End dates automatically complete the schedule. To change its identity, stop and create a new schedule.
- If the worker is offline, due grants catch up after restart, using their original due date as accounting date. Negative and unused entitlement carry over, with no expiry.

## Idempotency and integrity

- Creating a schedule requires an operation ID unique within its household; retrying an identical payload returns the same schedule. Conflicting reuse fails.
- A recurring grant ledger entry records its rule ID, and PostgreSQL uniquely indexes **(rule ID, occurrence date)** for non-null rule IDs. This is the idempotent receipt; no synthetic bank transaction is created.
- Each grant and schedule advancement commit in **the same PostgreSQL transaction** behind the existing household row lock. Concurrent workers and retries re-read the version and cannot double-credit.
- Each update requires the current schedule version; stale updates fail.
- A worker queries up to 100 due rules in date/ID order with a rotating cursor. An invalid rule is marked **Needs review** rather than starving other rules. It resumes only after the owner edits and saves it.
- The recipient's membership must still be active in the same household and the household must still be financial-onboarded. Departed/archived households are never granted. Wallet currency mismatches are blocked; no conversion occurs.
- The worker runs alongside existing recurring bank transactions but uses a **separate repository and handler** to prevent coupling or accidental cash movement.

## UI / privacy

Show a small upcoming-grants panel in My allowance and a separate owner-only **Monthly Personal Money** management card within Owner allowance management. Include recipient, amount, currency, first date, optional end, next date, pause/resume/stop, blocked/completed statuses and a compact grant-only history. Follow Averio's Tailwind cards, accessible touch targets and responsive layout, with EN/DE/HU translations.

## Deferred

- Automatic bank transfers and automatic funding/surrender are **not** part of recurring grants; physical transfers remain manually recorded as in PR 1/2.
- Weekly/annual patterns, per-member multiple currencies, per-month custom amounts, automatic inflation increases, arbitrary skipped months and local-time exact-hour scheduling are deferred.
- Recipient notifications and a full audit-of-changes trail can be added in later releases; existing grant events remain immutable.
