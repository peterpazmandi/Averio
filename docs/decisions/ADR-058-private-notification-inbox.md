# ADR-058 — Private household notification inbox

Status: Accepted

Date: 2026-11-28

## Context

Reimbursement reminders currently appear only inside the reimbursement screens. Recurring rules expose due and blocked states in their own page. No durable personal inbox exists. A household can contain private accounts, claims, recurring rules and wallets, so a household-wide broadcast or an Owner-wide financial feed would violate ADR-003 and ADR-057.

## Decision and event catalogue

An inbox entry belongs to exactly one user and one household. `HouseholdMemberId` identifies the membership that owned the event; `RecipientUserId` is the authenticated inbox principal. The household role is never a blanket recipient rule. The backend checks active membership and current access before listing an entry and again when its destination is opened. The first UI shows the selected household's inbox. Entries contain only a type, target ID, due date and state. Do not persist descriptions, merchant names, claim amounts or private account names in the inbox.

| Type | Recipient | Due / idempotency key | Resolution | Priority |
| --- | --- | --- | --- | --- |
| `ReimbursementSubmissionDue` | Claim creator, if source, destination and payment links are visible | Claim ID + reminder date | Submitted, cancelled, rejected, inaccessible or source no longer posted expense | Action |
| `ReimbursementPaymentOverdue` | Claim creator, with the same visibility requirements | Claim ID + expected date | Fully received, rejected, cancelled, inaccessible, no outstanding amount or source no longer posted expense | Action |
| `RecurringConfirmationDue` | Rule creator | Rule ID + occurrence due date | Occurrence processed, paused, stopped, blocked, inaccessible or date advanced | Action |
| `RecurringNeedsReview` | Rule creator | Rule ID + blocked rule version | Rule edited, unblocked, stopped or membership ended | Action |
| `PersonalMoneyGrantPosted` | Grant recipient only | Grant receipt ID | Informational, future slice | Info |
| `PersonalMoneyGrantNeedsReview` | Rule-managing Owner only | Rule ID + blocked version | Rule corrected or stopped, future slice | Action |
| `PersonalMoneySourceAdjusted` | Each affected allowance holder separately | Mutation ID + recipient membership | Informational, future slice requiring transactional outbox | Important |
| `HouseholdInvitationChanged` | Inviter and authorized membership administrators | Invitation ID + transition | Informational, future slice | Info |
| `ReceiptScanReady` / `ReceiptScanFailed` | Initiating user only | Scan ID + terminal state | Informational, future slice | Info |

No event is emitted for every ordinary transaction or successful nightly run. Private claim and wallet existence must not be inferable from another member's bell badge or feed. A notification is a pointer to an authorized workflow, not proof of an account balance or of an external payment. The recipient must reauthenticate and reauthorize at the destination. A departed membership's entries are unavailable; old household financial history follows its existing read-only policy independently.

## Persistence and delivery

Store one inbox row per recipient and stable occurrence key, with created/read/dismissed/resolved timestamps. Unique `(recipient_user_id, type, target_id, occurrence_key)` makes retries harmless; read and dismissed states are never reset by reconciling the same occurrence. Due reminders are derived from existing claim/rule state, then materialized into the inbox. Reconciliation closes stale actionable entries. The first release reconciles on authorized inbox reads and in nightly maintenance; concurrent writers must use database idempotency, and no reminder may cause a bank movement.

For future mutation-triggered notices, stage a small outbox row in the **same transaction** as the financial mutation and fan it out idempotently to individually authorized recipients. Do not broadcast to a household group. This transactional outbox is required before the deferred Personal Money source adjustment event is shipped. The current due-state reminders are regenerated from authoritative state and do not require an outbox event for every date transition.

The first UI uses authorized HTTP reads on focus and a modest interval while open. Realtime delivery, email, push, user preferences and retained historical informational entries are subsequent decisions. Localize static labels in EN/DE/HU; only the authenticated destination shows sensitive details. Read/dismissal never modifies the underlying claim or schedule. No automatic production migrations occur at application startup.

## Consequences

The inbox is personally scoped even inside shared households. Submission, payment, due and blocked reminders converge on current workflow state, including retry and date changes. Privacy tests must cover two members, Owner versus private claim, departure, visibility changes, read/dismissal persistence, repeated reconciliation, and completed work removing the action. Deploy the database migration before the matching API and frontend.
