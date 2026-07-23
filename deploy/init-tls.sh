#!/usr/bin/env sh
set -eu
ENV_FILE="${ENV_FILE:-.env.production}"
if [ -z "${LETSENCRYPT_EMAIL:-}" ] && [ -f "$ENV_FILE" ]; then
  LETSENCRYPT_EMAIL="$(sed -n 's/^LETSENCRYPT_EMAIL=//p' "$ENV_FILE" | tail -n 1)"
fi
if [ -z "${PORTAL_DOMAIN:-}" ] && [ -f "$ENV_FILE" ]; then
  PORTAL_DOMAIN="$(sed -n 's/^PORTAL_DOMAIN=//p' "$ENV_FILE" | tail -n 1)"
fi
: "${LETSENCRYPT_EMAIL:?LETSENCRYPT_EMAIL is required in the environment or .env.production}"
: "${PORTAL_DOMAIN:?PORTAL_DOMAIN is required in the environment or .env.production}"
docker compose -f docker-compose.production.yml --env-file "$ENV_FILE" run --rm --service-ports certbot certonly --standalone -d "$PORTAL_DOMAIN" --email "$LETSENCRYPT_EMAIL" --agree-tos --no-eff-email
