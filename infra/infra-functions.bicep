/// Azure Functions (Consumption plan) for Vue-Net-Admin-Demo backend.Functions
param location string = resourceGroup().location
param uniqueSuffix string
param sbConnStr string
param sqlConnBase string
param authAuthority string

var functionAppName = 'vue-admin-func-${uniqueSuffix}'
var hostingPlanName = 'vue-admin-func-plan-${uniqueSuffix}'
var storageAccountName = 'vafunc${uniqueSuffix}'

// Storage account required by Azure Functions runtime
resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  kind: 'StorageV2'
  sku: { name: 'Standard_LRS' }
  properties: {
    supportsHttpsTrafficOnly: true
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
  }
}

// Consumption (Y1) hosting plan — Windows, free tier eligible
resource hostingPlan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: hostingPlanName
  location: location
  sku: {
    name: 'Y1'
    tier: 'Dynamic'
  }
  kind: 'functionapp'
  properties: {
    reserved: false
  }
}

var connAuth = '${sqlConnBase}Database=vue_demo_auth;'
var connTasks = '${sqlConnBase}Database=vue_demo_tasks;'
var connOrders = '${sqlConnBase}Database=vue_demo_orders;'

// Function App
resource functionApp 'Microsoft.Web/sites@2024-04-01' = {
  name: functionAppName
  location: location
  kind: 'functionapp'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: hostingPlan.id
    siteConfig: {
      netFrameworkVersion: 'v8.0'
      alwaysOn: false
      http20Enabled: true
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      appSettings: [
        {
          name: 'AzureWebJobsStorage'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storage.name};AccountKey=${storage.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}'
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet-isolated'
        }
        {
          name: 'ServiceBus__ConnectionString'
          value: sbConnStr
        }
        {
          name: 'ConnectionStrings__Auth'
          value: connAuth
        }
        {
          name: 'ConnectionStrings__Tasks'
          value: connTasks
        }
        {
          name: 'ConnectionStrings__Orders'
          value: connOrders
        }
        {
          name: 'Auth__Authority'
          value: authAuthority
        }
        {
          name: 'Auth__Audience'
          value: 'api'
        }
      ]
    }
    httpsOnly: true
  }
}

output functionAppName string = functionApp.name
output functionAppDefaultHostName string = functionApp.properties.defaultHostName
output functionAppUrl string = 'https://${functionApp.properties.defaultHostName}'
