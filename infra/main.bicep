/// <summary>
/// Azure infrastructure for Vue-Net-Admin-Demo microservices
/// Deploys: ACR, PostgreSQL, Key Vault, Service Bus, Container Apps Environment
/// </summary>
param location string = resourceGroup().location
@secure()
param adminPassword string
param imageTag string = 'latest'

targetScope = 'resourceGroup'

// ============================================================
// Naming conventions
// ============================================================
var namePrefix = 'vueadmin'
var uniqueSuffix = uniqueString(resourceGroup().id)
var pgAdminUser = 'vueadmin'
var containerCpu = json('0.5')
var containerMemory = '1Gi'

// ============================================================
// 1. Azure Container Registry (ACR)
// ============================================================
resource acr 'Microsoft.ContainerRegistry/registries@2023-01-01-preview' = {
  name: '${namePrefix}${uniqueSuffix}acr'
  location: location
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: true
  }
}

var acrLoginServer = '${acr.name}.azurecr.io'

// ACR images
var apiGatewayImage = '${acrLoginServer}/api-gateway:${imageTag}'
var authApiImage = '${acrLoginServer}/auth-api:${imageTag}'
var tasksApiImage = '${acrLoginServer}/tasks-api:${imageTag}'
var ordersApiImage = '${acrLoginServer}/orders-api:${imageTag}'
var paymentsApiImage = '${acrLoginServer}/payments-api:${imageTag}'
var usersApiImage = '${acrLoginServer}/users-api:${imageTag}'
var frontendImage = '${acrLoginServer}/frontend:${imageTag}'

output acrLoginServerOutput string = acrLoginServer

