#!/usr/bin/env sh
set -eu
if [ "$#" -ne 2 ]; then echo "Usage: restore.sh database.dump uploads.tar.gz"; exit 2; fi
ENV_FILE="${ENV_FILE:-.env.production}"
COMPOSE_FILE="${COMPOSE_FILE:-docker-compose.production.yml}"
DATABASE_DUMP="$1"
UPLOAD_ARCHIVE="$2"
[ -f "$DATABASE_DUMP" ] || { echo "Database dump not found: $DATABASE_DUMP"; exit 2; }
[ -f "$UPLOAD_ARCHIVE" ] || { echo "Upload archive not found: $UPLOAD_ARCHIVE"; exit 2; }

cat "$DATABASE_DUMP" | docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" exec -T postgres sh -c 'PGPASSWORD="$POSTGRES_PASSWORD" pg_restore --clean --if-exists -U "$POSTGRES_USER" -d "$POSTGRES_DB"'
cat "$UPLOAD_ARCHIVE" | docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" exec -T api sh -c 'mkdir -p "${UPLOADS_ROOT:-/var/lib/digitmak-portal/uploads}" && tar -C "${UPLOADS_ROOT:-/var/lib/digitmak-portal/uploads}" -xzf -'
echo "Restore completed. Run the documented smoke test before reopening production traffic."
