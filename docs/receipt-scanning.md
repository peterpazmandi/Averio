# Receipt scanning

Receipt scanning creates a private, reviewable draft. Extracted text is untrusted and never writes transactions, balances, categories or allowances.

## Category suggestions and household rules

Gemini receives up to 500 available expense category IDs with hierarchy paths, including custom categories, in the existing extraction call. Clear Item lines may return an optional `suggestedCategoryId`. Unknown, foreign and non-item suggestions are discarded. Inbox/detail reads revalidate against current active categories and prefer an exact remembered household match. The editor labels the source and requires the user to apply a suggestion or select a category manually; uncertainty remains editable/uncategorized.

“Remember this category for our household” is unchecked by default and explicitly explains sharing. On confirmation, optional `rememberCategoryRules` entries map original `lineIndex` to reviewed transaction `itemIndex`. The server derives matching keys from the original merchant/printed item, validates the selected category, and upserts the rule atomically with the expense. No rules are saved on failed/discarded/unconfirmed scans. Nonempty rule consent is included in confirmation fingerprints; unchanged retries return the original expense without replaying rule writes. Absent/empty selections retain compatibility with earlier fingerprints.

Rules use normalized exact merchant/item matching, not fuzzy matching or other members' private financial history. Only match hashes, category IDs, household/member IDs and timestamps are stored. A later explicit remembered selection replaces that match; a dedicated rule management/delete screen is not included yet. Item breakdowns and discounts continue to use the existing single-payment accounting path.

Deploy `AddReceiptCategoryRules` after `AddReceiptConfirmationFingerprint`, followed by the API, worker and frontend together. See [ADR-036](decisions/ADR-036-receipt-category-suggestions.md). No new API key or extra model call is needed. Older drafts remain reviewable; model suggestions appear on new extractions and rules can apply to existing drafts.

| PR | Scope |
| --- | --- |
| 1 — Private processing foundation | Upload normalization, private S3 documents, durable jobs, Gemini structured extraction, uploader-only APIs, cleanup and generated client. Disabled by default. |
| 2 — Capture, review and posting | Capture/inbox/review UI, editable fields and category suggestions, reconciliation, account/currency/privacy choice, optional allowance allocation, atomic explicit confirmation into normal transactions. |
| 3 — Rollout and operational hardening | Rollout checks, private exact-photo duplicate suggestions, monthly quotas/entitlements, operational metrics, a HU/DE/EN benchmark runner and opt-in posted-photo retention. Live benchmark remains a release gate. |

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

Amounts are nullable invariant decimal strings with at most two decimal places. Original item descriptions, discounts, deposits, tax, subtotal and payment lines are preserved separately. A printed payment line is not a ledger transaction. Uncertain dates/currencies/totals require review; warnings are stable codes, not confidence scores. Provider text cannot select a household, owner or account, or assign a ledger category. Validated category IDs are suggestions only. Raw provider responses, image content and keys must not be logged.

## Deployment

Apply the EF migration before enabling the API/worker. Existing installations need no new credentials while disabled.

Set on API and worker:

- `Receipts__Enabled=true`
- `Receipts__DailyUploadLimit=20`
- `Receipts__Storage__Bucket` and `Receipts__Storage__Region`
- `Receipts__Gemini__ApiKey` through secret configuration
- `Receipts__Gemini__Model`: explicit model selection; no implicit production default

Use standard AWS role credentials. Configure a private S3 bucket with Block Public Access, encryption and least-privilege GetObject/PutObject/DeleteObject access to `receipt-drafts/*`. Uploads request AES256 server-side encryption. Set an eight-day lifecycle expiration for that prefix as an outage safety net. Use an unversioned bucket or configure noncurrent-version deletion too. Do not reuse this draft prefix for future posted attachments.

No bucket is created and no production credentials are provisioned by this change. Before enabling for users, benchmark 30–50 representative Hungarian, German and English receipts with the selected provider/model; verify amount/date extraction, discounts/deposits, privacy, latency and cost. Production S3/Gemini calls are not part of automated tests. PR 2 supplies explicit human confirmation and atomic posting; PR 3 supplies quota/retention controls and the benchmark runner. Broad rollout still requires the real benchmark and storage checks below.

