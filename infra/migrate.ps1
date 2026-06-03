# Run EF Core migrations against Azure PostgreSQL
# Prerequisites: infra deployed, .env.azure exists, psql installed
# Usage: .\infra\migrate.ps1

$ErrorActionPreference = 'Stop'
$env:PATH = 'C:\Program Files\Microsoft SDKs\Azure\CLI2\wbin;' + $env:PATH

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Split-Path -Parent $ScriptDir
$BackendDir = Join-Path $RepoRoot 'backend'

# Load env vars
if (Test-Path "$ScriptDir/.env.azure") {
    Get-Content "$ScriptDir/.env.azure" | ForEach-Object {
        if ($_ -notmatch '^\s*#' -and $_ -match '=') {
            $key, $value = $_ -split '=', 2
            [System.Environment]::SetEnvironmentVariable($key, $value, 'Process')
        }
    }
}

if (-not $env:PG_FQDN -or -not $env:PG_PASSWORD) {
    Write-Host "ERROR: PG_FQDN or PG_PASSWORD not found. Run deploy.ps1 first." -ForegroundColor Red
    exit 1
}

# Construct connection strings
$pgUser = "vueadmin@${env:PG_SERVER_NAME}"
$connAuth = "Host=${env:PG_FQDN};Port=5432;Database=vue_demo_auth;Username=${pgUser};Password=${env:PG_PASSWORD};SSL Mode=require"
$connTasks = "Host=${env:PG_FQDN};Port=5432;Database=vue_demo_tasks;Username=${pgUser};Password=${env:PG_PASSWORD};SSL Mode=require"
$connOrders = "Host=${env:PG_FQDN};Port=5432;Database=vue_demo_orders;Username=${pgUser};Password=${env:PG_PASSWORD};SSL Mode=require"
$connPayments = "Host=${env:PG_FQDN};Port=5432;Database=vue_demo_payments;Username=${pgUser};Password=${env:PG_PASSWORD};SSL Mode=require"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Running EF Core Migrations" -ForegroundColor Cyan
Write-Host " PostgreSQL: ${env:PG_FQDN}" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check if psql is available
$psqlPath = Get-Command psql -ErrorAction SilentlyContinue
if (-not $psqlPath) {
    Write-Host "⚠️  psql not found - databases will be created via Azure portal" -ForegroundColor Yellow
    Write-Host "   Install PostgreSQL client or create databases manually:" -ForegroundColor Yellow
    Write-Host "   - vue_demo_auth" -ForegroundColor Gray
    Write-Host "   - vue_demo_tasks" -ForegroundColor Gray
    Write-Host "   - vue_demo_orders" -ForegroundColor Gray
    Write-Host "   - vue_demo_payments" -ForegroundColor Gray
    Write-Host ""
} else {
    # Create databases using psql
    Write-Host "[1/5] Creating databases..." -ForegroundColor Yellow
    $databases = @('vue_demo_auth', 'vue_demo_tasks', 'vue_demo_orders', 'vue_demo_payments')
    foreach ($db in $databases) {
        Write-Host "  Creating '$db'..." -ForegroundColor Gray
        $result = psql "host=${env:PG_FQDN} port=5432 dbname=postgres user=${pgUser} password=${env:PG_PASSWORD} sslmode=require" -c "SELECT 1 FROM pg_database WHERE datname = '${db}'" 2>&1
        if ($result -notmatch '\(1 row\)') {
            psql "host=${env:PG_FQDN} port=5432 dbname=postgres user=${pgUser} password=${env:PG_PASSWORD} sslmode=require" -c "CREATE DATABASE ${db}" 2>&1 | Out-Null
            Write-Host "  ✓ Created '$db'" -ForegroundColor Green
        } else {
            Write-Host "  ✓ '$db' already exists" -ForegroundColor Green
        }
    }
}

# Run migrations
$efTool = "$env:USERPROFILE\.dotnet\tools\dotnet-ef.exe"
if (-not (Test-Path $efTool)) {
    Write-Host ""
    Write-Host "ERROR: dotnet-ef tool not found at $efTool" -ForegroundColor Red
    Write-Host "Install it: dotnet tool install --global dotnet-ef" -ForegroundColor Yellow
    exit 1
}

Set-Location $BackendDir

@migrations = @(
    @('AuthDbContext',   'backend.Auth.Api',   $connAuth),
    @('TasksDbContext',  'backend.Tasks.Api',  $connTasks),
    @('OrdersDbContext', 'backend.Orders.Api', $connOrders),
    @('PaymentsDbContext', 'backend.Payments.Api', $connPayments)
)

foreach ($migration in @migrations) {
    $context = $migration[0]
    $startup = $migration[1]
    $conn = $migration[2]

    Write-Host ""
    Write-Host "[*] Migrating $context..." -ForegroundColor Yellow
    
    # Set connection string in appsettings
    $env:ConnectionStrings__Auth = $conn
    $env:ConnectionStrings__Tasks = $conn
    $env:ConnectionStrings__Orders = $conn
    $env:ConnectionStrings__Payments = $conn

    # Run migration
    $result = & $efTool database update `
        --context $context `
        --startup-project $startup `
        --project backend.Domain `
        --connection $conn `
        2>&1

    if ($LASTEXITCODE -eq 0) {
        Write-Host "  ✓ $context migrated" -ForegroundColor Green
    } else {
        Write-Host "  ⚠️  $context migration output:" -ForegroundColor Yellow
        Write-Host $result -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Migrations complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
