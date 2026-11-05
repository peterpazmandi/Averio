# Averio Current State

**Status date:** 2026-10-28
**Scope:** restarted implementation on the `develop` branch

This document describes only the restarted codebase. Features from the earlier
implementation are not counted unless they exist in the current repository.

## Repository and platform foundation — implemented

The solution is a .NET 10 modular monolith with explicit application handlers:

```text
apps/Averio.Api
apps/averio-web
src/Averio.Application
src/Averio.Contracts
src/Averio.Domain
src/Averio.Infrastructure
```

The backend foundation includes ASP.NET Core, PostgreSQL/EF Core migrations,
FluentValidation, localized Problem Details, cookie authentication, CSRF
protection, rate limiting, OpenAPI, and Scalar. MediatR is not used.

The frontend uses React, TypeScript, Vite, Tailwind, TanStack Router, TanStack
Query, and react-i18next. English, German, and Hungarian are supported.

The backend OpenAPI document is the authoritative frontend contract. The
generated TypeScript client covers every implemented API area, and CI fails if
regeneration produces a diff. Handwritten feature adapters may narrow UI-only
literal types, but do not duplicate API DTO shapes.

## Identity and authentication — implemented foundation

Implemented flows include:

- registration and email verification
- resend verification
- login, logout, cookie session, and current-user state
- forgot-password and password reset
- localized auth pages and errors

## Household and onboarding — implemented

The current model includes `Household`, `HouseholdMember`, identity membership,
profile/household onboarding, and first-account setup. The first-account use
case retains explicit transaction handling and household-row locking.

The household context is the authorization boundary used by account,
transaction, dashboard, and statistics use cases.

## Account Management — implemented

Account Management provides:

- active and optional archived account lists
- dedicated create and edit routes with direct-link loading
- household and personal economic scope
- explicit personal-account holders
- household/private visibility and reporting participation
- immutable currency and balance-anchor fields after creation
- archive and restore without deleting financial history
- server-side visibility and modification checks
- optimistic concurrency through `updated_at`
- current balances derived from each account's anchor plus posted, visible
  ledger movements on or after the anchor date

Household-scope accounts are household-visible and included in reports.
Personal accounts require at least one holder. Private accounts are personal,
excluded from household reports, and remain inaccessible to other household
members. Household role does not grant access to another member's private
account.

## Transaction Management — implemented foundation

The transaction slice includes:

- itemized Income and Expense transactions
- exact four-decimal amounts with totals derived from item values
- merchant, note, occurrence date, status, and optimistic version fields
- Household and Private transaction visibility constrained by account rules
- edit and non-destructive void workflows
- searchable, cursor-paginated transaction history
- a hierarchical category model with exactly 100 seeded system categories
- stable category localization keys with EN/DE/HU frontend names
- category name/icon snapshots on transaction items
- a 240-entry icon catalog; categories copy icon values and have no icon FK
- reusable Household and Account labels
- synchronous Account and Category aggregates at Day, Month, and Year grain
- currency-separated cash-flow and category-statistics read models
- account and dashboard balances derived from posted requester-visible entries
- shared transaction editor entry points from the header, dashboard, and list
- a searchable category picker with parent context and responsive statistics UI

Aggregate writes participate in the same PostgreSQL transaction as ledger
create, update, and void operations. Private transactions do not contribute to
household aggregates. Category statistics retain the source account dimension
so current account visibility/reporting rules are applied when reading them.

## Dashboard and statistics — implemented foundation

The dashboard endpoint returns visible accounts with current balances,
household balances by currency, six months of cash flow, and the latest visible
transactions. The statistics page presents monthly cash flow and yearly
category totals without combining currencies.

The dashboard category card shows month-to-date household-reportable expenses using daily aggregates through today (UTC), separate currencies, exact totals, and the largest four direct categories plus Other. It replaces the category placeholder with the canonical donut/legend design, localized states and browser verification. See [Dashboard category spending](dashboard-category-spending.md).

## Personal Money foundation — in this PR (not yet merged)

ADR-029 formalizes member-specific entitlement separate from financial accounts and reporting. This branch introduces Wallet/Entry persistence, manual Owner grants with operation idempotency, an owner-only wallet read, explicitly linked private charges, and explicit transfer funding purposes. Existing transfer edits/voids and transaction edits/voids append correcting entries. No automatic monthly grants or bank execution is introduced.

