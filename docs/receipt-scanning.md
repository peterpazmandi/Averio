# Receipt scanning

Receipt scanning creates a private, reviewable draft. Extracted text is untrusted and never writes transactions, balances, categories or allowances.

| PR | Scope |
| --- | --- |
| 1 — Private processing foundation | Upload normalization, private S3 documents, durable jobs, Gemini structured extraction, uploader-only APIs, cleanup and generated client. Disabled by default. |
| 2 — Capture, review and posting | Capture/inbox/review UI, editable fields and category suggestions, reconciliation, account/currency/privacy choice, optional allowance allocation, atomic explicit confirmation into normal transactions. |
| 3 — Rollout and operational hardening | End-to-end rollout checks, duplicate suggestions, paid monthly quotas, operational metrics, HU/DE/EN provider benchmark and posted-receipt retention. |

## API

All endpoints require an authenticated active household member. Drafts and images belong to their uploader, including within a shared household.

Base: `/api/v1/households/{householdId}/receipt-scans`.

- `POST`: multipart `operationId` UUID and required `file`; returns 202 with draft status. Repeating an operation with the same normalized image returns the existing record; different content conflicts. Operation IDs remain reserved after discard/expiry. An abandoned upload is not resumed: upload with a new operation ID after checking status.
- `GET`: newest first, 50 at a time, `skip` offset (0–10000).
- `GET /{id}`: status, concurrency version, sanitized failure code and extracted draft when Ready and unexpired.
- `GET /{id}/image`: authenticated JPEG response, no public URL, no cache.
- `DELETE /{id}?expectedVersion={version}`: clears the draft and fences active extraction; worker deletes the document asynchronously. Stale versions conflict.

Uploads accept decoded JPEG/PNG only, up to 10 MiB, 24 million pixels, dimensions 32–12000. Fresh JPEG encoding applies orientation and removes metadata. PDF, HEIC, animations and other formats are rejected. Five uploads per minute per authenticated user are permitted per API instance, plus a database-serialized rolling daily limit (default 20 per member). These are abuse controls, not subscription quotas.

## Processing and privacy

`Uploading → Queued → Processing → Ready / Failed`. Transient failures requeue after one minute, up to three attempts. Atomic `SKIP LOCKED` claims use a two-minute lease; completion requires the live lease token. Worker timeouts are shorter than leases. Crashed final attempts become Failed. Discard, expiry and departed membership fence completion.

Uploads abandoned for five minutes and drafts older than seven days are cleared and deleted by the worker. Status and idempotency metadata remain; extracted JSON is cleared on discard. Physical deletion waits until at least five minutes after upload creation, including when a user discards an upload immediately. Storage writes have a one-minute deadline anchored to that persisted creation time; the remaining interval lets in-flight writes settle even if the API process exits. A discarded image becomes inaccessible immediately. Cleanup claims use the existing next-attempt timestamp as a two-minute reservation and retry failed deletions after one minute. Each object failure is isolated. Cleanup orders by next attempt before creation time so previously failed objects cannot starve unattempted deletions. Cleanup and extraction run in independent loops and scopes, so slow storage or provider requests do not block the other workload. Keep the worker and storage configuration active when disabling new scans so cleanup continues.

Amounts are nullable invariant decimal strings with at most two decimal places. Original item descriptions, discounts, deposits, tax, subtotal and payment lines are preserved separately. A printed payment line is not a ledger transaction. Uncertain dates/currencies/totals require review; warnings are stable codes, not confidence scores. Provider text cannot select a household, owner, account or category. Raw provider responses, image content and keys must not be logged.

## Deployment

Apply the EF migration before enabling the API/worker. Existing installations need no new credentials while disabled.

Set on API and worker:

- `Receipts__Enabled=true`
- `Receipts__DailyUploadLimit=20`
- `Receipts__Storage__Bucket` and `Receipts__Storage__Region`
- `Receipts__Gemini__ApiKey` through secret configuration
- `Receipts__Gemini__Model`: explicit model selection; no implicit production default

Use standard AWS role credentials. Configure a private S3 bucket with Block Public Access, encryption and least-privilege GetObject/PutObject/DeleteObject access to `receipt-drafts/*`. Uploads request AES256 server-side encryption. Set an eight-day lifecycle expiration for that prefix as an outage safety net. Use an unversioned bucket or configure noncurrent-version deletion too. Do not reuse this draft prefix for future posted attachments.

No bucket is created and no production credentials are provisioned by this change. Before enabling for users, benchmark 30–50 representative Hungarian, German and English receipts with the selected provider/model; verify amount/date extraction, discounts/deposits, privacy, latency and cost. Production S3/Gemini calls are not part of automated tests. PR2 must introduce explicit human confirmation and atomic posting; PR3 completes the release and paid quota controls.
