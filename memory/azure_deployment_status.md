# Azure Deployment Status

## Deployment Summary
- **Resource Group**: `vue-admin-rg` (eastus2)
- **Subscription**: `1ada15a6-3265-45aa-a934-50fcfde21390`
- **Unique Suffix**: `3l73qz2t2b7bo`
- **Commit**: `efa8cb7` — Azure Container Apps deployment with infrastructure fixes

## Infrastructure Deployed
- [x] Azure Container Registry (ACR): `vueadmin3l73qz2t2b7boacr.azurecr.io`
- [x] Managed Environment: `vueadmin3l73qz2t2b7boenv`
- [x] Key Vault: `vueadminkv3l73qz2t2b7bo`
- [x] Service Bus: `vueadmin3l73qz2t2b7bosb` (6 queues)
- [x] PostgreSQL Container (internal FQDN, transport: 'Tcp')
- [x] 4 PostgreSQL databases created (vue_demo_auth, vue_demo_orders, vue_demo_payments, vue_demo_tasks)
- [x] DB migrations applied and seed data populated

## Container Apps Running (Port 8080)
| Service | FQDN | Status |
|---------|------|--------|
| auth-api | `https://auth-api-3l73qz2t2b7bo.agreeablesky-42707e9e.eastus2.azurecontainerapps.io` | ✅ Running (/health returns 200) |
| tasks-api | `https://tasks-api-3l73qz2t2b7bo.agreeablesky-42707e9e.eastus2.azurecontainerapps.io` | ✅ Running (Swagger accessible) |
| orders-api | `https://orders-api-3l73qz2t2b7bo.agreeablesky-42707e9e.eastus2.azurecontainerapps.io` | ✅ Running (Swagger accessible) |
| payments-api | `https://payments-api-3l73qz2t2b7bo.agreeablesky-42707e9e.eastus2.azurecontainerapps.io` | ✅ Running (Swagger accessible) |
| users-api | `https://users-api-3l73qz2t2b7bo.agreeablesky-42707e9e.eastus2.azurecontainerapps.io` | ✅ Running (Swagger accessible) |
| api-gateway | `https://api-gateway-3l73qz2t2b7bo.agreeablesky-42707e9e.eastus2.azurecontainerapps.io` | ✅ Running |
| frontend | `https://frontend-3l73qz2t2b7bo.agreeablesky-42707e9e.eastus2.azurecontainerapps.io` | ✅ Running (v3 with build-time env vars) |

## Known Issues
- Auth API `/connect/token` endpoint returns "This server only accepts HTTPS requests" — likely forwarded headers issue with Container Apps ingress proxy
  - Health endpoint works fine on HTTPS
  - Swagger UI works fine on other APIs
  - This may require configuring `ForwardedHeaders` middleware in auth-api

## Bugs Fixed
1. **PostgreSQL TCP Transport**: Changed from `Auto` to `Tcp` (Auto defaults to HTTP which breaks PG protocol)
2. **Auth API SeedData**: Wrapped StartAsync in try/catch to prevent startup failure on DB issues
3. **Tasks/Payments API**: Added try/catch around EnsureCreatedAsync for resilient DB initialization
4. **Frontend Vite Env Vars**: Switched from runtime ENV to .env.production file approach for build-time env var baking
5. **Frontend nginx.conf**: Removed proxy_pass to non-existent backend_api host

## Frontend Configuration (v3)
- `VITE_AUTHORITY`: `https://auth-api-3l73qz2t2b7bo.eastus2.azurecontainerapps.io`
- `VITE_API_URL`: `https://api-gateway-3l73qz2t2b7bo.eastus2.azurecontainerapps.io`
- Both URLs verified baked into bundle `index-C6PEaNX3.js`
