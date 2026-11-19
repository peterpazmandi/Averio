# ADR-045: Recurring reimbursement opportunities require external submission

Status: Accepted

Date: 2026-11-18

## Context

An eligible mortgage payment does not imply that a health fund will reimburse it. The receipt must be submitted externally, and even then the expected amount is not bank income. Recurring processing must tolerate retries and preserve historical estimates and financial privacy.

## Decision

Create a reimbursement opportunity atomically with a successfully recorded recurring expense. Persist immutable occurrence estimates and a unique source-transaction association. Require explicit user confirmation of external submission before exposing a submitted receivable. Track NeedsSubmission, Submitted, Rejected and Cancelled with append-only events and optimistic concurrency. Keep reminders in-app and derive their due state from UTC dates.

Claims belong to the recurring-rule creator and require visibility of the source transaction and destination account. Account mutation authority remains independent of household administrative role. Estimate tracking never writes incoming transactions, transfers, balances or aggregates. Bank-payment matching and received statuses are a separate deliverable.

## Consequences

Users can distinguish potential reimbursement from requested reimbursement without overstating cash. Retries cannot duplicate opportunities, and rule edits cannot rewrite past estimates. A source payment edit requires review; voiding excludes the claim from expected totals and reminders. Received amounts remain zero until matching is implemented. This slice intentionally has no retrospective backfill, provider upload, outgoing notifications or received-payment reconciliation.
