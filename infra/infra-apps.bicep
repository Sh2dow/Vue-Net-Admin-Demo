/// Container apps — deployed AFTER images are built and pushed to ACR
param location string = resourceGroup().location
param uniqueSuffix string
param containerAppsEnvId string
param acrLoginServer string
param uaiId string
param imageTag string = 'latest'
param pgConnBase string
param sbConnStr string
param envDomain string

var containerCpu = json('0.5')
var containerMemory = '1Gi'

// ACR images
var authApiImage = '${acrLoginServer}/auth-api:${imageTag}'
var tasksApiImage = '${acrLoginServer}/tasks-api:${imageTag}'
var ordersApiImage = '${acrLoginServer}/orders-api:${imageTag}'
var paymentsApiImage = '${acrLoginServer}/payments-api:${imageTag}'
var usersApiImage = '${acrLoginServer}/users-api:${imageTag}'
var apiGatewayImage = '${acrLoginServer}/api-gateway:${imageTag}'
var functionsImage = '${acrLoginServer}/functions:${imageTag}'
var frontendImage = '${acrLoginServer}/frontend:${imageTag}'

// URLs derived from managed environment's default domain (passed from infra-core)
var authDomain = 'auth-api-${uniqueSuffix}.${envDomain}'
var tasksDomain = 'tasks-api-${uniqueSuffix}.${envDomain}'
var ordersDomain = 'orders-api-${uniqueSuffix}.${envDomain}'
var paymentsDomain = 'payments-api-${uniqueSuffix}.${envDomain}'
var usersDomain = 'users-api-${uniqueSuffix}.${envDomain}'
var gatewayDomain = 'api-gateway-${uniqueSuffix}.${envDomain}'
var functionsDomain = 'functions-${uniqueSuffix}.${envDomain}'
var frontendDomain = 'frontend-${uniqueSuffix}.${envDomain}'

var authUrl = 'https://${authDomain}'
var tasksUrl = 'https://${tasksDomain}'
var ordersUrl = 'https://${ordersDomain}'
var paymentsUrl = 'https://${paymentsDomain}'
var usersUrl = 'https://${usersDomain}'
var gatewayUrl = 'https://${gatewayDomain}'
var functionsUrl = 'https://${functionsDomain}'
var frontendUrl = 'https://${frontendDomain}'

var effectiveFrontendApiUrl = gatewayUrl
var effectiveFrontendAuthority = authUrl

// Connection strings
var connAuth = '${pgConnBase};Database=vue_demo_auth'
var connTasks = '${pgConnBase};Database=vue_demo_tasks'
var connOrders = '${pgConnBase};Database=vue_demo_orders'
var connPayments = '${pgConnBase};Database=vue_demo_payments'

// Shared identity + registry config
var sharedIdentity = {
  type: 'UserAssigned'
  userAssignedIdentities: {
    '${uaiId}': {}
  }
}
var sharedRegistry = { server: acrLoginServer, identity: uaiId }

