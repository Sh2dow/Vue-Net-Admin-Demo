# Azure deployment script for Vue-Net-Admin-Demo
# Prerequisites: Azure CLI installed and logged in
# Usage: .\infra\deploy.ps1              # run all stages
#        .\infra\deploy.ps1 -StartAt 4   # skip core infra, build & deploy
#        .\infra\deploy.ps1 -StartAt 5   # skip build, deploy apps only

param(
    [int]$StartAt = 1
)

$ErrorActionPreference = 'Stop'

$ScriptDir          = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot           = Split-Path -Parent $ScriptDir
Set-Location $RepoRoot

$ResourceGroup      = 'vue-admin-rg'
$Location           = 'polandcentral'
$CoreDeploymentName = 'vue-admin-core'
$AppsDeploymentName = 'vue-admin-apps'

# Unique image tag per build — ACA caches digest on first pull, won't auto-update 'latest'
$ImageTag = (git rev-parse --short HEAD 2>$null)
if ([string]::IsNullOrWhiteSpace($ImageTag)) {
    $ImageTag = (Get-Date -Format 'yyyyMMddHHmmss')
}

# Verify Bicep CLI is installed
az bicep version > $null 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "Installing Bicep CLI..." -ForegroundColor Yellow
    az bicep install | Out-Null
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Azure Deployment: Vue-Net-Admin-Demo" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Starting at stage: $StartAt" -ForegroundColor Cyan
Write-Host ""

# Stage 1 — Verify login
if ($StartAt -le 1) {
    Write-Host "[1/5] Verifying Azure login..." -ForegroundColor Yellow
    az account show --query '{name:name,id:id}' | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Not logged in. Run: az login" -ForegroundColor Red
        exit 1
    }
    Write-Host "  ✓ Logged in" -ForegroundColor Green
}

# Stage 2 — Ensure resource group exists
if ($StartAt -le 2) {
    Write-Host "[2/5] Checking resource group..." -ForegroundColor Yellow
    $ErrorActionPreference = 'Continue'
    $rgExists = az group show --name $ResourceGroup --query id -o tsv 2>$null
    $ErrorActionPreference = 'Stop'
    if ([string]::IsNullOrWhiteSpace($rgExists)) {
        Write-Host "  Creating resource group '$ResourceGroup' in $Location..." -ForegroundColor Gray
        az group create --name $ResourceGroup --location $Location --tags Environment=dev | Out-Null
        Write-Host "  ✓ Resource group created" -ForegroundColor Green
    } else {
        Write-Host "  ✓ Resource group '$ResourceGroup' exists" -ForegroundColor Green
    }
}

# Stage 2.5 — Load persisted PG password from .env.azure if available
$pgPasswordFile = "$ScriptDir/.env.azure"
if (Test-Path $pgPasswordFile) {
    $existingEnv = Get-Content $pgPasswordFile
    foreach ($line in $existingEnv) {
        if ($line -match '^PG_PASSWORD=(.*)') {
            $adminPassword = $Matches[1]
            Write-Host "  Using existing PG password from .env.azure" -ForegroundColor Green
            break
        }
    }
}

