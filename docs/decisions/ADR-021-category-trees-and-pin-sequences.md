# ADR-021 - Nested category trees, pin sequences and modal selection

**Status:** Accepted
**Date:** 2026-10-01

## Decision

The explicit category UX follow-up supersedes ADR-019's two-level limit and ADR-020's alphabetical pin order.

- Custom categories may belong below any active compatible category, including subcategories. Moving a branch preserves its descendants. Under the household transaction lock, traverse the complete proposed ancestor chain to reject cycles, self-parenting and unavailable ancestors.
- Category kinds remain immutable, system definitions remain read-only, and household isolation is unchanged. Archive active descendants from the leaves upwards; restore ancestors before descendants.
- Personal pins have persisted integer positions. New pins append; retained pins keep their positions across archive/restore. Up/down controls reorder the user's pins, including subcategories. Reordering filtered visible pins swaps their positions without dropping hidden pins.
- A pin-order PUT supplies both desired IDs and the expected existing sequence. Under the same household lock, reject stale sequences and non-permutations. Never accept another member's category preferences or create pins implicitly.
- Selectors show pinned shortcuts in saved sequence above an alphabetical category tree. Main categories have distinct surfaces and labels; descendants have branch lines, indentation and full ancestor paths. Keep ancestor context when filtering.
- Use the existing AverioDialog popup for category choices. Opening it must not expand the transaction editor. Escape closes only the top popup; focus returns to its trigger, and nested dialogs share a counted body-scroll lock.
- Parent selection excludes self and descendants in the UI as well as validating the complete chain on the server.

## Persistence and rollout

Apply AddCategoryPinOrder explicitly. Existing pins receive deterministic positions by creation timestamp and category ID; their membership/category identity remains unchanged. No transaction snapshots, balances or reporting rollups change.
