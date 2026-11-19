# ADR-046: Manual claims share the expense reimbursement workflow

Status: Accepted

Date: 2026-11-18

## Decision

Allow reimbursement claims for ordinary recorded expenses without requiring a recurring rule. Reuse the existing claim snapshots, submission confirmation, status history, privacy checks and reminders. The recurring-rule reference becomes optional; existing recurring claims remain intact.

New expenses can create their claim in the same database transaction. Existing expenses use a separate creation endpoint with the saved transaction revision. The existing unique source-transaction index prevents multiple claims for one payment. A private request fingerprint permits identical retries by the same claim creator while different requests receive a generic conflict without exposing existing claim details.

Claim ownership belongs to its creator, independent of household administrative role. Current source account authority and visibility of both accounts are required. Destination currency must match the source payment. Claim creation never adds incoming transactions or writes financial aggregates; the original expense is the only financial movement in the atomic create path.

## Consequences

One-off, imported and scanned expenses can use the same workflow after they are recorded. Unsaved expense edits are not implicitly applied by the separate claim action. Manual claims preserve history without a schedule, so rollback of the nullable-rule migration refuses to proceed while manual claims exist. Payment matching and received-payment reconciliation remain a separate deliverable.