// ============================================================
// 2. PostgreSQL container (runs in Container Apps environment)
// Free trial doesn't support Flexible Server — use containerized PG instead
// ============================================================
resource postgresqlContainer 'Microsoft.App/containerApps@2024-03-01' = {
  name: 'postgresql-${uniqueSuffix}'
  location: location
  dependsOn: [
    pgEnvironmentStorage
  ]
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
      scale: {
        minReplicas: 1
        maxReplicas: 1
      }
      volumes: [
        {
          name: 'pgdata'
          storageType: 'AzureFile'
          storageName: 'pgdata'
        }
      ]
      containers: [
        {
          name: 'postgresql'
          image: 'docker.io/library/postgres:16-alpine'
          resources: {
            cpu: json('0.75')
            memory: '1.5Gi'
          }
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
            {
              name: 'PGDATA'
              value: '/var/lib/postgresql/data/pgdata'
            }
          ]
          volumeMounts: [
            {
              volumeName: 'pgdata'
              mountPath: '/var/lib/postgresql/data'
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

// Connection string — Container Apps internal DNS
var pgInternalHost = 'postgresql-${uniqueSuffix}'
var pgConnBase = 'Host=${pgInternalHost};Port=5432;Username=${pgAdminUser};Password=${adminPassword}'

output pgFqdnOutput string = pgInternalHost

// ============================================================
// 3. Azure Container Apps Environment
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

// Storage account + file share for PostgreSQL persistent data
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

// Register storage as a volume source in the Container Apps Environment
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

output pgStorageAccountName string = pgStorageAccount.name

// ============================================================
// 4. Azure Service Bus (replaces RabbitMQ)
// ============================================================
var serviceBusName = '${namePrefix}${uniqueSuffix}sb'

resource serviceBusNamespace 'Microsoft.ServiceBus/namespaces@2022-10-01-preview' = {
  name: serviceBusName
  location: location
  sku: {
    name: 'Standard'
    tier: 'Standard'
  }
  properties: {
    zoneRedundant: false
    publicNetworkAccess: 'Enabled'
  }
}

// Queues
resource queuePaymentRequests 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = {
  name: 'payments-stub-requests'
  parent: serviceBusNamespace
  properties: {
    lockDuration: 'PT10S'
    maxDeliveryCount: 3
    defaultMessageTimeToLive: 'P14D'
    enablePartitioning: false
    status: 'Active'
  }
}

resource queueOrderSaga 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = {
  name: 'orders-saga'
  parent: serviceBusNamespace
  properties: {
    lockDuration: 'PT10S'
    maxDeliveryCount: 3
    defaultMessageTimeToLive: 'P14D'
    enablePartitioning: false
    status: 'Active'
  }
}

resource queueOrderExecution 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = {
  name: 'orders-execution-dispatch'
  parent: serviceBusNamespace
  properties: {
    lockDuration: 'PT10S'
    maxDeliveryCount: 3
    defaultMessageTimeToLive: 'P14D'
    enablePartitioning: false
    status: 'Active'
  }
}

resource queueDlx 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = {
  name: 'dlx-queue'
  parent: serviceBusNamespace
  properties: {
    lockDuration: 'PT10S'
    maxDeliveryCount: 1
    defaultMessageTimeToLive: 'P10675199DT2H48M5.4775807S'
    enablePartitioning: false
    status: 'Active'
  }
}

resource queueDeadLetters 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = {
  name: 'dead-letters'
  parent: serviceBusNamespace
  properties: {
    lockDuration: 'PT10S'
    maxDeliveryCount: 1
    defaultMessageTimeToLive: 'P10675199DT2H48M5.4775807S'
    enablePartitioning: false
    status: 'Active'
  }
}

resource queueNotifications 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = {
  name: 'notifications'
  parent: serviceBusNamespace
  properties: {
    lockDuration: 'PT10S'
    maxDeliveryCount: 3
    defaultMessageTimeToLive: 'P14D'
    enablePartitioning: false
    status: 'Active'
  }
}

// Authorization rule for connection string — listKeys() must be called on the rule, not the namespace
resource serviceBusAuthRule 'Microsoft.ServiceBus/namespaces/AuthorizationRules@2022-10-01-preview' = {
  name: 'RootManageSharedAccessKey'
  parent: serviceBusNamespace
  properties: {
    rights: [
      'Listen'
      'Send'
      'Manage'
    ]
  }
}

var sbConnStr = serviceBusAuthRule.listKeys().primaryConnectionString

output serviceBusName string = serviceBusNamespace.name
output serviceBusHostName string = 'https://${serviceBusName}.servicebus.windows.net/'
// NOTE: sbConnStr uses listKeys() on the AuthorizationRule (not namespace) — this is the correct pattern.
// AllowAll firewall is kept for dev/demo — Container Apps need to reach PostgreSQL externally.

output containerAppsEnvId string = containerAppsEnvironment.id
output containerAppsEnvName string = containerAppsEnvironment.name

// ============================================================
// Environment variables (shared across all container apps)
// ============================================================
var authUrl = 'https://auth-api-${uniqueSuffix}.${location}.azurecontainerapps.io'
var tasksUrl = 'https://tasks-api-${uniqueSuffix}.${location}.azurecontainerapps.io'
var ordersUrl = 'https://orders-api-${uniqueSuffix}.${location}.azurecontainerapps.io'
var paymentsUrl = 'https://payments-api-${uniqueSuffix}.${location}.azurecontainerapps.io'
var usersUrl = 'https://users-api-${uniqueSuffix}.${location}.azurecontainerapps.io'
var gatewayUrl = 'https://api-gateway-${uniqueSuffix}.${location}.azurecontainerapps.io'
var frontendUrl = 'https://frontend-${uniqueSuffix}.${location}.azurecontainerapps.io'

param frontendApiUrl string = ''
param frontendAuthority string = ''

var effectiveFrontendApiUrl = !empty(frontendApiUrl) ? frontendApiUrl : gatewayUrl
var effectiveFrontendAuthority = !empty(frontendAuthority) ? frontendAuthority : authUrl

var connAuth = '${pgConnBase};Database=vue_demo_auth'
var connTasks = '${pgConnBase};Database=vue_demo_tasks'
var connOrders = '${pgConnBase};Database=vue_demo_orders'
var connPayments = '${pgConnBase};Database=vue_demo_payments'

// ============================================================
// 6. Container Apps
// ============================================================

// --- Auth API ---
resource authApi 'Microsoft.App/containerApps@2024-03-01' = {
  name: 'auth-api-${uniqueSuffix}'
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    managedEnvironmentId: containerAppsEnvironment.id
    configuration: {
      ingress: {
        transport: 'Auto'
        external: true
        targetPort: 8080
        allowInsecure: false
        traffic: [
          {
            latestRevision: true
            weight: 100
          }
        ]
      }
      activeRevisionsMode: 'Single'
      registries: [
        {
          server: acrLoginServer
          identity: 'system'
        }
      ]
    }
    template: {
      scale: {
        minReplicas: 1
        maxReplicas: 2
      }
      containers: [
        {
          name: 'auth-api'
          image: authApiImage
          resources: {
            cpu: containerCpu
            memory: containerMemory
          }
          env: [
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: 'Production'
            }
            {
              name: 'ASPNETCORE_URLS'
              value: 'http://+:8080'
            }
            {
              name: 'RabbitMq__Enabled'
              value: 'false'
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
              name: 'Auth__Authority'
              value: authUrl
            }
            {
              name: 'Auth__Issuer'
              value: authUrl
            }
            {
              name: 'CORS__AllowedOrigins'
              value: frontendUrl
            }
          ]
          probes: [
            {
              type: 'Liveness'
              httpGet: { path: '/health', port: 8080 }
              initialDelaySeconds: 10
              periodSeconds: 30
            }
            {
              type: 'Readiness'
              httpGet: { path: '/health', port: 8080 }
              initialDelaySeconds: 5
              periodSeconds: 15
            }
          ]
        }
      ]
    }
  }
}

// --- Tasks API ---
resource tasksApi 'Microsoft.App/containerApps@2024-03-01' = {
  name: 'tasks-api-${uniqueSuffix}'
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    managedEnvironmentId: containerAppsEnvironment.id
    configuration: {
      ingress: {
        transport: 'Auto'
        external: true
        targetPort: 8080
        allowInsecure: false
        traffic: [
          {
            latestRevision: true
            weight: 100
          }
        ]
      }
      activeRevisionsMode: 'Single'
      registries: [
        {
          server: acrLoginServer
          identity: 'system'
        }
      ]
    }
    template: {
      scale: {
        minReplicas: 0
        maxReplicas: 2
      }
      containers: [
        {
          name: 'tasks-api'
          image: tasksApiImage
          resources: {
            cpu: containerCpu
            memory: containerMemory
          }
          env: [
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: 'Production'
            }
            {
              name: 'ASPNETCORE_URLS'
              value: 'http://+:8080'
            }
            {
              name: 'RabbitMq__Enabled'
              value: 'false'
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
              name: 'Auth__Authority'
              value: authUrl
            }
            {
              name: 'CORS__AllowedOrigins'
              value: frontendUrl
            }
          ]
          probes: [
            {
              type: 'Liveness'
              tcpSocket: { port: 8080 }
              initialDelaySeconds: 10
              periodSeconds: 30
            }
            {
              type: 'Readiness'
              tcpSocket: { port: 8080 }
              initialDelaySeconds: 5
              periodSeconds: 15
            }
          ]
        }
      ]
    }
  }
}

