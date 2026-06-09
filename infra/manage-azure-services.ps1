<#
.SYNOPSIS
    Start, stop, restart, or check status of all Azure Container Apps for Vue-Net-Admin-Demo.

.DESCRIPTION
    Manages the lifecycle of all container apps deployed by infra-apps.bicep.
    Start/Stop use the Azure REST API (via 'az rest') because the CLI does not
    expose native 'containerapp start/stop' commands in all versions.

.PARAMETER Action
    The operation to perform: Start, Stop, Restart, or Status.

.PARAMETER ResourceGroup
    Azure resource group name. Defaults to 'vue-admin-rg'.

.PARAMETER UniqueSuffix
    The unique suffix from the Bicep deployment (e.g., 'abc123').
    If omitted, the script attempts to read it from infra/.env.azure.

.PARAMETER Wait
    Poll until each app reaches the desired state (Running for Start/Restart, Stopped for Stop).

.EXAMPLE
    .\manage-azure-services.ps1 -Action Stop
    .\manage-azure-services.ps1 -Action Start -Wait
    .\manage-azure-services.ps1 -Action Status
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateSet('Start', 'Stop', 'Restart', 'Status')]
    [string]$Action,

    [string]$ResourceGroup = 'vue-admin-rg',

    [string]$UniqueSuffix = '',

    [switch]$Wait
)

$ErrorActionPreference = 'Stop'

# ---------------------------------------------------------------------------
# Pre-flight
# ---------------------------------------------------------------------------
$account = az account show --query 'name' -o tsv --only-show-errors 2>$null
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($account)) {
    Write-Host "ERROR: Not logged in to Azure CLI. Run 'az login' first." -ForegroundColor Red
    exit 1
}
Write-Host "Azure account: $account" -ForegroundColor Green

if ([string]::IsNullOrWhiteSpace($UniqueSuffix)) {
    $envFile = "$PSScriptRoot/.env.azure"
    if (Test-Path $envFile) {
        foreach ($line in (Get-Content $envFile)) {
            if ($line -match '^UNIQUE_SUFFIX=(.*)') {
                $UniqueSuffix = $Matches[1]
                Write-Host "Resolved unique suffix from $envFile`: $UniqueSuffix" -ForegroundColor Green
                break
            }
        }
    }
}

if ([string]::IsNullOrWhiteSpace($UniqueSuffix)) {
    Write-Host "ERROR: Could not determine unique suffix. Pass -UniqueSuffix or ensure infra/.env.azure exists with UNIQUE_SUFFIX set." -ForegroundColor Red
    exit 1
}

$appNames = @(
    "auth-api-$UniqueSuffix"
    "users-api-$UniqueSuffix"
    "tasks-api-$UniqueSuffix"
    "orders-api-$UniqueSuffix"
    "payments-api-$UniqueSuffix"
    "api-gateway-$UniqueSuffix"
    "frontend-$UniqueSuffix"
)

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------
function Get-AppInfo($appName) {
    $json = az containerapp show --name $appName --resource-group $ResourceGroup -o json --only-show-errors 2>$null
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($json)) {
        return @{ Status = 'Missing'; RevName = 'none' }
    }
    $obj = $json | ConvertFrom-Json
    return @{
        Status  = $obj.properties.runningStatus
        RevName = $obj.properties.latestReadyRevisionName
        Id      = $obj.id
    }
}

function Wait-ForState($appName, $desired, $timeoutSec = 180) {
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    do {
        $info = Get-AppInfo $appName
        if ($info.Status -eq $desired) { return $true }
        Start-Sleep -Seconds 5
    } while ($sw.Elapsed.TotalSeconds -lt $timeoutSec)
    return $false
}

# ---------------------------------------------------------------------------
# Execute
# ---------------------------------------------------------------------------
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Azure Container Apps: $Action" -ForegroundColor Cyan
Write-Host " Resource Group: $ResourceGroup" -ForegroundColor Cyan
Write-Host " Suffix: $UniqueSuffix" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

