# PR 4 — Item-Level Personal Money, Privacy and Transaction Lifecycle

**Status:** Draft implementation; see ADR-032 and ADR-030.

## Accounting and UX
The ordinary transaction remains a single posted bank expense. Every item may have an optional full or partial **private Personal Money charge** for the signed-in membership's wallet. A mixed receipt with HUF 12,000 groceries and HUF 3,000 cosmetics can charge HUF 3,000 to Personal Money, without creating a second expense. Item allocation is optional. The transaction editor offers a per-item switch, amount input, Charge all items shortcut and a household/personal split preview. The private amount is accepted **only as a mutation input** and is not serialized on the shared TransactionItem response.

Only authorized active members may charge their own wallet in the original currency; eligible account types are Household/Household/Included or Personal/Private/Excluded. The sum of all effective charges on one item cannot exceed the item amount; legacy whole-transaction charges still count against total transaction capacity. The wallet ledger retains the transaction ID and nullable item ID, and an append-only correction for each change. A member's `/personal-money/me` response exposes their own item-linked entries. Owner role alone never permits reading another wallet.

## Editing and lifecycle
- Existing item IDs survive metadata/category edits. Changing only merchant, note or labels preserves all private allocation entries.
- Changes that would shrink an item below active wallet allocations are rejected with a generic conflict that does not identify another member.
- Removing a linked item creates signed reversals in the same transaction. Purchase-level refunds remain valid while the affected holder's remaining receipt allocation covers them; otherwise that holder's refund attributions are reversed atomically. The real bank income is preserved. Financial edits to a refund source retract its wallet credit; metadata-only edits preserve it.
- A source account, kind, date or visibility change on an actively linked transaction is rejected rather than reassigning money silently. Unlink/reconcile first.
- Historical transaction-level charges are not falsely attached to arbitrary items; source monetary edits retain the previous reversal rule.
- After all active charges of one attribution mode are unlinked, reversed history does not prevent linking through the other mode. Active legacy and item charges cannot be mixed on a receipt.
- **Archive** is a reversible visibility change stored on the transaction. It hides a transaction from normal list and dashboard recents, but not from account balances, cash-flow/category statistics or allowance history. Use IncludeArchived to see archived rows and restore them. This is intentionally distinct from **Void**, which atomically reverses the transaction's aggregate impact, every linked charge and dependent refund.
- Transactions archived by any account-authorized household member remain visible through explicit retrieval and history. The actor does not see hidden Personal Money attribution of another member.

## API and database
- `TransactionItemRequest.personalMoneyAmount` is an optional decimal-string input. Null/omission on update means preserve any existing attribution, and explicit `"0"` means unlink the **acting member's own** item attribution. New posted transaction and item charges commit in one DB transaction.
- `GET /personal-money/me`: private entry events additionally contain `transactionItemId` (nullable, so legacy links stay intact).
- `PUT /transactions/{id}/archive` with `{ archived, expectedUpdatedAt }`: account-authorized, optimistic and no accounting side effects.
- `GET /transactions?includeArchived=true`: includes archived records, still subject to account and transaction visibility checks; defaults false.
- New nullable ledger column `transaction_item_id` is intentionally a historical **reference**, not a cascading FK to current transaction_items. Removing an item must not destroy immutable financial audit entries; the application verifies source existence before the initial charge.
- New `is_archived` transaction flag defaults false; no new synthetic cash-flow line or virtual wallet account is introduced.
- Apply the EF migration before starting updated API and worker; regenerate the OpenAPI TypeScript client from the API.

## Privacy and review scenarios
Use two authenticated household members, one shared account and distinct membership-scoped wallets. Verify that A can attribute their own cosmetics item and B sees the original common item but never A's private wallet link; B's metadata edit does not reset A's allowance; archive/restore has no monetary effect; reducing below someone else's linked amount gives a generic conflict; deleting an item restores affected allowance without exposing who it belonged to; void cancels the original bank aggregate and all linked wallet charges/refunds atomically. Also test concurrent update versions, currency mismatch, legacy transaction-level links, partial item amounts, mobile responsive layout and all EN/DE/HU labels.

### Explicitly deferred
There is currently no durable in-app notification/outbox infrastructure to privately notify affected allowance owners when another member deletes/voids their linked source. This must be implemented in a separate notification slice with private recipient routing and an idempotent outbox; do not treat public household notifications as a safe workaround. Similarly, personal-only hiding is distinct from shared archival but is deferred to an account-scoped preference feature.
