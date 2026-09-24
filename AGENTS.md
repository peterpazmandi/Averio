# Averio AI Development Instructions

This file defines the authoritative working rules for AI-assisted development of Averio.

Averio is a household-centric personal finance and budgeting application designed to support solo users and collaborative households without forcing users to surrender personal financial autonomy.

## Source-of-truth priority

When sources conflict, use this order:

1. Current repository implementation on the active branch
2. Accepted ADRs under `docs/decisions/`
3. `docs/domain-model.md`
4. `docs/architecture.md`
5. `docs/product.md`
6. `docs/current-state.md`
7. `docs/implementation-rules.md`
8. `docs/ui/averio-ui-concept.html`
9. Current explicit user instruction
10. Previous conversations and remembered Averio context

The newest explicit user decision supersedes older conversation history. Never silently revive a superseded decision. If current code, ADRs, docs, and older discussions disagree, call out the contradiction and follow the order above.

## Restart boundary

Averio was deliberately restarted in late August / early September 2026. Pre-restart code is historical context only. Do not describe old features as currently implemented unless `docs/current-state.md` or the repository confirms them.

## Technology baseline

### Frontend

- React
- TypeScript
- Vite
- Tailwind CSS
- TanStack Router
- TanStack Query v5
- react-i18next
- date-fns
- `@openapitools/openapi-generator-cli`
- generated TypeScript client from the backend OpenAPI document

### Backend

- .NET 10
- ASP.NET Core Web API
- PostgreSQL / Neon
- EF Core
- FluentValidation
- RFC 7807 Problem Details
- localized backend errors
- Scalar API UI
- cookie-based authentication
- Clean Architecture / DDD-inspired modular monolith
- explicit application handlers/use cases

### Do not introduce without an accepted ADR

- MediatR
- Kubernetes
- Helm
- microservices
- event sourcing
- Kafka
- generic repository abstractions over EF Core

Do not add a major dependency merely because it is popular. First explain the concrete Averio problem it solves.

## Current high-level repository structure

```text
Averio/
├── apps/
│   ├── Averio.Api/
│   └── averio-web/
├── src/
│   ├── Averio.Application/
│   ├── Averio.Contracts/
│   ├── Averio.Domain/
│   └── Averio.Infrastructure/
├── docs/
└── Averio.slnx
```

A Worker is part of the intended production architecture, but do not assume it exists until the repository contains it.

## Domain rules

### Household-first

Every active financial user operates in a household context, including a household of one.

The household is the collaboration and financial-context boundary. It is not automatically the legal owner of every account and it does not imply universal visibility.

### Keep these concepts separate

- household membership
- household administrative role
- economic account ownership/scope
- legal/physical account holder
- account visibility
- transaction visibility
- reporting participation
- personal-money attribution
- cash-flow effect

A household `Owner` role must never become an implicit financial super-admin over another member's private finances.

### Account semantics

- an account belongs to a household context
- economic ownership/scope is distinct from legal holder
- household-owned money may be legally held by one member
- personal/private accounts may still exist inside the user's household context
- account currency is immutable after creation
- initial currencies are EUR, USD, HUF
- do not directly sum different currencies
- archive accounts instead of hard-deleting financial history

### Privacy

Ownership != visibility != reporting participation.

Private details must not leak through transaction lists, search, statistics, categories, exports, notifications, goals, balances, or inferred merchant/category information.

Privacy-sensitive filtering must be enforced server-side, not only in React.

### Personal money

Personal money is a budget concept, not merely a category or physical bank account.

Track allocation, spending/usage, remaining amount, and rollover. Unused allocation rolls over by default.

An allocation is not inherently an expense. If money is later spent or crosses a household/private boundary, reporting must avoid double counting.

### Transfers

A transfer is money movement between financial containers, not an ordinary income/expense pair.

### Multi-currency

Every money value retains its original currency. Cross-currency reporting requires explicit exchange-rate source, rate date, and conversion semantics.

## UI and UX

`docs/ui/averio-ui-concept.html` is the canonical application UI reference until explicitly superseded.

Use it as the visual starting point for Dashboard, Accounts, Transactions, Recurring Transactions, Categories, Goals, Statistics, Household Settings, and User Profile.

Primary brand color: `#10B981`.

Use the official Averio logo through a reusable component rather than duplicating SVG markup page-by-page.

