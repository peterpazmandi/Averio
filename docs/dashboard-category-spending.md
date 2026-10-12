# Dashboard category spending

The dashboard's canonical “Spending by category” card shows actual household-reportable expenses from the first day of the current UTC month through today. It uses the existing generated statistics client with `period=Day`; monthly aggregates would include future-dated entries later in the month. The date range refreshes while the page remains open and when the window regains focus.

The card initially prefers the household reporting currency when that currency has expenses, otherwise the first available currency. Members can choose another expense currency. Currencies are never added together. Exact decimal strings are summed and formatted without floating-point money calculations; only chart geometry and percentages are approximate.

The largest four direct categories and an “Other categories” remainder cover the complete expense total. Parent categories include only their own assigned items, not a rollup of descendants. Existing server-side visibility/reporting filters apply. Private transactions, excluded accounts, income, transfers, voided entries and out-of-range dates do not become category spending. System category names are localized; custom names remain literal.

The card reuses the canonical donut/legend composition, shared Tailwind card/select/button primitives, emerald accent, EN/DE/HU copy and responsive light/dark presentation. An accessible text legend includes exact amounts. Loading and failure never appear as an empty month; errors offer retry. The empty state opens the transaction editor, and View report opens Statistics.

No migration, backend contract, generated-client change or dependency is required. Existing transaction/category invalidation refreshes the report. Account mutation invalidation now also refreshes statistics so changing reporting participation cannot leave cached category totals behind.

## Verification

`node --test tests/*.test.mjs` in `apps/averio-web` includes exact totals, daily grouping, currency separation, direct categories, Other, large decimal ordering, zero/income-only rows, UTC boundaries and account-report invalidation.

`npm run test:collaboration` includes `tests/collaboration/category-dashboard.mjs`. It uses a dedicated household in the existing real API/PostgreSQL test host and covers browser create/edit/void, first-day/current-day inclusion, previous/future exclusion, private and excluded-account entries, currency switching, Other, report navigation, foreign-household denial, localized desktop/light and mobile/dark states, loading, failure and retry. Only explicit loading/error scenarios intercept requests. Success paths use real reporting endpoints. CI runs this suite and retains failure evidence under the existing collaboration artifact.

See [Collaboration verification](collaboration-verification.md) for prerequisites. Screenshots are produced as `test-results/collaboration/category-*.png`.
