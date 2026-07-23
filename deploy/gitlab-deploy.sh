#!/usr/bin/env sh
set -eu

: "${DEPLOY_HOST:?DEPLOY_HOST is required}"
: "${DEPLOY_USER:?DEPLOY_USER is required}"
: "${DEPLOY_PATH:?DEPLOY_PATH is required}"
: "${DEPLOY_ENV_FILE:?DEPLOY_ENV_FILE must contain the complete production environment file}"

RELEASE_ID="${CI_COMMIT_SHA:-manual-$(date -u +%Y%m%dT%H%M%SZ)}"
ARCHIVE="digitmak-${RELEASE_ID}.tar.gz"
REMOTE="${DEPLOY_USER}@${DEPLOY_HOST}"
REMOTE_RELEASE="${DEPLOY_PATH}/releases/${RELEASE_ID}"

tar --exclude=.git --exclude=frontend/node_modules --exclude=frontend/dist --exclude='**/bin' --exclude='**/obj' --exclude=work --exclude=outputs -czf "$ARCHIVE" .
scp "$ARCHIVE" "$REMOTE:/tmp/$ARCHIVE"

printf '%s\n' "$DEPLOY_ENV_FILE" | ssh "$REMOTE" "umask 077; mkdir -p '$DEPLOY_PATH/shared' '$REMOTE_RELEASE'; cat > '$DEPLOY_PATH/shared/.env.production'"
ssh "$REMOTE" "set -eu; tar -xzf '/tmp/$ARCHIVE' -C '$REMOTE_RELEASE'; rm -f '/tmp/$ARCHIVE'; ln -sfn '$REMOTE_RELEASE' '$DEPLOY_PATH/current'; cd '$DEPLOY_PATH/current'; docker compose -p digitmak-portal --env-file '$DEPLOY_PATH/shared/.env.production' -f docker-compose.production.yml up -d --build --remove-orphans; docker compose -p digitmak-portal --env-file '$DEPLOY_PATH/shared/.env.production' -f docker-compose.production.yml ps"

if [ -n "${DEPLOY_HEALTH_URL:-}" ]; then
  for attempt in $(seq 1 30); do
    if curl --fail --silent --show-error "${DEPLOY_HEALTH_URL%/}/health" >/dev/null; then echo "Deployment health check passed."; exit 0; fi
    sleep 5
  done
  echo "Deployment completed but health check failed." >&2
  exit 1
fi

echo "Deployment completed: $RELEASE_ID"
