# ADR-004 — Personal money as a rollover budget sub-ledger

**Status:** Accepted  
**Date:** 2026-09

## Context

Household members may want a fixed discretionary allowance while retaining autonomy over how it is spent. Treating the allocation itself as an ordinary expense can double-count later purchases.

## Decision

Personal money is a budget sub-ledger, not merely a category or physical account.

Track:

- periodic allocation
- chargeable usage/spending
- remaining amount
- carry-over/rollover

Unused personal money rolls over by default.

An allocation is not inherently a household expense. Cash-flow reporting depends on whether money actually leaves the household financial boundary.

If household money is transferred into a private account and household reporting stops at that boundary, later private merchant spending must not be counted again as another household expense.

## Consequences

- requires dedicated domain semantics rather than a category hack
- reporting must distinguish allocation from cash movement
- transfer/personal-money integration must prevent double counting
- privacy can be preserved after funds enter a private boundary
