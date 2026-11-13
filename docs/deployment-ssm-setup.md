# Set up deployment through AWS Systems Manager

The deployment workflow uses GitHub-hosted runners and AWS Systems Manager Run Command.
Keep EC2 SSH restricted to your home IP. No new inbound rule, self-hosted runner,
SSH tunnel or deployment PEM key is needed.

GitHub assumes an environment-specific AWS role with OIDC, sends the checked-out
host deployment script to one EC2 instance, and waits for its result. SSM Agent
runs the script as root. Database credentials remain in the existing files on EC2.
Release creation, integration deployment and production deployment remain separate
manual actions.

## 1. Make both EC2 instances managed nodes

In AWS select **Europe (Stockholm), eu-north-1**.

1. Open **EC2 → Instances**, select the integration instance and open **Security**.
2. Follow its **IAM role** link. In IAM choose **Add permissions → Attach policies**.
3. Attach **AmazonSSMManagedInstanceCore** to the existing instance role. Preserve
   its ECR permissions; do not replace a working instance role with a role that
   only has SSM permissions.
4. Repeat for production. If no instance role exists, create an IAM role with
   trusted entity **AWS service → EC2**, attach SSM and the required ECR read
   permissions, then use **EC2 → Actions → Security → Modify IAM role** to attach it.
5. Open **Systems Manager → Fleet Manager** (Managed nodes) and confirm both
   instance IDs appear online. Your integration Session Manager session already
   demonstrates that integration has a working SSM connection; verify production too.

The EC2 role needs ECR authorization-token permission and image download permissions
for averio-api, averio-worker and averio-migrator. This is separate from the GitHub
deployment role created below.

From your local PowerShell, check integration without changing it:

```powershell
aws ssm describe-instance-information --filters "Key=InstanceIds,Values=i-0d54ed79d21e72d8f" --region eu-north-1 --profile averio --query "InstanceInformationList[].{Instance:InstanceId,Status:PingStatus,OS:PlatformType}" --output table
```

Repeat with the production instance ID copied from EC2. Expect Online and Linux.
An empty list means the instance is not registered in this account/region or the
profile cannot see it. AccessDenied means your local IAM identity needs read access.

If a node is offline, check the agent in an existing SSH/Session Manager session.
Ubuntu can use either a snap or a package installation; check the installed service:

```bash
sudo systemctl status snap.amazon-ssm-agent.amazon-ssm-agent.service
# For a package installation instead:
sudo systemctl status amazon-ssm-agent.service
```

SSM Agent initiates outbound HTTPS connections. Allow outbound TCP 443 to the
regional SSM endpoints (ssm and ssmmessages; older agents can also use ec2messages).
Private subnets need suitable outbound access or VPC endpoints. ECR image downloads
also need ECR/S3 connectivity. Keep the existing inbound SSH rule unchanged.

## 2. Reuse or create the GitHub OIDC provider

In **IAM → Identity providers**, look for `token.actions.githubusercontent.com`.
Reuse it if the release workflow already uses it. Otherwise choose **Add provider**:

- Provider type: OpenID Connect.
- Provider URL: `https://token.actions.githubusercontent.com`.
- Audience: `sts.amazonaws.com`.

Do not create long-lived AWS access keys for this workflow.

## 3. Create the integration deployment role

In **IAM → Roles → Create role → Custom trust policy**, insert this complete policy:

```json
{
  "Version": "2012-10-17",
  "Statement": [{
    "Effect": "Allow",
    "Principal": {
      "Federated": "arn:aws:iam::678745735110:oidc-provider/token.actions.githubusercontent.com"
    },
    "Action": "sts:AssumeRoleWithWebIdentity",
    "Condition": {
      "StringEquals": {
        "token.actions.githubusercontent.com:aud": "sts.amazonaws.com",
        "token.actions.githubusercontent.com:sub": "repo:peterpazmandi/Averio:environment:integration"
      }
    }
  }]
}
```

