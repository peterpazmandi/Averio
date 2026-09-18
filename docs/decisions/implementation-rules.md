# Averio Implementation Rules

## Step-by-step guides

Each implementation step must contain:

### Purpose
What this change accomplishes.

### Why
Why Averio needs it.

### Files affected
Exact paths.

### Change
Clearly state whether to:
- create
- replace
- insert
- delete

### Explanation
Explain important types, methods and relationships.

### Complete file
When an existing source file changes, show the complete resulting file.

## Generated client

Backend OpenAPI is the API contract.

Do not manually duplicate generated API DTOs in the frontend.

## React

Prefer:
- route components
- feature components
- shared components
- hooks
- TanStack Query query options
- generated API client

Avoid giant page components.

## Backend

Prefer:
- explicit use cases
- thin controllers
- domain invariants
- EF configurations outside entities
- database constraints for critical invariants

Do not introduce MediatR.

## Database

For every new table consider:

- primary key
- foreign keys
- indexes
- unique constraints
- check constraints
- deletion behavior
- timestamps
- concurrency