## Capture and review (PR 2)

Open **Receipts** from navigation, the sidebar inbox shortcut, or **Scan receipt** on Transactions. Capture/upload accepts JPEG/PNG; the UI checks type and size before upload, while the server still decodes and validates the image. `GET /capabilities` exposes whether new uploads are enabled and the maximum size. Disabling new scans leaves existing drafts reviewable and cleanup operational.

The private inbox separates loading, failure, disabled and empty states; supports refresh and 50-row pagination; and polls while loaded scans are Uploading, Queued or Processing. An upload retry retains the file and operation UUID. A lost response triggers an inbox refresh before retry; abandoned Uploading records must be discarded and uploaded under a new operation. Discard requires explicit confirmation and the current scan version.

**Review receipt** opens the normal transaction editor with the private image, localized extraction warnings and every original observation. Only Item and Deposit lines prefill positive transaction items; discounts, tax, subtotal and payment lines remain evidence. Negative or unknown item amounts require correction. More than 100 candidate items require manually grouped items; the app never silently truncates them. Category suggestions match a unique complete existing, authorized category name in the item text and require a click to apply. They do not use other members' private transaction histories or create categories.

The reviewer must choose an account explicitly, resolve a missing date, check currency and total, reconcile all item amounts exactly and acknowledge the details/privacy before confirming. Currency choices are EUR/USD/HUF and account choices must match the reviewed currency; there is no implicit FX. Discounts must be reflected in the final positive item amounts. The normal editor retains labels, activities, visibility and optional item-level Personal Money allocations. Scanning cannot create a recurring rule or income transaction. Closing review preserves the server draft; unsaved local corrections are not persisted.

`POST /{id}/transaction` accepts `expectedVersion`, exact decimal-string `reviewedTotal`, `currency` and the existing `CreateTransactionRequest` as `transaction`. Posting rechecks active membership, uploader ownership, Ready/unexpired state, concurrency, total and account currency. It uses the normal transaction creation handler within one database transaction and the existing household lock. Transaction, items, aggregates, activity/allowance effects, immutable confirmation fingerprint and the compare-and-set transition to Posted commit together or all roll back. A receipt ID is the confirmation idempotency key, bound to the original reviewed request: identical concurrent/lost-response retries return the original transaction ID, including after draft expiry and later transaction edits, without another ledger or allowance write. Different account, amount, item/allocation, metadata or concurrency values conflict with HTTP 409. Legacy posted rows without a fingerprint also conflict; their original request is never guessed from mutable financial history. Changing a posted expense uses ordinary transaction editing/voiding; a posted scan cannot be discarded or reposted.

Receipt totals, item amounts and optional Personal Money amounts are submitted using the same invariant decimal normalization as reconciliation. For example, a displayed input of `0009,00` reconciles and is sent as `9`, which the API accepts. Ordinary manual/recurring transaction submission behavior is unchanged. See [ADR-035](decisions/ADR-035-receipt-confirmation-request-identity.md).

Posted scan metadata remains uploader-only. Shared transaction visibility never grants access to the private scan/image. Image access still expires after seven days; cleanup preserves Posted status and the transaction link after deleting the temporary image. PR 3 adds opt-in retention for new-prefix images; see the rollout controls below. Legacy temporary images keep this deadline. A transaction link uses ordinary transaction-detail authorization and does not grant additional access.

### Deployment and verification

Apply **AddReceiptPosting** after **AddReceiptScans**, then **AddReceiptConfirmationFingerprint**, before deploying the updated API/frontend/worker together as one compatible release. Older workers do not understand the Posted state; avoid mixed application versions. These add the nullable household-bound transaction foreign key, unique posting link, Posted state, immutable request fingerprint and database integrity checks. No shared database migration is run by implementation tooling. Keep receipt scanning disabled until the existing private-storage/provider benchmark gates are satisfied. Rollback to the old application version requires a deliberate data-aware migration plan once receipts are posted; do not drop posting links or fingerprints from an active installation.

