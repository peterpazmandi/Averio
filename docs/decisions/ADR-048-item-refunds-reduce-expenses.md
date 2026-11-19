# ADR-048: Received merchant refunds reduce individual expense items

Status: Accepted

Date: 2026-11-18

## Decision

A received refund belongs to exactly one stable item on an original posted expense. A receipt may have multiple refunds for different items, including partial returns. Cumulative active refunds cannot exceed any item's original cost. A refund can be manually recorded or an existing unlinked incoming transaction can be explicitly reclassified; it is stored as `TransactionKind.Refund`, never counted as income. One returned-money transaction backs one item refund and cannot be reused for another item.

Store each expense item's remaining net amount and cumulative active refunded amount. Their sum retains its original paid amount. The transaction amount is the sum of its net items; a fully refunded expense can be zero while its original positive payment remains recoverable. Existing editors submit original gross item amounts; retained item identities preserve refunds across edits. Refunded items cannot be removed, and refund-linked account/type/visibility/date are protected.

Rebuild the original transaction's account and category aggregates atomically using the net amounts. This restates the original purchase period as net expense; refunds do not add income in their receipt period. Activity expense results use the same net amount. Actual account balances instead debit the original gross payment on its original date and credit the distinct Refund transaction on the actual receipt date, respecting balance anchors. Net expense reports are therefore cost reports, not a reconstruction of dated bank cash movements.

Require actual-money receipt confirmation, the current source revision and an idempotent operation ID. Household locking serializes refunds with edits, visibility changes and other financial writes. Recheck membership, source modification authority, account status and visibility. The first slice requires the original account, currency and transaction visibility, preventing private receiving-account details from leaking through a shared receipt. Selecting an existing credit requires its current revision, matching full amount/date and no prior financial dependencies.

Refund history is retained. Reversing a mistaken refund restores its item's cost, voids the returned-money credit and reverses dependent Personal Money attribution in the same transaction. Ordinary edit/void APIs cannot bypass this lifecycle. Import undo protects both the source and returned-money records, including history. Migration rollback refuses while item refunds or Refund transactions exist.

Personal Money restores the refund's proportion of that item's allocated cost, capped at outstanding allocation. Legacy receipt-wide allocations use their share of the original receipt. Four-decimal exact integer arithmetic avoids overflow or floating-point rounding. A final full return restores any remaining allocated share. Wallet details remain private even when the shared receipt refund is visible. Correcting a refund reverses its wallet credit, never another bank movement.

## Superseded scope and consequences

This replaces the merchant-refund-as-income convention in ADR-022 and the incoming-income requirement of ADR-030 for this new item-refund workflow. Historical allowance-only refund attributions remain unchanged; they are not silently migrated or reclassified. Expected third-party reimbursement claims under ADR-045/046/047 remain separate from actual received refunds and never create money automatically.

The UI shows original paid, refunded and net expense amounts, item-specific history and a dated returned-money link. Refund classification appears in transaction lists and filters. A bundled bank credit covering multiple original items must be split into separate received-money records before using this one-credit-per-item workflow; do not retain both a bundled income and replacement refund credits. Cross-account/currency returns, automatic bank matching and allocation of one combined credit across items remain future extensions.

While refunds are active, original paid amounts and allowance allocations are protected from changes. Removing an allowance allocation remains possible and reverses its related allowance refund attribution together. Cancelling the refund afterwards cannot restore that removed attribution twice. Refund actions use the latest saved expense revision; unsaved edits must be saved or discarded first, and a refund invalidates the original editing session to prevent overwriting the updated saved expense.
