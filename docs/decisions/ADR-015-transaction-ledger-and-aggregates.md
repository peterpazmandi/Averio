# ADR-015 — Transaction ledger and synchronous reporting aggregates

**Status:** Accepted  
**Date:** 2026-09

## Context

Transaction history is the authoritative financial ledger, while the dashboard and statistics need fast repeated cash-flow reads. Re-scanning an ever-growing transaction table for every chart would become unnecessarily expensive, but asynchronous aggregation would make a young finance product harder to reason about and could temporarily show totals that disagree with the ledger.

Privacy and multi-currency rules also require the reporting path to be explicit: private transaction details must not leak into household reporting, and currencies must never be silently combined.

## Decision

The first transaction-management slice supports posted **income** and **expense** ledger entries. A transaction:

- belongs to one household and one account
- keeps the account currency as an immutable value for that ledger version
- stores a positive magnitude; kind determines income vs expense semantics
- has Household or Private visibility
- can be corrected while posted
- is voided rather than hard-deleted
- uses optimistic concurrency through `updated_at`

Transfers are deliberately not represented as an income/expense pair. They remain a separate paired-movement feature so they cannot be double-counted or orphaned.

For fast cash-flow reads, Averio maintains a derived `transaction_aggregates` table at **day** and **month** grain. Each row is keyed by household, account, currency, period kind and period start. Income total, expense total and transaction count are updated incrementally in the same database transaction as the ledger mutation.

Only posted household-visible transaction contributions are written to these household-reporting aggregates. Read queries additionally join the current account reporting/visibility rules, so an account excluded from household reports does not surface through statistics.

Aggregate rows are derived data. The transaction ledger remains authoritative and an aggregate rebuild operation may be added later for repair/verification.

## Consequences

- dashboard and statistics cash-flow queries scale with aggregate periods/accounts rather than raw transaction count
- create/edit/void operations perform a small fixed number of aggregate upserts
- ledger mutation and aggregate mutation are atomic
- private transactions cannot leak through household aggregate rows
- each currency is returned separately; cross-currency totals wait for an explicit FX layer
- editing a transaction subtracts its old contribution and adds its new contribution
- voiding preserves audit history and removes the posted contribution
- transfer management remains a separate implementation milestone
