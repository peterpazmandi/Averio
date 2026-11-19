# ADR-047: Explicit reimbursement edits retain claim history

Status: Accepted

Date: 2026-11-18

## Decision

Allow explicit user edits to an existing open reimbursement claim. This extends ADR-045/046: automatic rule changes still never rewrite past claim estimates. A claim edit changes only its provider, occurrence estimate, destination and timing; it preserves the source payment snapshot, recurring rule, submission date/reference and current status.

Require the claim version and current permission checks. Cancelled claims are read-only. Record before/after audit snapshots for each edit, including older claims whose existing events lack snapshots. Preserve old events without inventing historical values. New events have a sequence for deterministic order when timestamps are equal.

## Consequences

Users can correct tracking details without cancelling or recreating a claim. A submitted claim's expected date follows its saved submission date and edited delay. Neither edits nor audit events create incoming transactions, write financial aggregates or change bank balances. The audit migration must precede API deployment.
