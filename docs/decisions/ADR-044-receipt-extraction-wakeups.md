# ADR-044 Receipt extraction wake-ups

- Status: Accepted
- Date: 2026-11-17

## Context

The Worker queried PostgreSQL every ten seconds for new receipt scans. That kept the Neon compute active even when there were no uploads. Each environment currently has a single API process.

## Decision

Run receipt extraction in an API-hosted background service. PostgreSQL remains the durable queue. The API signals its local service after storing an upload and finishing the queue transaction. Startup recovery drains due scans and wakes at the earliest future retry or claim expiry. With no pending scans, the service waits in memory for an upload signal.

Reuse the atomic claims, processing leases, retry budget, and result fencing. Keep image cleanup in the Worker's nightly maintenance window. Deploy the new API before removing the old Worker's extraction loop. The shared claim protects scans while the two versions overlap.

## Consequences

Receipt status, uploads, and results survive API restarts. The upload endpoint returns while extraction runs. A connection failure during recovery triggers a retry. This design assumes uploads for an environment reach its API-hosted service. If extraction moves to another process, revisit job notification and recovery.