Important first-slice limitations: no shared-account charges, no dedicated boundary cash-flow metric, no refund/return settlement, no cross-currency attribution, no wallet snapshot inside a departed household history, and no dedicated Personal Money dashboard outside Household Settings. The initial EN/DE/HU Household Settings allowance controls, private purchase charge form, and transfer funding purpose selector are present, but dedicated end-to-end personal-money browser tests remain needed. These must be resolved before presenting the feature as a complete household finance solution.

## Personal Money Shared Spending & Reconciliation — PR 2 (merged)

This new PR adds ADR-030, shared-account spending attribution, manual refund linkage, private funding returns and entitlement surrender, plus a distinct monthly financial-boundary projection that does not mutate existing categorized income/expense aggregates. UI includes shared purchases, refund/surrender and funding return selection. The complete source and integration checks must pass before merge. Cross-currency matching, automation and scheduled grants remain deferred.

## Monthly Recurring Personal Money — PR 3 (merged)

PR 3 builds on merged manual Personal Money and PR 2 reconciliation. It introduces recurring monthly **entitlement** rules, owner-only lifecycle controls, anchored month-end dates, member-only upcoming grant visibility, an append-only occurrence receipt with database uniqueness, worker catch-up, EN/DE/HU UI and explicit non-transfer semantics. See ADR-031 and docs/personal-money-recurring.md. The feature was merged in PR #18 after migration, PostgreSQL, OpenAPI, browser and collaboration checks.

## Item-Level Personal Money and Transaction Lifecycle — PR 4 (draft)

