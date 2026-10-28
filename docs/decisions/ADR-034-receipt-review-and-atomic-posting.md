# ADR-034: Explicit receipt review and atomic posting

Status: Accepted

## Context

ADR-033 introduced private temporary receipt processing. Extracted observations are untrusted and cannot change financial state. Human confirmation must reuse the established transaction rules and remain safe under lost responses, concurrent requests and worker cleanup.

## Decision

Use the existing transaction editor for receipt review. Show the private image, all original observations and warnings. Require explicit account choice, reviewed currency/total, an exact item reconciliation and review acknowledgement. Offer conservative suggestions from the authorized category catalogue. Never infer missing dates, perform FX or count payment/subtotal/tax lines as additional expenses.

Expose a receipt-scoped transaction resource. Within the existing locked-household database transaction, authorize the uploader and create the expense through `CreateTransactionHandler.CreateWithinTransactionAsync`. Fence confirmation with scan version, Ready state and expiry. Update the scan to Posted with its transaction ID in the same transaction, so financial writes and receipt consumption either all commit or all roll back.

The receipt ID is the posting idempotency key. Replays return its original transaction ID after confirming active membership and ownership. A database unique constraint and household-bound foreign key protect the link. Posted scans cannot be discarded/reposted; subsequent financial changes use normal transaction lifecycle endpoints.

## Consequences

Scans/images remain uploader-only even when the resulting expense is shared. Only ordinary transaction authorization controls access to the posted expense. Draft evidence is cleared on posting. Temporary images retain the existing seven-day expiry and cleanup; cleanup preserves posting metadata for retry safety. Permanent attachment retention, duplicate suggestions, paid quotas and rollout metrics remain PR 3 work.

Local review corrections are transient until explicit confirmation. Discounts require reconciliation into positive ledger item amounts; receipts over the 100-item transaction limit require manual grouping. These boundaries are explained in the review UI.
