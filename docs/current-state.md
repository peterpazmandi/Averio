# Averio Current State

**Status date:** 2026-09-30
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

- household invitations/partner invite lifecycle
- recurring transaction domain and worker execution
- category drag reordering, shared system-category customization, and private categories
- goals
- personal-money allocation and rollover
- historical CSV/XLSX import
- AI receipt scanning
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

Define the household invitation/joining lifecycle next, including existing users with financial history. Recurring execution remains a later milestone requiring idempotency and worker design.


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
- Two-level hierarchy with household/type/parent lifecycle validation and optimistic concurrency.
- Localized management page with filters, shared Tailwind cards/dialogs, catalog icons and colors.
- Existing item references preserve category snapshots, including archived assignments, through transaction edits.
- Category validation holds PostgreSQL shared row locks through ledger commit to serialize with archival.
- No ledger deletion, aggregate rewrite, private-category semantics, shared system preference overrides or drag ordering.

### Category preferences follow-up - 2026-10-01

ADR-020 adds database-backed personal category pins (including system categories), localized alphabetical ordering, shared category selectors, and a responsive named icon picker with 240 EN/DE/HU names. Pins always precede eligible unpinned choices; archived assignments remain subject to the existing lifecycle rules.
