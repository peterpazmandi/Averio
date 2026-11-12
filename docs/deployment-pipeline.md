# Release and deployment pipeline

## What changes

PR checks → merge to develop → successful Verify push run → manually Create release.
Later: manually Deploy release to integration → migration/deployment → integration checks → stop.
When ready: manually Deploy release to production → validate saved integration evidence →
production migration/deployment of the same artifacts → production smoke checks.

Creating a release never starts a deployment. A deployment targets exactly one environment.
Successful integration never starts or queues production.

Create release is manual so several merged PRs can form one release. Version numbers
are automatic. Feature releases align all components at the next minor, starting
with 0.1.0. A hotfix release requires every first-parent change since the preceding
automated release to come from a merged `hotfix/` PR. Example: frontend 0.1.1,
API 0.1.0, Worker 0.1.0. Shared `src/` changes bump API and Worker. Unknown build
inputs bump all components conservatively. Docs-only hotfix releases are rejected.
Use a feature release when changing deployment tooling to rebuild all artifacts.

Release tags identify the whole manifest (`release-<run-number>-<attempt>`), while
component semantic versions describe each app. Rerunning a failed creation uses a
new attempt tag. Draft/incomplete releases are never a baseline. Do not overwrite
published assets or delete ECR images referenced by retained releases.

## 1. Create GitHub Environments

Open repository Settings → Environments → New environment. Create `release`,
`integration`, and `production`. Restrict deployment branches to `develop`.
Production always requires a separate manual workflow dispatch. Optionally add a required
reviewer as an additional approval after that dispatch; it is not needed to prevent automatic promotion.

Under Settings → Secrets and variables → Actions, define repository variables:

| Variable | Value/purpose |
| --- | --- |
| `AWS_REGION` | `eu-north-1` |
| `ECR_REGISTRY` | `678745735110.dkr.ecr.eu-north-1.amazonaws.com` |
| `CLOUDFLARE_ACCOUNT_ID` | Your Cloudflare account ID |

In the `release` environment add variable `AWS_ROLE_ARN` for a GitHub OIDC role.
The role trust must restrict `aud` to `sts.amazonaws.com` and `sub` to
`repo:peterpazmandi/Averio:environment:release`. Give it ECR authorization-token
permission and push/pull permissions only for `averio-api`, `averio-worker`, and
`averio-migrator`. Create all three repositories first; the workflow does not create them.

From local PowerShell, the new repository can be created with:

```powershell
aws ecr create-repository --repository-name averio-migrator --image-tag-mutability IMMUTABLE --region eu-north-1 --profile averio
```

Existing API/Worker repositories can stay in place. Retain images for your supported
recovery window; a frontend-only hotfix continues referencing older backend digests.

## 2. Configure each deployment environment

Add these variables separately under integration and production:

| Variable | Integration example / meaning |
| --- | --- |
| `DEPLOY_HOST` | Reachable EC2 DNS/IP; SSH port 22 |
| `DEPLOY_USER` | `ubuntu` (dedicated deployment account also supported) |
| `COMPOSE_PROJECT` | `averio-integration`; production must match its existing project name |
| `COMPOSE_FILE` | `/etc/averio/compose.integration.yml` |
| `ENV_FILE` | `/etc/averio/.env.integration` |
| `MIGRATION_ENV_FILE` | `/etc/averio/migration.integration.env` |
| `API_URL` | `https://integration-api.averioapp.com` |
| `FRONTEND_URL` | Your actual integration frontend HTTPS origin |
| `CLOUDFLARE_PAGES_PROJECT` | Existing integration Pages project name |
| `CLOUDFLARE_PAGES_BRANCH` | That Pages project's configured production branch |

For production use its actual host, Compose project/files and Pages project, plus
`https://api.averioapp.com` and `https://averioapp.com`. Do not copy integration paths
blindly: the workflow operates on the exact Compose project you name.

Use the working custom frontend domain under `averioapp.com` for integration,
not its `pages.dev` address. Frontend and API must remain on the same site for the
existing SameSite cookie policy, and the frontend origin must be in API CORS settings.

Add these environment secrets:

