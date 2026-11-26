# ADR-057 - Protect private reimbursement attribution

Status: Accepted

Date: 2026-11-26

Partially supersedes the claim-creation eligibility in [ADR-046](ADR-046-manual-expense-reimbursement-claims.md) and pooled-credit eligibility in [ADR-055](ADR-055-pooled-reimbursement-credits.md).

## Context

A claim is private to its creator, but its source expense and received credit can be shared. Allowing another household member to create a claim on that expense reveals whether a hidden claim already exists. Likewise, showing the unallocated balance of a shared credit can reveal another member's private allocations.

## Decision

Only the creator of a transaction can start a new reimbursement claim from it or allocate it as a received credit. Both source and credit must be marked eligible under this policy; candidates are filtered before allocation amounts and pagination are calculated. Normal source modification, destination access and claim visibility rules still apply. The creator may allocate an eligible credit across several of their eligible claims.

Migration `20261126101159_ProtectReimbursementPrivacy` marks every pre-existing transaction ineligible. Newly created transactions are eligible, including transactions created from recurring occurrences. This deliberately freezes new claims and allocations on all pre-change transactions, not just transactions known to have claims or links: a narrower distinction could disclose private history. It is not a backfill or a change to bank balances.

Existing claims, payment links and history remain in place and retain their existing visibility and management rules. Existing links can be unlinked, including through access recovery. Identical retries of prior claim-creation and payment-link operations return their original result before the new eligibility check; they do not create new links. Other requests against ineligible transactions receive a generic privacy restriction without revealing claim or allocation existence.

This boundary does not eliminate every inference: the generic linked-transaction edit lock defined by [ADR-054](ADR-054-safe-edits-for-linked-reimbursement-transactions.md) remains visible where applicable and can indicate that a dependency exists. Removing that separate side channel would require a broader transaction-edit policy.

## Deployment and consequences

Deploy the matching frontend and schema-epoch-4 API, worker and migrator images together; migrate before accepting writes. Older binaries must not run against the new schema, as they do not enforce this policy. The migration defaults old rows to false and does not infer eligibility from private records.

Downgrade refuses while any reimbursement claim history exists: removing the marker and running older code would reintroduce the disclosure. Preserve backups and resolve history deliberately before rollback. Users cannot newly claim old expenses or allocate old credits even if they created them; recreating transactions merely to bypass this rule risks duplicate financial records.
