# ADR-025 — Recurring income and expenses

**Status:** Accepted
**Date:** 2026-10-15

## Decision

Recurring rules are member-owned automation instructions, private to their creator. Posted transactions follow the existing account/transaction visibility rules. A household owner cannot inspect another member's rules. The creator must still be an active member and able to modify the source account each time a transaction is posted. Leaving a household does not copy or execute its rules in the destination.

The first slice supports one categorized amount, income or expense, weekly/monthly/yearly cadence, optional inclusive end date, pause/resume and permanent stop. Account, initial date and cadence are immutable; replace a stopped rule to change these. New rules start today (UTC) or later. Confirmation is the default; fixed amounts may opt into automatic recording. A confirmation can use the actual amount for that occurrence only. Transfers, goals and personal-money allocations require their own semantics and are not modeled as expenses here.

Monthly and yearly dates are derived from the original anchor and occurrence index. Missing days clamp to month end without drifting the next occurrence (January 31 → February end → March 31; leap-day yearly rules return to February 29 in leap years). Calendar boundaries are UTC. Pending occurrences do not affect balances or reports.

Posting or skipping consumes one occurrence identified by rule and due date. A unique database key and household serialization protect retries and concurrent workers. Ledger creation, aggregates, occurrence receipt and schedule advancement commit atomically. Voiding an emitted transaction never reopens its occurrence. Rule edits affect only unprocessed occurrences; posted ledger entries remain editable through normal transaction flows. Resume retains missed occurrences and processes them oldest first; the UI explains catch-up before saving automatic mode.

A separate .NET Worker polls due automatic rules, using existing application services. It processes a bounded batch, one transaction per occurrence, and uses database state rather than an in-memory schedule. Multiple instances are safe. Expected authorization/reference failures block the rule with a non-sensitive status until explicitly edited; infrastructure failures retry on later polls. No public execution endpoint, production startup migrations, external scheduler, or new runtime package is introduced.

## Consequences

Rules and occurrence receipts are retained. User requests carry the original rule version and due date, preventing stale edits or a double click from consuming the next occurrence. Manual confirmation and worker execution use the same ledger posting implementation. Account privacy, archived categories, closed/cutover accounts and changed membership are revalidated at execution.

Basic recurring tracking has no billing gate. Automatic posting records transactions only; it never moves money at a bank. Durable notifications, inferred patterns, itemized templates and advanced scheduling are later scope.
