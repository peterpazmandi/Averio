# Received item refunds

German UI: **Rückzahlungen**. Hungarian UI: **Pénzvisszatérítések**. These names distinguish returned expense payments from reimbursement claims; see [money-back terminology](ui/money-back-terminology.md).

The Money Back page lists visible refunds alongside your private reimbursement claims, with status filters and links to the original expense. A refund represents money already received, not a claim awaiting external submission. See [reimbursements](mortgage-reimbursements.md) for the overview, access rules and claim workflow.

Refund history on an expense includes only records whose credit is currently visible to the viewer. In particular, a reversed credit subsequently moved to a private account does not expose that credit through the shared expense history.

Open an ordinary expense and choose **Add refund** in **Item refunds**. Choose exactly one receipt item and either record returned money or link an existing unlinked incoming transaction. Confirm the money was actually received. Use the original account and currency, an actual receipt date from the purchase date through today, and an amount no greater than the item's remaining cost.

For an existing recorded credit, select it instead of creating another movement. Its whole amount is reclassified as Refund, removing its income/category contribution. A returned-money transaction belongs to one active item refund at a time. Income with active Personal Money, receipt, recurring or reimbursement dependencies cannot be reused; fully reversed allowance history does not prevent re-linking.

The refund dialog separates **Choose the receipt item**, **Choose the returned money**, and **Review and confirm** into numbered cards. Existing credits can be searched by merchant, item or note and filtered by received-date range, bounded by the purchase date and today. Search and dates filter the full saved history before pagination; use **Load more transactions** to see another page. Candidate rows show the date and amount, with credits larger than the item's remaining cost excluded. Changing the item, mode or filters resets confirmation; filter changes also clear the selected credit. The final summary shows the refund and the receipt's resulting net cost before confirmation.

Search and date controls start collapsed. Use the filter icon to show or hide them; its badge counts active filters. Hiding the controls keeps filters, the selected credit and confirmation intact.

Each receipt can have multiple item refunds. A EUR 100 clothing item plus a EUR 50 grocery item costs EUR 150. A EUR 20 clothing refund changes clothing expense to EUR 80 and the receipt to EUR 130; groceries remain EUR 50. A further EUR 10 grocery refund changes groceries to EUR 40 and the receipt to EUR 120. Income remains zero. Refunds may cover an entire item or receipt, leaving zero net expense without deleting original amounts or receipt items.

The original expense and category totals show net cost in the original purchase period. The dated bank balance still uses the original gross debit and actual refund credit, including when an anchor lies between them. Activities also use the net expense; a linked returned-money credit adds no income. Refunded items remain stable across edits. The account, type, date and visibility of linked records cannot be changed through ordinary editing, and refunded items cannot be removed. Original paid amounts of items with active refunds are protected; reverse those refunds before correcting the original cost.

Use **Reverse refund** only to correct a mistaken link or record. It restores the expense and reverses any related allowance restoration:

- **New credit created for the refund:** the credit is voided and removed from tracked cash.
- **Existing recorded/imported Income linked as a refund:** the same transaction stays posted and returns to independent Income, with its original item IDs, amounts and categories. Its date, account, metadata, labels and import links remain intact. Cash stays unchanged and income reporting is restored.
- **Historical link without reliable origin:** reversal is blocked with an explanation, leaving finances unchanged until its provenance can be verified.

The original refund and its reversal remain in history. A restored income can be linked again using a new operation; an old reversal retry cannot cancel the new link. Reverse active refunds before voiding their source expense. Viewing a Refund credit links back to its original expense; an independent restored income no longer exposes that origin.

The member's Personal Money is restored proportionally to the item's allocation; whole-receipt legacy allocations use their proportion of the gross receipt. Details of another member's allowance are never returned in the shared refund UI. Historical allowance-only refund attribution is preserved and remains a separate legacy workflow.

Apply `AddItemRefunds` and `PreserveRefundCreditOrigin` before deploying the API, worker and frontend. It adds the refund relationship and net/gross preservation columns and permits the explicit Refund kind and fully refunded zero-value expense rows. Existing rows receive zero refunded amount. Rollback refuses while refund records exist. See ADR-048 for semantics and limits. One combined bank credit spanning multiple receipt items cannot be allocated across items by this version; split the received amount deliberately without retaining a duplicate bundled income. Cross-account/currency refunds and automatic matching are deferred.

Refund actions use the latest saved receipt while the ordinary edit form retains its own revision. Save or discard unsaved edits before managing refunds. After adding, linking or reversing a refund, the open editor automatically reloads the saved transaction, its concurrency version and Personal Money, then enables editing again. The same window stays open; no reopen is required. Background refreshes never replace an unsaved draft or silently advance its version. If the post-refund reload fails, the saved refund remains in place and editing stays locked with **Retry refresh**. This retries loading only, never the refund or reversal. See [ADR-049](decisions/ADR-049-refresh-editor-after-item-refunds.md).

While an item's refunds are active, its allowance allocation is protected from amount changes. You can still unlink it; this removes both its allowance charge and related refund attribution. Reverse the item's refunds before setting a different allocation. The automatically refreshed form updates these protections without closing the expense.

Unlinking an item retracts only that item's Personal Money refund attribution, even when other items on the receipt still have allocations or refunds. Their coverage and credits remain intact. Later refund reversals cannot retract that unlinked attribution a second time.

If the response to a refund submission is lost, **Retry refund** repeats the original submitted request and its original versions. Editing stays locked while the outcome is unknown, including across reconnection and background updates. This also works for fully refunded items and existing credits that are no longer listed as income. Close and check the saved history before starting a different refund. An initial, definite rejection permits a new review and requires a fresh confirmation; a later rejection after a lost response does not erase the unresolved request.

See [ADR-050](decisions/ADR-050-existing-refund-credit-reversal.md) for provenance storage, the safe historical-data policy, recovery evidence and rollout/rollback requirements. Existing historical cancellations are never automatically undone.

Existing-credit choices now exclude protected reimbursement and other dependency links on the server before pagination. Unlinking a reimbursement makes an otherwise eligible credit selectable again; a concurrent new link is still rejected when recording the refund. See [ADR-053](decisions/ADR-053-refund-eligibility-and-payment-access-recovery.md).
