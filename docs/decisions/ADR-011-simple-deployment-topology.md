# ADR-011 — Simple Cloudflare + EC2 + Neon deployment topology

**Status:** Accepted  
**Date:** 2026-09

## Context

Averio is a solo-developed early-stage product with a small infrastructure budget. Kubernetes/EKS/Helm would solve operational problems the product does not yet have.

## Decision

Target a simple topology:

- Cloudflare for DNS/edge and frontend static hosting
- AWS EC2 for Dockerized API
- separate Dockerized Worker when background workload requires it
- Neon PostgreSQL
- Docker Compose on EC2
- ECR as image registry where used
- GitHub Actions for CI/CD after the manual deployment path works

Do not use Kubernetes/Helm for the current stage.

## Consequences

- low operational complexity/cost
- easy local-to-production mental model
- vertical scaling is preferred before orchestration
- infrastructure can be revisited if real load/team needs justify it
