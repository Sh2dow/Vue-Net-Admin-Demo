# Creates databases in the ACA PostgreSQL container via az containerapp exec.
# vue_demo_auth is already created by POSTGRES_DB in the container.
# EF Core MigrateAsync() on each API startup applies schema migrations.
# Usage: .\infra\migrate.ps1

$ErrorActionPreference = 'Stop'

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

if (Test-Path "$ScriptDir/.env.azure") {
    Get-Content "$ScriptDir/.env.azure" | ForEach-Object {
        if ($_ -notmatch '^\s*#' -and $_ -match '=') {
            $key, $value = $_ -split '=', 2
            [System.Environment]::SetEnvironmentVariable($key, $value, 'Process')
        }
    }
}

if (-not $env:UNIQUE_SUFFIX -or -not $env:RESOURCE_GROUP) {
    Write-Host "ERROR: UNIQUE_SUFFIX or RESOURCE_GROUP not found. Run deploy.ps1 first." -ForegroundColor Red
    exit 1
}

$pgContainerName = "postgresql-$($env:UNIQUE_SUFFIX)"
$resourceGroup   = $env:RESOURCE_GROUP
$pgUser          = 'vueadmin'

# Only create databases not already created by POSTGRES_DB
$databases = @('vue_demo_tasks', 'vue_demo_orders', 'vue_demo_payments')

# Helper — runs a command inside the PG container, returns {Output, ExitCode}
function ExecInPg($shellCmd) {
    $output = az containerapp exec `
        --name $pgContainerName `
        --resource-group $resourceGroup `
        --command "sh" --command "-c" --command "$shellCmd" `
        2>&1
    @{ Output = $output; ExitCode = $LASTEXITCODE }
}

# Wait for PostgreSQL to be accepting connections
Write-Host "Waiting for PostgreSQL to be ready..." -ForegroundColor Yellow
$maxRetries = 30
$ready = $false
for ($i = 1; $i -le $maxRetries; $i++) {
    $result = ExecInPg "pg_isready -h localhost -U $pgUser"
    if ($result.ExitCode -eq 0) {
        $ready = $true
        break
    }
    Start-Sleep -Seconds 10
}

if (-not $ready) {
    Write-Host "ERROR: PostgreSQL did not become ready within ${maxRetries}0s" -ForegroundColor Red
    exit 1
}
Write-Host "✓ PostgreSQL is ready" -ForegroundColor Green

# Discover existing databases (trust auth — no password needed for localhost)
Write-Host ""
Write-Host "Discovering existing databases..." -ForegroundColor Yellow
$result = ExecInPg "psql -U $pgUser -d postgres -t -A -c 'SELECT datname FROM pg_database'"
$existingDbs = $result.Output -split "`r?`n" | ForEach-Object { $_.Trim() } | Where-Object { $_ -match '^[a-z][a-z0-9_]*$' }

Write-Host "  Found: $($existingDbs -join ', ')" -ForegroundColor Gray

# Create missing databases
Write-Host ""
Write-Host "Creating databases..." -ForegroundColor Yellow
foreach ($db in $databases) {
    if ($existingDbs -contains $db) {
        Write-Host "  ✓ $db already exists" -ForegroundColor Gray
    } else {
        $result = ExecInPg "psql -U $pgUser -d postgres -c 'CREATE DATABASE $db'"

        if ($result.ExitCode -ne 0) {
            Write-Host "  ERROR: Failed to create $db" -ForegroundColor Red
            Write-Host $result.Output -ForegroundColor Gray
            exit 1
        }
        Write-Host "  ✓ $db created" -ForegroundColor Green
    }
}

Write-Host ""
Write-Host "Databases ready. Schema migrations run on each API startup via MigrateAsync()." -ForegroundColor Gray
Write-Host "  Auth.Api      → vue_demo_auth     (seeds admin/admin)" -ForegroundColor Gray
Write-Host "  Tasks.Api     → vue_demo_tasks" -ForegroundColor Gray
Write-Host "  Orders.Api    → vue_demo_orders   (no migration call)" -ForegroundColor Gray
Write-Host "  Payments.Api  → vue_demo_payments" -ForegroundColor Gray
Write-Host "  Users.Api     → vue_demo_auth" -ForegroundColor Gray
