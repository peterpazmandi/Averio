# ADR-001 — Household-first model

**Status:** Accepted  
**Date:** 2026-09

## Context

Averio must serve solo users and couples/households without maintaining two incompatible product models.

## Decision

Every active financial user operates inside a Household context. A solo user receives a household of one. Collaboration is introduced by membership, not by migrating a solo financial model into a separate shared-finance model.

`User → HouseholdMember → Household` is the core membership relationship.

## Consequences

- onboarding establishes a household context
- household-level authorization is based on membership
- solo-to-shared evolution is straightforward
- private/personal finances still exist inside the household context
- household membership must not imply universal financial visibility
