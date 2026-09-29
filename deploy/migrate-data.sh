#!/usr/bin/env bash
#
# Copies the data of the five per-service databases (identity_service, product_service,
# route_service, approval_service, notification_service) into the modular monolith's single
# database, where each module has its own schema.
#
# The source databases are only read, never modified - rolling back means starting the old stack
# again (see deploy/MIGRATION.md).
#
# Prerequisites
#   * The target database exists and the new app has been started against it ONCE, so every
#     module's migrations (schemas + tables) are applied. Stop the app before running this.
#   * Old and new databases live in the same PostgreSQL server (the compose "postgres" service).
#   * The database user is a superuser (pg_dump --disable-triggers needs it) - the compose
#     POSTGRES_USER is.
#
# Usage
#   PG_CONTAINER=inventory_postgres DB_USER=<user> TARGET_DB=inventory ./deploy/migrate-data.sh
#   Add FORCE=1 to overwrite a target that already contains business data.
#
set -euo pipefail

PG_CONTAINER="${PG_CONTAINER:-inventory_postgres}"
DB_USER="${DB_USER:?DB_USER is required}"
TARGET_DB="${TARGET_DB:-inventory}"
FORCE="${FORCE:-0}"

# source database | target schema | tables (parents before children)
MODULES=(
  "identity_service|identity|AspNetRoles AspNetUsers AspNetRoleClaims AspNetUserClaims AspNetUserLogins AspNetUserRoles AspNetUserTokens Permissions RolePermissions UserPermissions RefreshTokens"
  "product_service|product|Categories Departments Products"
  "route_service|route|InventoryRoutes"
  "approval_service|approval|ApprovalRequests"
  "notification_service|notification|Notifications"
)

psql_in()  { docker exec -i "$PG_CONTAINER" psql -U "$DB_USER" -v ON_ERROR_STOP=1 -q "$@"; }
psql_val() { docker exec "$PG_CONTAINER" psql -U "$DB_USER" -At -d "$1" -c "$2"; }
count()    { psql_val "$1" "SELECT count(*) FROM $2"; }

echo "==> Checking source and target databases"
for module in "${MODULES[@]}"; do
  IFS='|' read -r source schema tables <<< "$module"
  [[ "$(psql_val postgres "SELECT count(*) FROM pg_database WHERE datname = '$source'")" == 1 ]] \
    || { echo "Source database '$source' not found"; exit 1; }
  for table in $tables; do
    [[ "$(psql_val "$TARGET_DB" "SELECT to_regclass('$schema.\"$table\"') IS NOT NULL")" == t ]] \
      || { echo "Target table $schema.\"$table\" is missing - start the new app once against '$TARGET_DB' first"; exit 1; }
  done
done

# Seed rows (roles, permissions, the default admin) are expected; anything else means the target
# is already in use.
existing=$(( $(count "$TARGET_DB" 'product."Products"') + $(count "$TARGET_DB" 'route."InventoryRoutes"') \
           + $(count "$TARGET_DB" 'approval."ApprovalRequests"') + $(count "$TARGET_DB" 'notification."Notifications"') ))
if [[ "$existing" -gt 0 && "$FORCE" != 1 ]]; then
  echo "Target '$TARGET_DB' already holds $existing business rows. Re-run with FORCE=1 to replace them."
  exit 1
fi

echo "==> Copying data (single transaction)"
{
  echo "BEGIN;"
  for module in "${MODULES[@]}"; do
    IFS='|' read -r _ schema tables <<< "$module"
    list=""
    for table in $tables; do list+="${list:+, }$schema.\"$table\""; done
    echo "TRUNCATE $list RESTART IDENTITY CASCADE;"
  done

  for module in "${MODULES[@]}"; do
    IFS='|' read -r source schema tables <<< "$module"
    args=()
    for table in $tables; do args+=(-t "public.\"$table\""); done
    # Only statement lines are rewritten (COPY/ALTER TABLE/setval); COPY data rows are untouched.
    docker exec "$PG_CONTAINER" pg_dump -U "$DB_USER" -d "$source" \
        --data-only --disable-triggers --no-owner --no-privileges "${args[@]}" \
      | grep -v -E '^SELECT pg_catalog\.set_config\(.search_path' \
      | sed -E \
          -e "s/^COPY public\./COPY $schema./" \
          -e "s/^ALTER TABLE (ONLY )?public\./ALTER TABLE \1$schema./" \
          -e "s/^SELECT pg_catalog\.setval\('public\./SELECT pg_catalog.setval('$schema./"
  done
  echo "COMMIT;"
} | psql_in -d "$TARGET_DB" > /dev/null

echo "==> Verifying row counts"
failed=0
for module in "${MODULES[@]}"; do
  IFS='|' read -r source schema tables <<< "$module"
  for table in $tables; do
    old=$(count "$source" "public.\"$table\"")
    new=$(count "$TARGET_DB" "$schema.\"$table\"")
    status=OK; [[ "$old" == "$new" ]] || { status=MISMATCH; failed=1; }
    printf '  %-40s %8s -> %-8s %s\n' "$schema.$table" "$old" "$new" "$status"
  done
done
[[ "$failed" == 0 ]] || { echo "Row counts differ - do NOT start the new app; investigate first."; exit 1; }

echo "==> Aligning identity sequences with the copied ids"
psql_in -d "$TARGET_DB" <<'SQL'
DO $$
DECLARE r record; seq text; max_id bigint;
BEGIN
    FOR r IN SELECT table_schema, table_name, column_name
             FROM information_schema.columns
             WHERE is_identity = 'YES'
               AND table_schema IN ('identity', 'product', 'route', 'approval', 'notification')
    LOOP
        seq := pg_get_serial_sequence(format('%I.%I', r.table_schema, r.table_name), r.column_name);
        EXECUTE format('SELECT COALESCE(MAX(%I), 0) FROM %I.%I', r.column_name, r.table_schema, r.table_name) INTO max_id;
        PERFORM setval(seq, max_id + 1, false);
    END LOOP;
END $$;
SQL

echo "==> Done. Start the new app."