$results = [System.Collections.Generic.List[object]]::new()

foreach ($app in $appNames) {
    $info = Get-AppInfo $app

    if ($Action -eq 'Status') {
        $results.Add([PSCustomObject]@{ App = $app; Status = $info.Status; Revision = $info.RevName })
        continue
    }

    if ($PSCmdlet.ShouldProcess("$app", "$Action")) {
        Write-Host "[$Action] $app (status=$($info.Status) rev=$($info.RevName)) ..." -ForegroundColor Yellow -NoNewline
        $ErrorActionPreference = 'Continue'

        switch ($Action) {
            'Start' {
                if (-not [string]::IsNullOrWhiteSpace($info.Id)) {
                    $url = "https://management.azure.com$($info.Id)/start?api-version=2024-03-01"
                    $null = az rest --method POST --url $url --only-show-errors 2>&1
                    $exitCode = $LASTEXITCODE
                } else {
                    $exitCode = 1
                }
            }
            'Stop' {
                if (-not [string]::IsNullOrWhiteSpace($info.Id)) {
                    $url = "https://management.azure.com$($info.Id)/stop?api-version=2024-03-01"
                    $null = az rest --method POST --url $url --only-show-errors 2>&1
                    $exitCode = $LASTEXITCODE
                } else {
                    $exitCode = 1
                }
            }
            'Restart' {
                if (-not [string]::IsNullOrWhiteSpace($info.RevName)) {
                    $null = az containerapp revision restart --name $app --resource-group $ResourceGroup --revision $info.RevName --only-show-errors 2>&1
                    $exitCode = $LASTEXITCODE
                } else {
                    $exitCode = 1
                }
            }
        }

        $ErrorActionPreference = 'Stop'

        if ($exitCode -ne 0) {
            Write-Host " FAILED" -ForegroundColor Red
            $results.Add([PSCustomObject]@{ App = $app; Result = 'FAILED'; Status = $info.Status; Revision = $info.RevName })
            continue
        }
        Write-Host " OK" -ForegroundColor Green
    }

    # Post-op status
    Start-Sleep -Seconds 3
    $infoAfter = Get-AppInfo $app

    if ($Wait) {
        $desired = if ($Action -in @('Start','Restart')) { 'Running' } else { 'Stopped' }
        Write-Host "  Waiting for '$desired' (current=$($infoAfter.Status)) ..." -ForegroundColor Gray -NoNewline
        $ok = Wait-ForState $app $desired
        $infoAfter = Get-AppInfo $app
        if ($ok) {
            Write-Host " $($infoAfter.Status)" -ForegroundColor Green
        } else {
            Write-Host " TIMEOUT ($($infoAfter.Status))" -ForegroundColor Red
        }
    } else {
        Write-Host "  Current status=$($infoAfter.Status) rev=$($infoAfter.RevName)" -ForegroundColor Gray
    }

    $results.Add([PSCustomObject]@{
        App      = $app
        Result   = 'OK'
        Status   = $infoAfter.Status
        Revision = $infoAfter.RevName
    })
}

# ---------------------------------------------------------------------------
# Summary
# ---------------------------------------------------------------------------
Write-Host ""
$results | Format-Table -AutoSize

$failed = $results | Where-Object { $_.Result -eq 'FAILED' }
if ($failed) {
    Write-Host "WARNING: $($failed.Count) operation(s) failed." -ForegroundColor Red
    exit 1
}

$notReady = $results | Where-Object { $Action -in @('Start','Restart') -and $_.Status -ne 'Running' }
if ($notReady) {
    Write-Host "NOTE: $($notReady.Count) app(s) are not Running yet. ACA cold-start can take 30-120s. Run with -Wait or check again in a minute." -ForegroundColor Yellow
}

Write-Host "Done." -ForegroundColor Green
