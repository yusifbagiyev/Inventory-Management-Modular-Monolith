#!/usr/bin/env bash
# Deploys one app image on the production server. The CD workflow runs it on the self-hosted
# runner; it also works by hand (e.g. a rollback without GitHub):
#
#   DEPLOY_DIR=/opt/inventory deploy/deploy.sh ghcr.io/<owner>/inventory-app:sha-<commit>
#
# 1. pg_dump backup - migrations run at startup and an image rollback does not undo them.
# 2. Pull the image and recreate only the app container.
# 3. Wait for the container healthcheck (/health). If it never passes, put the previous image
#    back and fail.
# 4. Record the image as APP_IMAGE in .env, so a plain `docker compose up -d` keeps running it.
set -euo pipefail

IMAGE="${1:?usage: deploy.sh <image>}"
DEPLOY_DIR="${DEPLOY_DIR:-$(pwd)}"
HEALTH_TIMEOUT="${HEALTH_TIMEOUT:-240}"   # seconds; migrations run before the app turns healthy
KEEP_BACKUPS="${KEEP_BACKUPS:-14}"
APP_CONTAINER=inventory_app
PG_CONTAINER=inventory_postgres

cd "$DEPLOY_DIR"
[ -f .env ] || { echo "No .env in $DEPLOY_DIR" >&2; exit 1; }

env_value() { grep -E "^$1=" .env | tail -n 1 | cut -d= -f2- || true; }

set_env_value() {
    if grep -qE "^$1=" .env; then
        sed -i "s|^$1=.*|$1=$2|" .env
    else
        printf '\n%s=%s\n' "$1" "$2" >> .env
    fi
}

run_app() {
    # A locally built image (inventory-app:local) is not in the registry; `up` then uses it as is.
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

# 1. Backup
db_name=$(env_value DB_NAME)
mkdir -p backups && chmod 700 backups   # full copies of the data
backup="backups/pre-deploy-$(date +%Y%m%d-%H%M%S).dump"
echo "==> Backup of database '${db_name:-inventory}' -> $backup"
docker exec "$PG_CONTAINER" sh -c 'pg_dump -U "$POSTGRES_USER" -Fc "$1"' _ "${db_name:-inventory}" > "$backup" \
    || { rm -f "$backup"; echo "Backup failed; nothing was deployed" >&2; exit 1; }
[ -s "$backup" ] || { rm -f "$backup"; echo "Backup is empty; nothing was deployed" >&2; exit 1; }
ls -1t backups/pre-deploy-*.dump | tail -n +"$((KEEP_BACKUPS + 1))" | xargs -r rm --

# 2-3. Deploy, then wait for /health
if run_app "$IMAGE" && wait_healthy; then
    # 4. Remember it
    set_env_value APP_IMAGE "$IMAGE"
    echo "$(date -Is) $IMAGE" >> deploy-history.log

    # The other services pick up compose-file changes (no-op when nothing changed);
    # nginx re-reads its config, which is a bind mount and does not trigger a recreate.
    docker compose up -d --no-build
    docker compose exec -T nginx nginx -t && docker compose exec -T nginx nginx -s reload
    docker image prune -f > /dev/null
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
