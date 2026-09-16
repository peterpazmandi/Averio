# ADR-009 — Backend OpenAPI drives the frontend API client

**Status:** Accepted  
**Date:** 2026-09

## Context

Hand-maintaining matching C# and TypeScript DTOs creates drift and repetitive work.

## Decision

Treat the backend OpenAPI document as the client integration contract and generate the TypeScript client using `@openapitools/openapi-generator-cli`.

Generated client code is not manually edited.

## Consequences

API contract changes follow this flow:

1. update backend contract/endpoint
2. produce updated OpenAPI
3. regenerate TypeScript client
4. adapt feature/UI integration

UI-specific view models remain allowed when they are not duplicates of transport DTOs.
