# ADR-015 — Transaction ledger and synchronous reporting aggregates

**Status:** Superseded  
**Date:** 2026-09  
**Superseded by:** ADR-016

ADR-016 keeps the ledger/atomic-aggregate principles from this decision but replaces the temporary free-text category/transaction-attached label model and extends aggregate periods/dimensions.

## Context

Transaction history is the authoritative financial ledger, while the dashboard and statistics need fast repeated cash-flow reads. Re-scanning an ever-growing transaction table for every chart would become unnecessarily expensive, but asynchronous aggregation would make a young finance product harder to reason about and could temporarily show totals that disagree with the ledger.

Privacy and multi-currency rules also require the reporting path to be explicit: private transaction details must not leak into household reporting, and currencies must never be silently combined.

Receipt-style purchases add another requirement: one bank transaction can contain multiple economically different items. The total used by the ledger and reports must therefore be derived from those item amounts rather than maintained as a second independently editable number.

## Decision

The first transaction-management slice supports posted **income** and **expense** ledger entries. A transaction:

- belongs to one household and one account
- keeps the account currency as an immutable value for that ledger version
- contains one or more ordered transaction items
- derives its positive total amount from the exact decimal sum of its items
- may store a free-text merchant/store and optional note
- allows an item description, optional category-name snapshot and amount per item
- may carry Household-scoped or Account-scoped labels
- has Household or Private visibility
- can be corrected while posted
- is voided rather than hard-deleted
- uses optimistic concurrency through `updated_at`

`transactions.description` remains a persisted summary for list/search compatibility. It is derived from merchant/store when supplied, otherwise from the first item; it is not an independently editable financial field.

The category vertical slice does not exist yet. Transaction items therefore keep an optional category-name snapshot so itemized receipts can be recorded now without pretending Category management already exists. A future Category implementation may add a category identifier while retaining a snapshot when useful for history.

Labels in this slice are transaction-attached labels with explicit scope. Household labels have no account id; Account labels are tied to the transaction's selected account. A reusable label catalogue/autocomplete experience can be introduced later without changing the privacy meaning of the stored scope.

Transfers are deliberately not represented as an income/expense pair. They remain a separate paired-movement feature so they cannot be double-counted or orphaned.

For fast cash-flow reads, Averio maintains a derived `transaction_aggregates` table at **day** and **month** grain. Each row is keyed by household, account, currency, period kind and period start. Income total, expense total and transaction count are updated incrementally in the same database transaction as the ledger mutation.

Only posted household-visible transaction contributions are written to these household-reporting aggregates. The contribution amount is the transaction's item-derived total. Read queries additionally join the current account reporting/visibility rules, so an account excluded from household reports does not surface through statistics.

Aggregate rows are derived data. The transaction ledger remains authoritative and an aggregate rebuild operation may be added later for repair/verification.

## Consequences

- itemized receipts have one authoritative total instead of item totals plus a manually maintained transaction amount
- dashboard and statistics cash-flow queries scale with aggregate periods/accounts rather than raw transaction count
- create/edit/void operations keep ledger details and aggregate deltas in one database transaction
- editing a transaction subtracts its old derived contribution and adds its new derived contribution
- private merchant, item, category, label, note and receipt detail cannot leak through household aggregate rows
- each currency is returned separately; cross-currency totals wait for an explicit FX layer
- voiding preserves audit history and removes the posted contribution
- full Category management, reusable label management and transfer management remain separate implementation milestones
