# ADR-039 — Versioned releases and integration-to-production promotion

Status: Accepted  
Date: 2026-11-11

## Context

Manual deployment now works on integration. Rebuilding independently for production,
manual migrations, CPU architecture mismatches and frontend Git auto-deploys make
releases difficult to reproduce. This extends ADR-011 without replacing its topology.

## Decision

Keep PR verification separate from a manually initiated Create release workflow on
develop. Require successful push verification for the exact source commit. Creating
a release is an intentional action after merging; merges do not deploy production.

Feature releases set frontend, API and Worker to the next shared minor version,
patch zero. Hotfix releases require merged hotfix/ branches since the preceding
release and increase only affected component patches. Shared backend changes affect
both backend hosts. Unknown root build inputs conservatively affect all components.
The first automated release starts at 0.1.0; this does not reinterpret older manual tags.

A published release contains a manifest with source SHA, component versions, ECR
digests and frontend archive SHA-256. Build AMD64 and ARM64 images once. Unchanged
hotfix components reuse prior images/archive. Runtime public frontend configuration
selects environment/API origin and the integration banner; compiled assets are identical.

A separate serialized deployment workflow migrates integration, deploys it, and
tests public health, release identity and real browser cookie/CORS/CSRF authentication.
Only success permits production migration/deployment and public smoke tests.
The production GitHub Environment can require approval, without changes to workflows.
No migrations occur during normal API startup. Use a dedicated EF bundle image and
a host-local migration credential file. Credentials never enter release assets.

SSH uses a pinned host key and the host's AWS instance role for ECR pulls. Existing
Compose files, networking, proxy and environment settings remain authoritative;
generated persistent overlays select exact images. Host and workflow locks prevent
overlapping deployment commands. Only the latest published automated release may deploy.

## Consequences

The API/Worker/frontend rollout is ordered, not atomic. Migrations must support the
old application during deployment (expand/contract). Never automatically downgrade
financial schemas. Recovery requires an explicit operator decision and verified
database compatibility. A frontend upload or smoke failure can leave partially
updated production; the workflow reports failure and preserves image records.

Integration uses a dedicated verified test user; checks do not mutate ledger data.
Production smoke checks are anonymous and read-only. Existing PR regression suites
remain the business correctness gate; live deployment smoke tests do not replace them.

One-time external setup is required. The repository implementation alone does not
configure GitHub secrets, IAM, SSH, Cloudflare auto-build settings, or execute a release.
