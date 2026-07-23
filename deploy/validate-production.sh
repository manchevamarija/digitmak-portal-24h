#!/usr/bin/env sh
set -eu

ENV_FILE="${ENV_FILE:-.env.production}"
[ -f "$ENV_FILE" ] || { echo "Missing $ENV_FILE. Copy .env.production.example and fill the values." >&2; exit 1; }

env_value() { sed -n "s/^$1=//p" "$ENV_FILE" | tail -n 1; }
required='POSTGRES_DB POSTGRES_USER POSTGRES_PASSWORD JWT_SIGNING_KEY BREVO_SMTP_HOST BREVO_SMTP_PORT BREVO_SMTP_USERNAME BREVO_SMTP_PASSWORD BREVO_FROM_EMAIL ADMIN_BOOTSTRAP_EMAIL ADMIN_BOOTSTRAP_PASSWORD UPLOADS_ROOT CLAMAV_HOST CLAMAV_PORT LETSENCRYPT_EMAIL PORTAL_DOMAIN APP_PUBLIC_URL'
for key in $required; do
  value="$(env_value "$key")"
  [ -n "$value" ] || { echo "Production value $key is missing." >&2; exit 1; }
  case "$value" in CHANGE_ME*) echo "Production value $key is still a placeholder." >&2; exit 1;; esac
done

JWT_SIGNING_KEY="$(env_value JWT_SIGNING_KEY)"
ADMIN_BOOTSTRAP_PASSWORD="$(env_value ADMIN_BOOTSTRAP_PASSWORD)"
PORTAL_DOMAIN="$(env_value PORTAL_DOMAIN)"
APP_PUBLIC_URL="$(env_value APP_PUBLIC_URL)"
[ "${#JWT_SIGNING_KEY}" -ge 64 ] || { echo 'JWT_SIGNING_KEY must contain at least 64 characters.' >&2; exit 1; }
[ "${#ADMIN_BOOTSTRAP_PASSWORD}" -ge 16 ] || { echo 'ADMIN_BOOTSTRAP_PASSWORD must contain at least 16 characters.' >&2; exit 1; }
[ "$APP_PUBLIC_URL" = "https://$PORTAL_DOMAIN" ] || { echo 'APP_PUBLIC_URL must equal https://PORTAL_DOMAIN.' >&2; exit 1; }

export ENV_FILE
docker compose --env-file "$ENV_FILE" -f docker-compose.production.yml config --quiet
echo 'Production configuration is structurally valid.'
