# Averio Implementation Rules

**Status:** Authoritative implementation guidance  
**Updated:** 2026-09-16

## Goal

These rules define how Averio implementation work should be proposed and executed so future changes remain consistent with the repository, domain decisions, UI concept, and solo-developer constraints.

## Before coding

When repository access is available:

1. inspect the relevant current files
2. inspect related ADRs
3. inspect the domain/architecture docs
4. verify the current branch/state
5. identify whether the requested change is incremental or architectural

Never invent paths, namespaces, contracts, packages, or existing entities.

## Step-by-step answer format

For every step that changes code, explain:

### Purpose
What the change accomplishes.

### Why
Why it is necessary for Averio.

### Files affected
Exact paths.

### Action
State exactly whether to create, replace, insert, rename, move, or delete.

### Behavior and impact
Explain what changes at runtime/domain/UI level and what it influences.

### Complete resulting files
At the end of that step, provide the complete content of every changed hand-maintained source file.

If a file is generated, do not encourage hand-editing it; provide the source change and regeneration command instead.

## Backend rules

- .NET 10
- explicit handlers/use cases
- no MediatR
- thin controllers
- FluentValidation for request/use-case input validation
- domain invariants remain in Domain where they truly belong
- persistence-specific code remains in Infrastructure
- contracts do not become domain entities
- use RFC 7807 Problem Details
- localize user-facing errors

Prefer feature/vertical-slice folders inside Application.

### Handler shape

A handler may directly receive the required abstractions through constructor injection. Do not add a mediator merely to hide a direct method call.

### Repositories

Create domain/application repository abstractions only when they provide a meaningful use-case boundary. Do not wrap every EF Core operation in a generic repository.

### Transactions

Use explicit transaction boundaries when multiple writes must succeed atomically. Household/account ownership and membership transitions require particular care.

## Database rules

For every schema change consider:

- PK
- FK
- index
- unique constraint
- check constraint
- delete behavior
- nullability
- money precision
- timestamps
- concurrency/race conditions
- migration rollback/compatibility

Use PostgreSQL constraints for critical invariants where practical.

Index common foreign-key and filter paths.

Do not store money as float/double.

Do not silently alter account currency after creation.

Do not hard-delete ledger history casually.

## Authentication/security rules

- secure cookie auth
- never expose secrets/tokens to frontend storage unnecessarily
- protect email-triggering endpoints with appropriate rate limits/cooldowns
- apply server-side authorization to every financial resource
- enforce privacy before serialization
- avoid user/account enumeration in anonymous auth flows
- use CSRF protection consistent with cookie auth

## Frontend rules

- React + TypeScript + Vite + Tailwind
- TanStack Router
- TanStack Query v5
- generated OpenAPI client
- react-i18next
- date-fns

### Query management

Keep query keys/query options structured and reusable. Invalidate/update narrowly after mutations.

### Forms

Show server-side validation/domain errors in a useful localized way. Prevent double-submit where appropriate. Preserve onboarding progress safely.

### Components

Always use Tailwind CSS utilities for new or changed UI styling. Do not add feature CSS files or copy `av-*` rules from the HTML concept; reproduce the canonical appearance with utilities.

Use `shared/components/ui` for Averio-specific controls: AverioButton, AverioInput, AverioSelect, AverioTextarea, AverioField and AverioDialog. Preserve native props/ref forwarding, accessible names, focus handling, disabled states and light/dark behavior. Extract further components for repeated presentation or shared behavior where useful. Legacy stylesheets are not a precedent for new feature styling.

Prefer shared primitives and feature components over copied markup.

Avoid route files that become large all-purpose components.

### Accessibility/responsiveness

Every flow must be usable on mobile and desktop. Check overflow, focus order, keyboard operation, labels, contrast, and error association.

## i18n rules

Any new static user-visible string requires `en`, `de`, and `hu` translations in the same change.

Do not use translation keys as visible fallbacks in production UI.

Backend resource keys remain stable identifiers; translated text can evolve.

## UI rules

Use `docs/ui/averio-ui-concept.html` as the design source.

Primary brand color: `#10B981`.

Follow the established shell, spacing, cards, navigation, dark/light behavior, table/list treatment, modal behavior, and mobile adaptations.

When a new feature is absent from the prototype, extend the established design rather than inventing a new style.

Use the official Averio logo through a shared component.

For new or changed empty states and cards, follow [UI component patterns](ui/component-patterns.md). Use illustrated, contextual empty states and the menu-inspired card treatment in both overviews and create/edit dialogs. Reuse the shared Tailwind components; preserve form semantics and privacy-aware data boundaries.

## Domain-change rules

When a change affects ownership, privacy, transfer semantics, personal money, multi-currency, household membership, or authorization:

1. explain the domain effect first
2. update/add ADR if it changes an accepted decision
3. update `domain-model.md`
4. then implement schema/application/API/frontend changes

Do not encode a new domain decision only in UI code.

## API/client rules

Backend OpenAPI is the client contract source.

When API contracts change:

1. update backend contract/endpoint
2. update OpenAPI output
3. regenerate frontend client
4. update frontend integration

Do not manually patch generated client files.

## Testing expectations

At minimum consider:

- domain/unit tests for non-trivial invariants
- validator/use-case tests
- authorization/privacy tests
- integration tests around EF/database constraints where valuable
- API behavior tests for auth/error contracts
- frontend tests for high-value behavior where practical

Privacy and double-counting bugs deserve tests even if the UI appears straightforward.

## Deployment rules

Current target remains simple:

- frontend through Cloudflare static hosting/edge
- API on AWS EC2 in Docker
- Worker in Docker when introduced
- Neon PostgreSQL
- ECR optional/expected image registry
- GitHub Actions for CI/CD after manual deployment works

Do not add Kubernetes/Helm.

Use environment variables/secrets for production configuration. Do not commit `.env` secrets.

## Documentation rules

When a stable decision changes, update the relevant ADR/docs in the same PR.

`docs/current-state.md` should be refreshed after a meaningful vertical slice lands.

Do not let old design/implementation guides become accidental authorities; mark them historical/deprecated when superseded.

## Pull-request/review checklist

Before calling a feature complete, ask:

- Does it preserve domain invariants?
- Is authorization server-side?
- Can private information leak?
- Can money be double-counted?
- Is currency handling correct?
- Are DB constraints/indexes appropriate?
- Are errors localized?
- Are all UI strings translated?
- Is mobile responsive?
- Are empty/loading/error states covered?
- Is the generated client regenerated if API changed?
- Did docs/ADR/current-state need updating?



## Commit messages

All future commits must follow [Conventional Commits 1.0.0](https://www.conventionalcommits.org/en/v1.0.0/).
Use `<type>[optional scope][!]: <description>`. Use `feat` for new features and `fix` for bug fixes; other useful types include `docs`, `test`, `refactor`, `perf`, `build`, `ci`, and `chore`.
Use lowercase types/scopes consistently in Averio. A scope identifies the affected area, such as `categories`.
Separate an optional body and footers with blank lines. Mark breaking changes with `!` before the colon or an uppercase `BREAKING CHANGE: <explanation>` footer.
Split unrelated changes into separate commits where practical. PR titles and squash-merge messages must also follow this convention.
Examples: `feat(categories): add household category management`, `fix(transactions): retain archived category assignments`, `docs(contributing): require conventional commits`.
Do not rewrite already published history merely to apply this rule retroactively.
