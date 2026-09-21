# ADR-017 — Current account balance is a privacy-aware ledger projection

**Status:** Accepted  
**Date:** 2026-09

## Context

Account creation records a balance anchor and date so a user can start using Averio without importing their entire historical ledger.

Once Transaction Management exists, displaying that unchanged anchor as the account's main balance is incorrect: new income and expense must affect the balance. Mutating the anchor itself on every transaction would destroy its meaning and make reconciliation/audit behavior difficult.

Averio also allows Private transactions on a Household-visible Personal account. A current-balance calculation must therefore not reveal private movement to another household member indirectly.

## Decision

The persisted Account balance anchor remains immutable in the normal Account Management flow.

A current account balance is derived when the account/dashboard read model is requested:

```text
CurrentBalance(asOf)
    = BalanceAnchorAmount
    + posted Income on/after BalanceAnchorDate and on/before asOf
    - posted Expense on/after BalanceAnchorDate and on/before asOf
```

`BalanceAnchorDate` is interpreted as the start date of tracked ledger movement. Transactions dated before it are historical context and do not alter the anchor-derived current balance. Future-dated transactions do not alter today's balance.

Only transactions visible to the requesting household member are included:

- Household-visible transaction: included when the containing account is visible
- Private transaction: included only when the requester is one of the account holders

This means the displayed balance is a requester-safe tracked balance. Averio does not expose another member's private transaction amount by allowing the change to appear in a shared balance.

The current balance is not stored in a second mutable column. It is a read-model calculation from Account anchor + authoritative posted ledger rows.

Transaction create/update/void invalidates the Accounts and Dashboard TanStack Query keys so the updated balance is fetched immediately.

## Consequences

- inserting posted income or expense immediately changes the account's displayed balance
- edits and voids are reflected without rewriting account setup data
- account balance history remains auditable from one immutable anchor plus ledger movement
- backdated rows before the anchor date and future-dated rows do not affect today's balance
- requester-visible balances may differ between an account holder and another household member when private movements exist; this is intentional privacy behavior
- this implementation reads the ledger for balance projection; a dedicated balance projection can be introduced later if scale requires it, provided it preserves the same semantics
- credit-card-specific debt presentation may require a future explicit domain rule; the underlying signed ledger calculation remains unchanged until such a decision is accepted
