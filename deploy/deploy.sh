#!/usr/bin/env bash
# Deploys one app image on the production server, run by CD on the self-hosted runner or by hand
# Usage is DEPLOY_DIR=/opt/inventory deploy/deploy.sh ghcr.io/owner/inventory-app:sha-commit
# A failed deploy puts back the previous image together with the docker-compose.yml and deploy/nginx it ran with
set -euo pipefail

IMAGE="${1:?usage: deploy.sh <image>}"
DEPLOY_DIR="${DEPLOY_DIR:-$(pwd)}"
HEALTH_TIMEOUT="${HEALTH_TIMEOUT:-240}"   # Seconds, long enough for the startup migrations
KEEP_BACKUPS="${KEEP_BACKUPS:-14}"
KEEP_IMAGES="${KEEP_IMAGES:-3}"   # App images kept for rollback, the deployed and previous one included
APP_CONTAINER=inventory_app
PG_CONTAINER=inventory_postgres
PREVIOUS_FILES=.previous   # The deployment files of the last successful deploy, first saved by CD

cd "$DEPLOY_DIR"
[ -f .env ] || { echo "No .env in $DEPLOY_DIR" >&2; exit 1; }

# The server's .env has Windows line endings and Compose ignores the \r, so this does too
env_value() { grep -E "^$1=" .env | tail -n 1 | cut -d= -f2- | tr -d '\r' || true; }

set_env_value() {
    if grep -qE "^$1=" .env; then
        sed -i "s|^$1=.*|$1=$2|" .env
    else
        printf '\n%s=%s\n' "$1" "$2" >> .env
    fi
}

run_app() {
    # A locally built image is not in the registry, so the pull fails and up uses the local copy
    APP_IMAGE="$1" docker compose pull --quiet app || echo "Pull of $1 failed; using a local copy if there is one"
    APP_IMAGE="$1" docker compose up -d --no-build --no-deps app
}

wait_healthy() {
    local deadline=$((SECONDS + HEALTH_TIMEOUT)) status
    while [ "$SECONDS" -lt "$deadline" ]; do
        status=$(docker inspect -f '{{if .State.Health}}{{.State.Health.Status}}{{else}}none{{end}}' \
            "$APP_CONTAINER" 2>/dev/null || echo missing)
        case "$status" in
            healthy) return 0 ;;
            unhealthy) return 1 ;;
        esac
        sleep 5
    done
    return 1
}

# Tests the config on disk in a new container of the nginx service, with a stand-in address for the app it looks up
nginx_config_valid() {
    docker compose run --rm --no-deps -T nginx sh -c 'echo "127.0.0.1 app" >> /etc/hosts && nginx -t'
}