| Secret | Purpose |
| --- | --- |
| `DEPLOY_SSH_KEY` | Private key for the deployment account |
| `DEPLOY_KNOWN_HOSTS` | Verified SSH known_hosts line for DEPLOY_HOST |
| `CLOUDFLARE_API_TOKEN` | Token with Account → Cloudflare Pages → Edit for this account |
| `SMOKE_EMAIL` | Integration only: dedicated verified Averio test account with onboarding completed |
| `SMOKE_PASSWORD` | Integration only: password for that account |

Verify the SSH host key through your existing trusted EC2 session; do not blindly
trust `ssh-keyscan` output. The workflow requires strict host-key checking.
The runner must be able to reach SSH; if your host is private or restricted to your
home IP, use an approved runner/network route before enabling deployments. Do not
open unrestricted SSH solely to make the workflow pass. Deployment currently uses
SSH, not SSM or a Cloudflare Access SSH tunnel.

## 3. Prepare EC2 once

Keep the working API/Worker Compose configuration and Cloudflare tunnel. The service
names must be `webapi` and `worker`; deployment only replaces their image references.
The chosen account must support `sudo -n bash` (the workflow is effectively root
deployment access). Docker Compose v2, AWS CLI, Bash, and `flock` must be installed.
Give the EC2 instance role ECR read permissions for all three repositories.

Create a separate migration env file on each host using the configured path:

```bash
sudo install -m 600 /dev/null /etc/averio/migration.integration.env
sudo nano /etc/averio/migration.integration.env
```

Insert the entire following file, replacing the example with that environment's
direct Neon connection string and migration role credentials:

```dotenv
AVERIO_MIGRATION_CONNECTION_STRING=Host=YOUR_DIRECT_NEON_HOST;Database=YOUR_DATABASE;Username=YOUR_MIGRATION_ROLE;Password=YOUR_PASSWORD;SSL Mode=Require
```

Use the production file/path/credentials separately for production. No quotes around
the value: Docker `--env-file` passes them literally. This is for the EF design-time
factory; application runtime credentials remain in the existing application env file.
Never commit either file. Confirm Neon recovery/backup retention before the first release.

Migrations execute in a short-lived, architecture-matched image before containers
are replaced. A failure prevents application deployment. Existing apps continue
running during migration, so migrations must be backward compatible. Destructive
changes require a separate planned maintenance procedure.

The workflow preserves `/etc/averio/deploy-<environment>/current.yml` and
`previous.yml`. It also retains content-addressed overlays under `history/`.
Migration, Compose and startup failures leave both references untouched; rotation
happens only after backend startup checks succeed. An unchanged retry preserves the
previous reference. These are backend rollout records, not evidence that public
frontend checks passed. Failed candidates remain available for diagnosis.

Compose compares the desired images and effective service configuration with the
running containers. Unchanged API/Worker containers remain running during a
frontend-only hotfix; changed backend configuration still triggers recreation.
Use the current overlay in later manual Compose commands, otherwise
the old image in your original Compose file could be started again:

```bash
sudo docker compose --project-name averio-integration \
  --env-file /etc/averio/.env.integration \
  -f /etc/averio/compose.integration.yml \
  -f /etc/averio/deploy-integration/current.yml ps
```

## 4. Make Cloudflare follow the release pipeline

Open Workers & Pages → each Averio Pages project → Settings → Builds & deployments
→ Branch control. Disable automatic production and preview Git deployments for the
projects controlled by this pipeline. Keep existing custom domains. Wrangler can
upload prebuilt files to an existing Git-integrated Pages project.

Set `CLOUDFLARE_PAGES_BRANCH` to the project's production branch so the custom domain
receives the upload. Use separate integration and production projects. Ensure existing
Cloudflare cache rules do not override no-store on `/runtime-config.js` and `/version.json`.
Preflight verifies that the project's associated domains contain the exact hostname
in `FRONTEND_URL`, as well as checking its name and branch. A mismatched target stops
the job before database migrations or uploads.
Integration still receives its banner and `X-Robots-Tag: noindex`; noindex is not access control.

The frontend archive is built once. Deployment changes only public `runtime-config.js`
and hosting `_headers`; JS/CSS assets and `version.json` are promoted unchanged.
Runtime settings contain no secrets. Existing local Vite `.env` behavior remains supported.

## 5. Create a release, then deploy each environment separately

