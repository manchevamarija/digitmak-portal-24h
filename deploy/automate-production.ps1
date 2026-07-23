[CmdletBinding()]
param(
    [ValidateSet('Verify', 'Prepare', 'Deploy')]
    [string]$Mode = 'Verify',
    [string]$EnvFile = '.env.production',
    [string]$BaseUrl = '',
    [string]$ReportPath = 'outputs/production-automation-report.json',
    [switch]$SkipSourceTests,
    [switch]$SkipBackupRehearsal,
    [switch]$ForceEnvironment
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $root
$startedAt = [DateTime]::UtcNow
$steps = [Collections.Generic.List[object]]::new()

function Add-Result([string]$Name, [string]$Status, [string]$Detail) {
    $steps.Add([pscustomobject]@{
        name = $Name
        status = $Status
        detail = $Detail
        timestampUtc = [DateTime]::UtcNow.ToString('o')
    })
    $color = if ($Status -eq 'PASS') { 'Green' } elseif ($Status -eq 'SKIP') { 'Yellow' } else { 'Red' }
    Write-Host "[$Status] $Name - $Detail" -ForegroundColor $color
}

function Invoke-Step([string]$Name, [scriptblock]$Action) {
    try {
        & $Action
        if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) { throw "Command exited with code $LASTEXITCODE." }
        Add-Result $Name 'PASS' 'Completed successfully.'
    }
    catch {
        Add-Result $Name 'FAIL' $_.Exception.Message
        throw
    }
}

function Require-Command([string]$Name) {
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) { throw "Required command is not installed or not on PATH: $Name" }
}

function Read-Environment([string]$Path) {
    $values = @{}
    if (Test-Path -LiteralPath $Path) {
        Get-Content -LiteralPath $Path | Where-Object { $_ -match '^[A-Z0-9_]+=' } | ForEach-Object {
            $key, $value = $_ -split '=', 2
            $values[$key] = $value
        }
    }
    return $values
}

try {
    Invoke-Step 'Prerequisites' {
        Require-Command 'dotnet'
        Require-Command 'npm.cmd'
        Require-Command 'npx.cmd'
        if ($Mode -in @('Prepare', 'Deploy')) { Require-Command 'docker' }
    }

    if (-not $SkipSourceTests) {
        Invoke-Step 'Backend Release tests' {
            dotnet test backend/tests/DigitMak.Portal.Api.Tests/DigitMak.Portal.Api.Tests.csproj -c Release --nologo
        }
        Invoke-Step 'Frontend install' {
            Push-Location frontend
            try { npx.cmd -y pnpm@11 install --frozen-lockfile } finally { Pop-Location }
        }
        Invoke-Step 'Frontend tests, lint and build' {
            Push-Location frontend
            try {
                npm.cmd test -- --run
                if ($LASTEXITCODE -ne 0) { throw 'Frontend tests failed.' }
                npm.cmd run lint
                if ($LASTEXITCODE -ne 0) { throw 'Frontend lint failed.' }
                npm.cmd run build
                if ($LASTEXITCODE -ne 0) { throw 'Frontend build failed.' }
            }
            finally { Pop-Location }
        }
    }
    else { Add-Result 'Source verification' 'SKIP' 'Skipped by parameter.' }

    if ($Mode -in @('Prepare', 'Deploy')) {
        if (-not (Test-Path -LiteralPath $EnvFile)) {
            Invoke-Step 'Production environment generation' {
                & "$PSScriptRoot/setup-production.ps1" -OutputPath $EnvFile -NonInteractive
            }
        }
        elseif ($ForceEnvironment) {
            Invoke-Step 'Production environment regeneration' {
                & "$PSScriptRoot/setup-production.ps1" -OutputPath $EnvFile -NonInteractive -Force
            }
        }
        else { Add-Result 'Production environment generation' 'SKIP' "$EnvFile already exists and was preserved." }

        Invoke-Step 'Production configuration validation' {
            & "$PSScriptRoot/validate-production.ps1" -EnvFile $EnvFile
        }
    }

    if ($Mode -eq 'Deploy') {
        $environment = Read-Environment $EnvFile
        if (-not $BaseUrl) { $BaseUrl = $environment.APP_PUBLIC_URL }
        if (-not $BaseUrl) { throw 'BaseUrl is required when APP_PUBLIC_URL is unavailable.' }

        Invoke-Step 'Production containers' {
            docker compose -f docker-compose.production.yml --env-file $EnvFile up -d --build
        }

        Invoke-Step 'Production health wait' {
            $deadline = [DateTime]::UtcNow.AddMinutes(5)
            do {
                try {
                    $health = Invoke-RestMethod -Uri "$($BaseUrl.TrimEnd('/'))/health/ready" -TimeoutSec 10
                    if ($health.status -ne 'Unhealthy') { break }
                }
                catch { Start-Sleep -Seconds 5 }
            } while ([DateTime]::UtcNow -lt $deadline)
            if (-not $health -or $health.status -eq 'Unhealthy') { throw 'Production did not become ready within five minutes.' }
        }

        Invoke-Step 'Automated acceptance' {
            & "$PSScriptRoot/acceptance-test.ps1" -BaseUrl $BaseUrl
        }

        if (-not $SkipBackupRehearsal) {
            Invoke-Step 'Backup and restore rehearsal' {
                & "$PSScriptRoot/rehearse-backup-restore.ps1" -EnvFile $EnvFile -ProjectName 'digitmak_production_rehearsal'
            }
        }
        else { Add-Result 'Backup and restore rehearsal' 'SKIP' 'Skipped by parameter.' }
    }
}
catch {
    $failure = $_
}
finally {
    $directory = Split-Path -Parent $ReportPath
    if ($directory) { New-Item -ItemType Directory -Path $directory -Force | Out-Null }
    $report = [pscustomobject]@{
        project = 'DigitMak Portal'
        mode = $Mode
        startedAtUtc = $startedAt.ToString('o')
        finishedAtUtc = [DateTime]::UtcNow.ToString('o')
        succeeded = -not [bool]$failure
        steps = $steps
        externalApprovalsStillRequired = @('DNS/VM ownership', 'Brevo credentials', 'GitLab credentials', 'legal approval', 'official branding approval')
    }
    [IO.File]::WriteAllText((Join-Path $root $ReportPath), ($report | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
    Write-Host "Automation report: $ReportPath"
}

if ($failure) { throw $failure }
Write-Host "Production automation completed in $Mode mode." -ForegroundColor Green
