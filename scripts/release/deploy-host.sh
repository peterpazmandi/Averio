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
# Immutable image metadata defines which binaries may share a migrated database.
# Images released before this protocol belong to epoch zero.
image_epoch() {
  local value
  value=$(docker image inspect --format '{{index .Config.Labels "io.averio.schema-epoch"}}' "$1") || return 1
  if [[ -z "$value" || "$value" == '<no value>' ]]; then value=0; fi
  [[ "$value" =~ ^(0|[1-9][0-9]{0,5})$ ]] || return 1
  printf '%s\n' "$value"
}
epoch=$(image_epoch "$api_image")
[[ "$(image_epoch "$worker_image")" == "$epoch" && "$(image_epoch "$migrator_image")" == "$epoch" ]] || {
  echo 'Backend images have incompatible schema epochs.' >&2; exit 1;
}
floor=0
if [[ -e "$state/minimum-schema-epoch" ]]; then
  floor=$(cat "$state/minimum-schema-epoch")
  [[ "$floor" =~ ^(0|[1-9][0-9]{0,5})$ ]] || { echo 'Invalid schema compatibility marker.' >&2; exit 1; }
fi
((epoch >= floor)) || { echo 'This database cannot run the selected older backend images. Use a forward release.' >&2; exit 1; }
candidate="$state/candidate.yml"
printf 'services:\n  webapi:\n    image: %s\n  worker:\n    image: %s\n' "$api_image" "$worker_image" > "$candidate"
compose=(docker compose --project-name "$project" --env-file "$env_file" -f "$compose_file")
# Validate before touching the database or running services. Do not print expanded secrets.
"${compose[@]}" -f "$candidate" config --quiet
if [[ -f "$state/current.yml" ]]; then
  snapshot=$(sha256sum "$state/current.yml" | cut -d ' ' -f 1)
  if [[ ! -f "$state/history/$snapshot.yml" ]]; then cp "$state/current.yml" "$state/history/$snapshot.yml"; fi
fi
# Only a verified pair from the same epoch is eligible for automatic recovery.
# Missing/pruned images and a missing overlay fail closed, including upgrade retries.
can_restore=false
if [[ -f "$state/current.yml" ]]; then
  mapfile -t previous_images < <(sed -n 's/^    image: //p' "$state/current.yml")
  if [[ ${#previous_images[@]} == 2 ]] &&
    previous_api_epoch=$(image_epoch "${previous_images[0]}") &&
    previous_worker_epoch=$(image_epoch "${previous_images[1]}") &&
    [[ "$previous_api_epoch" == "$epoch" && "$previous_worker_epoch" == "$epoch" ]]; then
    can_restore=true
  fi
fi
if ((epoch > floor)) || [[ "$can_restore" != true ]]; then
  echo 'Entering maintenance: stopping both backends before migration.'
  "${compose[@]}" -f "$candidate" stop webapi worker
  # A daemon reboot must not restart obsolete containers via restart: always.
  # Remove stopped containers only; retain every volume and the recovery overlays.
  "${compose[@]}" -f "$candidate" rm -f webapi worker
fi
# Fence BEFORE migration: a failed/lost migrator response does not prove rollback.
# Keep this marker on failures; only a reviewed, consistent DB restore can lower it.
printf '%s\n' "$epoch" > "$state/minimum-schema-epoch.next"
mv "$state/minimum-schema-epoch.next" "$state/minimum-schema-epoch"
# Explicit migration failure stops deployment. Never automatically downgrade the database.
docker run --rm --env-file "$migration_env" "$migrator_image"
# Outside maintenance, Compose retains unchanged services and keeps the compatible
# old Worker available until the new API passes readiness checks.
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
  if [[ "$can_restore" == true ]]; then
    # Restore the last deployed backend pair together. Migrations stay applied.
    if "${compose[@]}" -f "$state/current.yml" up -d --no-build --no-deps webapi worker; then
      echo 'Requested restoration of both backend containers from current.yml.' >&2
    else
      echo 'Restore both backend containers from current.yml before retrying.' >&2
    fi
  else
    "${compose[@]}" -f "$candidate" stop webapi worker
    echo 'Backends remain stopped: previous images are incompatible or unverified. Retry a compatible forward release.' >&2
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
