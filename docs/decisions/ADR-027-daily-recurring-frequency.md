# ADR-027 — Daily recurring frequency

**Status:** Accepted
**Date:** 2026-10-16
**Extends:** ADR-026; its existing template, ownership, privacy and lifecycle decisions remain unchanged.

## Decision

Offer Daily alongside Weekly, Monthly and Yearly when creating a recurring schedule. Daily occurrence N is the original UTC first date plus N calendar days. End dates remain inclusive. Confirmation remains the default, and optional automatic recording uses the same worker, duplicate protection and backlog handling as other frequencies. Frequency remains fixed after creation.

## Compatibility

Append Daily to the domain enum to preserve existing numeric values. AddDailyRecurringFrequency extends the database check constraint without modifying existing schedules. Apply it before deploying the updated API/frontend/worker. Downgrading while Daily schedules exist must fail rather than deleting or converting those schedules.
