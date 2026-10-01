# ADR-020 - Personal category pins and localized ordering

**Status:** Accepted
**Date:** 2026-10-01

## Context

The category management follow-up requests alphabetical overviews/selectors, meaningful icon names and pins that work throughout Averio. This extends ADR-019's original boundary, which deferred pinning.

## Decision

- Pins are personal preferences scoped to household membership, including pins on read-only system categories. They never change shared category data, timestamps, ledger history or another member's choices.
- Persist membership/category pairs with a composite primary key and foreign keys. Membership deletion removes its preferences. The authenticated user determines membership; no user/member ID is accepted from the client.
- Use an idempotent PUT with the desired boolean, not a toggle. Serialize against category archival using the existing household lock. Last committed intent wins across sessions; retries cannot create duplicate pins.
- Only available active categories may be newly pinned. Archival retains the preference but active selectors exclude the category. Restoring makes its pin effective again; archived pins may be removed.
- Sort names with Intl.Collator for the current UI language. Use the displayed translated system name or custom name, then stable ID as a tie-breaker. Never mutate query-cache arrays.
- Overviews preserve alphabetical parent/child trees and provide an alphabetical pinned shortcut section above them. Selectors put every eligible pin first, including child categories, followed by other categories alphabetically. Show parent context alongside child choices.
- All category selectors use AverioCategorySelect. Refresh the user/household category query prefix after pin changes, including failed requests whose outcome is uncertain.
- Replace icon placeholder names via an explicit data migration, preserving IDs and emoji values. Translate all 240 existing names in EN/DE/HU. Future unmapped icons fall back to their API name.
- Use AverioIconPicker for named, searchable alphabetical tiles with native radio keyboard behavior, selected preview, accessible labels, responsive layout, and light/dark styles.

## Rollout

Apply AddCategoryPinsAndIconNames explicitly before deploying the new API. No startup production migration is introduced. API category responses add IsPinned; regenerate the TypeScript client.

Manual drag ordering, shared category preferences and parent financial rollups remain separate features.