# Stage 3 — Deploy core infrastructure (ACR, Service Bus, Key Vault, PG)
if ($StartAt -le 3) {
    $CoreBicepFile = "$ScriptDir/infra-core.bicep"
    Write-Host "[3/5] Deploying core infrastructure (ACR, Service Bus, Key Vault, PostgreSQL)..." -ForegroundColor Yellow

    # Only generate a new password if one wasn't loaded from .env.azure
    if (-not $adminPassword) {
        $chars = [char[]]'abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789'
        $adminPassword = -join ($chars | Get-Random -Count 24)
    }
    $coreResult = az deployment group create `
        --resource-group $ResourceGroup `
        --name $CoreDeploymentName `
        --template-file $CoreBicepFile `
        --parameters "adminPassword=$adminPassword" `
        --output json `
        --only-show-errors

    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Core deployment failed" -ForegroundColor Red
        exit 1
    }

    $coreOutputs = $coreResult | Out-String | ConvertFrom-Json
    $suffix = $coreOutputs.properties.outputs.uniqueSuffix.value
    $acrLogin = $coreOutputs.properties.outputs.acrLoginServer.value
    $acrName = $acrLogin -replace '\.azurecr\.io$', ''
    $uaiId = $coreOutputs.properties.outputs.uaiId.value
    $pgFqdn = $coreOutputs.properties.outputs.pgFqdn.value
    $containerAppsEnvId = $coreOutputs.properties.outputs.containerAppsEnvId.value
    $pgConnBase = $coreOutputs.properties.outputs.pgConnBase.value
    $sbConnStr = $coreOutputs.properties.outputs.sbConnectionString.value
    $envDomain = $coreOutputs.properties.outputs.envDomain.value

    Write-Host "  ✓ ACR: $acrLogin" -ForegroundColor Green
    Write-Host "  ✓ Suffix: $suffix" -ForegroundColor Green
    Write-Host "  ✓ Image tag: $ImageTag" -ForegroundColor Green
}

# Resolve core outputs from existing deployment when skipping stages 1-3
if ($StartAt -gt 3) {
    Write-Host "Reading existing core outputs..." -ForegroundColor Yellow
    $coreResult = az deployment group show `
        --resource-group $ResourceGroup `
        --name $CoreDeploymentName `
        --output json 2>$null

    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Core deployment '$CoreDeploymentName' not found. Deploy core first (-StartAt 1)." -ForegroundColor Red
        exit 1
    }

    $coreOutputs = $coreResult | Out-String | ConvertFrom-Json
    $suffix = $coreOutputs.properties.outputs.uniqueSuffix.value
    $acrLogin = $coreOutputs.properties.outputs.acrLoginServer.value
    $acrName = $acrLogin -replace '\.azurecr\.io$', ''
    $uaiId = $coreOutputs.properties.outputs.uaiId.value
    $pgFqdn = $coreOutputs.properties.outputs.pgFqdn.value
    $containerAppsEnvId = $coreOutputs.properties.outputs.containerAppsEnvId.value
    $pgConnBase = $coreOutputs.properties.outputs.pgConnBase.value
    $sbConnStr = $coreOutputs.properties.outputs.sbConnectionString.value
    $envDomain = $coreOutputs.properties.outputs.envDomain.value

    # Read password from .env if available (for local scripts)
    if (Test-Path "$ScriptDir/.env.azure") {
        $envLines = Get-Content "$ScriptDir/.env.azure"
        foreach ($line in $envLines) {
            if ($line -match '^PG_PASSWORD=(.*)') {
                $adminPassword = $Matches[1]
            }
        }
    }

    Write-Host "  ✓ ACR: $acrLogin" -ForegroundColor Green
    Write-Host "  ✓ Suffix: $suffix" -ForegroundColor Green
    Write-Host "  ✓ Image tag: $ImageTag" -ForegroundColor Green
}

# Stage 3.5 — Create additional databases (PG default entrypoint only creates POSTGRES_DB)
if ($StartAt -le 4) {
    Write-Host ""
    Write-Host "[3.5] Creating additional databases..." -ForegroundColor Yellow

    # Wait for PG to be ready
    Write-Host "  Waiting for PostgreSQL..." -ForegroundColor Gray
    $pgReady = $false
    for ($i = 1; $i -le 30 -and -not $pgReady; $i++) {
        try {
            $pgHealth = az containerapp show-logs --name "postgresql-${suffix}" --resource-group $ResourceGroup --output json 2>$null
            $pgReady = ($LASTEXITCODE -eq 0)
        } catch {}
        if (-not $pgReady) { Start-Sleep -Seconds 5 }
    }

    # Create missing databases via container exec
    $dbNames = @('vue_demo_tasks', 'vue_demo_orders', 'vue_demo_payments')
    foreach ($dbName in $dbNames) {
        Write-Host "  Creating $dbName..." -ForegroundColor Gray
        $execCmd = "psql -U vueadmin -d postgres -tc `"`SELECT 1 FROM pg_database WHERE datname='$dbName'`" | grep -q 1 || psql -U vueadmin -d postgres -c `"`CREATE DATABASE $dbName`"`" 2>&1"
        $ErrorActionPreference = 'Continue'
        az containerapp exec `
            --name "postgresql-${suffix}" `
            --resource-group $ResourceGroup `
            --command sh `
            --command -c `
            --command $execCmd `
            2>&1 | Out-Null
        $ErrorActionPreference = 'Stop'
        Write-Host "  ✓ $dbName" -ForegroundColor Green
    }
}

