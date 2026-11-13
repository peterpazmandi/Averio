# ADR-040 — Independent manual release deployments

Status: Superseded by [ADR-041](ADR-041-ssm-manual-deployments.md) (manual orchestration retained; transport changed to SSM)  
Date: 2026-11-12

## Context

ADR-039 connected integration success directly to production deployment and optionally
started deployment while creating a release. The clarified product requirement is three
independent operator actions: create a release, deploy integration, and later deploy production.

## Decision

Supersede ADR-039's orchestration and latest-release restriction. Retain its component
versioning, immutable artifacts, topology, explicit migrations, health/browser checks,
recovery history and security decisions.

Create release only publishes artifacts. Deploy release is manual, takes a release tag
and one environment, and deploys only that environment. Integration success ends its run.
Production must be dispatched separately, even when no environment reviewer is configured.
Any retained published release can be selected; operators must verify database compatibility
when choosing an older release. Nothing automatically downgrades a schema.

After all integration checks, save a versioned verification artifact containing the release
tag, manifest digest, repository, workflow commit, run ID and attempt. A later production
run takes the successful integration run ID, verifies GitHub run provenance and success,
and uses only that run's current-attempt evidence. The existing staging check enforces
that the downloaded release manifest matches the verified digest before any migration.
Failed, unfinished, foreign or mismatched runs and missing/expired evidence fail closed.

Artifacts have 90-day requested retention. Re-run integration when evidence is unavailable.
Serialize individual deployments without canceling active migrations. Production retries
can reuse valid integration evidence without deploying integration again.

## Consequences

Operators control the testing interval and production timing without keeping a workflow
waiting for approval. GitHub Environment approval remains an optional extra control.
Production dispatch requires the release tag and integration run ID from its summary.
No live deployment is implied by release creation or integration success.
