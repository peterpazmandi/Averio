#!/usr/bin/env bash
set -euo pipefail
# Runs as root on the EC2 host. Existing Compose/env/proxy configuration is retained.
environment=$1
project=$2
compose_file=$3
env_file=$4
migration_env=$5
region=$6
registry=$7
api_image=$8
worker_image=$9
migrator_image=${10}
[[ "$environment" =~ ^(integration|production)$ ]]
[[ "$project" =~ ^[a-z0-9_-]+$ ]]
[[ "$registry" =~ ^[0-9]+\.dkr\.ecr\.[a-z0-9-]+\.amazonaws\.com$ ]]
[[ "$region" =~ ^[a-z0-9-]+$ ]]
for file in "$compose_file" "$env_file" "$migration_env"; do
  [[ "$file" =~ ^/etc/averio/[a-zA-Z0-9._/-]+$ && "$file" != *..* && -f "$file" ]]
done
[[ "$api_image" =~ ^${registry//./\.}/averio-api@sha256:[a-f0-9]{64}$ ]]
[[ "$worker_image" =~ ^${registry//./\.}/averio-worker@sha256:[a-f0-9]{64}$ ]]
[[ "$migrator_image" =~ ^${registry//./\.}/averio-migrator@sha256:[a-f0-9]{64}$ ]]
exec 9>"/var/lock/averio-${environment}-deploy.lock"
flock -w 900 9
state="/etc/averio/deploy-${environment}"
mkdir -p "$state"
chmod 700 "$state"
aws ecr get-login-password --region "$region" | docker login --username AWS --password-stdin "$registry"
docker pull "$api_image"
docker pull "$worker_image"
docker pull "$migrator_image"
candidate="$state/candidate.yml"
printf 'services:\n  webapi:\n    image: %s\n  worker:\n    image: %s\n' "$api_image" "$worker_image" > "$candidate"
compose=(docker compose --project-name "$project" --env-file "$env_file" -f "$compose_file")
# Validate before touching the database or running services. Do not print expanded secrets.
"${compose[@]}" -f "$candidate" config --quiet
if [[ -f "$state/current.yml" ]]; then cp "$state/current.yml" "$state/previous.yml"; fi
# Explicit migration failure stops deployment. Never automatically downgrade the database.
docker run --rm --env-file "$migration_env" "$migrator_image"
"${compose[@]}" -f "$candidate" up -d --no-build --no-deps --force-recreate webapi worker
for service in webapi worker; do
  container=$("${compose[@]}" -f "$candidate" ps -q "$service")
  [[ -n "$container" ]]
  expected="$api_image"
  [[ "$service" == webapi ]] || expected="$worker_image"
  [[ "$(docker inspect --format '{{.Config.Image}}' "$container")" == "$expected" ]]
  # Check again after startup so an immediate worker crash is not mistaken for success.
  sleep 10
  [[ "$(docker inspect --format '{{.State.Running}}' "$container")" == true ]]
  [[ "$(docker inspect --format '{{.RestartCount}}' "$container")" == 0 ]]
done
mv "$candidate" "$state/current.yml"
echo 'Backend deployment completed; public smoke checks run next.'
