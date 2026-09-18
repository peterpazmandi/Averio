# ADR-005 — Do not use MediatR

**Status:** Accepted  
**Date:** 2026-09

## Context

Earlier Averio discussions/design documents referenced MediatR. During the restarted implementation, the project explicitly decided not to depend on MediatR because the dependency/licensing trade-off is not worthwhile for Averio.

The restarted repository already uses explicit application handler classes.

## Decision

Do not use MediatR.

Implement commands/queries/use cases as explicit classes and handlers with direct dependency injection/invocation.

Controllers remain thin and business logic stays in Application/Domain.

## Consequences

- no `IRequest` / `IRequestHandler`
- no MediatR pipeline behavior
- validators/cross-cutting behavior are wired explicitly
- dependencies are visible and easy to debug
- older documentation mentioning MediatR is superseded by this ADR