[PR #19](https://github.com/peterpazmandi/Averio/pull/19) builds on merged PR 3 and ADR-032. This draft introduces optional private per-item partial/full wallet charges on eligible shared/private receipts, stable IDs across transaction edits, privacy-preserving source correction and reversals, an append-only item reference in the member-private ledger, responsive item-level Tailwind controls with EN/DE/HU translations, and authorized archive/restore distinct from financial void. Migration, OpenAPI TypeScript client, PostgreSQL and browser regression checks are part of the PR. The PR must not merge before all checks pass. Dedicated holder-private notification/outbox delivery and member-only hide preferences remain explicitly deferred.

## Verification and CI — implemented

The repository has three executable backend verification suites:

- `tests/Averio.Accounts.Tests` for account domain/application/EF regressions
- `tests/Averio.Transactions.Tests` for transaction domain and EF-model checks
- `tests/Averio.Persistence.Tests` for disposable PostgreSQL integration checks

The PostgreSQL suite creates an isolated temporary database and verifies the
complete migration chain, seeded catalogs, transactional rollback, persisted
transaction details, aggregate and balance changes across create/update/void,
privacy boundaries, stale-write handling, idempotent voiding, and database
optimistic concurrency. It always removes its temporary database.

GitHub Actions now verifies:

- solution restore and Release build
- all three executable backend suites against PostgreSQL 17
- frontend clean install, production build, and repository-wide lint
- deterministic OpenAPI client regeneration

## Canonical UI concept

`docs/ui/averio-ui-concept.html` remains the canonical design reference. It is
a product/UI specification, not evidence that every illustrated feature has
been implemented.

## Current boundaries

The following are not yet treated as implemented:

- member removal, leaving and ownership transfer
- advanced recurring schedules, recurring transfers and reminders
- category drag reordering, shared system-category customization, and private categories
- goals
- cross-currency personal-money attribution, bank refund detection and automatic settlement
- historical CSV/XLSX import
- production rollout of AI receipt scanning (disabled by default; provider benchmark and operational gates pending)
- bank connections
- 2FA and social login
- production Worker project

Aggregate repair/rebuild tooling is also not present yet. Current aggregate
maintenance is synchronous and transactional.

## Infrastructure status and target

The target remains Cloudflare for frontend edge/static hosting, AWS EC2 with
Docker Compose for the API and future Worker, Neon PostgreSQL, and ECR where
container images are used. Earlier deployed containers do not represent the
restarted application's current production readiness.

## Recommended next implementation milestone

Verify the two-person beta journey and operational email configuration next. Invitations implement ADR-023 lifecycle security and ADR-024 account cutover with retained, read-only history. Standalone leaving and removal remain deferred. Recurring income/expense execution is implemented in ADR-025 with confirmation by default and an optional separate worker.

## Household settings — 2026-10-05

- Household profile and member directory at `/app/household`, following the canonical UI with EN/DE/HU text, responsive Tailwind cards and light/dark states.
- Members can view their own household's profile, display names, roles and join dates. The directory does not expose login emails or financial data.
- Owner-only settings writes require an expected UUID version; stale requests and database races return localized conflicts. Completed financial onboarding cannot overwrite these settings through the onboarding endpoints.
- Preferred reporting currency changes no ledger amounts or account currencies.
- `AddHouseholdVersion` migration, regenerated OpenAPI client and PostgreSQL regression coverage.
- ADR-023 invitation/joining policy is implemented: owner create/list/resend/revoke, verified email matching, explicit acceptance, full query-cache reset and EN/DE/HU UI/email copy. See [Household invitations](household-invitations.md).

See [Household settings](household-settings.md) for API compatibility and deployment.


## Transfer Management — 2026-09-23

Merged into develop through PR #7 (ADR-018):

- Atomic create, edit and void of paired account movements; preserved history and optimistic concurrency.
- Same-currency and explicitly entered cross-currency settlement amounts, without cash-flow double counting.
- Server-side per-side privacy, hidden counterparty/note redaction and authorized account filters.
- Anchor/as-of-aware balance projection; archived-account void support.
- Generated OpenAPI client, localized EN/DE/HU transfer page, filters, editor and confirmation dialog.
- AddTransfers EF migration, database constraints/indexes and PostgreSQL regression coverage.

Deployment requires explicitly applying the AddTransfers migration. This does not implement bank execution, recurring transfers, fees as part of a transfer, personal-money attribution or FX reporting.



## Category Management — 2026-09-30

Implemented on the category-management feature branch (ADR-019):
- Household custom categories: create, edit, archive and restore; system definitions stay read-only.
- Nested hierarchy with household/type/parent lifecycle validation and optimistic concurrency.
- Localized management page with filters, shared Tailwind cards/dialogs, catalog icons and colors.
- Existing item references preserve category snapshots, including archived assignments, through transaction edits.
- Category validation holds PostgreSQL shared row locks through ledger commit to serialize with archival.
- No ledger deletion, aggregate rewrite, private-category semantics, shared system preference overrides or drag ordering.

### Category preferences follow-up - 2026-10-01

ADR-020 adds database-backed personal category pins (including system categories), localized alphabetical ordering, shared category selectors, and a responsive named icon picker with 240 EN/DE/HU names. Pins always precede eligible unpinned choices; archived assignments remain subject to the existing lifecycle rules.

### Category tree and pin sequence follow-up - 2026-10-01

ADR-021 enables subcategory parents with cycle protection, saved personal pin sequences with stale-write protection, and popup-based hierarchical selectors. Category selection uses the shared AverioDialog and does not expand transaction forms. New migration: AddCategoryPinOrder.


## Personal Activities — 2026-10-03

Activity cards also provide an on-demand transaction chart: daily income/expense bars, a currency selector, horizontally scrollable date labels, and an exact-value table. The private timeline endpoint aggregates all eligible assigned transactions server-side, independently of transaction-list pagination. No additional migration is required for the chart.

Implemented in the activity-tracking change (ADR-022): personal activities, optional dates, close/reopen, exact per-currency income/expenses/net, transaction editor assignment, atomic bulk assignment/removal, and activity-filtered history. Names and assignments remain private to each member; current transaction/account visibility is enforced server-side. Future and voided entries do not contribute to results; corrections still affect closed activities.

Includes the generated OpenAPI client, EN/DE/HU UI, `AddActivities` migration and PostgreSQL regression coverage. Apply the migration explicitly before deploying the API. Expected reimbursements, shared activities, partial allocation and currency conversion remain deferred. See [Activities](activities.md) for use, semantics and deployment.


## Invitation cutover — 2026-10-09

- Averio-branded invitation emails and invitation-aware registration/onboarding, including cross-tab verified-recipient discovery and explicit decline.
- Existing members may join with sole-holder personal account copies and reviewed opening balances; no old transactions or transfers enter the destination. Household-owned/joint accounts stay behind.
- Original financial history and former-holder attribution are preserved. The last-member household is archived, not deleted; departing users receive immutable, private, read-only history snapshots with date-filtered cash flow.
- Owner succession during acceptance, active membership uniqueness, serialized financial writes, exact decimal string contracts and PostgreSQL regression coverage.
- See [ADR-024](decisions/ADR-024-invitation-cutover-and-retained-history.md) and the [deployment guide](household-invitations.md).


## Collaboration verification and polish - 2026-10-11

- Real two-user browser verification against the production API pipeline and an isolated PostgreSQL database, including registration/verification, invitations, privacy, cutover, retained history and session switching.
- Complete financial query/mutation cleanup on logout and membership changes, preserving the mounted current-user observer. CSRF requests cannot restore a token from a previous identity.
- Replacement invitation links opened in the same tab refresh the credential and reset review/consent instead of retaining the previous hash token.
- Localized requester-visible tracked balance explanations on the dashboard and account cards, with EN/DE/HU desktop/mobile and light/dark browser checks.
- CI failure screenshots, browser traces and server logs, plus a release smoke evidence checklist. Provider/inbox and staging checks are recorded separately from automated captured-mail verification.
- No new migration or API contract. See [Collaboration verification](collaboration-verification.md).

Dashboard category card refinements: “This month so far” subtitle; adjacent currency and Expenses/Income selectors; independently ranked top four with a separated Other row; shared green-left-border surfaces across dashboard cards. See `dashboard-category-spending.md` for reporting semantics and browser coverage.

Dashboard category reports now support contextual empty-state actions, simpler expandable help, URL-preserved date/currency/type filters, complete category totals and daily category drill-down. Transaction entry opens with the requested income/expense type.


## Recurring transactions — 2026-10-15

Member-private weekly/monthly/yearly schedules, review/actual amount/skip, opt-in automatic recording, pause/resume/stop, anchored dates, atomic idempotent ledger posting and visible occurrence history are implemented. The EN/DE/HU screens use shared Tailwind cards/dialogs and the canonical design. Apply AddRecurringTransactions explicitly and deploy Averio.Worker for automatic recording. See [Recurring transactions](recurring-transactions.md) for operation, limits and verification.



## Private receipt scanning foundation

Receipt uploads create uploader-only expiring drafts, processed by a leased background worker. Extraction alone never writes ledger transactions. The capture/inbox/review slice adds EN/DE/HU image capture/upload, polling, private image/evidence review, editable fields and category suggestions, explicit account/currency/privacy selection, exact reconciliation and human confirmation through the existing transaction editor. Posting atomically commits the normal ledger/aggregate/optional Personal Money effects and the receipt's unique transaction link and immutable confirmation fingerprint; identical retries return the original transaction, while changed intent conflicts. Temporary image cleanup preserves that link. The feature remains disabled by default; production provider benchmarking, paid quotas, metrics and permanent attachment retention remain PR 3 work. Apply AddReceiptPosting and AddReceiptConfirmationFingerprint before deploying this slice. See [receipt scanning](receipt-scanning.md) and [ADR-035](decisions/ADR-035-receipt-confirmation-request-identity.md).


## Receipt category suggestions (PR 24 follow-up)

- Gemini suggests only from the household expense catalogue, with current-category validation and explicit apply actions.
- Users can opt in to remembering exact merchant/printed-item matches for the household at confirmation. Rules take precedence over model suggestions and commit atomically with the expense; retry identity includes rule-sharing intent.
- EN/DE/HU UI, generated API contracts, EF migration `AddReceiptCategoryRules`, and regression coverage accompany ADR-036. No production migration or live provider benchmark has been run.

## Receipt rollout controls (PR 3)

Household UTC monthly receipt allowances, time-bounded operator-managed paid entitlements, uploader-only exact-photo duplicate warnings, opt-in retained posted photos and content-free metrics are implemented. Posted-photo deletion preserves the financial transaction and immutable confirmation identity. No new schema migration is needed. See [ADR-037](decisions/ADR-037-receipt-rollout-quotas-and-retention.md) and [rollout/benchmark instructions](receipt-scanning.md). Real provider accuracy and production storage checks remain release gates; checkout/billing is not implemented.


## XLSX import review and recovery

Import now includes explicit cross-file overlap decisions, remembered account/category mappings, a completion summary with actual counts, balance reconciliation and partial safe undo. Undo voids eligible records atomically, preserving edited or dependent records and balance references. Batch privacy and exact decimals are maintained. See [XLSX import](xlsx-import.md) and [ADR-038](decisions/ADR-038-import-review-and-safe-undo.md).
