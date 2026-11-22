# ADR-054 — Safe edits for reimbursement-linked transactions

Status: Accepted

Date: 2026-11-22

Supersedes the blanket ordinary-edit restriction in ADR-052. The accounting policy, unlink/recovery rules, privacy filtering and protection against voiding or new merchant refunds remain unchanged.

## Decision

Active reimbursement payment links protect both the source expense and incoming Income. An ordinary update may change merchant, note, item descriptions, categories, labels and activity assignment. It may also save unchanged values. Existing transaction authorization, reference validation, optimistic concurrency and the household write lock still apply.

The account, kind, date and visibility must remain identical. All existing item IDs and original paid amounts must be retained exactly, with no added, removed or duplicate items. Comparing original item amounts is necessary when a source already has merchant refunds: its reported expense is net, but edit inputs preserve the original payment. Explicit Personal Money allocation changes are rejected; omitted allocations retain existing entries. Active payments still prevent voiding and new source refunds.

Category and activity corrections may change their respective reporting breakdowns. Account totals, bank movements, reimbursement amount/date snapshots, claim status/coverage and Personal Money remain unchanged. Category aggregates are reversed and reapplied through the ordinary writer; they are not frozen or silently left stale.

## UI and privacy

Transaction detail and list responses include a generic paymentDetailsLocked flag for visible, modifiable transactions. It communicates an editing restriction without identifying a claim, creator, provider, credit, received amount or private history. It does not grant permission to access claims or relax ordinary transaction visibility.

The editor immediately shows a localized protection notice and disables the protected controls, voiding and adding merchant refunds. Descriptive fields and Save remain available. The notice lists protected fields and explains that linking is saved independently of the transaction form.

Link/unlink controls require a clean transaction draft. After a successful action the editor explicitly reloads saved transaction details and Personal Money, then updates its local snapshot and protection flag. A failed reload locks editing and offers Retry refresh; that action only reloads and never repeats the payment write. Closing a payment dialog after an unknown result also refreshes the editor. Background query updates never replace unsaved drafts or advance their concurrency token silently.

## Delivery and verification

No schema migration or compatibility epoch change is needed. Deploy the generated API client with the API. Older API binaries remain conservative and reject ordinary linked-transaction edits.

PostgreSQL regression tests cover both source and Income edits, unchanged saves, stale versions, protected fields and item identity, a prior merchant refund, accurate net category totals, unchanged account totals/Personal Money, preserved coverage and removal of protection after unlinking. Browser coverage verifies immediate lock/unlock without reopening, safe saves, dirty drafts, failed refresh/retry, and EN/DE/HU responsive notices alongside existing lost-response and access-recovery tests.
