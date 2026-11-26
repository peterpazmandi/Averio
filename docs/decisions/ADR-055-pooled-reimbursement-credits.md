# ADR-055 - Allocate one received credit to several reimbursement claims

Status: Accepted

Date: 2026-11-23

Supersedes ADR-052's exclusive-credit and whole-credit allocation restrictions. Its other accounting, privacy and lifecycle rules remain in force.

The eligible credit and source scope is superseded by [ADR-057](ADR-057-reimbursement-privacy-boundary.md); pooling remains available within that boundary.

## Decision

An existing posted Income transaction remains one bank movement. The claim creator explicitly assigns a positive amount, precise to four decimal places, to a submitted claim. Several claims may reference the same credit, provided the sum of their active allocations does not exceed its original amount. Each allocation also fits the claim's outstanding amount and source expense limit. One claim may have at most one active allocation from the same credit; unlink that allocation before replacing it.

Candidate search computes available credit from active allocations before pagination and exposes the original and available amounts. The financial write obtains the household lock, reloads active allocations and validates both limits inside the transaction. The original credit revision and claim revision remain required. An identical operation retry returns its original allocation, even if later unlinked. Unlink removes only the selected claim allocation; the posted Income, other allocations and bank totals do not change.

Privacy and other dependency checks remain in force. Merchant refunds still cannot reclassify a credit that backs active reimbursement payments, and an actively reimbursed source expense cannot receive a merchant refund. Pooled merchant refund accounting is a separate decision.

## Rollout

`AllowPooledReimbursementCredits` replaces the active unique credit index with an active non-unique lookup and an active unique claim/credit pair. Existing payment amounts already store full-credit allocations and are unchanged. Downgrade refuses while a credit has multiple active links. API, Worker and migrator images use schema epoch 3; deploy the matching frontend and migrate before starting the new API. Old backend images must not run on this schema.