// --- Orders API ---
resource ordersApi 'Microsoft.App/containerApps@2024-03-01' = {
  name: 'orders-api-${uniqueSuffix}'
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    managedEnvironmentId: containerAppsEnvironment.id
    configuration: {
      ingress: {
        transport: 'Auto'
        external: true
        targetPort: 8080
        allowInsecure: false
        traffic: [
          {
            latestRevision: true
            weight: 100
          }
        ]
      }
      activeRevisionsMode: 'Single'
      registries: [
        {
          server: acrLoginServer
          identity: 'system'
        }
      ]
    }
    template: {
      scale: {
        minReplicas: 0
        maxReplicas: 2
      }
      containers: [
        {
          name: 'orders-api'
          image: ordersApiImage
          resources: {
            cpu: containerCpu
            memory: containerMemory
          }
          env: [
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: 'Production'
            }
            {
              name: 'ASPNETCORE_URLS'
              value: 'http://+:8080'
            }
            {
              name: 'RabbitMq__Enabled'
              value: 'false'
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
              name: 'ConnectionStrings__Orders'
              value: connOrders
            }
            {
              name: 'ConnectionStrings__Payments'
              value: connPayments
            }
            {
              name: 'Auth__Authority'
              value: authUrl
            }
            {
              name: 'AuthService__BaseUrl'
              value: authUrl
            }
            {
              name: 'CORS__AllowedOrigins'
              value: frontendUrl
            }
          ]
        }
      ]
    }
  }
}

