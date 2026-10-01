# ADR-019 — Household category management and historical assignments

**Status:** Accepted
**Date:** 2026-09-30

## Context

ADR-016 provides category identities, system localization keys and historical item snapshots. The next vertical slice needs category mutations without globally editing the system catalog, exposing private names or breaking historical transaction edits.

## Decision

- System definitions are read-only. Custom categories are household-shared and may be managed by any financially onboarded household member, consistent with shared account editing. Private categories and per-member/system preferences need separate models.
- Keep two hierarchy levels. A custom category may be a root or a child of an active compatible system/household root. Both-kind roots accept either kind; single-kind roots accept only the same kind. Prevent self-parenting, deeper trees, foreign-household parents and moving a category with any children beneath another category.
- Category kind is immutable after creation. Name, copied catalog icon, color and permitted parent are editable only while active. Duplicate names are allowed; identity is the category ID.
- System localized keys are never replaced with translated text. Select new icons from the existing catalog; retain an existing copied icon even if the catalog later removes it.
- Archive rather than delete. Parents cannot be archived while active children remain. Restoring a child requires an active parent. Archived categories remain in management/history/reporting, but disappear from new transaction choices.
- Use UpdatedAt as an EF concurrency token, advanced monotonically at PostgreSQL microsecond precision. All category mutations lock the household row to serialize hierarchy decisions; the version check still rejects stale editors.
- Transaction item input may include ExistingItemId, checked against the authorized transaction's original items. IDs may be supplied once only. An unchanged assignment preserves that item's original name/icon snapshot, including archived categories. New/reassigned items need an active compatible category. Older clients that omit the ID keep active-category behavior, but cannot retain an archived assignment.
- Resolve selected category rows with ordered FOR SHARE locks inside the same transaction as ledger writes. Category updates/archival wait for in-flight ledger writes; an archive that commits first is observed by later validation.
- Category edits do not rewrite ledger or aggregates. Historical detail uses snapshots; category reports use current category display data grouped by stable ID. Parent rollups remain out of scope.
- Implement the canonical tree/icon design with shared Tailwind components, compact filters, illustrative empty states, EN/DE/HU text and explicit stale-draft reload.

## Persistence and rollout

Add a self-parent check and category concurrency metadata through an EF migration. No data backfill is needed. Apply the migration explicitly before API deployment. No production startup migration is introduced.

## Boundaries

This slice does not implement pinning, drag ordering, global system customization, private categories, parent report rollups or personal-money attribution. Those are independent extensions, not implied by the visual prototype.

## Follow-up

ADR-020 extends the original boundary with personal category pins and localized ordering. Shared system definitions remain immutable.

ADR-021 supersedes the original two-level hierarchy and alphabetical pin-order restrictions, and requires modal category selection.
