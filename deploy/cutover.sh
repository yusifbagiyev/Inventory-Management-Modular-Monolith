#!/usr/bin/env bash
# One-time switch of the production server from the old 11-container stack (5 services, gateway,
# web, RabbitMQ, ...) to the modular monolith. Run as root in the stack folder, after the
# "Prepare cut-over" workflow has staged next/ (new compose file, deploy/, the pulled image):
#
#   cd /opt/inventory166
#   bash next/deploy/cutover.sh check      # preflight only, changes nothing (run it the day before)
#   bash next/deploy/cutover.sh            # the cut-over (~5-10 min downtime)
#   bash deploy/cutover.sh rollback        # back to the old stack (after a cut-over)
#
# The old databases are only read. Rolling back starts the old containers on them again; anything
# entered in the new system after the cut-over is not in them. See deploy/MIGRATION.md.
set -Eeuo pipefail

MODE="${1:-run}"
DIR="$(pwd)"
NEXT="$DIR/next"
TS="$(date +%Y%m%d-%H%M%S)"
TARGET_DB=inventory
BACKUP_DIR="${BACKUP_DIR:-/root}"   # full dumps of every database: keep it root-only
PG_CONTAINER=inventory_postgres
APP_CONTAINER=inventory_app
OLD_APP_SERVICES="nginx web api-gateway identity-service product-service route-service approval-service notification-service rabbitmq"
OLD_DATABASES="identity_service product_service route_service approval_service notification_service"
REQUIRED_KEYS="DB_USER DB_PASSWORD JWT_SECRET_KEY JWT_ISSUER JWT_AUDIENCE SEQ_ADMIN_PASSWORD COMPOSE_PROJECT_NAME"
STAGE=preflight

say()  { printf '\n==> %s\n' "$*"; }
fail() { printf '\n!! %s\n' "$*" >&2; hint; exit 1; }

env_value() { grep -E "^$1=" "$DIR/.env" | tail -n 1 | cut -d= -f2- || true; }
set_env_value() {
    if grep -qE "^$1=" "$DIR/.env"; then
        sed -i "s|^$1=.*|$1=$2|" "$DIR/.env"
    else
        printf '\n%s=%s\n' "$1" "$2" >> "$DIR/.env"
    fi
}

psql_val() { docker exec "$PG_CONTAINER" psql -U "$DB_USER" -d "$1" -Atc "$2"; }

wait_healthy() {  # container, seconds
    local deadline=$((SECONDS + $2)) status
    while [ "$SECONDS" -lt "$deadline" ]; do
        status=$(docker inspect -f '{{if .State.Health}}{{.State.Health.Status}}{{else}}none{{end}}' "$1" 2>/dev/null || echo missing)
        [ "$status" = healthy ] && return 0
        [ "$status" = unhealthy ] && return 1
        sleep 5
    done
    return 1
}

# What to do after a failure, depending on how far the cut-over got.
hint() {
    case "$STAGE" in
        stopped)
            printf '   No file was changed yet. Start the old stack again: docker compose up -d --no-build\n' >&2 ;;
        switched)
            printf '   Nothing in the old databases was changed. Back to the old stack: bash deploy/cutover.sh rollback\n' >&2 ;;
    esac
}
trap 'printf "\n!! A command failed (line %s).\n" "$LINENO" >&2; hint' ERR

rollback() {
    [ -f "$DIR/docker-compose.old.yml" ] || fail "docker-compose.old.yml not found - there is nothing to roll back to."
    say "Stopping the new containers"
    docker compose stop app nginx || true
    docker rm -f "$APP_CONTAINER" >/dev/null 2>&1 || true
    say "Restoring the old compose file"
    mv "$DIR/docker-compose.yml" "$DIR/docker-compose.monolith.yml"
    mv "$DIR/docker-compose.old.yml" "$DIR/docker-compose.yml"
    say "Starting the old stack"
    docker compose up -d --no-build
    docker compose ps
    printf '\nRolled back. The "%s" database is kept; drop it once it is no longer needed.\n' "$TARGET_DB"
    exit 0
}

