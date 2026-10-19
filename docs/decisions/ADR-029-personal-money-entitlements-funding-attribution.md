# ADR-029 — Personal Money entitlements, funding and attribution

**Status:** Accepted  
**Date:** 2026-10-17  
**Extends:** ADR-004 (rollover budget sub-ledger), ADR-018 (paired account transfers), ADR-024 (household cutover), ADR-026/027/028 (recurring transaction lifecycle).  
**Implementation:** Personal Money foundation, followed by shared-funding reconciliation and recurring allocations.

## Context

Members need discretionary spending autonomy while keeping a household budget consistent with real account balances. Existing Account scope, visibility and holder ownership are separate; existing Transfer is an atomic two-account aggregate and is never a regular income/expense pair. A virtual bank account or special expense category would misrepresent those facts and double-count money.

## Accepted invariants

1. Personal Money is an entitlement scoped to a particular `HouseholdMember`, not a bank account or category. A different household membership always has a different wallet.
2. A household-to-private transfer can explicitly **create-and-fund** a new grant or **fund an existing** entitlement. An ordinary transfer never grants entitlement implicitly. Funding a grant must not grant it again.
3. Grants and funding are independent events, even when posted atomically by one command. An unfunded grant does not affect bank balances or ordinary cash-flow reports.
4. Private transactions consume entitlement only when explicitly charged to a wallet; private merchant, item, note, receipt, account balance, and private consumption remain private by default.
5. Shared-account expenses may explicitly charge a personal entitlement without a second income/expense ledger entry. This may be partial.
6. Any unspent entitlement rolls over without expiry by default, including a negative amount. A completed over-budget charge is recordable, and no monthly synthetic rollover transfer is needed.
7. Automatic monthly grants and bank-payment execution are excluded from the first slice. Existing recurring income/expense schedules must not be reused as surrogate grant transactions.
8. All changes to source transfers/transactions and corresponding personal-money entries are atomic, authorization checked and reversibly auditable.

## Ledger semantics

```text
Entitlement = grants + attributable refunds - charges - surrenders + signed adjustments/reversals
Private funding = funding in - linked private charges + linked private refunds - funding returned + corrections
Funding gap = entitlement - private funding
```

Private funding is attributed provenance, **not the private account's actual balance**; unrelated money may exist in that account. Household cash movement, personal entitlement, and consumption are three distinct reporting axes.

For a €300 create-and-fund transfer, one €300 grant and one €300 funding record are linked to the existing Transfer. A later €80 private purchase reduces entitlement and attributed funding by €80 but adds **no further household cash outflow**. A €80 shared-account personal purchase reduces entitlement only and remains one €80 ordinary household expense.

A transfer funding an existing €300 grant changes funding only; the grant remains €300. A return from private to household affects funding and real cash, not entitlement. Surrendering entitlement is a separate budget operation and must not silently move money.

### Reporting boundary

`TransactionAggregate` continues to represent posted, authorized income/expense transactions. Do not turn a transfer into an ordinary categorized expense. Build a separate **household financial-boundary movement** read model that accounts for real transfers from included household funds to excluded personal/private accounts (and reversals) alongside ordinary income/expenses. Grant volume and wallet consumption remain separately named metrics; they cannot be added indiscriminately to household expense totals. A shared-account personal charge may be shown as a spending-purpose split, but never creates an extra expense.

## Initial policy decisions

### Grant authority

Only a current household Owner may grant or increase another member's entitlement in the first release. Administrative role does not grant inspection of that member's private wallet usage or private account activity. Delegated grant permissions are future scope. The wallet member may charge and see their own entitlement.

### Private destination and transfer authority

A first-release create-and-fund transfer is permitted only from an active, included Household-scope account to an active, excluded, Private-visibility Personal-scope account exclusively held by the recipient. Both are in the same household. The actor must satisfy existing ADR-018 write access to **both** accounts: ordinarily the private recipient. The grant must separately have the Owner's authorization. If the actor is not the grant authority, the Owner issues a grant first and the recipient executes `fund-existing`. This avoids enabling a household Owner to access or select a private account they cannot inspect.

