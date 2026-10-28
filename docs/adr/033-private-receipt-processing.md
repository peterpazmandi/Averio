# ADR 033: Private receipt draft processing

Status: Accepted

Receipt OCR belongs before transaction creation. Averio already has explicit transaction ownership, household visibility, ledger rules and a worker; extracted text must not bypass those rules.

Use uploader-scoped receipt drafts with a seven-day lifetime, private object storage and PostgreSQL jobs claimed by the existing worker. Normalize image pixels before storage or provider submission. Use an explicit configurable Gemini model through a narrow structured-extraction interface. Provider/model rollout remains gated by a representative HU/DE/EN benchmark.

Application contracts do not depend on the provider or storage SDK. The infrastructure layer uses AWSSDK.S3 for authenticated object operations and SkiaSharp for bounded image decoding, orientation and metadata-free JPEG encoding. These dependencies avoid custom AWS request signing or image parsing. No direct ledger or account writes are permitted from extraction.

Leased atomic claims and token-fenced completion tolerate multiple worker instances and crashes. Storage operations occur outside database locks. Upload operation IDs and version-based discard handle client retries and concurrent changes. Daily admission limits are serialized by the existing household lock. Draft images are served only after authorization, with caching disabled.

This introduces external storage/provider operational dependencies only when enabled. Separate database and object writes require abandoned-upload cleanup and a bucket lifecycle safety net. Human review and posting UI follow in PR2; quotas, metrics and production rollout checks follow in PR3.