The Verify workflow includes PostgreSQL confirmation privacy, invalid/stale/expired state, reconciliation, rollback, concurrent confirmation, retry and cleanup tests; exact-decimal/category frontend tests; and EN/DE/HU mobile/desktop light/dark browser scenarios for inbox recovery, review, confirmation retry, idempotent upload retry, discard and the disabled state. Browser screenshots are uploaded as `receipt-review-screenshots`. Automated tests use synthetic receipts and mocked extraction/storage, without production provider calls.



### Receipt review usability

Capture uses explicit camera/gallery buttons and a local photo preview with remove/replace controls before uploading. A cancelled picker keeps the existing selection. Retrying an uncertain upload keeps its operation ID. The inbox puts ready drafts first and explains automatic processing updates.

Review is grouped into purchase details, items/categories, and final total confirmation. Source photo and raw observations are expandable; extraction warnings remain visible. Labels follow the items in a collapsed optional section. Receipt privacy remains visible, and the acknowledgement sits beside the receipt/item totals at the end. Model and household category suggestions still require explicit application, and remembering a household rule remains opt-in. Ordinary transaction and recurring forms retain their existing layout.

## PR 3 — Rollout controls (ADR-037)

The rollout implementation adds household monthly allowances, private exact-photo duplicate suggestions, opt-in posted-image retention and content-free metrics. The temporary-image descriptions above remain applicable to legacy `receipt-drafts/` objects and to installations with retention disabled. Longer retention applies only to newly uploaded `receipt-images/` objects. There is no database migration in PR 3; apply all earlier receipt migrations before deploying.

### Configuration and safe deployment

Deploy API, worker and frontend together. Keep the worker/storage enabled for cleanup even when new uploads are disabled. Configure the same retention settings on API and worker; changing retention days affects future confirmations only.

| Setting | Default | Meaning |
| --- | --- | --- |
| `Receipts__FreeMonthlyLimit` | 10 | Household uploads per UTC month; zero disables free admissions |
| `Receipts__PaidMonthlyLimit` | 100 | Household uploads with a valid operator-managed paid entitlement |
| `Receipts__PaidHouseholds__<household-uuid>` | absent | ISO UTC entitlement expiry; configure only after independently verifying eligibility |
| `Receipts__RetainPostedImages` | false | Use the new image prefix for newly accepted uploads |
| `Receipts__PostedImageRetentionDays` | 365 | Days after posting; permitted range 1–365 |

Monthly limits may be 0–10,000, and the paid limit must be at least the free limit. These defaults are not finalized pricing. The application does not yet collect payment. Expired paid access returns to the free allowance; usage does not reset. No upgrade button implies a checkout that does not exist.

Before setting `RetainPostedImages=true`:

1. Extend the API/worker role's existing GetObject/PutObject/DeleteObject permissions to the private `receipt-images/*` prefix. Preserve Block Public Access and encryption.
2. Keep the old eight-day lifecycle on `receipt-drafts/*`. Configure a separate **400-day** expiration for `receipt-images/*` as an outage backstop. Do not leave a bucket-wide eight-day rule applying to this prefix. Configure noncurrent-version expiration if versioning is enabled. The worker remains responsible for timely seven-day draft and per-photo expiry/deletion; S3 lifecycle is a slower safety net.
3. In a nonproduction bucket, confirm a new draft expires after seven days, posting extends its API deadline, saved-photo deletion removes access immediately and the worker physically removes the object. Verify bucket rules and permission failures. A new-prefix object cannot exceed seven days before posting plus 365 retained days, below the 400-day backstop.
4. Verify another household member cannot GET the image or DELETE it, even when they can read the shared expense. Confirm transaction editing/voiding and photo deletion remain independent.
5. Deploy and complete the benchmark below before broad enablement. Disable new scanning if latency/failures/cost exceed your agreed budget; keep cleanup running. Rollback requires preserving the new prefix/deadlines and continuing a compatible worker, not merely restoring the old S3 lifecycle.

