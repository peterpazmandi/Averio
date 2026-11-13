# ADR-041 — SSM transport for independent manual deployments

Status: Accepted
Date: 2026-11-13

## Context

Deployment hosts restrict SSH to the operator's home IP. GitHub-hosted runners
cannot use the previous SSH transport without changing that boundary or adding
runner/network infrastructure. Integration already supports AWS Systems Manager.

## Decision

Supersede ADR-040 while retaining its independent manual orchestration and
integration-proof requirements in full: release creation only publishes artifacts;
integration and production are separate manual dispatches. Production requires
the exact manifest verified by a successful integration run, with current-attempt
evidence and provenance checks. Retained releases remain selectable subject to
database compatibility. Evidence retention is 90 days.

Replace SSH with SSM Run Command using AWS-RunShellScript version 1. GitHub uses
OIDC roles scoped separately to each environment and its exact EC2 instance.
Validate the AWS account, target node and immutable image references before sending
the trusted checkout's host script. Database credentials stay in host env files.
SSM executes as root; document scoping does not restrict the contents of root commands.
Environment branch restrictions and trusted workflow changes remain essential.

Send the command once, disable SDK submission retries, record its ID and poll the
exact invocation until successful with exit code zero. Eventual invocation visibility
is retried; terminal or unknown results fail closed. Do not upload the frontend or
record integration evidence until remote deployment succeeds. Do not mirror remote
command output into GitHub logs.

Preserve existing host locking, explicit migrations, startup checks, image digest
pinning, recovery overlays, public/browser checks and unchanged-service behavior.
No automatic database rollback is introduced.

## Consequences

No inbound SSH access from GitHub, self-hosted runner or long-lived AWS credentials
are required. Hosts need SSM Agent, instance-role permissions and outbound access.
Deployment environments replace SSH variables/secrets with AWS_ROLE_ARN and
DEPLOY_INSTANCE_ID. Release build credentials remain separate.

Remote execution has a 30-minute limit. Monitoring waits up to 40 minutes. Neither
GitHub cancellation nor a lost response proves the command stopped. Do not send
automatic CancelCommand or resubmit ambiguous requests; operators inspect SSM
command history before retrying. A timeout can interrupt a migration; recovery is
an explicit operational decision. Ordered rollout remains non-atomic.