[ "$(id -u)" = 0 ] || fail "Run as root."
[ -f "$DIR/.env" ] || fail "Run from the stack folder (no .env in $DIR)."
[ "$MODE" = rollback ] && rollback
[ "$MODE" = run ] || [ "$MODE" = check ] || fail "Unknown mode '$MODE' (use: check, rollback, or nothing)."

# ---------------------------------------------------------------------------------------------
say "Preflight"
[ ! -f "$DIR/docker-compose.old.yml" ] || fail "docker-compose.old.yml exists - the cut-over already ran here."
for f in docker-compose.yml deploy/migrate-data.sh deploy/deploy.sh deploy/nginx/nginx.conf APP_IMAGE; do
    [ -f "$NEXT/$f" ] || fail "next/$f is missing - run the 'Prepare cut-over' workflow first."
done
IMAGE="$(tr -d '[:space:]' < "$NEXT/APP_IMAGE")"
docker image inspect "$IMAGE" > /dev/null 2>&1 || fail "Image $IMAGE is not on this server - re-run 'Prepare cut-over'."
echo "   image: $IMAGE"

for key in $REQUIRED_KEYS; do
    [ -n "$(env_value "$key")" ] || fail ".env has no value for $key."
done
[ "$(env_value COMPOSE_PROJECT_NAME)" = "$(docker inspect -f '{{index .Config.Labels "com.docker.compose.project"}}' "$PG_CONTAINER")" ] \
    || fail "COMPOSE_PROJECT_NAME in .env does not match the running postgres container's project."
[ "$(env_value JWT_SECRET_KEY | tr -d '\n' | wc -c)" -ge 32 ] || fail "JWT_SECRET_KEY is shorter than 32 characters."
[ -n "$(env_value SERVICEDESK_API_KEY)" ] || echo "   note: SERVICEDESK_API_KEY is empty - the ServiceDesk X-Api-Key integration will be off."
[ -n "$(env_value WHATSAPP_API_TOKEN)" ] || echo "   note: WHATSAPP_API_TOKEN is empty - WhatsApp messages will be off."

DB_USER="$(env_value DB_USER)"
[ "$(docker inspect -f '{{.State.Running}}' "$PG_CONTAINER" 2>/dev/null)" = true ] || fail "$PG_CONTAINER is not running."
for db in $OLD_DATABASES; do
    [ "$(psql_val postgres "SELECT count(*) FROM pg_database WHERE datname = '$db'")" = 1 ] || fail "Old database $db not found."
done
if [ "$(psql_val postgres "SELECT count(*) FROM pg_database WHERE datname = '$TARGET_DB'")" = 1 ]; then
    fail "Database '$TARGET_DB' already exists. If it is left over from an earlier attempt, drop it first:
   docker exec $PG_CONTAINER dropdb -U \"\$DB_USER\" $TARGET_DB"
fi
psql_val postgres "SELECT '   ' || datname || ': ' || pg_size_pretty(pg_database_size(datname)) FROM pg_database WHERE datname LIKE '%\_service' ORDER BY 1"
free_kb=$(df -Pk "$DIR" | awk 'NR==2 {print $4}')
[ "$free_kb" -gt 2000000 ] || fail "Less than 2 GB free in $DIR."
[ -d "$BACKUP_DIR" ] && [ -w "$BACKUP_DIR" ] || fail "Backup folder $BACKUP_DIR is missing or not writable."

if [ "$MODE" = check ]; then
    printf '\nPreflight OK - nothing was changed. Run without "check" to cut over.\n'
    exit 0
fi

# ---------------------------------------------------------------------------------------------
say "1/7 Stopping the old application containers (postgres and seq keep running)"
STAGE=stopped
# shellcheck disable=SC2086
docker compose stop $OLD_APP_SERVICES

