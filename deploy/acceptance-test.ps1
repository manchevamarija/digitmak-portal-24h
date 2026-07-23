[CmdletBinding()]
param(
    [string]$BaseUrl = 'http://localhost:5000',
    [string]$ClientEmail = 'client@digitmak.mk',
    [string]$ClientPassword = 'DigitMak!2026Client',
    [string]$BackupProjectName = 'digitmak_production_rehearsal',
    [string]$BackupEnvFile = '.env.production',
    [switch]$RunBackupRehearsal
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Net.Http
$BaseUrl = $BaseUrl.TrimEnd('/')
$results = [Collections.Generic.List[object]]::new()
function Pass([string]$Name, [string]$Detail) { $results.Add([pscustomobject]@{ Check=$Name; Status='PASS'; Detail=$Detail }) }
function Api([string]$Path, [string]$Method='GET', [object]$Body=$null, [string]$Token='') {
    $headers = @{}; if ($Token) { $headers.Authorization = "Bearer $Token" }
    $params = @{ Uri="$BaseUrl$Path"; Method=$Method; Headers=$headers; UseBasicParsing=$true }
    if ($null -ne $Body) { $params.ContentType='application/json'; $params.Body=($Body | ConvertTo-Json -Depth 8) }
    return Invoke-RestMethod @params
}

$live = Api '/health/live'; if ($live.status -ne 'Healthy') { throw 'Liveness check failed.' }; Pass 'API liveness' $live.status
$ready = Api '/health/ready'; if ($ready.status -eq 'Unhealthy') { throw 'Readiness check failed.' }; Pass 'API readiness' $ready.status
$session = Api '/api/auth/login' 'POST' @{ email=$ClientEmail; password=$ClientPassword }
$token = $session.accessToken; if (-not $token) { throw 'Login did not return an access token.' }; Pass 'Client login' $ClientEmail
$me = Api '/api/auth/me' 'GET' $null $token; Pass 'Authenticated profile' $me.email
$ticket = Api '/api/tickets' 'POST' @{ category='OTHER'; priority='Normal'; title="Acceptance test $([DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'))"; description='Automated production acceptance ticket. It may be closed after verification.' } $token
if (-not $ticket.id) { throw 'Ticket creation did not return an id.' }; Pass 'Ticket creation' $ticket.ticketNumber

$temporaryFile = Join-Path ([IO.Path]::GetTempPath()) "digitmak-acceptance-$([Guid]::NewGuid().ToString('N')).txt"
try {
    [IO.File]::WriteAllText($temporaryFile, 'DigitMak production acceptance attachment', [Text.UTF8Encoding]::new($false))
    $client = [Net.Http.HttpClient]::new(); $client.DefaultRequestHeaders.Authorization = [Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer', $token)
    $multipart = [Net.Http.MultipartFormDataContent]::new(); $bytes = [IO.File]::ReadAllBytes($temporaryFile); $content = [Net.Http.ByteArrayContent]::new($bytes); $content.Headers.ContentType = [Net.Http.Headers.MediaTypeHeaderValue]::new('text/plain'); $multipart.Add($content, 'file', 'acceptance.txt')
    $response = $client.PostAsync("$BaseUrl/api/tickets/$($ticket.id)/attachments", $multipart).GetAwaiter().GetResult()
    if (-not $response.IsSuccessStatusCode) { throw "Attachment upload failed: $([int]$response.StatusCode)" }
    Pass 'Attachment upload' 'text/plain signature and storage path accepted'
} finally { if (Test-Path -LiteralPath $temporaryFile) { Remove-Item -LiteralPath $temporaryFile -Force }; if ($client) { $client.Dispose() } }

$negotiate = Invoke-WebRequest -Uri "$BaseUrl/hubs/tickets/negotiate?negotiateVersion=1" -Method Post -Headers @{ Authorization="Bearer $token" } -UseBasicParsing
if ($negotiate.StatusCode -ne 200) { throw 'SignalR negotiate failed.' }; Pass 'SignalR' 'authenticated negotiation succeeded'
Api '/api/auth/forgot-password' 'POST' @{ email=$ClientEmail } | Out-Null; Pass 'Email queue' 'password-reset notification queued for configured delivery worker'

if ($RunBackupRehearsal) {
    docker compose -f docker-compose.production.yml --env-file $BackupEnvFile config --quiet
    if ($LASTEXITCODE -ne 0) { throw 'Docker Compose production validation failed.' }
    $shell = Get-Command sh -ErrorAction SilentlyContinue
    if ($shell) { & $shell.Source deploy/rehearse-backup-restore.sh }
    else { & powershell -NoProfile -ExecutionPolicy Bypass -File deploy/rehearse-backup-restore.ps1 -EnvFile $BackupEnvFile -ProjectName $BackupProjectName }
    if ($LASTEXITCODE -ne 0) { throw 'Backup/restore rehearsal failed.' }; Pass 'Backup and restore' 'rehearsal completed'
}
$results | Format-Table -AutoSize
Write-Host "Acceptance completed: $($results.Count) checks passed."
