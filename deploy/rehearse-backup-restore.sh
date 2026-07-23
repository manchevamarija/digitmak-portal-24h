#!/usr/bin/env sh
set -eu

ENV_FILE="${ENV_FILE:-.env.production}"
COMPOSE_FILE="${COMPOSE_FILE:-docker-compose.production.yml}"
REHEARSAL_ROOT="${REHEARSAL_ROOT:-./work/restore-rehearsal}"
export ENV_FILE COMPOSE_FILE BACKUP_ROOT="$REHEARSAL_ROOT/backups" BACKUP_RETENTION_DAYS=1

mkdir -p "$REHEARSAL_ROOT"
sh deploy/backup.sh
DATABASE_DUMP="$(find "$BACKUP_ROOT/database" -type f -name '*.dump' | sort | tail -n 1)"
UPLOAD_ARCHIVE="$(find "$BACKUP_ROOT/uploads" -type f -name '*.tar.gz' | sort | tail -n 1)"
[ -s "$DATABASE_DUMP" ] && [ -s "$UPLOAD_ARCHIVE" ] || { echo "Backup rehearsal did not produce non-empty artifacts." >&2; exit 1; }

sh deploy/restore.sh "$DATABASE_DUMP" "$UPLOAD_ARCHIVE"
docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" exec -T postgres sh -c 'PGPASSWORD="$POSTGRES_PASSWORD" psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -tAc "select count(*) from \"AspNetUsers\""' | grep -Eq '^[[:space:]]*[1-9][0-9]*[[:space:]]*$'
echo "Backup and restore rehearsal passed. Artifacts: $REHEARSAL_ROOT"
