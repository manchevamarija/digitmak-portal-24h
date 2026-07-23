[CmdletBinding()]
param([string]$EnvFile = '.env.production')
$ErrorActionPreference = 'Stop'
$required = @('POSTGRES_DB','POSTGRES_USER','POSTGRES_PASSWORD','JWT_SIGNING_KEY','BREVO_SMTP_HOST','BREVO_SMTP_PORT','BREVO_SMTP_USERNAME','BREVO_SMTP_PASSWORD','BREVO_FROM_EMAIL','ADMIN_BOOTSTRAP_EMAIL','ADMIN_BOOTSTRAP_PASSWORD','UPLOADS_ROOT','CLAMAV_HOST','CLAMAV_PORT','LETSENCRYPT_EMAIL','PORTAL_DOMAIN','APP_PUBLIC_URL')
if (-not (Test-Path -LiteralPath $EnvFile)) { throw "Missing $EnvFile. Run deploy/setup-production.ps1 first." }
$values = @{}
Get-Content -LiteralPath $EnvFile | Where-Object { $_ -match '^[A-Z0-9_]+=' } | ForEach-Object { $key,$value = $_ -split '=',2; $values[$key]=$value }
foreach ($key in $required) { if (-not $values[$key] -or $values[$key] -like 'CHANGE_ME*') { throw "Production value $key is missing or still a placeholder." } }
if ($values['JWT_SIGNING_KEY'].Length -lt 64) { throw 'JWT_SIGNING_KEY must contain at least 64 characters.' }
if ($values['ADMIN_BOOTSTRAP_PASSWORD'].Length -lt 16) { throw 'ADMIN_BOOTSTRAP_PASSWORD must contain at least 16 characters.' }
$ports = @('BREVO_SMTP_PORT','CLAMAV_PORT')
foreach ($key in $ports) { $parsed = 0; if (-not [int]::TryParse($values[$key], [ref]$parsed) -or $parsed -lt 1 -or $parsed -gt 65535) { throw "$key must be a valid TCP port." } }
$publicUri = [Uri]$values['APP_PUBLIC_URL']
if ($publicUri.Scheme -ne 'https' -or $publicUri.Host -ne $values['PORTAL_DOMAIN']) { throw 'APP_PUBLIC_URL must use HTTPS and the same host as PORTAL_DOMAIN.' }
foreach ($prefix in @('GOOGLE_CALENDAR','MICROSOFT_CALENDAR')) {
    $hasCredentials = [bool]$values["${prefix}_CLIENT_ID"] -or [bool]$values["${prefix}_CLIENT_SECRET"]
    if ($hasCredentials -and (-not $values["${prefix}_CLIENT_ID"] -or -not $values["${prefix}_CLIENT_SECRET"] -or -not $values["${prefix}_REDIRECT_URI"])) { throw "$prefix must have client id, client secret and redirect URI together." }
    if ($hasCredentials) { $redirect = [Uri]$values["${prefix}_REDIRECT_URI"]; if ($redirect.Scheme -ne 'https' -or $redirect.Host -ne $values['PORTAL_DOMAIN']) { throw "$prefix redirect URI must use the production HTTPS domain." } }
}
if ($values['MOODLE_SSO_MODE'] -and $values['MOODLE_SSO_MODE'] -notin @('ExternalLink','SignedLaunch','OIDC','SAML')) { throw 'MOODLE_SSO_MODE must be ExternalLink, SignedLaunch, OIDC or SAML.' }
if ($values['MOODLE_SSO_MODE'] -in @('OIDC','SAML') -and -not $values['MOODLE_SSO_ENTRY_URL']) { throw 'MOODLE_SSO_ENTRY_URL is required for Moodle OIDC/SAML.' }
$env:ENV_FILE = $EnvFile
docker compose -f docker-compose.production.yml --env-file $EnvFile config --quiet
Write-Host 'Production configuration is structurally valid.'