# Stage 4 — Build and push Docker images
if ($StartAt -le 4) {
    Write-Host ""
    Write-Host "[4/5] Building and pushing Docker images..." -ForegroundColor Yellow

    $ErrorActionPreference = 'Continue'
    az acr login --name $acrName | Out-Null
    $ErrorActionPreference = 'Stop'

    # Services: (displayName, dockerfilePath, context dir)
    # Dockerfiles reference paths from repo root, so context must be repo root (.)
    $services = @(
        @('Auth API',        'backend/backend.Auth.Api/Dockerfile',     '.'),
        @('Tasks API',       'backend/backend.Tasks.Api/Dockerfile',    '.'),
        @('Orders API',      'backend/backend.Orders.Api/Dockerfile',   '.'),
        @('Payments API',    'backend/backend.Payments.Api/Dockerfile', '.'),
        @('Users API',       'backend/backend.Users.Api/Dockerfile',    '.'),
        @('API Gateway',     'backend/backend.Api/Dockerfile',          '.'),
        @('Functions',       'backend/backend.Functions/Dockerfile',    '.'),
        @('Frontend',        'frontend/Dockerfile',                     'frontend')
    )

    $imageTags = @('auth-api', 'tasks-api', 'orders-api', 'payments-api', 'users-api', 'api-gateway', 'functions', 'frontend')

    foreach ($i in 0..($services.Length - 1)) {
        $displayName = $services[$i][0]
        $dockerfile  = $services[$i][1]
        $context     = $services[$i][2]
        $svcName     = $imageTags[$i]
        $fullImage   = "${acrLogin}/${svcName}:${ImageTag}"

        Write-Host "  Building $displayName..." -ForegroundColor Gray
        $ErrorActionPreference = 'Continue'
        $buildOutput = docker build -f "$RepoRoot/$dockerfile" -t $fullImage "$RepoRoot/$context" 2>&1
        $buildExitCode = $LASTEXITCODE
        $ErrorActionPreference = 'Stop'

        if ($buildExitCode -ne 0) {
            Write-Host "  ERROR: Build failed for $displayName" -ForegroundColor Red
            Write-Host $buildOutput -ForegroundColor Gray
            exit 1
        }

        Write-Host "  Pushing $displayName..." -ForegroundColor Gray
        $ErrorActionPreference = 'Continue'
        docker push $fullImage 2>&1 | Out-Null
        $pushExitCode = $LASTEXITCODE
        $ErrorActionPreference = 'Stop'

        if ($pushExitCode -ne 0) {
            Write-Host "  ERROR: Push failed for $displayName" -ForegroundColor Red
            exit 1
        }
        Write-Host "  ✓ $displayName pushed" -ForegroundColor Green
    }
}

