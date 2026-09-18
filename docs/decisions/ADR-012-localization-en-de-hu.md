# ADR-012 — English, German, and Hungarian localization baseline

**Status:** Accepted  
**Date:** 2026-09

## Context

Averio is intended for multilingual use and the existing frontend/backend already contains localization infrastructure.

## Decision

Support English (`en`), German (`de`), and Hungarian (`hu`) as the required product languages.

All static frontend user-visible strings use i18n. Backend validation/business messages use stable localization resource keys.

A feature that adds UI text is incomplete until all three translations exist.

## Consequences

- no hard-coded English UI strings in normal feature code
- translation completeness is part of feature review
- locale formatting remains separate from currency semantics
