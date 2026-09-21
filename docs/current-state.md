# Averio Current State

**Status date:** 2026-09-19  
**Scope:** restarted implementation on the `develop` branch

This file intentionally describes only the restarted codebase. Pre-restart features are not counted as implemented.

## Repository foundation — implemented

Current high-level projects:

```text
apps/Averio.Api
apps/averio-web
src/Averio.Application
src/Averio.Contracts
src/Averio.Domain
src/Averio.Infrastructure
```

The backend uses explicit application handlers. MediatR is not part of the accepted architecture.

## Backend platform foundation — implemented

Established baseline includes:

- .NET 10 solution structure
- ASP.NET Core Web API host
- Application / Contracts / Domain / Infrastructure layers
- PostgreSQL/EF Core persistence foundation
- migrations
- global exception handling
- localized errors/resources
- FluentValidation
- OpenAPI/Scalar setup
- rate-limiting/security infrastructure where required by auth flows

## Identity/authentication — implemented foundation

The restarted repository contains flows/components for:

- registration
- email verification
- resend email verification
- login
- logout/session handling
- forgot/request password reset
- reset password
- cookie authentication
- current-user/session state

The frontend has the related guest/auth pages and generated API client types.

## Household foundation — implemented

The restarted domain/persistence layer contains:

- `Household`
- `HouseholdMember`
- relationship from identity user to household membership
- EF configuration/migrations for household membership
- repository support for membership lookup
- household membership information exposed in current-user/auth contracts

The current product design assumes the user's household context drives onboarding and authorization.

## Onboarding and Account Management — implemented on this branch

Onboarding establishes profile, household, and the first account. The first-account handler retains its explicit transaction and household-row locking behavior.

Account Management now provides:

- visible active-account list, optional archived accounts, name/scope/currency filters
- dedicated create (`/app/accounts/new`) and edit (`/app/accounts/$accountId/edit`) pages, with direct-link loading through the account detail query
- readable icon-led account details, primary-color active badges, aligned controls, and accessible explanations for fixed fields in English, German, and Hungarian
- additional account creation using the same domain validation as onboarding
- edit name, type, holders, visibility and household reporting participation
- archive/reactivate with confirmation; historical records are retained
- server-side household membership, visibility and modification checks
- stale-edit detection and EF optimistic concurrency using the existing updated_at column
- generated OpenAPI transport, scoped TanStack Query invalidation, EN/DE/HU strings
- responsive cards and forms using the existing application shell and design
- executable account regression suite under tests/Averio.Accounts.Tests

Current implementation boundaries:

- account currency is immutable (ADR-006)
- economic scope and balance anchors are not changed by the edit endpoint; scope transfer and balance reconciliation need separate semantics
- account_owners continues to store the named holders used by the current access rules; economic scope remains separate
- household-scope accounts are household-visible and included in reports
- personal accounts require at least one holder; the acting member must remain a holder when creating/editing personal accounts
- private accounts are personal and excluded from household reports
- household administrative role gives no extra access to personal accounts
- active lists are sorted by name and ID; account pinning and custom order are not introduced
- balances are recorded anchors, not transaction-derived live balances; currencies are never summed together

Verification: backend and frontend builds, executable regression suite, changed-feature lint, and browser workflows with mocked API responses. Live PostgreSQL migration, round-trip persistence and concurrency require a database environment and have not been verified here. Repository-wide frontend lint still reports pre-existing errors outside this slice.

## Frontend foundation — implemented

The restarted frontend uses:

- React + TypeScript + Vite
- Tailwind
- TanStack Router
- TanStack Query
- react-i18next
- generated OpenAPI API client

Auth UI work already covered responsive behavior and light/dark handling. The official Averio brand/logo direction and primary color `#10B981` are established.

## Localization — implemented foundation

Supported languages:

- English
- German
- Hungarian

Auth translation gaps were iteratively fixed, including password strength/requirements, login, forgot-password, verify-email, and resend-verification keys.

Any new feature must provide all three languages.

## Canonical application UI concept — available

`docs/ui/averio-ui-concept.html` is the canonical design reference for:

- Dashboard
- Accounts
- Goals
- Statistics
- Transactions
- Recurring Transactions
- Categories/icon handling
- Household Settings
- User Profile

It is a design specification/prototype, not proof that those features are implemented.

## Not yet treated as implemented in the restarted codebase

Unless the repository changes after this status date, do not claim these are complete:

- household invitations/partner invite lifecycle
- transaction ledger
- transfers
- recurring transaction domain
- category management domain
- goals domain
- dashboard cash-flow aggregates
- statistics/reporting domain
- personal-money ledger/rollover
- historical CSV/XLSX import
- AI receipt scanning
- bank connections
- 2FA
- Google/Facebook auth
- production Worker project

Some existed or were discussed before the restart; that does not make them present in the restarted implementation.

## Infrastructure status / target

Averio previously ran/deployed using AWS EC2, Docker, Nginx/Cloudflare, Neon, and ECR during the earlier implementation. Those lessons remain useful.

The current target architecture remains:

- Cloudflare for domain/frontend edge/static hosting
- AWS EC2 + Docker Compose for API and later Worker
- Neon PostgreSQL
- ECR for images where used
- GitHub Actions for CI/CD after the deployment path is stable

Do not assume old deployed containers represent the restarted app's current production readiness.

## Product research decisions already influencing the domain

Reddit/customer research reinforced that couples frequently need mixed shared/private finances. Current design therefore explicitly separates:

- household membership
- economic account ownership/scope
- legal holder
- visibility
- reporting participation

Personal-money allocation/rollover is also retained as an important future feature.

## Recommended next implementation milestone

Implement the Transaction/Transfer ledger foundation, including immutable currency handling, explicit transfer linkage, privacy-aware reporting, balance-anchor boundaries, and database-backed integration coverage. Scope-transfer and balance-reconciliation workflows are deliberately deferred until their financial-history semantics are settled.


### Transaction Management review additions — 2026-09-21

The current transaction-management branch now includes:

- itemized Income/Expense transactions with transaction total derived from exact item amounts
- free-text Merchant/Store
- a first-class hierarchical Category model with exactly 100 seeded base categories
- a 240-entry seeded icon catalog; categories store copied icon values and have no Icon foreign key
- required per-item CategoryId plus category name/icon history snapshots
- reusable Household and Account labels, selectable on every transaction form and creatable inline
- label selection before item entry in the editor
- four visually separated editor sections: basics, labels, items, details/privacy
- green Income and red Expense visual semantics
- header, dashboard and transaction-page Add transaction entry points opening the same editor
- synchronous Account and Category aggregates at Day, Month and Year grain
- yearly category statistics grouped by currency
- source AccountId retained on Category aggregates so current privacy/reporting rules are enforced before statistics are returned
- searchable Bento-style category selector in the transaction editor
- transaction overview search/filter interaction aligned with Account Management
- statistics chart bars reveal their exact value on hover/focus and remain visible when clicked/tapped
- account/dashboard current balances are derived from the balance anchor plus posted requester-visible ledger movements; transaction mutations invalidate account and dashboard queries

The full Category Management page (create/edit/reorder/archive/custom icons) is still a future UI slice. The persistence model and icon/category APIs now provide its foundation.
