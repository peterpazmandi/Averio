# ADR-051 — Enforce schema compatibility during deployment

Status: Accepted

Date: 2026-11-20

Extends ADR-050's maintenance requirement with enforcement in the supported single-host deployment workflow. Its refund provenance and downgrade rules remain unchanged.

## Context

The previous deployment script ran migrations while the old API was live and automatically restarted the previous backend pair after startup failures. After the refund provenance migration, old writers omit the required origin and old reversal handlers can void genuine bank credits. An EF downgrade guard cannot protect a binary-only fallback.

## Decision

API, Worker and migrator runtime images carry an immutable `io.averio.schema-epoch` label. Missing historical labels mean epoch 0; this release declares epoch 1 on all three images. A deployment rejects mixed or malformed epochs before stopping services or migrating. Changes within an epoch must remain compatible with that epoch's binaries; future incompatible changes increment all three labels together.

Under the existing deployment lock, the host stops both backends before crossing an epoch or whenever the recorded current backend pair cannot be verified compatible. Stopped containers are removed without deleting volumes, preventing obsolete images from restarting after a Docker daemon reboot. Stop/removal failure prevents migration. Before running the migrator it atomically records the candidate epoch in `/etc/averio/deploy-<environment>/minimum-schema-epoch`. This monotonic floor rejects older releases after failed, interrupted or ambiguously completed migration attempts. Recovery references are rotated only after successful startup, so retries also inspect the actual images named by the current overlay rather than assuming the floor means the previous deployment succeeded.

Automatic startup-failure fallback is allowed only when both current images are verified to have the candidate epoch. Otherwise both candidate services are stopped and the environment remains in maintenance for a compatible forward retry. Unavailable image metadata fails closed. Same-epoch deployments retain the existing rolling startup and compatible fallback behavior.

The supported boundary is the updated deployment workflow on the single managed host. Operators must preserve the floor with host state, use the updated script when deploying historical artifacts, and never run historical workflow code or direct Compose rollbacks against the upgraded database. A database moved to another host must carry its compatibility floor before deployment. Multiple independent writers/hosts require coordinated maintenance outside this script. Lowering the floor requires a reviewed restoration of a matching pre-upgrade database backup and backend/frontend release while writers remain stopped; a failed migrator exit alone is not evidence that no schema changes committed.

## Verification

Host-script regression tests exercise upgrade success, migration/Compose/API readiness/Worker failures, stop failure, missing recovery metadata, mixed labels, corrupt markers, rejection of older releases after migration failure, and successful forward retries. Existing same-epoch fallback and unchanged-backend tests remain in place. CI executes these against the real Bash script with isolated filesystem paths and substituted external commands, without accessing an environment or database.
