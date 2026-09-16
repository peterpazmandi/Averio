# ADR-014 — Archive financial accounts instead of deleting history

**Status:** Accepted  
**Date:** 2026-09

## Context

Accounts become referenced by transactions, balances, imports, goals, and reports. Hard deletion can corrupt historical meaning and auditability.

## Decision

Once an account has meaningful financial history, deactivate/archive it instead of hard-deleting it.

Archived accounts remain available for historical reporting but are excluded from normal active-account selection unless explicitly requested.

## Consequences

- APIs/UI need active/archive state
- historical transaction relationships remain valid
- destructive cleanup is reserved for genuinely empty/never-used records if permitted by implementation rules
