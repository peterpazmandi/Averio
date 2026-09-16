# ADR-013 — Aggregated dashboard read model

**Status:** Accepted  
**Date:** 2026-09

## Context

The Averio dashboard presents many related metrics/cards. Loading each card through separate dependent requests would make the first main screen slower and more fragile.

## Decision

Allow a purpose-built aggregated dashboard endpoint/read model for initial dashboard loading, while keeping granular resource endpoints for detailed pages and mutations.

This is pragmatic read-model/CQRS separation and does not require a CQRS framework or mediator library.

## Consequences

- dashboard payload can be optimized for UI needs
- detailed account/transaction/category APIs remain independently usable
- privacy filtering and currency semantics must be applied before dashboard aggregation
