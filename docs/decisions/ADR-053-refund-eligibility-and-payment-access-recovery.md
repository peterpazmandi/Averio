# ADR-053 — Refund eligibility and reimbursement access recovery

Status: Accepted

Date: 2026-11-21

Extends ADR-052 with a narrow recovery exception. Ordinary claim visibility, bank transaction authorization and reimbursement accounting are unchanged.

## Refund choices

The transaction-list query supports refundEligibleOnly. The server excludes non-posted/non-Income/archived rows, accounts the caller cannot modify, and credits with active refund, reimbursement, Personal Money, receipt, recurring or source-claim dependencies before keyset pagination. Candidate filtering and refund write validation share the same dependency query. The dialog additionally applies source account, visibility, date, currency and item-amount restrictions. Write validation remains authoritative if eligibility changes after selection. Unlinked reimbursement history alone does not exclude a credit, but continues to protect import history.

## Recovery authority and privacy

Account ownership or visibility can change after a payment is linked. Ordinary claim reads deliberately require visibility of the source, destination and every historical credit; ordinary unlink also requires source write access. This can strand an active link. The dashboard now offers recovery for the current active household member's own claims that no longer meet these ordinary access requirements.

GET payment-recovery returns at most 100 stable, ordered pairs of opaque claim/payment IDs. It returns no transaction IDs, amounts, currency, provider, account, dates, references, status or history. It is not a financial overview, and does not allow household administrators to inspect another creator's claims.

POST payment-recovery accepts an explicitly reviewed set of those pairs. A creator may revoke their own attribution even if account access has changed or been restored since review. It neither reads bank details into a response nor modifies bank records, aggregates, balances or Personal Money. Other creators' claims, other households and unknown links are rejected. All removals in one request commit atomically under the household write lock; claim status and outstanding coverage are recalculated by the existing unlink operation. Existing history and import dependencies remain intact.

Exact payment IDs are never reused. Repeating recovery is a no-op for already unlinked IDs, including after a later replacement link. The dialog freezes reviewed IDs across refreshes and unknown responses and retries only that set. A request cannot acquire newly created links implicitly. More than 100 stranded links can be recovered in successive reviewed batches.

Recovery does not restore account access or reveal hidden history. A claim can remain hidden after recovery, especially when an unlinked historical credit is private. Restoring authorized access is necessary to inspect that history. Former household members do not gain recovery access through this endpoint.

## Delivery and verification

No schema migration or compatibility-epoch change is required. The API/client additions are additive. Ship the matching generated frontend client with the API. EN/DE/HU warnings and confirmation explain that coverage is removed while cash records and history are retained.

PostgreSQL tests cover candidate eligibility, creator-only recovery, atomic failure, concurrent retries, preserved records/import links and late recovery after relinking. Browser tests use a real joint-account ownership/visibility change and verify private detail suppression, candidate filtering, confirmation, committed/uncommitted lost responses and localized responsive layouts.
