# Averio Current State

**Status date:** 2026-09-22
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
- a searchable grouped category picker and responsive statistics UI

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

- transfers and transfer linkage
- household invitations/partner invite lifecycle
- recurring transaction domain and worker execution
- full category management UI (create/edit/reorder/archive/custom icons)
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

Implement transfer semantics on top of the hardened transaction foundation.
The design must preserve immutable account currencies, link both transfer legs,
define cross-currency behavior explicitly, keep privacy enforcement server-side,
and extend the PostgreSQL integration suite before adding UI workflows.
