# ADR-018 — Atomic account transfers with per-side privacy

**Status:** Accepted  
**Date:** 2026-09-23

## Context

The income/expense ledger and its aggregates must not inflate cash flow when money moves between accounts. The balance projection in ADR-017 needs to include those movements without rewriting account anchors. A household member may see one account but not the other.

## Decision

A Transfer is a separate financial aggregate stored as one row containing both account IDs, both original currencies, sent/received amounts, date, note, creator, timestamps and a nullable void timestamp. One insert/update atomically changes both legs. Transfers do not create Transaction or TransactionAggregate rows. Existing income/expense APIs cannot edit a transfer leg.

Both accounts must be distinct, in the current household and writable by the actor. Household administrator status does not grant permission over another member's Personal accounts. Create/edit requires active accounts. Void remains available for archived accounts to correct history. Account IDs and currencies are immutable after creation; a wrong pair is corrected by voiding and recreating. Positive decimal amounts have at most four fractional digits. Same-currency amounts must match. Fees are recorded as separate expenses.

For cross-currency movements, both actual settlement amounts are explicitly entered and retained. The exchange source is the user's settlement information, the effective rate date is OccurredOn, and the directional effective ratio is ReceivedAmount / SentAmount. The ratio is derived, never rounded and stored as a replacement for the original amounts. No market-rate provider, household conversion, FX gain/loss calculation or cross-currency total is introduced.

Account balances extend ADR-017 with posted incoming transfer amounts minus posted outgoing transfer amounts, each in its own account currency. Each leg independently observes that account's inclusive anchor date and requested as-of date. Voided and future movements do not affect current balances. The transfer row remains authoritative; there is no separate mutable balance or asynchronous projection.

Transfer visibility follows current account visibility, independently for each side. Lists/details return only a side the requester may see. An inaccessible side is null: no account ID, name, currency or amount is serialized. Shared notes are returned only when both accounts are visible. A hidden account cannot be used as a list filter to infer transfer associations. A member seeing neither account receives no record. Amount and date on an explicitly visible account remain visible, including when its counterparty is private. The UI explains this before entry. This does not add transaction-level private transfer visibility; use a Private account for a private side.

Edits and voids use the observed UpdatedAt token, advanced at PostgreSQL microsecond precision and enforced by EF optimistic concurrency. Authorized repeated voids are idempotent. History is never hard-deleted. Lists use date/creation/ID cursor ordering, with account and date filters.

## Consequences and boundaries

- Transfers have a dedicated page linked from the money navigation and Transactions page, extending the canonical visual language.
- Both balances refresh after create/edit/void; reporting totals are unchanged.
- Household-to-personal movement is still a transfer, not an expense. Personal allowance usage and boundary cash-flow reporting require their own future attribution model to avoid double counting; this slice does not imply those features exist.
- No transfers across households, bank execution, recurring transfer scheduling or split fee calculation is introduced.
- Notes are one shared transfer note; separate private per-leg notes are not supported.
- Composite household/account and household/creator foreign keys, restrictive deletion, money checks and history/account/date indexes protect persistence.
- The migration is applied explicitly through the normal deployment process, never during ordinary API startup.
