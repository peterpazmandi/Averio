# Averio Architecture

**Status:** Authoritative architecture reference  
**Updated:** 2026-09-24

## Architectural style

Averio is a modular monolith. It is intentionally simple enough for a solo developer while maintaining clear boundaries between HTTP/UI concerns, application use cases, domain rules, and infrastructure.

The backend follows Clean Architecture / DDD-inspired layering with feature-oriented application slices.

Averio is not a microservice system.

## Current repository layout

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

### `apps/Averio.Api`

ASP.NET Core host. Responsibilities include HTTP, authentication/session integration, routing/controllers, CORS, rate limiting, exception mapping, localization integration, Scalar/OpenAPI, and dependency composition.

Core business logic does not belong here.

### `apps/averio-web`

React/Vite frontend. Responsibilities include routes, forms, presentation state, TanStack Query server state, generated API-client integration, localization, and responsive UI.

### `src/Averio.Domain`

Pure domain model: entities, value objects, invariants, domain events where useful, and domain enums/types.

It must not depend on ASP.NET Core, HTTP, React, or provider-specific infrastructure.

### `src/Averio.Application`

Use cases/business orchestration. Contains feature-oriented commands, queries, handlers, validators, and abstractions.

MediatR is not used.

### `src/Averio.Contracts`

External/application contracts where separation is useful. The backend OpenAPI document is used to generate the frontend client.

### `src/Averio.Infrastructure`

Technical adapters such as EF Core/PostgreSQL, identity persistence, email delivery, tokens, external integrations, and provider implementations.

## Dependency direction

Conceptually:

```text
Averio.Api
    │
    ▼
Averio.Application
    │
    ▼
Averio.Domain

Averio.Infrastructure
    └── implements abstractions required by Application/Domain
```

Domain remains the most independent layer.

## Application use-case pattern

Use explicit handlers such as:

```text
CreateAccountCommand
CreateAccountValidator
CreateAccountHandler
```

Controllers deserialize HTTP input, invoke the relevant application use case, and map the result to a response contract.

Do not introduce MediatR dispatch abstractions. Explicit handlers provide visible dependencies, easy debugging, and vertical-slice organization without the dependency/licensing trade-off.

## Frontend architecture

Baseline:

- React
- TypeScript
- Vite
- Tailwind CSS
- TanStack Router
- TanStack Query v5
- react-i18next
- date-fns
- generated OpenAPI TypeScript client

Prefer feature organization where practical:

```text
src/
├── components/
├── features/
│   ├── accounts/
│   ├── auth/
│   ├── categories/
│   ├── goals/
│   ├── household/
│   └── transactions/
├── routes/
├── shared/
│   └── components/
│       └── ui/          # Reusable Averio Tailwind components
├── query/
└── i18n/
```

Avoid giant route files that combine networking, validation, transformation, state, and rendering.

### Reusable UI components

`apps/averio-web/src/shared/components/ui` is the standard location for reusable Averio UI components styled with Tailwind CSS. Feature-specific components stay within their feature folders and compose these shared primitives.

The shared controls are `AverioButton`, `AverioInput`, `AverioSelect`, `AverioTextarea`, `AverioField`, `AverioDialog`, and `AverioFilterPanel`. The filter panel uses a compact toolbar button with an active-filter count. It opens an AverioDialog (a bottom sheet on mobile) containing the filter controls, selection summary, and reset action, with focus restored on dismissal. Filter changes apply immediately; closing the dialog preserves the selection. Reuse or extend them when implementing forms and dialogs; extract additional shared components when repeated styling or behavior justifies it.

Always implement new or changed UI styling with Tailwind utilities, following the canonical HTML concept as the visual reference. Do not add feature stylesheets or copy its `av-*` CSS rules. Existing legacy CSS is not a precedent for new styling.

Shared components preserve native props and refs, accessible labels and focus behavior, disabled/error states, responsive layouts, and light/dark appearance. Keep business logic, API calls, and localized product copy in feature components and pass them through props.

`AverioCard` provides the menu-inspired card surface for related content and form groups. `AverioTransferIllustration` is a reusable, decorative SVG composition that reuses `AverioLogo`. Empty-state messages and actions belong to feature components. See [UI component patterns](ui/component-patterns.md) for the required card and empty-state behavior.

## Server-state/data access

TanStack Query is the default server-state mechanism. Query keys should be centralized/separated consistently.

Use the generated OpenAPI client as the transport integration. Do not duplicate authoritative API DTOs by hand.

Do not mirror authoritative server state into Context. Use Context/useReducer only for genuine cross-cutting client state.

## API design

Prefer resource-oriented routes:

```text
/accounts
/transactions
/categories
/households/{householdId}/goals
/households/{householdId}/invitations
```

Use nesting where the relationship and authorization context are meaningful.

Avoid action-heavy routes such as `/accounts/createAccount` unless the operation is genuinely not representable as a resource.

## Error handling

Use RFC 7807 Problem Details through the global exception-handling strategy.

Expected concerns include validation errors, unauthorized/forbidden access, not found, conflict/domain invariants, expired tokens, session/account state, and rate limiting.

User-facing detail is localizable. Internal stack traces/details are never exposed in production.

