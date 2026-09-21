# Transaction regression checks

Run from the repository root:

```sh
dotnet run --project tests/Averio.Transactions.Tests
```

This follows the existing dependency-free executable regression-suite approach used by the account slice. It covers transaction domain invariants, optimistic-concurrency version advancement, void semantics, account/transaction visibility compatibility, and EF metadata for both the ledger and aggregate tables.

The aggregate repository deliberately uses PostgreSQL-specific atomic upserts, so it needs a disposable PostgreSQL integration check before release. Verify:

1. Apply `20260921071000_AddTransactionLedger`.
2. Create household-visible, visible-personal and private accounts.
3. Add concurrent posted transactions to the same account/month and verify the day/month totals.
4. Edit amount, date, type and account and verify the old aggregate contribution is removed and the new one added exactly once.
5. Void a transaction and verify it remains in the ledger while its contribution disappears.
6. Race two edits of the same transaction and verify the losing write rolls back its aggregate delta.
7. Confirm private transactions cannot be listed by another member and never enter household cash-flow rows.
8. Toggle an account out of household reporting and verify existing aggregate rows stop appearing without requiring a rebuild.
9. Verify EUR, USD and HUF are returned separately and never summed.
10. Re-run the ledger-derived totals against the aggregate rows to detect drift.
