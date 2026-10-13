# Dashboard category spending

The dashboard's canonical “Spending by category” card shows actual household-reportable expenses or income from the first day of the current UTC month through today. It uses the existing generated statistics client with `period=Day`; monthly aggregates would include future-dated entries later in the month. The date range refreshes while the page remains open and when the window regains focus.

The card defaults to Expenses and the household reporting currency. Side-by-side Currency and Transaction type selectors support Expenses and Income. Available currencies include the reporting currency, currencies with either type of activity, and the explicitly selected currency. While the card stays open, the selected currency and report link are preserved through loading and month rollover; missing activity shows its empty state instead of switching currency. Both selectors remain usable when the selection has no data. The subtitle reads “This month so far”; the underlying UTC reporting boundary is unchanged. Currencies are never added together. Exact decimal strings are summed and formatted without floating-point money calculations; only chart geometry and percentages are approximate.

The largest four direct categories and an “Other” remainder cover the complete selected-type total. The top four names are emphasized and Other appears last below a divider, including when its combined amount exceeds an individual leading category. Parent categories include only their own assigned items, not a rollup of descendants. Existing server-side visibility/reporting filters apply. Private transactions, excluded accounts, transfers, voided entries and out-of-range dates do not enter either view. Income and expenses are calculated separately. System category names are localized; custom names remain literal.

The card reuses the canonical donut/legend composition, shared Tailwind card/select/button primitives, emerald accent, EN/DE/HU copy and responsive light/dark presentation. The other dashboard cards also use the shared green-left-border surface. An accessible text legend includes exact amounts. Loading and failure never appear as an empty month; errors offer retry. The empty state opens the transaction editor, and View report opens Statistics.

No migration, backend contract, generated-client change or dependency is required. Existing transaction/category invalidation refreshes the report. Account mutation invalidation now also refreshes statistics so changing reporting participation cannot leave cached category totals behind.

## Verification

`node --test tests/*.test.mjs` in `apps/averio-web` includes exact totals, daily grouping, currency separation, direct categories, Other, large decimal ordering, zero/income-only rows, UTC boundaries and account-report invalidation.

`npm run test:collaboration` includes `tests/collaboration/category-dashboard.mjs`. It uses a dedicated household in the existing real API/PostgreSQL test host and covers browser create/edit/void, first-day/current-day inclusion, previous/future exclusion, private and excluded-account entries, currency/type switching, income totals, empty-income recovery, ranked top four/Other, consistent card accents, report navigation, foreign-household denial, localized desktop/light and mobile/dark states, loading, failure, retry and selected-currency retention across month rollover (including the loading interval). Only explicit loading/error scenarios intercept requests. Success paths use real reporting endpoints. CI runs this suite and retains failure evidence under the existing collaboration artifact.

See [Collaboration verification](collaboration-verification.md) for prerequisites. Screenshots are produced as `test-results/collaboration/category-*.png`.

## Contextual actions and category reports

A month with no household-reportable activity shows the first-use introduction. When activity exists in another currency or transaction type, the empty state instead names the selected type and currency. Its Add expense/Add income action passes a validated one-shot transaction kind to the editor; closing or saving clears that intent, and editing an existing transaction always retains its own kind.

The short footer explains household amounts and currency separation. Expand About these totals for privacy exclusions, direct category accounting and UTC date boundaries.

View report opens a focused category report inside Statistics with `from`, `to`, `currency`, and `kind` in the URL. These filters survive reload and browser Back/Forward. Reports use the same daily aggregates and exact decimal calculations as the dashboard, show every category, and support changing the period (up to one year), currency and type. The general Statistics view remains available through All statistics. Malformed or incomplete report URLs fall back to that view; invalid filter submissions show a localized validation message.

Click a dashboard category or report row to see its daily amounts. All categories removes the category selection; Other opens the complete category report so its constituent categories can be inspected. This is a reporting drill-down, not a list of individual transactions: it uses the existing household-reportable endpoint and preserves its server-side privacy restrictions without introducing a new API.

Browser tests cover contextual empty states, expense/income editor defaults, report query context and reconciliation, complete category lists, daily drill-down, reload and Back, changing filters, invalid ranges and localized light/dark desktop/mobile layouts.
