# Transaction regression checks

Run from the repository root:

```sh
dotnet run --project tests/Averio.Transactions.Tests
```

This follows the existing dependency-free executable regression-suite approach used by the account slice. It covers item-derived totals, category snapshots, reusable labels, optimistic concurrency, void semantics, privacy rules, the Category/Icon/Label EF model, and dimension-aware account/category aggregates.

The aggregate repository uses PostgreSQL-specific atomic upserts, so it still needs a disposable PostgreSQL integration check before release. Verify:

1. Apply `20260921100602_AddTransactionLedger`, `20260921133000_AddTransactionDetails`, and `20260921151000_AddReusableCatalogsAndDimensionAggregates`.
2. Verify exactly 100 base category rows and at least 200 icon-catalog rows are present.
3. Verify Category has no foreign key to Icons and changing the icon catalog does not mutate existing category icon values.
4. Confirm the migration maps existing transaction items to a valid category, defaulting to Uncategorized when no base-category name matches.
5. Confirm legacy transaction-attached label rows are deduplicated into reusable `labels` and linked through `transaction_labels`.
6. Create Household and Account labels and verify the label list exposes Household labels plus only the selected account's Account labels.
7. Create a multi-item transaction with items in different categories and verify `transactions.amount` equals the exact decimal sum.
8. Verify account aggregates receive one transaction contribution while category aggregates receive item amounts/counts, without multiplying the account transaction count.
9. Verify Day, Month, and Year aggregate rows are written atomically for create/edit/void.
10. Change an item's category and verify the old category contribution is removed and the new category contribution is added exactly once.
11. Change transaction date/type/account and verify both account and category buckets are reversed/reapplied correctly.
12. Confirm private transactions do not enter household account or category aggregates.
13. Confirm category-statistics queries still filter by the source account's current visibility/reporting participation.
14. Verify EUR, USD, and HUF remain separate in account and category reporting.
15. Race two edits and verify the losing write rolls back item/label/aggregate changes.
16. Rebuild ledger-derived account/category totals independently and compare them with aggregate rows to detect drift.


## Frontend review checks added 2026-09-21

- Category selection opens a Bento-style grouped picker instead of a 100-row native select.
- Category picker search matches parent groups and child categories and works on desktop/mobile.
- Transaction overview uses the same search + expandable filter-panel interaction as Accounts.
- Statistics income/expense bars expose exact formatted values on hover and keyboard focus.
- Clicking/tapping a statistics bar keeps that value visible until the same or another bar is clicked.
- Create/edit/void invalidates Accounts and Dashboard queries so derived balances refresh immediately.
