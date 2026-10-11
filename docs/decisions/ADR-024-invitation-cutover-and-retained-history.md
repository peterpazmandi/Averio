# ADR-024 — Invitation cutover and retained household history

**Status:** Accepted  
**Date:** 2026-10-09

## Context

Users with existing finances must be able to accept an invitation without deleting
their old household or revealing its ledger to their new household. This decision
supersedes ADR-023's joining eligibility, empty-source deletion, and deferred
ownership-transfer policy. Its invitation security and household settings rules
remain applicable.

## Decision

Keep one active membership per user. Retain departed memberships with a departure
timestamp for authorship and account-holder attribution. When the departing owner
leaves other members behind, require an explicit successor. Archive a household
when its last member leaves; never delete it during invitation acceptance.

At acceptance, copy each active personal account held solely by the departing
member into the destination. Require a reviewed opening balance and an explicit
choice of personal/private, personal/shared, or household-owned handling. The copy
has a new identity and an opening balance as of the acceptance date. Do not copy
transactions, transfers, categories, activities, or other source context.
Household-owned and jointly held accounts stay in their original household.
Archived accounts are retained there and are not copied.

Permanently close the copied source accounts. Keep their original ledger and
reporting participation, so departure does not rewrite source cash flow. Remaining
members retain their existing visibility, with former-holder attribution.

Capture an immutable, server-filtered snapshot of what the departing member could
see at departure. Only that user can read their snapshot. It includes account
balances, visible transaction details, and visible transfer legs; redact hidden
legs and shared transfer notes when either leg is hidden. Later source edits and
entries are never exposed through the snapshot. Previous-household reports use
this frozen data, keep currencies separate, and exclude private transactions,
voided entries, transfers, and opening balances from household cash flow.
They are separate from the active household's totals and provide no write actions.

An unconfigured registration placeholder with no visible financial records is
archived without creating a redundant history entry. A verified invitee sees
invitation onboarding before household creation, even when verification happens
in another tab. They may explicitly decline an invitation to resume solo setup.
No household or invitation details are exposed before verified email matching.

## Concurrency and consequences

Acceptance locks the user, then source/destination households in ID order and
revalidates invitation authority, source version, account versions, balances,
complete handling choices, and successor. Financial mutation handlers acquire the
household lock before authorization and reads, then recheck active membership
after waiting. A changed review returns a conflict and requires another preview.
Snapshot, departure, ownership succession, membership, account copies, and token
consumption commit together. Active-user and active-owner filtered unique indexes
enforce cardinality while retaining historical memberships.

Snapshots increase storage and acceptance work in proportion to visible history.
There is no automatic history merging, live multi-household switching, bank-feed
reconnection, standalone leave/member-removal UI, or destructive history deletion.
Rollback after departures needs a data plan because the former unfiltered
membership indexes cannot represent the retained records.