# Stage 5 — Deploy container apps
if ($StartAt -le 5) {
    $AppsBicepFile = "$ScriptDir/infra-apps.bicep"
    Write-Host ""
    Write-Host "[5/5] Deploying container apps..." -ForegroundColor Yellow
    $appsResult = az deployment group create `
        --resource-group $ResourceGroup `
        --name $AppsDeploymentName `
        --template-file $AppsBicepFile `
        --parameters "uniqueSuffix=$suffix" `
            "containerAppsEnvId=$containerAppsEnvId" `
            "acrLoginServer=$acrLogin" `
            "uaiId=$uaiId" `
            "imageTag=$ImageTag" `
            "pgConnBase=$pgConnBase" `
            "sbConnStr=$sbConnStr" `
            "envDomain=$envDomain" `
        --output json `
        --only-show-errors

    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Apps deployment failed" -ForegroundColor Red
        exit 1
    }

    $appsOutputs = $appsResult | Out-String | ConvertFrom-Json
    $authApiUrl = $appsOutputs.properties.outputs.authApiUrl.value
    $apiGatewayUrl = $appsOutputs.properties.outputs.apiGatewayUrl.value
    $functionsUrl = $appsOutputs.properties.outputs.functionsUrl.value
    $frontendUrl = $appsOutputs.properties.outputs.frontendUrl.value

    Write-Host "  ✓ Auth API: $authApiUrl" -ForegroundColor Green
    Write-Host "  ✓ API Gateway: $apiGatewayUrl" -ForegroundColor Green
    Write-Host "  ✓ Functions: $functionsUrl" -ForegroundColor Green
    Write-Host "  ✓ Frontend: $frontendUrl" -ForegroundColor Green
}

# Resolve app URLs when skipping stage 5
if ($StartAt -gt 5) {
    $authApiUrl = "https://auth-api-${suffix}.eastus2.azurecontainerapps.io"
    $apiGatewayUrl = "https://api-gateway-${suffix}.eastus2.azurecontainerapps.io"
    $functionsUrl = "https://functions-${suffix}.eastus2.azurecontainerapps.io"
    $frontendUrl = "https://frontend-${suffix}.eastus2.azurecontainerapps.io"
}

# Save environment file
Write-Host ""
Write-Host "Saving environment variables..." -ForegroundColor Yellow
$envContent = @"
# Generated by deploy.ps1 on $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
RESOURCE_GROUP=$ResourceGroup
PG_FQDN=$pgFqdn
PG_PASSWORD=$adminPassword
UNIQUE_SUFFIX=$suffix
ACR_LOGIN=$acrLogin
AUTH_API_URL=$authApiUrl
API_GATEWAY_URL=$apiGatewayUrl
FUNCTIONS_URL=$functionsUrl
FRONTEND_URL=$frontendUrl
SB_CONNECTION_STRING=$sbConnStr
"@

$envFile = "$ScriptDir/.env.azure"
Set-Content -Path $envFile -Value $envContent
Write-Host "  ✓ Saved to $envFile" -ForegroundColor Green

# Verify services (cold-start can take 30-60s for minReplicas: 0)
Write-Host ""
Write-Host "Verifying services..." -ForegroundColor Yellow
$maxAttempts = 12

$testUrls = @(
    @('Auth API',    $authApiUrl + '/health'),
    @('API Gateway', $apiGatewayUrl + '/health'),
    @('Functions',   $functionsUrl + '/health'),
    @('Frontend',    $frontendUrl)
)

foreach ($test in $testUrls) {
    $name = $test[0]
    $url  = $test[1]
    $ok   = $false

    for ($attempt = 1; $attempt -le $maxAttempts -and -not $ok; $attempt++) {
        try {
            $response = Invoke-WebRequest -Uri $url -Method Get -TimeoutSec 10 -UseBasicParsing 2>$null
            if ($response.StatusCode -eq 200) { $ok = $true }
        } catch {}

        if (-not $ok) { Start-Sleep -Seconds 5 }
    }

    if ($ok) {
        Write-Host "  ✓ $name OK" -ForegroundColor Green
    } else {
        Write-Host "  ⚠ $name not responding yet (cold-start, check ACA logs)" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Deployment complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Frontend:    $frontendUrl" -ForegroundColor Cyan
Write-Host "API Gateway: $apiGatewayUrl" -ForegroundColor Cyan
Write-Host "Functions:   $functionsUrl" -ForegroundColor Cyan
Write-Host "Auth API:    $authApiUrl" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  Databases created automatically on PG startup" -ForegroundColor White
Write-Host "  Auth.Api seeds admin/admin on startup" -ForegroundColor White
Write-Host ""
Write-Host "⚠️  COST WARNING: Delete when done!" -ForegroundColor Red
Write-Host "   az group delete --name $ResourceGroup --yes --no-wait" -ForegroundColor Gray
