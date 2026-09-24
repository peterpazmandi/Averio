# Averio Architecture

**Status:** Authoritative architecture reference  
**Updated:** 2026-09-16

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
├── query/
└── i18n/
```

Avoid giant route files that combine networking, validation, transformation, state, and rendering.

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
