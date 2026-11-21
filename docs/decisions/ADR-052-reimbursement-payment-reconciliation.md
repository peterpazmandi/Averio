# ADR-052 — Receive and reconcile reimbursement payments

Status: Accepted

Date: 2026-11-20

Extends ADR-045/046/047 claim tracking with received-payment matching. ADR-048/050 merchant refund accounting remains separate.

## Decision

Reconciliation links an existing posted Income credit to a submitted claim. It does not create, reclassify, edit or void any bank transaction, restate expenses or income statistics, change activity results, or automatically restore Personal Money. This accounting policy was explicitly selected by the product owner. Claim coverage is a separate projection, not an additional financial movement.

Each link takes the whole credit, in the claim's destination account and currency, dated from the original purchase through today. A claim can receive multiple credits; each credit may belong to at most one active claim. Partial allocations of one credit across claims are deferred. The sum cannot exceed the claim estimate or current net source expense. Income with active Personal Money/refund dependencies, recurring/receipt associations, or a source-claim association is ineligible. Record absent bank income through the ordinary transaction workflow first.

Links persist amount/date snapshots, actor, operation ID, fingerprint and timestamps. Unlink retains history and the original Income unchanged. Status follows active payments: Submitted, PartiallyReceived, or Received. Outstanding is expected less received; fully received claims stop overdue reminders. Received claims stay visible for inspection and correction. Claim edits cannot reduce the estimate below active receipts or change their destination. Cancel/reject requires unlinking first. Source/credit ordinary edits and voids, source merchant refunds, and competing credit reclassification are blocked while matched. Account visibility changes are respected immediately; claims and badges are suppressed unless their creator can still see the source, destination and all historical credit references.

Financial writes use the existing household lock, with claim and credit revisions checked before new links. Identical operation retries return the original link even after unlinking. An old unlink retry is a no-op after a new link. A filtered unique credit index prevents two active links; operation IDs are unique per household. Foreign keys include household identity and restrict deletion. Import undo treats source claims and historical payment links as protected dependencies.

## Migration and rollout

AddReimbursementPayments adds a table and expands allowed claim states; existing claims and financial rows are unchanged. Downgrade refuses if any payment-link history exists, including unlinked history. API, Worker and migrator declare compatibility epoch 2 because old binaries cannot interpret settlement states or protect matched transactions. ADR-051 enforces maintenance and prevents fallback to epoch 1 binaries. Retain the database backup and host compatibility marker. Generate and deploy the matching frontend/client together; no migrations run at API startup.

## UX and verification

EN/DE/HU matching dialogs search and paginate eligible credits, show the resulting outstanding amount, require receipt confirmation and retain original requests after uncertain outcomes. Payment history exposes explicit bank-credit links and unlink confirmation. Closing a dialog does not cancel an in-flight server operation; users must check saved history before starting a different match.

Unit, PostgreSQL and browser tests cover partial/full receipt, unchanged financial records and totals, exclusivity, concurrent retries, unlink/relink, privacy, protected transactions/imports, migration history guards and uncertain responses. Provider submission, automatic matching, FX, and pooled-credit allocation remain outside this increment.
