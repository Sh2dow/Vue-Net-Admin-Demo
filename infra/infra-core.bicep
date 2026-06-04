/// Core infrastructure (no container apps) вЂ” deployed before building images
param location string = resourceGroup().location
@secure()
param adminPassword string

targetScope = 'resourceGroup'

var namePrefix = 'vueadmin'
var uniqueSuffix = uniqueString(resourceGroup().id)
var pgAdminUser = 'vueadmin'
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

resource pgStorageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: '${namePrefix}${uniqueSuffix}st'
  location: location
  sku: { name: 'Standard_LRS' }
  kind: 'StorageV2'
}

resource pgFileService 'Microsoft.Storage/storageAccounts/fileServices@2023-01-01' = {
  name: 'default'
  parent: pgStorageAccount
}

resource pgFileShare 'Microsoft.Storage/storageAccounts/fileServices/shares@2023-01-01' = {
  name: 'pgdata'
  parent: pgFileService
}

resource pgEnvironmentStorage 'Microsoft.App/managedEnvironments/storages@2024-03-01' = {
  name: 'pgdata'
  parent: containerAppsEnvironment
  properties: {
    azureFile: {
      accountName: pgStorageAccount.name
      shareName: pgFileShare.name
      accessMode: 'ReadWrite'
      accountKey: pgStorageAccount.listKeys().keys[0].value
    }
  }
}

// ============================================================
// 3. PostgreSQL Container
// ============================================================
resource postgresqlContainer 'Microsoft.App/containerApps@2024-03-01' = {
  name: 'postgresql-${uniqueSuffix}'
  location: location
  dependsOn: [ pgEnvironmentStorage ]
  properties: {
    managedEnvironmentId: containerAppsEnvironment.id
    configuration: {
      ingress: {
        transport: 'Tcp'
        external: false
        targetPort: 5432
        traffic: [
          {
            latestRevision: true
            weight: 100
          }
        ]
      }
      activeRevisionsMode: 'Single'
    }
    template: {
      scale: { minReplicas: 1, maxReplicas: 1 }
      containers: [
        {
          name: 'postgresql'
          image: 'docker.io/library/postgres:16-alpine'
          resources: { cpu: json('0.75'), memory: '1.5Gi' }
          env: [
            {
              name: 'POSTGRES_USER'
              value: pgAdminUser
            }
            {
              name: 'POSTGRES_PASSWORD'
              value: adminPassword
            }
            {
              name: 'POSTGRES_DB'
              value: 'vue_demo_auth'
            }
          ]
          probes: [
            {
              type: 'Liveness'
              tcpSocket: { port: 5432 }
              initialDelaySeconds: 20
              periodSeconds: 30
            }
            {
              type: 'Readiness'
              tcpSocket: { port: 5432 }
              initialDelaySeconds: 10
              periodSeconds: 15
            }
          ]
        }
      ]
    }
  }
}

var pgInternalHost = 'postgresql-${uniqueSuffix}'
var pgConnBase = 'Host=${pgInternalHost};Port=5432;Username=${pgAdminUser};Password=${adminPassword}'

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
output pgFqdn string = pgInternalHost
output containerAppsEnvId string = containerAppsEnvironment.id
output containerAppsEnvName string = containerAppsEnvName
output sbConnectionString string = sbConnStr
output serviceBusName string = serviceBusNamespace.name
output pgConnBase string = pgConnBase
output envDomain string = containerAppsEnvironment.properties.defaultDomain