Private transfers originating with a non-owner cannot unilaterally create a new grant. A current Owner who also owns the private destination can use create-and-fund in one operation.

For initial scope, all grant, funding, and charged expense amounts must match the wallet's immutable currency. Existing cross-currency ordinary transfers remain supported but are not eligible for Personal Money attribution until the conversion policy is implemented. The wallet's denomination cannot be silently changed when the household reporting currency changes.

### Transaction attribution

Only explicit expense charges count as consumption. Initial partial charges are amount-based and linked to stable `TransactionId`, because existing `Transaction.Update` recreates `TransactionItem` IDs. The linked charge cannot exceed the posted transaction amount; validate active membership and account authorization. For an expense recorded on a private account, only an authorized holder may create or read the charge. For an expense on a household account, existing visibility of the transaction remains; private attribution does not erase or hide the household bank movement.

A change or void to a linked transfer/transaction reverses and, where applicable, replaces corresponding personal entries in the same database transaction. Voiding a transfer that created a grant also reverses that grant; voiding funding of an existing grant does not revoke the original entitlement. Historical entries must remain auditable. No backdated action may leak private details through household historical reporting.

### Return and surrender

Returning attributed private funding and surrendering available entitlement are separate operations, possibly offered in a combined explicitly confirmed workflow later. Completing a real-world purchase may create a negative entitlement. Absent another policy, subsequent grants offset negative carryover.

### Household membership lifecycle

Personal Money must not be copied to a different household on invitation acceptance. Old wallet entries remain attached to original membership; no later grant or charge can post against a departed membership. Immutable `HouseholdHistory` snapshots must be privacy-filtered and extended before treating this slice as fully cutover-compatible.

### Privacy

Owner-only read models expose exact private charges, merchant/category/note/receipt and personal remaining entitlement. Other members may see authorized household grants and **their already-visible source transfer leg**, not private account IDs/balances or transaction-level private consumption. Do not show a supposedly anonymized purchase with exact private amount and date. Enforce privacy before serialization, report aggregation, search, notifications and exports. No amount can be inferred from a hidden private transfer side solely through the wallet endpoint.

## Persistence and reliability

Introduce a wallet identity and immutable entitlement entries with stable source references, reversible correction chains and unique idempotency semantics. Link transfers separately as funding; link transactions separately as consumption. Monetary amounts use decimal precision consistent with the existing ledger. Wallet balances are derived from authoritative entries and not maintained as independent mutable bank balances.

Use existing household/account locking and a stable documented lock order; keep ledger and grant/funding writes inside one PostgreSQL transaction. Include unique constraints for transfer↔funding, idempotency keys, source postings, and reversal references. Validate account scope/visibility, source posted status, membership, and authorization after acquiring locks. Preserve original recorded amounts, dates and currency for history.

## First implementation boundary

PR 1 establishes the manual ledger, member wallet queries, authorized grants, explicit transfer funding, private charge attribution, rollover, reversed-source handling and tests. PR 2 expands shared purchases, returns, surrender, partial funding reconciliation and household boundary reporting. PR 3 adds monthly scheduling and worker integration, with deterministic idempotency and no bank execution.

The slices must **not** label grant totals as ordinary expenses or pretend that tracked private funding equals a real private-account balance.

## Consequences

The feature demands its own ledger and API read models, plus integrations in transfer and transaction commands. It avoids double counting, preserves private account security and allows future multi-currency, delegated funding and recurring grants without changing the meaning of existing bank transactions.

## Implementation clarification — 2026-10-19

PR 1 includes wallet-member charge correction and unlink. A correction appends a signed PrivateCharge delta and preserves the original purchase and all ledger history. Zero unlinks; positive values replace the effective charge amount within source caps. Optimistic source-entry count plus expected amount prevents lost updates; a unique operation ID and stored request metadata protect retries. Existing household-first locking keeps corrections atomic with transaction edits/voids and membership changes. Reassignment is unlink followed by an explicit new link; another member's wallet remains inaccessible to household administrators. This clarification implements the accepted reversible-attribution invariant without changing the ten accepted product requirements or the later shared-account and scheduling slices.