# A request through the tunnel entry, which only succeeds when nginx reaches the app with the config it runs
site_answers() {
    local deadline=$((SECONDS + 60)) answer=""
    while [ "$SECONDS" -lt "$deadline" ]; do
        answer=$(docker compose exec -T nginx wget -q -T 5 -O /dev/null http://127.0.0.1:8080/health 2>&1) && return 0
        sleep 5
    done
    echo "$answer" >&2
    return 1
}

reload_nginx() { docker compose exec -T nginx nginx -t && docker compose exec -T nginx nginx -s reload; }

# Copied over the existing files, because nginx.conf is bind-mounted on its own and a replaced file would not reach nginx
restore_files() {
    if [ -f "$PREVIOUS_FILES/docker-compose.yml" ] && [ -d "$PREVIOUS_FILES/deploy/nginx" ]; then
        cp "$PREVIOUS_FILES/docker-compose.yml" docker-compose.yml
        cp -R "$PREVIOUS_FILES/deploy/nginx/." deploy/nginx/
        echo "==> docker-compose.yml and deploy/nginx are back to those of the last successful deploy" >&2
    else
        echo "!! No copy of the previous deployment files in $PREVIOUS_FILES, so the new ones stay" >&2
        return 1
    fi
}

save_files() {
    rm -rf "$PREVIOUS_FILES.tmp"
    mkdir -p "$PREVIOUS_FILES.tmp/deploy"
    cp docker-compose.yml "$PREVIOUS_FILES.tmp/"
    cp -R deploy/nginx "$PREVIOUS_FILES.tmp/deploy/"
    rm -rf "$PREVIOUS_FILES"
    mv "$PREVIOUS_FILES.tmp" "$PREVIOUS_FILES"
}

# Stops before anything changed, with the files the running stack was started from back in place
nothing_deployed() {
    echo "!! $1; nothing was deployed" >&2
    restore_files || true
    exit 1
}

previous=$(env_value APP_IMAGE)
[ -n "$previous" ] || previous=$(docker inspect -f '{{.Config.Image}}' "$APP_CONTAINER" 2>/dev/null || true)
echo "==> Deploying $IMAGE (running now: ${previous:-nothing})"

# The running nginx already sees the new files, so a broken config must stop the deploy before it is ever loaded
nginx_config_valid || nothing_deployed "The nginx configuration did not pass its test"

# Back up first, since migrations run at startup and an image rollback does not undo them
db_name=$(env_value DB_NAME)
mkdir -p backups && chmod 700 backups   # Full copies of the data
backup="backups/pre-deploy-$(date +%Y%m%d-%H%M%S).dump"
echo "==> Backup of database '${db_name:-inventory}' -> $backup"
docker exec "$PG_CONTAINER" sh -c 'pg_dump -U "$POSTGRES_USER" -Fc "$1"' _ "${db_name:-inventory}" > "$backup" \
    || { rm -f "$backup"; nothing_deployed "Backup failed"; }
[ -s "$backup" ] || { rm -f "$backup"; nothing_deployed "Backup is empty"; }
ls -1t backups/pre-deploy-*.dump | tail -n +"$((KEEP_BACKUPS + 1))" | xargs -r rm --

# Recreate only the app first, then bring the rest up to date, and stop at the first step that fails
failure=""
others_updated=""
if ! { run_app "$IMAGE" && wait_healthy; }; then
    failure="$IMAGE did not become healthy within ${HEALTH_TIMEOUT}s"
    docker compose logs --tail 100 app >&2 || true
else
    # Pinned in .env so a plain docker compose up keeps running this image
    set_env_value APP_IMAGE "$IMAGE"
    others_updated=1
    # Other services pick up compose changes, and nginx needs a reload because its config is a bind mount
    if ! docker compose up -d --no-build; then
        failure="The other services could not be brought up to date"
    elif ! wait_healthy; then
        failure="The app did not stay healthy while the other services were updated"
    elif ! reload_nginx; then
        failure="nginx could not load the new configuration"
    elif ! site_answers; then
        failure="The site does not answer through nginx"
    fi
fi

if [ -z "$failure" ]; then
    save_files
    echo "$(date -Is) $IMAGE" >> deploy-history.log
    docker image prune -f > /dev/null
    # Every deploy leaves a tagged image behind, and the registry still has the removed ones
    docker images "${IMAGE%:*}" --format '{{.CreatedAt}}\t{{.Repository}}:{{.Tag}}' | sort -r | cut -f2 \
        | grep -vxF -e "$IMAGE" -e "${previous:-none}" | tail -n +"$((KEEP_IMAGES - 1))" \
        | xargs -r docker image rm > /dev/null || true
    echo "==> Deployed $IMAGE"
    exit 0
fi

echo "!! $failure" >&2
restore_files || true
if [ -n "$previous" ]; then
    echo "==> Rolling back to $previous" >&2
    [ -z "$others_updated" ] || set_env_value APP_IMAGE "$previous"
    if ! { run_app "$previous" && wait_healthy; }; then
        echo "!! The previous image is not healthy either. If a migration is the cause, restore $backup (deploy/CD.md)." >&2
    elif [ -n "$others_updated" ] && ! { docker compose up -d --no-build && reload_nginx && site_answers; }; then
        echo "!! $previous runs again, but the site does not answer through nginx (docker compose logs nginx)" >&2
    else
        echo "==> Rolled back to $previous" >&2
    fi
fi
exit 1