## Localization

Backend uses localized application/business error resources and request-culture handling.

Frontend uses react-i18next with `en`, `de`, and `hu`.

Locale formatting and money currency are separate concepts.

## Authentication

Core flow:

```text
Register → Verify email → Login → Secure auth cookie → Authorized API calls
```

Also support resend verification, forgot password, reset password, and logout.

Production hosts are expected to use `averioapp.com` and `api.averioapp.com`.

Cookie/CORS/CSRF decisions must be treated together. Production cookies are secure. Credentials are allowed only for explicit origins. Secrets/tokens remain server-side. Anonymous email-triggering endpoints should be rate-limited.

## Persistence

Primary database: PostgreSQL hosted on Neon. ORM: EF Core.

Persistence conventions:

- explicit EF configurations
- migrations in source control
- indexes for normal FK/query paths
- clear cascade/restrict behaviors
- database constraints for important invariants
- explicit production migration strategy

Do not rely on automatic migrations during ordinary production API startup unless later accepted by ADR.

## Multi-currency architecture

Account currency is immutable. Money is modeled as amount + currency.

Do not perform implicit conversion inside entities. Cross-currency reporting needs an exchange-rate/conversion layer with source and effective-date semantics.

Initial supported currencies: EUR, USD, HUF.

## Privacy and authorization

Privacy is enforced server-side. React hiding is only presentation.

Authorization can depend on requesting user, household membership/role, resource scope, explicit owner(s), account visibility, transaction visibility, and reporting participation.

Never fetch private data to the browser and rely on CSS/React to hide it.

## Reporting/read models

Dashboard/statistics are read-model concerns. A combined dashboard endpoint is acceptable to optimize initial loading while detailed pages retain granular APIs.

This is pragmatic CQRS-style separation without requiring a CQRS framework.

## Domain events

Domain events may represent meaningful business transitions, but an event bus is not required. In-process handling is sufficient until a specific asynchronous/reliability problem justifies more infrastructure.

## Worker architecture

A separate .NET Worker/BackgroundService process is part of the target architecture for scheduled/background work such as imports, receipt processing, aggregate/projection maintenance, and recurring tasks.

Do not assume the restarted repo already contains `Averio.Worker` until it is created.

When introduced it should reuse Domain/Application/Infrastructure code rather than duplicate rules.

## Target production topology

```text
                         Internet
                            │
                            ▼
                       Cloudflare
                    DNS / edge / Pages
                       /          \
                      /            \
                     ▼              ▼
          averioapp.com      api.averioapp.com
               │                    │
               ▼                    ▼
         React / Vite            AWS EC2
                                Docker
                                ├── API
                                └── Worker (when added)
                                   │
                                   ▼
                                  Neon
                               PostgreSQL
```

Docker Compose is sufficient for the current scale. ECR can store images and GitHub Actions can automate deployment after the manual path is understood.

Do not introduce Kubernetes/Helm for the current stage.

## Infrastructure constraints

Averio is operated by one developer with a low infrastructure budget. Prefer one API, one Worker when needed, managed PostgreSQL, simple Docker deployment, Cloudflare edge/DNS, and vertical scaling before orchestration complexity.

## Observability direction

At minimum evolve toward structured logs, health checks, API error tracking, deployment logs, backup awareness, and worker-failure visibility.

Never log passwords, reset/verification tokens, cookies, or unnecessary sensitive transaction detail.

## Architecture choices that still need future ADRs

Create an ADR before finalizing major choices such as:

- FX-rate provider/history model
- bank-sync provider
- receipt-AI provider
- outbox implementation
- background scheduler
- billing/subscription provider
- advanced permission model


## Household settings and membership preparation

The existing household GET/PATCH slice now provides a minimal member directory and
owner-only, versioned profile writes. `Household.Version` is an EF concurrency token;
UnitOfWork maps write races to a localized household conflict. The generated client
is the sole frontend contract. The settings page invalidates household, onboarding,
identity and dashboard queries after saving. A draft retains its original version
until saved or explicitly discarded.

ADR-023 defines the invitation/joining lifecycle. The invitation slice adds
explicit application handlers, persistent hashed tokens and lifecycle/delivery
state, and an atomic PostgreSQL joining boundary. ADR-024 adds immutable privacy-filtered history snapshots and account copies with
opening balances. Financial mutations acquire household locks before authorization
and recheck active membership after waiting, serializing writes with cutover.
Filtered active-user/owner indexes retain departed memberships for attribution. See
[Household settings](household-settings.md) and [Household invitations](household-invitations.md).

## Activity tracking implementation

The Activities vertical slice uses an explicit `ActivityHandler`, a privacy-filtered EF repository and separate personal assignment records. Ledger create/update may include assignment changes in the same unit of work. Bulk assignment holds the member row lock and commits atomically; close/reopen uses the same lock. Independent GUID versions reject stale metadata writes without changing ledger concurrency tokens. Results are computed from visible source transactions, not household reporting aggregates, and preserve original currencies. Composite database foreign keys enforce household/member ownership. See [ADR-022](decisions/ADR-022-personal-activities.md) and the [activity API and rollout guide](activities.md).

