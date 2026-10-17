# ADR-028 — Recurring receipt identity and owner recovery

**Status:** Accepted
**Date:** 2026-10-17
**Extends:** ADR-026 and ADR-027.

## Decision

Store the ordered recorded item amounts on each new occurrence receipt, using invariant decimal formatting. Compare explicit item amounts against that immutable snapshot on retry, not against the current template or editable ledger. Equal totals with different allocations conflict. Old receipts cannot be reconstructed safely from mutable history: explicit per-item retries against receipts without a snapshot conflict; legacy scalar retries keep their existing total comparison.

The 100 unfinished schedule limit counts only non-stopped rules with a next occurrence, including paused and blocked rules. Creating a rule or extending a completed rule into an unfinished state checks the limit under the household lock.

Creators retain their own schedule templates after losing account visibility. The list does not read hidden account information and returns an empty currency with canRecord=false. The UI hides amounts, account details, editing and history, and provides pause/stop controls only. Posting, resuming and history still require the existing account authorization. This does not grant access to another member's schedules.

## Migration

Apply SnapshotRecurringOccurrenceAmounts before deploying the API/worker. It adds a nullable receipt snapshot without changing old receipts or financial history. No shared database migration is executed by this change.
