# ADR-035: Receipt confirmation request identity and canonical amounts

Status: Accepted

Supersedes [ADR-034](ADR-034-receipt-review-and-atomic-posting.md).

## Context

Returning a posted receipt's transaction ID for every subsequent request prevents duplicate expenses but can silently ignore different reviewed values from another tab or an edited retry. Comparing against the editable ledger would also break legitimate retries after a later transaction edit. The receipt editor additionally reconciled normalized decimal values while submitting their original leading-zero spelling, which the API rejects.

## Decision

Retain the existing transaction editor, private image/evidence review, explicit account/currency/privacy choice, missing-date correction, category review, exact positive-item reconciliation and review acknowledgement. Item and deposit observations may prefill items; discount, tax, subtotal and payment evidence must not create duplicate expense amounts. There is no implicit FX or automatic financial posting. More than 100 candidate items require manual grouping.

Within the existing locked-household transaction, authorize the active uploader, require a Ready/unexpired scan and its expected version, and create the expense through the normal transaction handler. Ledger, aggregates, activity, allowance effects, Posted link and immutable confirmation fingerprint commit together or all roll back.

The receipt ID is the posting idempotency key, bound to the original confirmation intent. Store a SHA-256 fingerprint of a versioned canonical payload covering the expected scan version, household/account, kind/visibility, merchant/note/date, ordered items with categories and Personal Money amounts, label IDs, activity assignment and whole-transaction charge flag. Decimal values use invariant, scale-independent formatting; optional text follows transaction trimming semantics; label order is irrelevant while item order is preserved. Do not expose or log the payload or fingerprint.

After confirming active membership and uploader ownership, identical requests return the original transaction ID even after subsequent transaction edits or temporary-image cleanup. Different requests return the existing localized receipt conflict (HTTP 409); they never change or silently accept different financial intent. Subsequent financial changes use ordinary transaction lifecycle endpoints.

Do not infer an original fingerprint for already-posted legacy rows from mutable transaction history. A missing fingerprint makes confirmation retries conflict safely; the existing posted transaction link remains available through authorized reads.

The frontend submits all receipt monetary values using the same canonical decimal normalization used by reconciliation, including reviewed total, item amounts and optional Personal Money amounts. Leading-zero/comma inputs remain editable but are sent as canonical invariant decimal strings. Invalid values remain rejected; this does not introduce floating-point conversion or change ordinary manual/recurring editor submissions.

## Consequences

Apply AddReceiptConfirmationFingerprint after AddReceiptPosting before deploying the updated API/worker. The nullable 64-character column preserves legacy rows; a PostgreSQL check permits fingerprints only on Posted scans and requires lowercase hexadecimal SHA-256 shape. New application confirmations always store their fingerprint atomically with the posting link.

Scans and images remain uploader-only even when the resulting expense is shared. Ordinary transaction authorization still controls access to the posted expense. Draft evidence is cleared on posting; image access retains seven-day expiry, and cleanup preserves posting identity and fingerprint. Review corrections remain transient until confirmation. Permanent attachments, duplicate suggestions, paid quotas and rollout metrics remain PR 3 work.