// --- Payments API ---
resource paymentsApi 'Microsoft.App/containerApps@2024-03-01' = {
  name: 'payments-api-${uniqueSuffix}'
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    managedEnvironmentId: containerAppsEnvironment.id
    configuration: {
      ingress: {
        transport: 'Auto'
        external: true
        targetPort: 8080
        allowInsecure: false
        traffic: [
          {
            latestRevision: true
            weight: 100
          }
        ]
      }
      activeRevisionsMode: 'Single'
      registries: [
        {
          server: acrLoginServer
          identity: 'system'
        }
      ]
    }
    template: {
      scale: {
        minReplicas: 0
        maxReplicas: 2
      }
      containers: [
        {
          name: 'payments-api'
          image: paymentsApiImage
          resources: {
            cpu: containerCpu
            memory: containerMemory
          }
          env: [
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: 'Production'
            }
            {
              name: 'ASPNETCORE_URLS'
              value: 'http://+:8080'
            }
            {
              name: 'RabbitMq__Enabled'
              value: 'false'
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
              name: 'ConnectionStrings__Orders'
              value: connOrders
            }
            {
              name: 'ConnectionStrings__Payments'
              value: connPayments
            }
            {
              name: 'CORS__AllowedOrigins'
              value: frontendUrl
            }
          ]
        }
      ]
    }
  }
}

// --- Users API ---
resource usersApi 'Microsoft.App/containerApps@2024-03-01' = {
  name: 'users-api-${uniqueSuffix}'
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    managedEnvironmentId: containerAppsEnvironment.id
    configuration: {
      ingress: {
        transport: 'Auto'
        external: true
        targetPort: 8080
        allowInsecure: false
        traffic: [
          {
            latestRevision: true
            weight: 100
          }
        ]
      }
      activeRevisionsMode: 'Single'
      registries: [
        {
          server: acrLoginServer
          identity: 'system'
        }
      ]
    }
    template: {
      scale: {
        minReplicas: 0
        maxReplicas: 2
      }
      containers: [
        {
          name: 'users-api'
          image: usersApiImage
          resources: {
            cpu: containerCpu
            memory: containerMemory
          }
          env: [
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: 'Production'
            }
            {
              name: 'ASPNETCORE_URLS'
              value: 'http://+:8080'
            }
            {
              name: 'RabbitMq__Enabled'
              value: 'false'
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
              name: 'ConnectionStrings__Orders'
              value: connOrders
            }
            {
              name: 'Auth__Authority'
              value: authUrl
            }
            {
              name: 'CORS__AllowedOrigins'
              value: frontendUrl
            }
          ]
        }
      ]
    }
  }
}