Support light mode, dark mode, mobile/responsive layouts, keyboard use, accessible labels/focus states, loading states, empty states, error states, and first-time-user states.

Do not invent a second design language for new features.

### Tailwind and reusable components

- Always implement new or changed UI styling with Tailwind CSS utilities. Do not add feature stylesheets, new `av-*` styling rules, or inline presentation styles to reproduce the HTML concept.
- Treat the canonical HTML as a visual reference; translate its appearance into Tailwind rather than copying its CSS.
- Reuse the Tailwind-based primitives under `shared/components/ui`: `AverioButton`, `AverioInput`, `AverioSelect`, `AverioTextarea`, `AverioField`, and `AverioDialog`. Extract additional shared components when they encapsulate repeated styling or meaningful behavior; do not create one-off wrappers without value.
- Preserve native props, refs, form association, labels, keyboard behavior, disabled/error states, responsive layouts and light/dark styling. Keep product copy localized at the call site.
- Existing legacy CSS is not a precedent for new work. Migrate the relevant component when modifying its styling; avoid unrelated whole-application restyling.
- Global Tailwind imports/theme tokens and genuine runtime behavior (for example, temporarily locking body scroll for a modal) remain appropriate. They are not substitutes for Tailwind component styling.

## Localization

Supported UI languages:

- English (`en`)
- German (`de`)
- Hungarian (`hu`)

Every static user-visible frontend string must use i18n. Backend validation/business errors must follow the localized resource strategy.

## API conventions

Prefer resource-oriented REST endpoints using nouns, for example:

```text
GET  /accounts
POST /accounts
GET  /households/{householdId}/goals
POST /households/{householdId}/invitations
```

Avoid RPC-style routes unless a real domain command cannot naturally be represented as a resource.

Controllers/endpoints stay thin; business rules belong in Application/Domain.

## Application layer

Use explicit commands/queries/use cases and handlers matching the existing repository pattern.

Do not use `IRequest`, `IRequestHandler`, or MediatR pipeline behaviors.

Prefer feature/vertical-slice organization inside Application. Keep dependencies and transaction boundaries visible.

## Persistence rules

For every persisted concept consider:

- primary key
- foreign keys
- indexes
- unique constraints
- check constraints
- delete behavior
- timestamps
- concurrency
- migration impact
- privacy/query filtering
- auditability

Protect critical invariants in application/domain logic and in PostgreSQL where practical. Index foreign keys used in normal lookup/filter paths.

Do not silently run production migrations at normal API startup unless a later ADR explicitly adopts that strategy.

## Authentication/security

Current auth model includes registration, verification, resend verification, login, logout, forgot password, reset password, and secure cookie auth.

Treat cookie and CORS configuration as one security design. Do not commit secrets. Authorization and finance/privacy rules are enforced server-side. 2FA and social login are planned, not assumed implemented.

## Frontend data access

TanStack Query is the server-state mechanism. Keep query keys centralized/separated according to project conventions.

Use the OpenAPI-generated client. Do not maintain a second handwritten set of API DTOs unless the UI deliberately needs a view model.

## Product and monetization thinking

When evaluating features, consider both usability/customer value and realistic monetization potential. Do not force monetization into every feature.

Natural future premium areas include AI receipt scanning, advanced insights, bank connections, advanced automation, richer exports/reports, and selected collaboration features.

Core correctness, privacy, account/transaction handling, and basic household usability must remain trustworthy.

## Working with code

When repository access is available, inspect current code before proposing paths, namespaces, DTOs, packages, database structures, or routes. Do not invent existing code.

When a proposal differs from the repository, say whether it is an incremental refactor, deliberate replacement, or future target.

## Step-by-step implementation answers

For every code-changing step explain:

1. what the change is for
2. why it is necessary
3. what it affects
4. exact file path
5. whether to create, replace, insert, or delete
6. important behavior/trade-offs
7. the complete resulting file for every file changed in that step

Do not give isolated snippets when a concrete implementation guide was requested.

## ADR discipline

Stable architecture/domain decisions belong in `docs/decisions/ADR-xxx-short-title.md`.

Statuses: Proposed, Accepted, Superseded, Deprecated.

Never rewrite history by silently replacing an old accepted ADR. Add a new ADR and mark the earlier one superseded.
