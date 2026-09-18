# ADR-006 — Multi-currency is first-class and conversion is explicit

**Status:** Accepted  
**Date:** 2026-09

## Context

Averio targets users who may hold EUR, USD, and HUF accounts. Treating currency as presentation-only would create incorrect balances/statistics.

## Decision

Money values retain currency. Account currency is immutable after account creation.

Initial supported currencies are EUR, USD, and HUF.

Different currencies are never directly summed. Normalized reports require an explicit exchange-rate source, effective date/time, and target/base currency.

Do not silently convert inside domain entities.

## Consequences

- money uses decimal-compatible amount + currency
- account creation validates supported currency
- cross-currency reports require a separate FX layer
- cross-currency transfers require explicit semantics
- original-currency values remain inspectable
