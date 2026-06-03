# Build and push Docker images to ACR
# Prerequisites: Docker Desktop running, az login, infra deployed
# Usage: .\infra\build-images.ps1

$ErrorActionPreference = 'Stop'
$env:PATH = 'C:\Program Files\Microsoft SDKs\Azure\CLI2\wbin;' + $env:PATH

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Split-Path -Parent $ScriptDir

# Load env vars from deployment
if (Test-Path "$ScriptDir/.env.azure") {
    Get-Content "$ScriptDir/.env.azure" | ForEach-Object {
        if ($_ -notmatch '^\s*#' -and $_ -match '=') {
            $key, $value = $_ -split '=', 2
            [System.Environment]::SetEnvironmentVariable($key, $value, 'Process')
        }
    }
}

if (-not $env:ACR_LOGIN) {
    Write-Host "ERROR: ACR_LOGIN not found. Run deploy.ps1 first." -ForegroundColor Red
    exit 1
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Building & Pushing Docker Images" -ForegroundColor Cyan
Write-Host " ACR: $env:ACR_LOGIN" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Login to ACR
Write-Host "[1/8] Logging in to ACR..." -ForegroundColor Yellow
az acr login --name $env:ACR_LOGIN | Out-Null
Write-Host "  ✓ Logged in" -ForegroundColor Green

# Define services: (displayName, projectPath, dockerfilePath, imageTag)
$services = @(
    @('Auth API',        'backend/backend.Auth.Api',       'backend/backend.Auth.Api/Dockerfile',     'auth-api'),
    @('Tasks API',       'backend/backend.Tasks.Api',      'backend/backend.Tasks.Api/Dockerfile',    'tasks-api'),
    @('Orders API',      'backend/backend.Orders.Api',     'backend/backend.Orders.Api/Dockerfile',   'orders-api'),
    @('Payments API',    'backend/backend.Payments.Api',   'backend/backend.Payments.Api/Dockerfile', 'payments-api'),
    @('Users API',       'backend/backend.Users.Api',      'backend/backend.Users.Api/Dockerfile',    'users-api'),
    @('API Gateway',     'backend/backend.Api',            'backend/backend.Api/Dockerfile',          'api-gateway'),
    @('Frontend',        'frontend',                       'frontend/Dockerfile',                     'frontend')
)

Set-Location $RepoRoot

foreach ($service in $services) {
    $displayName = $service[0]
    $context = $service[1]
    $dockerfile = $service[2]
    $imageTag = $service[3]
    $fullImage = "$env:ACR_LOGIN/$imageTag:latest"

    Write-Host ""
    Write-Host "[*] Building $displayName..." -ForegroundColor Yellow

    # Build
    docker build -t $fullImage -f $dockerfile $context | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  ERROR: Build failed for $displayName" -ForegroundColor Red
        continue
    }
    Write-Host "  ✓ Built" -ForegroundColor Green

    # Push
    Write-Host "  → Pushing to ACR..." -ForegroundColor Gray
    docker push $fullImage | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  ERROR: Push failed for $displayName" -ForegroundColor Red
        continue
    }
    Write-Host "  ✓ Pushed $fullImage" -ForegroundColor Green
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " All images built and pushed!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next: Run migrations" -ForegroundColor Yellow
Write-Host "  .\infra\migrate.ps1" -ForegroundColor White
