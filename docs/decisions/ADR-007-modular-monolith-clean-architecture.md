# ADR-007 — Modular monolith with Clean Architecture boundaries

**Status:** Accepted  
**Date:** 2026-09

## Context

Averio is built by one developer with limited weekly time. The domain needs clear boundaries, but microservices would add deployment and operational cost without current benefit.

## Decision

Use a modular monolith with:

- `Averio.Api`
- `Averio.Application`
- `Averio.Contracts`
- `Averio.Domain`
- `Averio.Infrastructure`
- React frontend as a separate web app

Organize Application primarily by feature/use case.

## Consequences

- simple deployment
- explicit boundaries and testability
- no distributed-system complexity for internal module calls
- future extraction remains possible if a real scaling/ownership reason appears
