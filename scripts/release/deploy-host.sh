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
command -v curl >/dev/null
exec 9>"/var/lock/averio-${environment}-deploy.lock"
flock -w 900 9
state="/etc/averio/deploy-${environment}"
mkdir -p "$state"
chmod 700 "$state"
mkdir -p "$state/history"
chmod 700 "$state/history"
aws ecr get-login-password --region "$region" | docker login --username AWS --password-stdin "$registry"
docker pull "$api_image"
docker pull "$worker_image"
docker pull "$migrator_image"
candidate="$state/candidate.yml"
printf 'services:\n  webapi:\n    image: %s\n  worker:\n    image: %s\n' "$api_image" "$worker_image" > "$candidate"
compose=(docker compose --project-name "$project" --env-file "$env_file" -f "$compose_file")
# Validate before touching the database or running services. Do not print expanded secrets.
"${compose[@]}" -f "$candidate" config --quiet
if [[ -f "$state/current.yml" ]]; then
  snapshot=$(sha256sum "$state/current.yml" | cut -d ' ' -f 1)
  if [[ ! -f "$state/history/$snapshot.yml" ]]; then cp "$state/current.yml" "$state/history/$snapshot.yml"; fi
fi
# Explicit migration failure stops deployment. Never automatically downgrade the database.
docker run --rm --env-file "$migration_env" "$migrator_image"
# Compose retains unchanged services. Start and verify the API with the old Worker
# still available before removing its receipt extraction loop.
start_service() {
  local overlay=$1 service=$2 expected=$3 container restart_count binding attempt status
  "${compose[@]}" -f "$overlay" up -d --no-build --no-deps "$service" || return 1
  container=$("${compose[@]}" -f "$overlay" ps -q "$service") || return 1
  [[ -n "$container" && "$(docker inspect --format '{{.Config.Image}}' "$container")" == "$expected" ]] || return 1
  restart_count=$(docker inspect --format '{{.RestartCount}}' "$container") || return 1
  if [[ "$service" == webapi ]]; then
    if ! binding=$("${compose[@]}" -f "$overlay" port webapi 8080 2>/dev/null) || [[ -z "$binding" ]]; then
      binding=$(docker inspect --format '{{range .NetworkSettings.Networks}}{{if .IPAddress}}{{println .IPAddress}}{{end}}{{end}}' "$container") || return 1
      read -r binding <<< "$binding" || return 1
      [[ -n "$binding" ]] || return 1
      binding="$binding:8080"
    fi
    for ((attempt = 1; attempt <= 12; attempt++)); do
      if status=$(curl --noproxy '*' --silent --max-time 5 --output /dev/null --write-out '%{http_code}' \
        "http://$binding/api/v1/system/health") && [[ "$status" == 200 ]]; then
        break
      fi
      sleep 5
    done
    ((attempt <= 12)) || return 1
  fi
  sleep 10
  [[ "$(docker inspect --format '{{.State.Running}}' "$container")" == true &&
    "$(docker inspect --format '{{.RestartCount}}' "$container")" == "$restart_count" ]]
}

if ! start_service "$candidate" webapi "$api_image" || ! start_service "$candidate" worker "$worker_image"; then
  echo 'Backend startup failed.' >&2
  if [[ -f "$state/current.yml" ]]; then
    # Restore the last deployed backend pair together. Migrations stay applied.
    if "${compose[@]}" -f "$state/current.yml" up -d --no-build --no-deps webapi worker; then
      echo 'Requested restoration of both backend containers from current.yml.' >&2
    else
      echo 'Restore both backend containers from current.yml before retrying.' >&2
    fi
  fi
  exit 1
fi
# Do not discard the recovery reference on migration, Compose or startup failure.
if [[ -f "$state/current.yml" ]] && ! cmp -s "$state/current.yml" "$candidate"; then
  cp "$state/current.yml" "$state/previous.next.yml"
  mv "$state/previous.next.yml" "$state/previous.yml"
fi
snapshot=$(sha256sum "$candidate" | cut -d ' ' -f 1)
if [[ ! -f "$state/history/$snapshot.yml" ]]; then cp "$candidate" "$state/history/$snapshot.yml"; fi
mv "$candidate" "$state/current.yml"
echo 'Backend deployment completed; public smoke checks run next.'
