# ADR-022 - Personal activity tracking

**Status:** Accepted  
**Date:** 2026-10-03

## Decision

Activities group related income and expenses across categories and accounts.
Each activity belongs to one household membership and is visible only to that
member. Household administration does not grant access. Shared activities are a
future extension requiring explicit sharing rules.

Each member can assign a visible transaction to one personal activity. Assignments
are separate from the shared ledger: other members cannot see or overwrite them.
Every read rechecks current account and transaction visibility before returning
assignment metadata, linked transactions or totals. Hidden records do not produce
counts or inferred totals. Visible private transactions can contribute to their
holder's personal result, independently of household reporting participation.

Income minus expenses is the net result, grouped by original currency. Only posted
transactions through today's UTC date contribute; future and voided entries remain
inspectable. Activity dates are descriptive, never implicit transaction filters.
Transfers never contribute. Refunds recorded as income offset costs in the net;
there is no separate refund classification in this slice.

Names, optional dates and Open/Closed status support optimistic concurrency.
Closing prevents new assignments, but existing ledger corrections and voids still
update totals. Reopen to add entries; there is no activity or ledger deletion.
Assignment versions are independent of ledger versions, and retained unassigned
rows prevent stale overwrites. Bulk assignment is atomic, limited to 100 entries.
Transaction create/edit can save personal assignment changes atomically; omitted
assignment input preserves existing metadata for compatibility with older clients.

## Boundaries

No FX conversion, expected reimbursement, partial allocation, shared activities,
budget or paid gate. Combined salary/reimbursement entries require separate ledger
entries to attribute only reimbursement. Core correctness and privacy remain free
of monetization gates; future planning and exports can be evaluated separately.

## Rollout

Apply AddActivities explicitly before deploying the API. No existing ledger rows,
account balances, category snapshots or reporting aggregates are rewritten.
