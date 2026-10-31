# ADR-037 — Receipt rollout quotas, private duplicates and image retention

Status: Accepted

## Decision

Receipt scanning remains explicitly enabled by operators. All accepted uploads reserve one household scan in a UTC calendar month, under the existing household database lock. Defaults are 10 free and 100 paid scans/month, configurable before launch; these are operational defaults, not published pricing. Failed/discarded scans count because provider and storage costs may already have been incurred. Retries with the same operation ID and image return their existing reservation, even after quota exhaustion or disabling uploads. Provider retries never reserve again. The rolling daily member limit remains an independent abuse control.

The application has no checkout/subscription billing integration. A server-managed `PaidHouseholds` mapping grants a household paid capacity until a UTC timestamp. It is an operational entitlement bridge, not payment verification. Only operators who have verified eligibility configure it. Expiry falls back to the free limit without resetting usage. No client or household-owner endpoint grants paid status. A future billing integration must replace this source with verified server-side subscription state, preserving admission semantics.

Duplicate suggestions compare normalized-image hashes within the current uploader's household membership only. They consider active queued/processing/ready scans and previously posted scans. They are advisory: no automatic merge, deletion, ledger write or posting block. Different photos/crops may not match. Other members' private scans never produce a warning. This conservative first version deliberately avoids retaining merchant/date/total fingerprints after the draft is cleared.

`RetainPostedImages` defaults false until storage permissions/lifecycle are configured. New uploads with it enabled use `receipt-images/`. Drafts still expire after seven days. Atomic posting extends the image deadline by `PostedImageRetentionDays` (default 365; 1–365 permitted). The existing `expires_at` is the access/deletion deadline for both phases; no new persisted state is necessary. A separate prefix avoids the old eight-day `receipt-drafts/` lifecycle. Legacy images are never automatically migrated or promised longer retention. Turning the flag off affects new uploads only; already issued retention promises remain valid.

Only the active uploader can view or delete a saved photo. A version-checked `DELETE .../{id}/image` expires access and schedules worker deletion, keeping Posted status, immutable confirmation identity, transaction and items intact. Already expired/deleted photos make this operation idempotent. Membership departure still makes images inaccessible and eligible for cleanup. Shared expense visibility grants no image access. A paid entitlement downgrade never removes an existing image early.

Metrics use the `Averio.Receipts` Meter with bounded outcome/kind labels. No image, merchant, amount, user/household identifier or provider body is a metric dimension. Token measurements support cost estimation against the chosen model's current rates; they are not billing invoices.

## Consequences

No schema migration is required: existing receipt reservation rows, content hashes, deadlines, ownership keys and confirmation links remain the durable sources. Quota accounting includes former members' rows, preventing a household from resetting its allowance through membership changes. Household members can see aggregate shared allowance usage, but no other member's receipt details.

This completes the software rollout controls; enabling production still requires a real HU/DE/EN benchmark, storage lifecycle verification and an operator budget decision. Automated fixtures are not evidence of model accuracy.
