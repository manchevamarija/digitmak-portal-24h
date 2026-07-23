[CmdletBinding()]
param(
    [string]$EnvFile = 'work/.env.rehearsal',
    [string]$ComposeFile = 'docker-compose.production.yml',
    [string]$ProjectName = 'digitmak_v16_rehearsal',
    [string]$RehearsalRoot = 'work/restore-rehearsal'
)
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $EnvFile)) { throw "Environment file not found: $EnvFile" }
$values = @{}; Get-Content -LiteralPath $EnvFile | Where-Object { $_ -match '^[A-Z0-9_]+=' } | ForEach-Object { $key,$value=$_ -split '=',2; $values[$key]=$value }
foreach ($key in @('POSTGRES_DB','POSTGRES_USER','POSTGRES_PASSWORD')) { if (-not $values[$key]) { throw "$key is required." }; if ($values[$key] -match '[\s"]') { throw "$key contains unsupported whitespace or quotes." } }
if ($ProjectName -notmatch '^digitmak_[a-z0-9_-]+_rehearsal$') { throw 'ProjectName must identify an isolated DigitMak rehearsal project.' }

function Invoke-DockerBinary([string]$Arguments, [string]$OutputFile = '', [string]$InputFile = '') {
    $info = [Diagnostics.ProcessStartInfo]::new(); $info.FileName='docker'; $info.Arguments=$Arguments; $info.UseShellExecute=$false; $info.RedirectStandardOutput=[bool]$OutputFile; $info.RedirectStandardInput=[bool]$InputFile
    $process = [Diagnostics.Process]::new(); $process.StartInfo=$info; if (-not $process.Start()) { throw 'Docker process could not be started.' }
    try {
        if ($OutputFile) { $stream=[IO.File]::Create($OutputFile); try { $process.StandardOutput.BaseStream.CopyTo($stream) } finally { $stream.Dispose() } }
        if ($InputFile) { $stream=[IO.File]::OpenRead($InputFile); try { $stream.CopyTo($process.StandardInput.BaseStream); $process.StandardInput.Close() } finally { $stream.Dispose() } }
        $process.WaitForExit(); if ($process.ExitCode -ne 0) { throw "Docker command failed with exit code $($process.ExitCode)." }
    } finally { $process.Dispose() }
}

$databaseDir = Join-Path $RehearsalRoot 'database'; $uploadsDir = Join-Path $RehearsalRoot 'uploads'; New-Item -ItemType Directory -Path $databaseDir,$uploadsDir -Force | Out-Null
$stamp = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ'); $dump = Join-Path $databaseDir "digitmak-$stamp.dump"; $archive = Join-Path $uploadsDir "uploads-$stamp.tar.gz"
$compose = "compose -p $ProjectName -f $ComposeFile --env-file $EnvFile exec -T"
Invoke-DockerBinary "$compose -e PGPASSWORD=$($values.POSTGRES_PASSWORD) postgres pg_dump -U $($values.POSTGRES_USER) -d $($values.POSTGRES_DB) -Fc" $dump
Invoke-DockerBinary "$compose api tar -C /var/lib/digitmak-portal/uploads -czf - ." $archive
if ((Get-Item -LiteralPath $dump).Length -eq 0 -or (Get-Item -LiteralPath $archive).Length -eq 0) { throw 'Backup artifacts are empty.' }
Invoke-DockerBinary "$compose -e PGPASSWORD=$($values.POSTGRES_PASSWORD) postgres pg_restore --clean --if-exists -U $($values.POSTGRES_USER) -d $($values.POSTGRES_DB)" '' $dump
Invoke-DockerBinary "$compose api tar -C /var/lib/digitmak-portal/uploads -xzf -" '' $archive
$count = docker compose -p $ProjectName -f $ComposeFile --env-file $EnvFile exec -T -e PGPASSWORD=$($values.POSTGRES_PASSWORD) postgres psql -U $($values.POSTGRES_USER) -d $($values.POSTGRES_DB) -tAc 'select count(*) from information_schema.tables where table_schema=current_schema()'
if ($LASTEXITCODE -ne 0 -or [int]$count -lt 10) { throw 'Restored database schema verification failed.' }
Write-Host "Backup/restore rehearsal passed. Application tables restored: $count"
Write-Host "Artifacts: $RehearsalRoot"
