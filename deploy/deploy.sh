#!/usr/bin/env bash
# Deploys one app image on the production server, run by CD on the self-hosted runner or by hand
# Usage is DEPLOY_DIR=/opt/inventory deploy/deploy.sh ghcr.io/owner/inventory-app:sha-commit
set -euo pipefail

IMAGE="${1:?usage: deploy.sh <image>}"
DEPLOY_DIR="${DEPLOY_DIR:-$(pwd)}"
HEALTH_TIMEOUT="${HEALTH_TIMEOUT:-240}"   # Seconds, long enough for the startup migrations
KEEP_BACKUPS="${KEEP_BACKUPS:-14}"
KEEP_IMAGES="${KEEP_IMAGES:-3}"   # App images kept for rollback, the deployed and previous one included
APP_CONTAINER=inventory_app
PG_CONTAINER=inventory_postgres

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

previous=$(env_value APP_IMAGE)
[ -n "$previous" ] || previous=$(docker inspect -f '{{.Config.Image}}' "$APP_CONTAINER" 2>/dev/null || true)
echo "==> Deploying $IMAGE (running now: ${previous:-nothing})"

# Back up first, since migrations run at startup and an image rollback does not undo them
db_name=$(env_value DB_NAME)
mkdir -p backups && chmod 700 backups   # Full copies of the data
backup="backups/pre-deploy-$(date +%Y%m%d-%H%M%S).dump"
echo "==> Backup of database '${db_name:-inventory}' -> $backup"
docker exec "$PG_CONTAINER" sh -c 'pg_dump -U "$POSTGRES_USER" -Fc "$1"' _ "${db_name:-inventory}" > "$backup" \
    || { rm -f "$backup"; echo "Backup failed; nothing was deployed" >&2; exit 1; }
[ -s "$backup" ] || { rm -f "$backup"; echo "Backup is empty; nothing was deployed" >&2; exit 1; }
ls -1t backups/pre-deploy-*.dump | tail -n +"$((KEEP_BACKUPS + 1))" | xargs -r rm --

# Recreate only the app and put the previous image back if it never turns healthy
if run_app "$IMAGE" && wait_healthy; then
    # Pinned in .env so a plain docker compose up keeps running this image
    set_env_value APP_IMAGE "$IMAGE"
    echo "$(date -Is) $IMAGE" >> deploy-history.log

    # Other services pick up compose changes, and nginx needs a reload because its config is a bind mount
    docker compose up -d --no-build
    docker compose exec -T nginx nginx -t && docker compose exec -T nginx nginx -s reload
    docker image prune -f > /dev/null
    # Every deploy leaves a tagged image behind, and the registry still has the removed ones
    docker images "${IMAGE%:*}" --format '{{.CreatedAt}}\t{{.Repository}}:{{.Tag}}' | sort -r | cut -f2 \
        | grep -vxF -e "$IMAGE" -e "${previous:-none}" | tail -n +"$((KEEP_IMAGES - 1))" \
        | xargs -r docker image rm > /dev/null || true
    echo "==> Deployed $IMAGE"
else
    echo "!! $IMAGE did not become healthy within ${HEALTH_TIMEOUT}s" >&2
    docker compose logs --tail 100 app >&2 || true
    if [ -n "$previous" ]; then
        echo "==> Rolling back to $previous" >&2
        if run_app "$previous" && wait_healthy; then
            echo "==> Rolled back to $previous" >&2
        else
            echo "!! The previous image is not healthy either. If a migration is the cause, restore $backup (deploy/CD.md)." >&2
        fi
    fi
    exit 1
fi