// --- API Gateway ---
resource apiGateway 'Microsoft.App/containerApps@2024-03-01' = {
  name: 'api-gateway-${uniqueSuffix}'
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    managedEnvironmentId: containerAppsEnvironment.id
    configuration: {
      ingress: {
        transport: 'Auto'
        external: true
        targetPort: 8080
        allowInsecure: false
        traffic: [
          {
            latestRevision: true
            weight: 100
          }
        ]
      }
      activeRevisionsMode: 'Single'
      registries: [
        {
          server: acrLoginServer
          identity: 'system'
        }
      ]
    }
    template: {
      scale: {
        minReplicas: 0
        maxReplicas: 3
      }
      containers: [
        {
          name: 'api-gateway'
          image: apiGatewayImage
          resources: {
            cpu: containerCpu
            memory: containerMemory
          }
          env: [
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: 'Production'
            }
            {
              name: 'ASPNETCORE_URLS'
              value: 'http://+:8080'
            }
            {
              name: 'RabbitMq__Enabled'
              value: 'false'
            }
            {
              name: 'ServiceBus__ConnectionString'
              value: sbConnStr
            }
            {
              name: 'AuthService__BaseUrl'
              value: authUrl
            }
            {
              name: 'DownstreamServices__UsersBaseUrl'
              value: usersUrl
            }
            {
              name: 'DownstreamServices__TasksBaseUrl'
              value: tasksUrl
            }
            {
              name: 'DownstreamServices__OrdersBaseUrl'
              value: ordersUrl
            }
            {
              name: 'DownstreamServices__PaymentsBaseUrl'
              value: paymentsUrl
            }
            {
              name: 'Auth__Authority'
              value: authUrl
            }
          ]
        }
      ]
    }
  }
}

// --- Frontend (Vue.js + nginx) ---
resource frontend 'Microsoft.App/containerApps@2024-03-01' = {
  name: 'frontend-${uniqueSuffix}'
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    managedEnvironmentId: containerAppsEnvironment.id
    configuration: {
      ingress: {
        transport: 'Auto'
        external: true
        targetPort: 80
        allowInsecure: false
        traffic: [
          {
            latestRevision: true
            weight: 100
          }
        ]
      }
      activeRevisionsMode: 'Single'
      registries: [
        {
          server: acrLoginServer
          identity: 'system'
        }
      ]
    }
    template: {
      scale: {
        minReplicas: 1
        maxReplicas: 1
      }
      containers: [
        {
          name: 'frontend'
          image: frontendImage
          resources: {
            cpu: containerCpu
            memory: containerMemory
          }
          env: [
            {
              name: 'VITE_API_URL'
              value: effectiveFrontendApiUrl
            }
            {
              name: 'VITE_AUTHORITY'
              value: effectiveFrontendAuthority
            }
          ]
        }
      ]
    }
  }
}

// ============================================================
// ACR Pull Role Assignments (Container Apps → ACR)
// ============================================================
param acrPullRoleId string = '7f951dda-4ed3-4680-a7ca-43fe172d538d'

resource acrPullAuth 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(authApi.id, acr.id, acrPullRoleId)
  scope: acr
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', acrPullRoleId)
    principalId: authApi.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

resource acrPullTasks 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(tasksApi.id, acr.id, acrPullRoleId)
  scope: acr
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', acrPullRoleId)
    principalId: tasksApi.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

resource acrPullOrders 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(ordersApi.id, acr.id, acrPullRoleId)
  scope: acr
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', acrPullRoleId)
    principalId: ordersApi.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

resource acrPullPayments 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(paymentsApi.id, acr.id, acrPullRoleId)
  scope: acr
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', acrPullRoleId)
    principalId: paymentsApi.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

resource acrPullUsers 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(usersApi.id, acr.id, acrPullRoleId)
  scope: acr
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', acrPullRoleId)
    principalId: usersApi.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

resource acrPullGateway 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(apiGateway.id, acr.id, acrPullRoleId)
  scope: acr
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', acrPullRoleId)
    principalId: apiGateway.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

resource acrPullFrontend 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(frontend.id, acr.id, acrPullRoleId)
  scope: acr
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', acrPullRoleId)
    principalId: frontend.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// ============================================================
// Outputs
// ============================================================
output authApiUrl string = authUrl
output apiGatewayUrl string = gatewayUrl
output frontendUrl string = frontendUrl
output tasksApiUrl string = tasksUrl
output ordersApiUrl string = ordersUrl
output paymentsApiUrl string = paymentsUrl
output usersApiUrl string = usersUrl
output uniqueSuffix string = uniqueSuffix
output sbConnectionString string = sbConnStr
