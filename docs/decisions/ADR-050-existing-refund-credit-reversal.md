# ADR-050 — Preserve existing bank credits when reversing item refunds

Status: Accepted

Date: 2026-11-20

Supersedes only the unconditional credit-voiding reversal rule in ADR-048. Its net expense, dated cash, privacy and retained-history rules continue to apply.

## Context

An item refund can create a new Refund transaction or reclassify an existing recorded/imported Income transaction. Voiding both during reversal incorrectly removes a genuine existing bank movement. Reclassification also replaced the original income items without retaining their IDs or category snapshots.

## Decision

Each new `ItemRefund` persists `CreditOrigin` (`Created` or `ExistingIncome`). Existing income links capture a schema-versioned JSON snapshot of the original item IDs, order, descriptions, category IDs/names/icons and exact amounts **before** reclassification or temporary position staging. The snapshot belongs to that refund link and is never included in API responses. The original transaction itself retains its ID, account, currency, amount, date, description, merchant, note, labels, creator, creation date, visibility and import origin; reversal changes only its kind, items and concurrency revision. It stays posted with no void timestamp.

Reversal restores the expense's net cost, retracts that credit's Personal Money attribution and either restores Income or voids a newly created Refund. Restored income aggregates are reapplied in its original period and dimensions; purchase aggregates return to their pre-link state. Cash is unchanged for existing income reversal. The entire operation runs within the existing household-locked database transaction.

A partial unique index permits only one active refund per credit. Cancelled links remain auditable. Re-linking requires fresh credit/source revisions and a new operation ID; fully offset Personal Money history is not an active dependency. Retrying an old cancellation is a no-op even after the credit is linked again. Retrying an original record operation returns its original refund ID, including after cancellation, rather than recreating the link. Import undo continues protecting all historical references. Independent restored Income does not expose an old refund origin through the origin endpoint.

## Existing data and rollout

`PreserveRefundCreditOrigin` marks **all** pre-migration links `Unknown`, including cancelled history. The old schema cannot prove which non-imported credits were reused, and it discarded their original items. Import origin alone does not recover those items. Timestamps, descriptions, IDs and request hashes are not sufficient evidence. The migration does not change financial rows, aggregates, wallet entries or import links, and does not resurrect already-voided credits.

Active unknown-origin reversals fail before mutations with a localized explanation. Cancelled historical requests remain idempotent. There is no public override that can silently classify an unknown link as a created credit. An operator must use an authoritative pre-link backup/import record and the original request to establish origin. For existing income, recover the complete original item snapshot (including stable IDs), verify it against the current credit identity/amount and category records, and correct provenance in a reviewed transaction. If evidence is incomplete, leave it unknown; never invent categories or delete a possible bank movement. Previously cancelled historical credits require a separately reviewed financial repair, not automatic migration.

Retain a pre-upgrade backup and stop the old API (or otherwise quiesce all refund writes) before applying the migration, then deploy the updated API/worker/frontend together and resume writes. Do not run old and new API versions side by side: the old reversal handler does not understand provenance. The migration deliberately leaves no database default for new origins, so old writers cannot silently create untracked links. Downgrade refuses once explicit provenance exists, because discarding snapshots would reintroduce the defect and repeated links cannot fit the former unique index. Restore a consistent backup if rollback is required.

## Verification

Unit checks cover snapshot restoration and invalid versions/identities. PostgreSQL tests cover populated migration and reruns, unknown active and cancelled history, split imported credits, stable item/transaction identity, exact aggregates and cash, Personal Money, simultaneous reversals, competing links, late old retries, private access and retained import dependencies. Browser tests cover both confirmations and reversal modes, saved-response loss/retry, fresh editor revisions, re-linking and EN/DE/HU responsive states. Core financial correctness remains a baseline product guarantee.
