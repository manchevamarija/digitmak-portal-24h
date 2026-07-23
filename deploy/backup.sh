#!/usr/bin/env sh
set -eu
ENV_FILE="${ENV_FILE:-.env.production}"
COMPOSE_FILE="${COMPOSE_FILE:-docker-compose.production.yml}"
env_value() { sed -n "s/^$1=//p" "$ENV_FILE" 2>/dev/null | tail -n 1; }
BACKUP_ROOT="${BACKUP_ROOT:-$(env_value BACKUP_ROOT)}"
BACKUP_ROOT="${BACKUP_ROOT:-/var/backups/digitmak}"
BACKUP_RETENTION_DAYS="${BACKUP_RETENTION_DAYS:-$(env_value BACKUP_RETENTION_DAYS)}"
BACKUP_RETENTION_DAYS="${BACKUP_RETENTION_DAYS:-30}"
STAMP="$(date -u +%Y%m%dT%H%M%SZ)"
mkdir -p "$BACKUP_ROOT/database" "$BACKUP_ROOT/uploads"

# Database and upload tools execute inside the Compose network/containers, so
# the VM host does not need pg_dump or access to the internal postgres hostname.
docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" exec -T postgres sh -c 'PGPASSWORD="$POSTGRES_PASSWORD" pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Fc' > "$BACKUP_ROOT/database/digitmak-$STAMP.dump"
docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" exec -T api sh -c 'root="${UPLOADS_ROOT:-/var/lib/digitmak-portal/uploads}"; mkdir -p "$root"; tar -C "$root" -czf - .' > "$BACKUP_ROOT/uploads/uploads-$STAMP.tar.gz"

find "$BACKUP_ROOT" -type f -mtime "+$BACKUP_RETENTION_DAYS" -delete
echo "Backup completed: $STAMP"
