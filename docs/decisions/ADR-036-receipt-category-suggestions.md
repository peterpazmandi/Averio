# ADR-036: Receipt category suggestions and explicitly remembered household rules

Status: Accepted

Extends ADR-035 without changing ledger or visibility semantics.

## Decision

Supply Gemini with up to 500 active expense-compatible category IDs and hierarchy paths from the scan's household catalogue (system and custom categories). Category values and receipt contents are untrusted data. Ask for an optional category ID only for clearly understood Item lines; ambiguity or personal intent must produce null. Validate IDs against the supplied catalogue at the application boundary. Do not infer an account, owner, allowance or beneficiary.

At authorized inbox/detail reads, revalidate suggestions against the current catalogue and prefer a remembered household rule. Invalid/archived/income/foreign targets are ignored. Suggestions never select an item category automatically; the existing editor offers an explicit apply action and supports manual selection. Amounts, discounts and category accounting still follow ordinary transaction item handling; there is one payment, not additional expenses for its breakdown.

Each original Item line can opt in to “remember for our household” after selecting an active category. The label explains household sharing. Do not learn implicitly from edits, private history or applying a suggestion. Confirmation sends source-line/item-index pairs; the server resolves the original merchant and printed description from the owned Ready scan, and the category from the reviewed transaction item. Manual items, deposits, invalid/duplicate indices, conflicting mappings and uncategorized targets cannot create rules. Renaming a product does not erase the original evidence used for matching.

Rules match merchant plus printed description after Unicode NFKC, invariant case and whitespace normalization. Preserve punctuation, product sizes and all other distinctions; no fuzzy matching. Store a SHA-256 match key, category, household, updating member and timestamps, not raw receipt text or financial amounts. The hash is a storage minimization measure, not a promise of anonymity. Rules are deliberately shared household preferences and remain in that household if a member leaves. No receipt image or private transaction access is granted.

Save rules only inside the locked-household posting transaction. A later explicitly remembered choice replaces the same household match. Include nonempty rule selections in the immutable confirmation fingerprint, sorted by source/item index; preserve the exact previous fingerprint format for absent/empty selections. Lost-response retries cannot create another expense, replay rule writes or silently change consent. Invalid rules roll back the expense and all its effects. Discarded/failed/unconfirmed scans do not create rules.

## Deployment and limits

Apply `AddReceiptCategoryRules` after `AddReceiptConfirmationFingerprint`, then deploy the API, worker and frontend together. EF generated the migration and snapshot. No provider switch or additional model call is required: category suggestions use the existing Gemini extraction request. Existing drafts without model categories remain reviewable and can use household rules.

The first version supports exact-match rules and replacement through subsequent explicit confirmation. A rule management/delete screen, fuzzy product matching, category descriptions and measured model benchmarks are future work. There is no promise of calibrated model confidence or categorization accuracy. Review remains mandatory.
