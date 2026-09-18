# ADR-003 — Financial privacy boundaries

**Status:** Accepted  
**Date:** 2026-09

## Context

User research showed couples often share household planning while keeping selected financial details private. Simply hiding rows in the UI is insufficient because private information can leak through aggregates, search, exports, category totals, or merchant names.

## Decision

Privacy is a server-enforced domain/authorization concern.

Ownership, visibility, and household reporting participation are separate concepts.

Household administrative roles do not automatically grant access to a member's private financial details.

Private details must not leak through API payloads, aggregates, search, exports, notifications, goals, or indirect category/merchant information.

Child/detail visibility may become more restrictive, but must not bypass the containing account/privacy boundary.

## Consequences

- authorization filters apply before serialization
- aggregate queries must be privacy-aware
- React hiding is presentation only
- privacy behavior requires dedicated tests
- anonymized household impact is possible only through explicit product rules
