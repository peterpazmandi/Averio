# ADR-038: Explicit import overlap decisions and partial safe undo

Status: Accepted

## Decision

Known-format imports remain deterministic. Possible overlaps require explicit keep/skip decisions and are revalidated under the household write lock. Source rows are immutable, including rows explicitly skipped. Mapping suggestions are private to the uploader and revalidated before reuse.

Reconciliation compares balances without automatic adjustment. Partial undo voids only unchanged, currently writable records without dependencies. It subtracts their aggregate contributions atomically and preserves blocked records, accounts, categories and balance references. The uploader reviews account-level balance effects and confirms a snapshot token that is checked again under the household lock.

## Consequences

Repeated legitimate purchases remain possible. History is auditable after skip and undo. Old untracked batches cannot be safely undone. Linked personal-money, activity, receipt and recurring records require individual review rather than cascading deletion. A stale preview or lost response requires refreshing; retries never reverse the same record twice. Reimporting an undone file retains existing fingerprint idempotency.
