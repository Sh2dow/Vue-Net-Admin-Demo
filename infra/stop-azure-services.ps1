<#
.SYNOPSIS
    Stops all Azure Container Apps for Vue-Net-Admin-Demo.

.DESCRIPTION
    Convenience wrapper around manage-azure-services.ps1.
    Reads the unique suffix from infra/.env.azure by default.

.PARAMETER ResourceGroup
    Azure resource group name. Defaults to 'vue-admin-rg'.

.PARAMETER UniqueSuffix
    The unique suffix from the Bicep deployment. If omitted, reads from infra/.env.azure.

.PARAMETER Wait
    Wait for each app to reach Stopped state.

.EXAMPLE
    .\stop-azure-services.ps1
    .\stop-azure-services.ps1 -Wait
    .\stop-azure-services.ps1 -UniqueSuffix abc123
#>
param(
    [string]$ResourceGroup = 'vue-admin-rg',
    [string]$UniqueSuffix = '',
    [switch]$Wait
)

& "$PSScriptRoot/manage-azure-services.ps1" `
    -Action Stop `
    -ResourceGroup $ResourceGroup `
    -UniqueSuffix $UniqueSuffix `
    -Wait:$Wait
