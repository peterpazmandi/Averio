# ADR-016 — Reusable labels, hierarchical categories, icon snapshots, and dimension aggregates

**Status:** Accepted  
**Date:** 2026-09

## Context

The first Transaction Management implementation proved the ledger, itemized receipt, privacy, and synchronous aggregate approach, but the review exposed four missing domain concepts:

1. labels need to be reusable choices rather than duplicated text attached independently to every transaction
2. transaction items need real categories with hierarchy rather than free-text category snapshots
3. category appearance needs a large reusable icon catalog without making categories depend on mutable icon rows
4. reporting needs yearly buckets and category totals in addition to account Day/Month cash flow

The reporting model must still preserve Averio's privacy rule: private details must not leak through category statistics, and currencies cannot be silently combined.

## Decision

### Reusable labels

Averio stores reusable `Label` entities.

A Label belongs to one household and has one of two scopes:

- **Household** — available throughout the household; `AccountId = null`
- **Account** — available only while recording/editing transactions for one specific account

A transaction selects zero or more labels through the `transaction_labels` join table.

The transaction editor always loads applicable reusable labels for the selected account. Existing labels are selectable and a new Household/Account label can be created inline and immediately selected.

Label scope does not override resource visibility. A label never grants access to a transaction.

### Categories

Averio stores categories in a first-class `categories` table.

A Category contains:

- stable Id and Code
- optional HouseholdId
- optional ParentId referencing another Category
- Name
- kind: Expense, Income, or Both
- copied Icon value
- accent color
- sort order
- base/archive state
- timestamps

The initial catalog contains exactly **100 base categories**, organized as 20 parent categories with four children each. Base categories are shared product data (`IsBase = true`, `HouseholdId = null`). The schema also supports household-specific categories for the later Category Management slice.

Each transaction item references a Category ID and also stores CategoryName and CategoryIcon snapshots. This keeps historical item presentation stable while the live Category remains the classification identity.

### Icon catalog and snapshot semantics

Averio seeds an `icons` table with **240 icon choices**.

There is intentionally **no foreign key from Category to Icon**.

Selecting an icon copies the icon value into `Category.Icon`. The Icons table is a chooser/catalog, not the owner of category appearance. Editing/removing/reordering catalog choices therefore cannot silently change existing categories.

### Aggregate dimensions

The reporting aggregate supports:

- periods: **Day, Month, Year**
- dimensions: **Account, Category**

Every aggregate row retains `AccountId` as the source-account privacy/reporting boundary even when `DimensionType = Category`.

For the Account dimension:

- `DimensionId = AccountId`
- amount contribution is the transaction's item-derived total
- `EntryCount` means transaction count

For the Category dimension:

- `DimensionId = CategoryId`
- amount contribution is the sum of transaction items assigned directly to that category
- `EntryCount` means categorized item count

Category parent/child rollups are not duplicated into persisted rows. A parent rollup can later be produced from the category tree at read time. This prevents one item from being persisted into several hierarchy buckets and makes rebuild verification simpler.

Create, edit, and void reverse/apply both account and category contributions in the same database transaction as the ledger mutation.

Only posted Household-visible transactions contribute to household aggregates. Aggregate reads additionally apply the current source account's visibility and reporting-participation rules.

Currencies remain independent aggregate series.

## Consequences

- transaction labels can be reused instead of repeatedly typed
- account labels cannot accidentally appear as choices for another account
- receipt items have real category identities and a stable category/icon snapshot
- category hierarchy and icon selection can evolve into a full management page without changing transaction storage again
- the icon catalog can change without mutating category appearance
- yearly account totals are maintained without rescanning the raw ledger
- yearly/monthly/daily category totals can be read without scanning transaction items
- category reporting remains privacy-safe because every category aggregate retains its source account
- account `EntryCount` and category `EntryCount` intentionally have different units and must not be compared as if they were the same metric
- parent-category rollups require hierarchy-aware read logic when the product needs them
- cross-currency totals still require a future explicit FX model