1. Merge this PR and wait for all Verify jobs on the develop push to pass.
2. Open Actions → Create release → Run workflow; select branch `develop` and kind
   `feature` (or `hotfix` when applicable). This only builds and publishes the release.
3. Copy the published release tag, for example `release-12-1`.
4. When ready for integration, open Actions → Deploy release → Run workflow on `develop`.
   Enter the tag, choose `integration`, and leave `integration_run_id` empty.
5. Wait for migration, backend/frontend deployment, public checks and real login/logout
   checks. The successful run saves an integration verification artifact and prints its
   **Integration verification run ID** in the summary. Production remains untouched.
6. Test integration manually for as long as needed. Other releases may be created or
   tested meanwhile; the selected release does not have to be the latest published one.
7. When ready for production, start **a new** Deploy release workflow on `develop`.
   Enter the same release tag, choose `production`, and paste the successful integration
   run ID from step 5 into `integration_run_id`. This does not redeploy integration.
8. The workflow verifies the source run and saved evidence, then migrates/deploys only
   production and runs its smoke checks. Any configured environment approval still applies.

The evidence binds the release tag and exact manifest SHA-256 to a successful manual
Deploy release run from this repository's develop branch. Production checks the latest
attempt of the supplied run and rejects failed/incomplete runs, foreign workflows,
wrong releases, earlier-attempt evidence and missing/deleted/expired artifacts. Evidence
is retained for 90 days (subject to repository policy). If unavailable, run integration
again for the same release and use the new successful run ID.

Production compares its downloaded manifest against this saved digest before staging
or migrations. Replacing the manifest/archive after integration therefore blocks deployment.
An older selected release must still be compatible with the target database schema;
manual selection is not an automatic database rollback. Prefer forward releases.
GitHub concurrency serializes individual deployments and may replace an older pending
run; it is not a FIFO queue. Running migrations are not automatically canceled.

## Failure and recovery

- Verification failure: fix the PR/commit; no release can be created from it.
- Build/upload failure: no complete published release is promoted. Fix the cause and
  rerun Create release. A draft tag may remain for diagnosis.
- Integration failure: no usable verification is recorded. Fix configuration and rerun
  Deploy release with environment `integration` and the same tag; EF applies only pending migrations.
- Production failure: stop and inspect which step failed. Retry production explicitly with
  the same release and successful integration run ID; integration need not run again. API, Worker and frontend
  deployment is not a single transaction. The release may be partially deployed.
- Prefer forward fixes. Do not roll back database migrations automatically. If older
  binaries are verified compatible with the current schema, an operator may use the
  recorded previous image overlay and restore the matching Cloudflare deployment.
  Otherwise follow the Neon recovery/maintenance procedure. A first automated release
  has no previous overlay: record the existing image digests and Pages deployment first.

Public checks verify DB connectivity, API version/source identity, frontend identity,
the served HTML, runtime API/environment settings, and the entry JS/CSS against the
staged release. Both environments load the real login page in a browser and verify
that the application itself calls the expected API and receives an anonymous 401.
Integration additionally submits the actual login form, reaches `/app`, uses the
Sign out button, and verifies that a reload is anonymous. It therefore exercises the
application's own API configuration, secure cookies and CSRF handling. The dedicated
smoke account must be verified and have completed onboarding; use an empty test
household. Production never submits credentials or performs ledger mutations.
Worker checks verify the requested image, running state and no immediate restarts;
they do not prove successful execution of every recurring/receipt background job.
PR business regression tests remain required. No real ledger test data is created by
the deployment smoke checks.

PR verification also builds the migration image, applies the complete migration
chain twice against a disposable PostgreSQL database, and checks the ARM64 build.
This checks the exact migration delivery mechanism before it can reach Neon.
Deployment regression tests cover broken HTML/runtime/assets, incorrect Pages domains,
manifest replacement after integration, and preservation of recovery overlays across
migration/Compose/startup failures. Browser tests exercise the same production smoke
implementation against compiled assets, including negative runtime and asset cases.

References: [GitHub workflow triggers](https://docs.github.com/en/actions/how-tos/write-workflows/choose-when-workflows-run/trigger-a-workflow),
[Cloudflare Git integration](https://developers.cloudflare.com/pages/configuration/git-integration/),
[Pages CI uploads](https://developers.cloudflare.com/pages/how-to/use-direct-upload-with-continuous-integration/).
