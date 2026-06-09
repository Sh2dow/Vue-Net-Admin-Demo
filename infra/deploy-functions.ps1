# Azure Functions deployment for backend.Functions (Windows Consumption Y1, zip deploy)
# Usage: .\infra\deploy-functions.ps1

param(
    [string]$ResourceGroup = 'vue-admin-rg'
)

$ErrorActionPreference = 'Stop'

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot  = Split-Path -Parent $ScriptDir
Set-Location $RepoRoot

# ---------------------------------------------------------------------------
# Read existing core infrastructure outputs
# ---------------------------------------------------------------------------
$coreDeploymentName = 'vue-admin-core'
Write-Host "Reading core infrastructure outputs..." -ForegroundColor Yellow

$coreResult = az deployment group show `
    --resource-group $ResourceGroup `
    --name $coreDeploymentName `
    --output json 2>$null

if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($coreResult)) {
    Write-Host "ERROR: Core deployment '$coreDeploymentName' not found. Run .\infra\deploy.ps1 first." -ForegroundColor Red
    exit 1
}

$coreOutputs = $coreResult | Out-String | ConvertFrom-Json
$suffix      = $coreOutputs.properties.outputs.uniqueSuffix.value
$sbConnStr   = $coreOutputs.properties.outputs.sbConnectionString.value
$sqlConnBase  = $coreOutputs.properties.outputs.sqlConnBase.value

# Derive auth authority from ACA auth-api (functions still need to validate tokens)
$authAuthority = "https://auth-api-${suffix}.${coreOutputs.properties.outputs.envDomain.value}"

Write-Host "  Suffix: $suffix" -ForegroundColor Green
Write-Host "  Auth:   $authAuthority" -ForegroundColor Green

# ---------------------------------------------------------------------------
# Deploy Functions infrastructure
# ---------------------------------------------------------------------------
Write-Host ""
Write-Host "Deploying Azure Functions infrastructure..." -ForegroundColor Yellow

# Delete stale resources from previous attempts
$storageAccountName = "vafunc$suffix"
$functionAppName = "vue-admin-func-$suffix"

$staleFunc = az functionapp show --name $functionAppName --resource-group $ResourceGroup --query name -o tsv 2>$null
if ($LASTEXITCODE -eq 0 -and ![string]::IsNullOrWhiteSpace($staleFunc)) {
    Write-Host "Deleting stale Function App '$staleFunc' from previous deployment..." -ForegroundColor Yellow
    az functionapp delete --name $staleFunc --resource-group $ResourceGroup --yes | Out-Null
    Write-Host "  ✓ Deleted" -ForegroundColor Green
}

$staleStorage = az storage account show --name $storageAccountName --resource-group $ResourceGroup --query name -o tsv 2>$null
if ($LASTEXITCODE -eq 0 -and ![string]::IsNullOrWhiteSpace($staleStorage)) {
    Write-Host "Deleting stale storage account '$staleStorage' from previous deployment..." -ForegroundColor Yellow
    az storage account delete --name $staleStorage --resource-group $ResourceGroup --yes | Out-Null
    Write-Host "  ✓ Deleted" -ForegroundColor Green
}

$funcResult = az deployment group create `
    --resource-group $ResourceGroup `
    --name 'vue-admin-functions' `
    --template-file "$ScriptDir/infra-functions.bicep" `
    --parameters "uniqueSuffix=$suffix" `
        "sbConnStr=$sbConnStr" `
        "sqlConnBase=$sqlConnBase" `
        "authAuthority=$authAuthority" `
    --output json `
    --only-show-errors

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Functions infrastructure deployment failed" -ForegroundColor Red
    exit 1
}

$funcOutputs = $funcResult | Out-String | ConvertFrom-Json
$functionAppName = $funcOutputs.properties.outputs.functionAppName.value
$functionAppUrl  = $funcOutputs.properties.outputs.functionAppUrl.value

Write-Host "  Function App: $functionAppName" -ForegroundColor Green
Write-Host "  URL:          $functionAppUrl" -ForegroundColor Green

# ---------------------------------------------------------------------------
# Build and zip deploy the Functions project
# ---------------------------------------------------------------------------
Write-Host ""
Write-Host "Building backend.Functions..." -ForegroundColor Yellow

$publishDir = "$RepoRoot/backend/backend.Functions/bin/publish"
$zipPath    = "$RepoRoot/backend/backend.Functions/bin/function.zip"

if (Test-Path $publishDir) {
    Remove-Item -Recurse -Force $publishDir
}
if (Test-Path $zipPath) {
    Remove-Item -Force $zipPath
}

New-Item -ItemType Directory -Path $publishDir | Out-Null

dotnet publish "$RepoRoot/backend/backend.Functions/backend.Functions.csproj" `
    -c Release `
    -o $publishDir `
    --self-contained true

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: dotnet publish failed" -ForegroundColor Red
    exit 1
}

Write-Host "Creating deployment zip..." -ForegroundColor Gray
Compress-Archive -Path "$publishDir/*" -DestinationPath $zipPath -Force

Write-Host "Waiting for Function App to be ready..." -ForegroundColor Gray
Start-Sleep -Seconds 15

Write-Host "Deploying zip to Azure Functions..." -ForegroundColor Yellow
az functionapp deployment source config-zip `
    --resource-group $ResourceGroup `
    --name $functionAppName `
    --src $zipPath `
    --only-show-errors

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Zip deployment failed" -ForegroundColor Red
    exit 1
}

Write-Host "  Deployment complete" -ForegroundColor Green

# ---------------------------------------------------------------------------
# Verify
# ---------------------------------------------------------------------------
Write-Host ""
Write-Host "Verifying Functions app..." -ForegroundColor Yellow
$maxAttempts = 12
$ok = $false

for ($attempt = 1; $attempt -le $maxAttempts -and -not $ok; $attempt++) {
    try {
        $response = Invoke-WebRequest -Uri "$functionAppUrl/api/health" -Method Get -TimeoutSec 10 -UseBasicParsing 2>$null
        if ($response.StatusCode -eq 200) { $ok = $true }
    } catch {}

    if (-not $ok) { Start-Sleep -Seconds 5 }
}

if ($ok) {
    Write-Host "  Functions app is responding" -ForegroundColor Green
} else {
    Write-Host "  Functions app not responding yet (cold start). URL: $functionAppUrl" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Azure Functions deployed!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "URL: $functionAppUrl" -ForegroundColor Cyan