Continue without broad managed policies and name the role
`averio-deploy-integration`. Open it, choose **Add permissions → Create inline
policy → JSON**, and insert this complete policy:

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Sid": "DeployOnlyToIntegration",
      "Effect": "Allow",
      "Action": "ssm:SendCommand",
      "Resource": [
        "arn:aws:ssm:eu-north-1::document/AWS-RunShellScript",
        "arn:aws:ec2:eu-north-1:678745735110:instance/i-0d54ed79d21e72d8f"
      ]
    },
    {
      "Sid": "CheckNodeAndCommand",
      "Effect": "Allow",
      "Action": ["ssm:DescribeInstanceInformation", "ssm:GetCommandInvocation"],
      "Resource": "*"
    }
  ]
}
```

Name the inline policy `averio-deploy-integration-ssm`.
The two read APIs require wildcard resources; command submission is limited to the
specified document and instance. This role does not need ECR push permissions.

**The permission still allows root shell commands on that instance.** It is an
instance boundary, not a shell-command allowlist. Protect develop and restrict the
GitHub environment's deployment branches to develop. Do not grant this role to
untrusted PR jobs.

## 4. Create the production deployment role

Repeat step 3 with role name `averio-deploy-production`. Use this full trust policy:

```json
{
  "Version": "2012-10-17",
  "Statement": [{
    "Effect": "Allow",
    "Principal": {
      "Federated": "arn:aws:iam::678745735110:oidc-provider/token.actions.githubusercontent.com"
    },
    "Action": "sts:AssumeRoleWithWebIdentity",
    "Condition": {
      "StringEquals": {
        "token.actions.githubusercontent.com:aud": "sts.amazonaws.com",
        "token.actions.githubusercontent.com:sub": "repo:peterpazmandi/Averio:environment:production"
      }
    }
  }]
}
```

For its inline policy, replace `PRODUCTION_INSTANCE_ID` with the exact instance ID
copied from the production EC2 instance before saving:

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Sid": "DeployOnlyToProduction",
      "Effect": "Allow",
      "Action": "ssm:SendCommand",
      "Resource": [
        "arn:aws:ssm:eu-north-1::document/AWS-RunShellScript",
        "arn:aws:ec2:eu-north-1:678745735110:instance/PRODUCTION_INSTANCE_ID"
      ]
    },
    {
      "Sid": "CheckNodeAndCommand",
      "Effect": "Allow",
      "Action": ["ssm:DescribeInstanceInformation", "ssm:GetCommandInvocation"],
      "Resource": "*"
    }
  ]
}
```

Name it `averio-deploy-production-ssm`. Do not include the production instance in
the integration role. The release environment retains its separate ECR build role.

## 5. Set the GitHub environment variables

Open **Averio → Settings → Environments → integration → Environment variables**.
Add these, then repeat under production:

| Variable | integration | production |
| --- | --- | --- |
| AWS_ROLE_ARN | arn:aws:iam::678745735110:role/averio-deploy-integration | arn:aws:iam::678745735110:role/averio-deploy-production |
| DEPLOY_INSTANCE_ID | i-0d54ed79d21e72d8f | Actual production instance ID |

Retain repository variables AWS_REGION=eu-north-1 and
ECR_REGISTRY=678745735110.dkr.ecr.eu-north-1.amazonaws.com.
Retain each environment's Compose project, Compose/env/migration file paths,
API/frontend URLs and Cloudflare settings described in [deployment-pipeline.md](deployment-pipeline.md).

These deployment workflows no longer read DEPLOY_HOST, DEPLOY_USER, DEPLOY_SSH_KEY
or DEPLOY_KNOWN_HOSTS. After switching to this workflow, those GitHub entries can
be removed if no other workflow uses them. Keep your local PEM keys for personal
SSH access; do not remove authorized_keys from EC2.

In each GitHub environment set **Deployment branches and tags → Selected branches
and tags → develop**. Optionally require a production reviewer. A new manual
production dispatch is required regardless of reviewer configuration.

## 6. Test integration first

Merge the pipeline PR and wait for Verify to pass. Create a feature release, then
manually run **Deploy release**, branch develop, environment integration and the
published release tag. Leave integration_run_id empty.

The SSM step checks account and managed-node identity, sends one command and records
its command ID in the GitHub job summary. It waits for Success with exit code zero.
Only then do Cloudflare deployment and public/browser checks run. Successful
integration records the evidence needed for a later production dispatch.

Inspect the command via **AWS Systems Manager → Run Command → Command history**,
selecting the command ID from the summary. Output is available there; it is not
copied into GitHub logs, because application errors could contain private settings.
Never paste environment file contents or credentials into diagnostic output.

Only after integration checks and your own testing, start a separate Deploy release
run for production with the same release tag and successful integration run ID.
Integration success never triggers production automatically.

## Interrupted commands and recovery

The delivery timeout is 120 seconds; remote execution is limited to 30 minutes.
The GitHub script monitors for up to 40 minutes and the job has a 60-minute limit.
SSM's remote timeout can terminate execution; it is not a database rollback.

Canceling the GitHub job does not guarantee that the remote process stops. The
workflow deliberately does not send CancelCommand during a migration. If submission
fails ambiguously, monitoring fails, or the workflow is canceled, inspect Run Command
and the instance before retrying. Do not assume a red GitHub run means no changes
were made. The host deployment lock still prevents simultaneous host deployments.

IAM/OIDC failures happen before command submission. Offline-node checks also stop
before submission. A failed migration prevents backend rollout and frontend upload.
After a later failure the rollout may be partial; follow the recovery procedure in
[deployment-pipeline.md](deployment-pipeline.md). No automatic database downgrade runs.

References:
- [GitHub OIDC with AWS](https://docs.github.com/en/actions/how-tos/secure-your-work/security-harden-deployments/oidc-in-aws)
- [Run Command permissions](https://docs.aws.amazon.com/systems-manager/latest/userguide/run-command-setting-up.html)
- [SSM Agent connectivity](https://docs.aws.amazon.com/systems-manager/latest/userguide/troubleshooting-ssm-agent.html)
- [Run Command status and timeouts](https://docs.aws.amazon.com/systems-manager/latest/userguide/monitor-commands.html)
