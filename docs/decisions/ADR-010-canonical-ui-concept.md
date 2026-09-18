# ADR-010 — Canonical Averio application UI concept

**Status:** Accepted  
**Date:** 2026-09-16

## Context

Repeated feature-by-feature UI work risks visual drift, especially across auth pages, dashboard, accounts, transactions, categories, goals, statistics, household settings, and profile.

## Decision

Use `docs/ui/averio-ui-concept.html` as the canonical application UI design reference until explicitly superseded.

Primary brand color is `#10B981`.

The official Averio logo is exposed through a shared component.

New UI extends the concept's visual language, responsive behavior, dark/light treatment, spacing, cards, tables, navigation, and interaction patterns.

## Consequences

- implementation should inspect the concept before creating new application surfaces
- responsive/mobile behavior is part of the design, not an afterthought
- future design changes should update/supersede this ADR/reference intentionally
