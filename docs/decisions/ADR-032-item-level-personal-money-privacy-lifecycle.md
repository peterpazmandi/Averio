# ADR-032 — Item-level Personal Money privacy and transaction lifecycle

**Status:** Accepted for PR 4 (2026-10-22)  
**Extends:** ADR-029, ADR-030, ADR-031.

## Decision

A posted expense may contain **mixed household and Personal Money items**, including partial attribution within an item. The underlying bank transaction (currency, amount, merchant and shared receipt items) remains one transaction and follows the payment account's ordinary visibility and reporting rules. Personal Money item allocations are **member-private secondary ledger facts**, never a second expense and never a public transaction-item property. The household Owner is not entitled to another member's private wallet or allocations.

### Ownership and authorization
- A member may allocate **their own active membership-scoped Personal Money wallet** to a visible, posted expense on an authorized private account or an eligible shared household account of the same currency. They cannot assign another wallet.
- Every ledger charge stores both transaction and item identity. A bounded item-level sum of effective charges across all wallets must not exceed that item's amount; transaction-level legacy links count against total transaction capacity. Negative available entitlement remains permitted for completed spending.
- Only the holder sees the association between the item, its amount and their wallet, through the `/personal-money/me` read model or holder-only endpoints. Ordinary household Transaction, TransactionItem, list/search and statistics DTOs never include other members' allocations, links, amounts or owner identities. Generic conflicts must not name the other wallet/member.
- Shared transaction/receipt descriptions remain visible under ordinary account authorization. Private merchant and receipt details cannot be inferred through Personal Money reporting or owner privileges.

### Edits, concurrency, deletion
- Keep stable item identity when an item is retained. An edit that only changes a merchant/note/labels does not reverse its Personal Money charges.
- The actor can amend their own item allocations; edits to another member's allocations are never allowed.
- If an item amount shrinks, the application revalidates the total of active allocations. A conflicting change must fail atomically with an **ordinary transaction-change conflict** rather than silently confiscating another member's spending entitlement or divulging its details. Where an item is removed, signed reversal events retract its charge (and dependent refunds as necessary), including for a different wallet, atomically with the source update.
- Existing legacy transaction-level attribution remains valid and must not be retroactively attached to arbitrary individual items.
- Source mutation, refunds, void, wallet corrections and category statistics are serialized with the same household database transaction lock and stale transaction versions are rejected.

### Archive, hide, void
- **Hide for me** is a member-level presentation preference, with no bank or allowance effect.
- **Archive** is a reversible household-wide visibility change for an authorized shared transaction; archived transactions remain queryable, auditable and present in all financial calculations. It never triggers wallet changes. No item descriptions or private allocations are exposed through the archive endpoint.
- **Void** is the existing financially consequential operation: only members authorized for the source account can void; in the same transaction it reverses the existing source's aggregate effect, all linked charge entries and dependent refunds across all affected wallets. The historical ledger and source record are not deleted.
- Affected allowance holders should be notified privately when another member's source edit/void changes their wallet. The actor sees only a generic warning about financial consequences. Notification delivery requires no loss of the transaction's accounting atomicity; outbox/notifications may be staged separately and must be tracked as an explicit follow-up if deferred.

### Presentation
- In the item editor, each item has its own Personal Money switch/amount, with a whole-receipt shortcut that selects all eligible items. Provide a live split summary (bank amount, household portion and Personal Money portion) with NO duplicated expense.
- Preserve the Tailwind/card design from `docs/ui/averio-ui-concept.html`; usable on 320–375px mobile, translated to EN/DE/HU.
- The transaction overview shows an attribution badge only for the signed-in member's active allocations. Never show another member's badge.
- Void prompts for confirmation, including a generic caveat that linked budgets may be adjusted, without revealing whether any linked wallet actually exists.

## Deferrals requiring separate decisions
- Attribution of a single item to multiple members in one editing operation, if both must authorize it; a member can charge only their own wallet.
- Exact optional transparency policies and personal hide/archive advanced filtering.
- Refund splits per item where one real refund covers multiple unrelated line items and wallets require new explicit authorization.
- Delivery of holder-private notifications when a dependable notification/outbox pipeline is not yet present; reconciliation must nonetheless remain atomic.
