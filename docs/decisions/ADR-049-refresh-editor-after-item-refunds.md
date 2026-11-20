# ADR-049: Refresh the open editor after item refunds

Status: Accepted

Date: 2026-11-20

## Context

ADR-048 invalidates the original editing session after a refund to prevent a stale draft from overwriting financial changes. Requiring users to close and reopen the expense makes continued editing unnecessarily difficult.

## Decision

Supersede only ADR-048's post-refund editor invalidation behavior. Keep its financial semantics and all other protections.

Require a clean transaction draft before adding, linking or reversing an item refund. After the successful write, reload the saved transaction and current member's Personal Money. Replace the clean form values, baseline and all concurrency tokens together, keeping the existing dialog mounted. Refund history, balances, lists and reports are invalidated as before. Ordinary background updates must not reset drafts or silently advance their concurrency tokens.

Keep editing and further refund actions locked until this explicit reload succeeds. If it fails, clearly state that the refund changes were saved and offer a read-only retry. A failed reload must never be reported as a failed money write or cause that write to be repeated. Preserve the open dialog and its last known values for retry or dismissal.

## Consequences

Users can continue editing immediately after a refund change without reopening the expense. Active-refund protections and allowance totals reflect the saved state. Subsequent concurrent edits still reject stale saves, retaining the user's typed draft. No API, financial model or migration changes are required.