say "2/7 Full backup -> $BACKUP_DIR/pre-cutover-$TS.sql.gz"
docker exec "$PG_CONTAINER" pg_dumpall -U "$DB_USER" | gzip > "$BACKUP_DIR/pre-cutover-$TS.sql.gz"
# A complete dump holds every old database; size alone says little.
gzip -t "$BACKUP_DIR/pre-cutover-$TS.sql.gz" || fail "The backup is not a valid gzip file."
dumped=$(zcat "$BACKUP_DIR/pre-cutover-$TS.sql.gz" | grep -oE '^CREATE DATABASE [a-z_]+' | awk '{print $3}' | tr '\n' ' ')
for db in $OLD_DATABASES; do
    case " $dumped " in *" $db "*) ;; *) fail "The backup does not contain $db." ;; esac
done
echo "   $(du -h "$BACKUP_DIR/pre-cutover-$TS.sql.gz" | cut -f1)"

say "3/7 Creating database '$TARGET_DB'"
docker exec "$PG_CONTAINER" createdb -U "$DB_USER" "$TARGET_DB"

say "4/7 Switching to the monolith compose file"
STAGE=switched
mv "$DIR/docker-compose.yml" "$DIR/docker-compose.old.yml"
cp "$NEXT/docker-compose.yml" "$DIR/docker-compose.yml"
[ -d "$DIR/deploy" ] && mv "$DIR/deploy" "$DIR/deploy.old-$TS"
cp -R "$NEXT/deploy" "$DIR/deploy"
set_env_value APP_IMAGE "$IMAGE"
[ -n "$(env_value DB_NAME)" ] || set_env_value DB_NAME "$TARGET_DB"
mkdir -p "$DIR/storage/keys" "$DIR/storage/images/products" "$DIR/storage/images/routes"
chown -R 1654:1654 "$DIR/storage/keys" "$DIR/storage/images"

say "5/7 First start of the new app against the empty database (creates the schemas)"
docker compose up -d --no-build postgres seq
wait_healthy "$PG_CONTAINER" 120 || fail "PostgreSQL did not become healthy."
docker compose up -d --no-build --no-deps app
wait_healthy "$APP_CONTAINER" 300 || { docker compose logs --tail 80 app; fail "The new app did not become healthy."; }
docker compose stop app

say "6/7 Copying the data from the old databases"
PG_CONTAINER="$PG_CONTAINER" DB_USER="$DB_USER" TARGET_DB="$TARGET_DB" bash "$DIR/deploy/migrate-data.sh"

say "7/7 Starting the new stack"
docker compose up -d --no-build
wait_healthy "$APP_CONTAINER" 300 || { docker compose logs --tail 80 app; fail "The new app did not become healthy."; }
docker compose exec -T nginx nginx -t
for _ in $(seq 1 12); do
    code=$(curl -sk -o /dev/null -w '%{http_code}' --resolve inventory166.az:443:127.0.0.1 https://inventory166.az/Account/Login || true)
    [ "$code" = 200 ] && break
    sleep 5
done
echo "   https://inventory166.az/Account/Login -> $code"
[ "$code" = 200 ] || fail "The login page did not answer 200."
echo "$(date -Is) $IMAGE (cut-over)" >> "$DIR/deploy-history.log"
# CD (deploy/deploy.sh) runs as the runner user: it rewrites these and writes its dumps to backups/.
chown -R github-runner: "$DIR/.env" "$DIR/docker-compose.yml" "$DIR/deploy" "$DIR/deploy-history.log" 2>/dev/null || true
mkdir -p "$DIR/backups" && chown github-runner: "$DIR/backups" 2>/dev/null || true

trap - ERR
cat <<EOF

Cut-over done. Backup: $BACKUP_DIR/pre-cutover-$TS.sql.gz
Check now (deploy/MIGRATION.md, step 5): sign in, counts, images, notifications, approvals.
If something is wrong:   bash deploy/cutover.sh rollback
The old containers are stopped, not removed; they stay until the clean-up step.
EOF