`GET /capabilities` now includes `monthlyLimit`, `monthlyUsed`, `resetsAt` and the retention promise for new uploads. Reaching the limit blocks new upload reservations only. Existing drafts, confirmation and photos remain available. Each accepted upload counts once, including failed/discarded uploads; retries preserve the operation ID. The API enforces the limit in the same locked transaction as reservation. The inbox shows aggregate household usage, reset time and clear exhausted/disabled states.

`ReceiptScanResult.imageAvailable` controls saved-photo actions. `possibleDuplicate` means the same normalized photo appears in this uploader's active inbox or posted history; it is not a universal duplicate detector. No other member's private information is used. A cropped or retaken photo can evade detection, so the user must still review before confirming.

### Operational measurements

Collect Meter **Averio.Receipts** from both API and worker using a .NET Meter-compatible collector, or `dotnet-counters monitor --process-id <pid> --counters Averio.Receipts` in an environment supporting diagnostics. Instrumentation does not deploy a monitoring backend or public metrics endpoint.

- `averio.receipts.uploads`: admitted/monthly_limit outcomes; admitted counts include reservations whose upload later fails.
- `averio.receipts.extractions`: ready/retry/failed/fenced attempt outcomes. Fenced results lost their lease or became inaccessible.
- `averio.receipts.processing.duration`: per-attempt seconds after storage/provider work and database completion.
- `averio.receipts.queue.age`: age from initial upload to each claimed attempt, including retries. It measures claimed work, not the age of unclaimed jobs during a worker outage.
- `averio.receipts.confirmations`: posted/replay; only committed new confirmations count as posted.
- `averio.receipts.cleanup`: deleted/retry outcomes.
- `averio.receipts.provider.tokens`: prompt, candidate and thought tokens when supplied by Gemini, including invalid-extraction responses that contain usage metadata. Missing metadata/HTTP failures may still have provider charges; reconcile against provider billing.

Start by investigating a sustained increase in failed attempts/cleanup retries, queue age over two minutes, and confirmation replay spikes. Monitor worker/container health separately: an idle or stopped worker cannot emit queue-age observations. Establish thresholds from the benchmark rather than claiming an unmeasured SLA. Record model-specific token prices separately; do not hard-code estimated dollar cost as an invoice.

### Reproducible HU/DE/EN benchmark — release gate still pending

No real receipt corpus or production provider run is included in this PR. Automated tests use synthetic images. Run 30–50 consented, privately stored receipts, at least ten per language, covering discounts, deposits, ambiguous dates, long receipts, blur and supported currencies. Independently label the correct payable total, date and currency before execution; manually assess item/category corrections and review time too.

Create a private JSON array (keep it and photos outside Git):

```json
[{"id":"hu-01","language":"hu","image":"images/hu-01.jpg","date":"2026-10-30","currency":"HUF","total":"12345"}]
```

Expand it to the full corpus. Set `Receipts__Gemini__ApiKey` and `Receipts__Gemini__Model` securely, then deliberately run:

```sh
dotnet run --project tools/Averio.ReceiptBenchmark -- --live /private/manifest.json /private/results.json
```

The command makes billable calls, uses the production normalizer/extractor/validator, uses the same 50-second provider HTTP timeout and 60-second overall processing deadline as production, and checkpoints per-sample accuracy, latency and available token counts without image/provider text. It exits nonzero unless total/date accuracy is at least 95% **per language** and currency accuracy is 100%; ten-sample language groups therefore require all ten to pass. Errors count as incorrect. The tool does not automatically retry or resume; after interruption inspect the checkpoint before deciding whether to incur repeat charges. Empty category catalogues mean this runner checks extraction fields, not custom-category suggestion accuracy.

Record the dated model/version, private results location, median/p95 latency, observed provider spend, correction time, item/discount/category review findings and operator sign-off. Passing field scores alone does not release the feature: validate privacy, duplicate warnings, quota boundaries, image cleanup, mobile capture and the existing accounting flow. Mark this gate complete only after that real run.
