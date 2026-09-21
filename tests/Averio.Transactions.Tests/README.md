# Transaction regression checks

Run from the repository root:

```sh
dotnet run --project tests/Averio.Transactions.Tests
```

This follows the existing dependency-free executable regression-suite approach used by the account slice. It covers itemized transaction totals, merchant/category data, household/account label scope, optimistic-concurrency version advancement, void semantics, account/transaction visibility compatibility, and EF metadata for the ledger, detail tables and aggregate table.

The aggregate repository deliberately uses PostgreSQL-specific atomic upserts, so it needs a disposable PostgreSQL integration check before release. Verify:

1. Apply `20260921100602_AddTransactionLedger` and `20260921133000_AddTransactionDetails`.
2. Confirm the detail migration backfills each pre-existing transaction into exactly one item without changing its total.
3. Create household-visible, visible-personal and private accounts.
4. Create a transaction with multiple receipt items and verify `transactions.amount` equals the exact decimal sum of the item amounts.
5. Edit item amounts, date, type and account and verify the old aggregate contribution is removed and the new derived total is added exactly once.
6. Void an itemized transaction and verify it remains in the ledger while its contribution disappears.
7. Race two edits of the same transaction and verify the losing write rolls back item/label changes and aggregate deltas.
8. Confirm private transactions cannot be listed by another member and merchant, item, category, note and labels do not leak.
9. Verify Household labels have no account id and Account labels are tied to the selected account.
10. Toggle an account out of household reporting and verify existing aggregate rows stop appearing without requiring a rebuild.
11. Verify EUR, USD and HUF are returned separately and never summed.
12. Re-run ledger-derived totals against the aggregate rows to detect drift.
