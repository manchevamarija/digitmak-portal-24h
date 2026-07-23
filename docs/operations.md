# DigitMak Portal operations

Production secrets must be supplied through protected GitLab variables. Never commit SMTP, database, JWT or bootstrap credentials.

On Windows, generate the protected environment file with `powershell -ExecutionPolicy Bypass -File deploy/setup-production.ps1`. The wizard generates database, JWT and administrator secrets and prompts for owner-supplied domain, Brevo and operations values. Validate it with `powershell -ExecutionPolicy Bypass -File deploy/validate-production.ps1`.

Operational probes are split by purpose: `/health/live` confirms that the API process is responsive, while `/health/ready` checks database, writable upload storage, SMTP and ClamAV. `/health` returns all checks and timing details as JSON.

## Backup

The supplied `deploy/systemd/digitmak-backup.timer` runs `deploy/backup.sh` daily at 02:30 with a persistent, randomised timer. Database and archive commands execute inside the `postgres` and `api` containers, so the VM does not need local PostgreSQL client tools. The script creates a PostgreSQL custom-format dump and uploads archive, then deletes files older than `BACKUP_RETENTION_DAYS` (30 by default).

## Restore test

Use a non-production database and uploads directory. Run `deploy/restore.sh <dump> <archive>`, start the API, verify `/health`, login, one ticket attachment and KPI totals. Record the date and operator. Perform this test quarterly.

Windows can run the binary-safe equivalent with `powershell -ExecutionPolicy Bypass -File deploy/rehearse-backup-restore.ps1`. After deployment, run `powershell -ExecutionPolicy Bypass -File deploy/acceptance-test.ps1 -BaseUrl https://portal.example` with approved test-account parameters. Add `-RunBackupRehearsal` only in an isolated rehearsal environment.

## Deployment

The API applies pending EF Core migrations at startup. Production deployment is manual/protected. Configure `PORTAL_DOMAIN`, its DNS record, all Brevo variables, `UPLOADS_ROOT`, and unique admin/JWT secrets before the first start. Nginx and certificate paths are rendered from `PORTAL_DOMAIN`; the repository does not hard-code a single deployment domain.

1. Point the selected domain's DNS A/AAAA record to the VM and set the same value as `PORTAL_DOMAIN`.
2. Copy `.env.production.example` to `.env.production` and fill every placeholder.
3. On the Linux VM run `sh deploy/validate-production.sh`. The equivalent Windows pre-check is `powershell -File deploy/validate-production.ps1`.
4. Before starting Nginx, run `deploy/init-tls.sh` to obtain the first Let's Encrypt certificate on port 80. The script reads `PORTAL_DOMAIN` and `LETSENCRYPT_EMAIL` from `.env.production` when they are not already exported.
5. Start the production stack with `docker compose -f docker-compose.production.yml --env-file .env.production up -d --build`.
6. Verify HTTPS, `/health`, admin login, Brevo delivery, upload/download, SignalR chat and one backup/restore rehearsal.

## Systemd automation

The supplied units assume the standard deployment location `/opt/digitmak-portal`, with the current release at `/opt/digitmak-portal/current` and the protected environment file at `/opt/digitmak-portal/shared/.env.production`. If `DEPLOY_PATH` differs, update those paths before installation.

```sh
sudo install -m 0644 deploy/systemd/*.service deploy/systemd/*.timer /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable --now digitmak-portal.service
sudo systemctl enable --now digitmak-backup.timer digitmak-certbot-renew.timer
systemctl list-timers 'digitmak-*'
```

`digitmak-certbot-renew.timer` checks certificates twice daily and reloads Nginx after a successful renewal command. Inspect operations with `journalctl -u digitmak-backup.service` and `journalctl -u digitmak-certbot-renew.service`.
