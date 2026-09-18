# ADR-002 — Separate account ownership, holder, and visibility

**Status:** Accepted  
**Date:** 2026-09

## Context

A real household bank account can legally be held by one person while economically belonging to the household. Separately, an individually owned account may be private or selectively visible.

A single `Account.Owner = User | Household` field collapses different meanings and produces misleading UI/authorization.

## Decision

Model these concepts separately:

1. Household context
2. Economic scope/ownership
3. Individual owner/member relationships where needed
4. Legal/physical account holder
5. Visibility
6. Reporting participation

A household-owned account may be legally held by an individual member. Do not use holder identity as proof of economic ownership.

Do not treat `Household` as a polymorphic human owner merely to represent shared ownership.

## Consequences

- UI can say "Household" while also saying "Held by Péter"
- joint/member ownership can be explicit
- privacy rules do not depend solely on ownership
- reporting can evolve independently of detail visibility
- the first Account implementation must preserve these separations
