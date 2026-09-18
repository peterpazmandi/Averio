# Averio Current State

**Status date:** 2026-09-16  
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

## Onboarding — partially implemented / active work area

The current API/client contracts indicate onboarding-required state and onboarding/household setup responses exist.

The next design/implementation work must continue from the latest account-ownership decisions rather than the older pre-restart onboarding model.

The important unresolved/next part is the **first Account vertical slice** with correct:

- household vs personal economic scope
- member owner(s) where required
- legal/physical holder
- visibility/privacy
- immutable account currency
- opening balance semantics

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

- full Account vertical slice
- household invitations/partner invite lifecycle
- transaction ledger
- transfers
- recurring transaction domain
- category management domain
- goals domain
- dashboard financial aggregates
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

Complete the Account vertical slice and onboarding first-account flow based on the accepted ownership/privacy/multi-currency semantics.

A sensible implementation sequence is:

1. finalize Account domain/entity/value semantics
2. EF configuration + migration
3. application create/list account use cases
4. API contracts/endpoints
5. regenerate frontend client
6. onboarding first-account integration
7. Accounts page foundation based on canonical UI
8. tests for ownership/holder/privacy/currency invariants
9. update this file

After that, the next major vertical slice should be the Transaction/Transfer ledger foundation, because most dashboard/statistics/goals behavior depends on trustworthy ledger semantics.
