/// Core infrastructure (no container apps) вЂ” deployed before building images
param location string = resourceGroup().location
@secure()
param adminPassword string

targetScope = 'resourceGroup'

var namePrefix = 'vueadmin'
var uniqueSuffix = uniqueString(resourceGroup().id)
var sqlAdminUser = 'vueadmin'
var kvUniqueSuffix = uniqueString(resourceGroup().id, subscription().subscriptionId)

// ============================================================
// 1. Azure Container Registry
// ============================================================
resource acr 'Microsoft.ContainerRegistry/registries@2023-01-01-preview' = {
  name: '${namePrefix}${uniqueSuffix}acr'
  location: location
  sku: { name: 'Basic' }
  properties: {
    adminUserEnabled: true
  }
}

var acrLoginServer = '${acr.name}.azurecr.io'

// ============================================================
// 1.5. User-Assigned Identity for ACR pull (avoids RBAC race)
// ============================================================
param acrPullRoleId string = '7f951dda-4ed3-4680-a7ca-43fe172d538d'

resource uai 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${namePrefix}${uniqueSuffix}uai'
  location: location
}

resource acrPullUai 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(uai.id, acr.id, acrPullRoleId)
  scope: acr
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', acrPullRoleId)
    principalId: uai.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// ============================================================
// 2. Container Apps Environment + Log Analytics + Storage
// ============================================================
var containerAppsEnvName = '${namePrefix}${uniqueSuffix}env'
var logAnalyticsName = '${namePrefix}${uniqueSuffix}la'

resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: logAnalyticsName
  location: location
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
  }
}

resource containerAppsEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: containerAppsEnvName
  location: location
  properties: {
    zoneRedundant: false
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalyticsWorkspace.properties.customerId
        sharedKey: logAnalyticsWorkspace.listKeys().primarySharedKey
      }
    }
  }
}

// ============================================================
// 3. Azure SQL Server + Databases
// ============================================================
var sqlServerName = '${namePrefix}${uniqueSuffix}sql'

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: sqlServerName
  location: location
  properties: {
    administratorLogin: sqlAdminUser
    administratorLoginPassword: adminPassword
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    restrictOutboundNetworkAccess: 'Disabled'
  }
}

// Allow Azure services (Container Apps, Functions) to access the server
resource sqlAllowAzureServices 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  name: 'AllowAllAzureIps'
  parent: sqlServer
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource sqlDbAuth 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  name: 'vue_demo_auth'
  parent: sqlServer
  location: location
  sku: { name: 'Basic', tier: 'Basic' }
  properties: { collation: 'SQL_Latin1_General_CP1_CI_AS' }
}

resource sqlDbTasks 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  name: 'vue_demo_tasks'
  parent: sqlServer
  location: location
  sku: { name: 'Basic', tier: 'Basic' }
  properties: { collation: 'SQL_Latin1_General_CP1_CI_AS' }
}

resource sqlDbOrders 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  name: 'vue_demo_orders'
  parent: sqlServer
  location: location
  sku: { name: 'Basic', tier: 'Basic' }
  properties: { collation: 'SQL_Latin1_General_CP1_CI_AS' }
}

resource sqlDbPayments 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  name: 'vue_demo_payments'
  parent: sqlServer
  location: location
  sku: { name: 'Basic', tier: 'Basic' }
  properties: { collation: 'SQL_Latin1_General_CP1_CI_AS' }
}

var sqlServerFqdn = sqlServer.properties.fullyQualifiedDomainName
var sqlConnBase = 'Server=${sqlServerFqdn},1433;User Id=${sqlAdminUser};Password=${adminPassword};Encrypt=True;TrustServerCertificate=False;'

// ============================================================
// 4. Service Bus
// ============================================================
var serviceBusName = '${namePrefix}${uniqueSuffix}sb'

resource serviceBusNamespace 'Microsoft.ServiceBus/namespaces@2022-10-01-preview' = {
  name: serviceBusName
  location: location
  sku: { name: 'Standard', tier: 'Standard' }
  properties: {
    zoneRedundant: false
    publicNetworkAccess: 'Enabled'
  }
}

resource queuePaymentRequests 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = {
  name: 'payments-stub-requests'
  parent: serviceBusNamespace
  properties: { lockDuration: 'PT10S', maxDeliveryCount: 3, defaultMessageTimeToLive: 'P14D', enablePartitioning: false, status: 'Active' }
}

resource queueOrderSaga 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = {
  name: 'orders-saga'
  parent: serviceBusNamespace
  properties: { lockDuration: 'PT10S', maxDeliveryCount: 3, defaultMessageTimeToLive: 'P14D', enablePartitioning: false, status: 'Active' }
}

resource queueOrderExecution 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = {
  name: 'orders-execution-dispatch'
  parent: serviceBusNamespace
  properties: { lockDuration: 'PT10S', maxDeliveryCount: 3, defaultMessageTimeToLive: 'P14D', enablePartitioning: false, status: 'Active' }
}

resource queueDlx 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = {
  name: 'dlx-queue'
  parent: serviceBusNamespace
  properties: { lockDuration: 'PT10S', maxDeliveryCount: 1, defaultMessageTimeToLive: 'P10675199DT2H48M5.4775807S', enablePartitioning: false, status: 'Active' }
}

resource queueDeadLetters 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = {
  name: 'dead-letters'
  parent: serviceBusNamespace
  properties: { lockDuration: 'PT10S', maxDeliveryCount: 1, defaultMessageTimeToLive: 'P10675199DT2H48M5.4775807S', enablePartitioning: false, status: 'Active' }
}

resource queueNotifications 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = {
  name: 'notifications'
  parent: serviceBusNamespace
  properties: { lockDuration: 'PT10S', maxDeliveryCount: 3, defaultMessageTimeToLive: 'P14D', enablePartitioning: false, status: 'Active' }
}

resource serviceBusAuthRule 'Microsoft.ServiceBus/namespaces/AuthorizationRules@2022-10-01-preview' = {
  name: 'RootManageSharedAccessKey'
  parent: serviceBusNamespace
  properties: { rights: ['Listen', 'Send', 'Manage'] }
}

var sbConnStr = serviceBusAuthRule.listKeys().primaryConnectionString

// ============================================================
// 5. Key Vault
// ============================================================
param existingKeyVaultName string = ''

// Use existing if name provided, otherwise create new with unique suffix
var kvName = !empty(existingKeyVaultName) ? existingKeyVaultName : '${namePrefix}kv${kvUniqueSuffix}'

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: kvName
  location: location
  properties: {
    enabledForDeployment: true
    enabledForDiskEncryption: true
    enabledForTemplateDeployment: true
    enableSoftDelete: false        // Allows immediate deletion
    sku: { family: 'A', name: 'standard' }
    accessPolicies: []
    tenantId: subscription().tenantId
  }
}

output keyVaultName string = keyVault.name

// ============================================================
// Outputs
// ============================================================
output uniqueSuffix string = uniqueSuffix
output acrLoginServer string = acrLoginServer
output uaiId string = uai.id
output sqlServerFqdn string = sqlServerFqdn
output sqlConnBase string = sqlConnBase
output containerAppsEnvId string = containerAppsEnvironment.id
output containerAppsEnvName string = containerAppsEnvName
output sbConnectionString string = sbConnStr
output serviceBusName string = serviceBusNamespace.name
output envDomain string = containerAppsEnvironment.properties.defaultDomain