// ============================================================
// Auth API
// ============================================================
resource authApi 'Microsoft.App/containerApps@2024-03-01' = {
  name: 'auth-api-${uniqueSuffix}'
  location: location
  identity: sharedIdentity
  properties: {
    managedEnvironmentId: containerAppsEnvId
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
      registries: [sharedRegistry]
    }
    template: {
      scale: { minReplicas: 1, maxReplicas: 2 }
      containers: [
        {
          name: 'auth-api'
          image: authApiImage
          resources: { cpu: containerCpu, memory: containerMemory }
          env: [
            { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
            { name: 'ASPNETCORE_URLS', value: 'http://+:8080' }
            { name: 'RabbitMq__Enabled', value: 'false' }
            { name: 'ServiceBus__ConnectionString', value: sbConnStr }
            { name: 'ConnectionStrings__Auth', value: connAuth }
            { name: 'Auth__Authority', value: authUrl }
            { name: 'Auth__Issuer', value: authUrl }
            { name: 'CORS__AllowedOrigins', value: frontendUrl }
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

// ============================================================
// Tasks API
// ============================================================
resource tasksApi 'Microsoft.App/containerApps@2024-03-01' = {
  name: 'tasks-api-${uniqueSuffix}'
  location: location
  identity: sharedIdentity
  properties: {
    managedEnvironmentId: containerAppsEnvId
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
      registries: [sharedRegistry]
    }
    template: {
      scale: { minReplicas: 1, maxReplicas: 2 }
      containers: [
        {
          name: 'tasks-api'
          image: tasksApiImage
          resources: { cpu: containerCpu, memory: containerMemory }
          env: [
            { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
            { name: 'ASPNETCORE_URLS', value: 'http://+:8080' }
            { name: 'RabbitMq__Enabled', value: 'false' }
            { name: 'ServiceBus__ConnectionString', value: sbConnStr }
            { name: 'ConnectionStrings__Auth', value: connAuth }
            { name: 'ConnectionStrings__Tasks', value: connTasks }
            { name: 'Auth__Authority', value: authUrl }
            { name: 'CORS__AllowedOrigins', value: frontendUrl }
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

// ============================================================
// Orders API
// ============================================================
resource ordersApi 'Microsoft.App/containerApps@2024-03-01' = {
  name: 'orders-api-${uniqueSuffix}'
  location: location
  identity: sharedIdentity
  properties: {
    managedEnvironmentId: containerAppsEnvId
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
      registries: [sharedRegistry]
    }
    template: {
      scale: { minReplicas: 1, maxReplicas: 2 }
      containers: [
        {
          name: 'orders-api'
          image: ordersApiImage
          resources: { cpu: containerCpu, memory: containerMemory }
          env: [
            { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
            { name: 'ASPNETCORE_URLS', value: 'http://+:8080' }
            { name: 'RabbitMq__Enabled', value: 'false' }
            { name: 'ServiceBus__ConnectionString', value: sbConnStr }
            { name: 'ConnectionStrings__Auth', value: connAuth }
            { name: 'ConnectionStrings__Orders', value: connOrders }
            { name: 'ConnectionStrings__Payments', value: connPayments }
            { name: 'Auth__Authority', value: authUrl }
            { name: 'AuthService__BaseUrl', value: authUrl }
            { name: 'CORS__AllowedOrigins', value: frontendUrl }
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

// ============================================================
// Payments API
// ============================================================
resource paymentsApi 'Microsoft.App/containerApps@2024-03-01' = {
  name: 'payments-api-${uniqueSuffix}'
  location: location
  identity: sharedIdentity
  properties: {
    managedEnvironmentId: containerAppsEnvId
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
      registries: [sharedRegistry]
    }
    template: {
      scale: { minReplicas: 1, maxReplicas: 2 }
      containers: [
        {
          name: 'payments-api'
          image: paymentsApiImage
          resources: { cpu: containerCpu, memory: containerMemory }
          env: [
            { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
            { name: 'ASPNETCORE_URLS', value: 'http://+:8080' }
            { name: 'RabbitMq__Enabled', value: 'false' }
            { name: 'ServiceBus__ConnectionString', value: sbConnStr }
            { name: 'ConnectionStrings__Auth', value: connAuth }
            { name: 'ConnectionStrings__Orders', value: connOrders }
            { name: 'ConnectionStrings__Payments', value: connPayments }
            { name: 'CORS__AllowedOrigins', value: frontendUrl }
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

// ============================================================
// Users API
// ============================================================
resource usersApi 'Microsoft.App/containerApps@2024-03-01' = {
  name: 'users-api-${uniqueSuffix}'
  location: location
  identity: sharedIdentity
  properties: {
    managedEnvironmentId: containerAppsEnvId
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
      registries: [sharedRegistry]
    }
    template: {
      scale: { minReplicas: 1, maxReplicas: 2 }
      containers: [
        {
          name: 'users-api'
          image: usersApiImage
          resources: { cpu: containerCpu, memory: containerMemory }
          env: [
            { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
            { name: 'ASPNETCORE_URLS', value: 'http://+:8080' }
            { name: 'RabbitMq__Enabled', value: 'false' }
            { name: 'ServiceBus__ConnectionString', value: sbConnStr }
            { name: 'ConnectionStrings__Auth', value: connAuth }
            { name: 'ConnectionStrings__Users', value: connAuth }
            { name: 'Auth__Authority', value: authUrl }
            { name: 'CORS__AllowedOrigins', value: frontendUrl }
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

// ============================================================
// API Gateway
// ============================================================
resource apiGateway 'Microsoft.App/containerApps@2024-03-01' = {
  name: 'api-gateway-${uniqueSuffix}'
  location: location
  identity: sharedIdentity
  properties: {
    managedEnvironmentId: containerAppsEnvId
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
      registries: [sharedRegistry]
    }
    template: {
      scale: { minReplicas: 1, maxReplicas: 3 }
      containers: [
        {
          name: 'api-gateway'
          image: apiGatewayImage
          resources: { cpu: containerCpu, memory: containerMemory }
          env: [
            { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
            { name: 'ASPNETCORE_URLS', value: 'http://+:8080' }
            { name: 'RabbitMq__Enabled', value: 'false' }
            { name: 'ServiceBus__ConnectionString', value: sbConnStr }
            { name: 'AuthService__BaseUrl', value: authUrl }
            { name: 'DownstreamServices__UsersBaseUrl', value: usersUrl }
            { name: 'DownstreamServices__TasksBaseUrl', value: tasksUrl }
            { name: 'DownstreamServices__OrdersBaseUrl', value: ordersUrl }
            { name: 'DownstreamServices__PaymentsBaseUrl', value: paymentsUrl }
            { name: 'Auth__Authority', value: authUrl }
            { name: 'CORS__AllowedOrigins', value: frontendUrl }
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

// ============================================================
// Functions
// ============================================================
resource functions 'Microsoft.App/containerApps@2024-03-01' = {
  name: 'functions-${uniqueSuffix}'
  location: location
  identity: sharedIdentity
  properties: {
    managedEnvironmentId: containerAppsEnvId
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
      registries: [sharedRegistry]
    }
    template: {
      scale: { minReplicas: 1, maxReplicas: 3 }
      containers: [
        {
          name: 'functions'
          image: functionsImage
          resources: { cpu: containerCpu, memory: containerMemory }
          env: [
            { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
            { name: 'ASPNETCORE_URLS', value: 'http://+:8080' }
            { name: 'FUNCTIONS_WORKER_RUNTIME', value: 'dotnet-isolated' }
            { name: 'RabbitMq__Enabled', value: 'false' }
            { name: 'ServiceBus__ConnectionString', value: sbConnStr }
            { name: 'ConnectionStrings__Auth', value: connAuth }
            { name: 'ConnectionStrings__Tasks', value: connTasks }
            { name: 'ConnectionStrings__Orders', value: connOrders }
            { name: 'Auth__Authority', value: authUrl }
            { name: 'CORS__AllowedOrigins', value: frontendUrl }
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

// ============================================================
// Frontend
// ============================================================
resource frontend 'Microsoft.App/containerApps@2024-03-01' = {
  name: 'frontend-${uniqueSuffix}'
  location: location
  identity: sharedIdentity
  properties: {
    managedEnvironmentId: containerAppsEnvId
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
      registries: [sharedRegistry]
    }
    template: {
      scale: { minReplicas: 1, maxReplicas: 3 }
      volumes: [
        {
          name: 'frontend-config'
          storageType: 'EmptyDir'
        }
      ]
      initContainers: [
        {
          name: 'config-writer'
          image: 'docker.io/library/alpine:3.20'
          env: [
            { name: 'AUTH_URL', value: effectiveFrontendAuthority }
            { name: 'API_URL', value: effectiveFrontendApiUrl }
          ]
          command: [
            'sh'
            '-c'
            'printf "{\\"authority\\":\\"%s\\",\\"apiUrl\\":\\"%s\\"}" "$AUTH_URL" "$API_URL" > /config/config.json'
          ]
          resources: { cpu: json('0.25'), memory: '0.25Gi' }
          volumeMounts: [
            {
              volumeName: 'frontend-config'
              mountPath: '/config'
            }
          ]
        }
      ]
      containers: [
        {
          name: 'frontend'
          image: frontendImage
          resources: { cpu: containerCpu, memory: containerMemory }
          volumeMounts: [
            {
              volumeName: 'frontend-config'
              mountPath: '/usr/share/nginx/html/config.json'
              subPath: 'config.json'
            }
          ]
          probes: [
            {
              type: 'Liveness'
              httpGet: { path: '/', port: 80 }
              initialDelaySeconds: 10
              periodSeconds: 30
            }
            {
              type: 'Readiness'
              httpGet: { path: '/', port: 80 }
              initialDelaySeconds: 5
              periodSeconds: 15
            }
          ]
        }
      ]
    }
  }
}

// ============================================================
// Outputs
// ============================================================
output authApiUrl string = authUrl
output apiGatewayUrl string = gatewayUrl
output frontendUrl string = frontendUrl
output functionsUrl string = functionsUrl
output tasksApiUrl string = tasksUrl
output ordersApiUrl string = ordersUrl
output paymentsApiUrl string = paymentsUrl
output usersApiUrl string = usersUrl
