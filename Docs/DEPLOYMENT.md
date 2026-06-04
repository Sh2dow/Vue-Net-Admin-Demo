# Deployment Guide

This project supports two deployment tracks: **Azure (Production)** and **Standalone (Dev/AWS)**.

---

## Table of Contents

- [Azure Deployment (Production)](#azure-deployment-production)
  - [Architecture](#azure-architecture)
  - [Prerequisites](#azure-prerequisites)
  - [Step 1: Deploy Infrastructure](#step-1-deploy-infrastructure)
  - [Step 2: Build and Push Images](#step-2-build-and-push-images)
  - [Step 3: Run Migrations](#step-3-run-migrations)
  - [Step 4: Configure Frontend](#step-4-configure-frontend)
  - [Step 5: Verify Deployment](#step-5-verify-deployment)
- [Standalone Deployment (Dev / AWS)](#standalone-deployment-dev--aws)
  - [Prerequisites](#standalone-prerequisites)
  - [Quick Start with Docker Compose](#quick-start-with-docker-compose)
  - [AWS EC2 Deployment](#aws-ec2-deployment)
- [Troubleshooting](#troubleshooting)

---

## Azure Deployment (Production)

### Azure Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                    Azure Container Apps                         │
│  ┌──────────┐ ┌─────────┐ ┌───────┐ ┌────────┐ ┌──────┐         │
│  │ Frontend │→│ API GW  │→│ Tasks │→│Orders  │→│Users │         │
│  └──────────┘ └────┬────┘ └───────┘ └────┬───┘ └──────┘         │
│                    │                     │                      │
│                    ▼                     ▼                      │
│  ┌────────────────────┐  ┌──────────────────────────┐           │
│  │ Auth API (Keycloak)│  │  Payments API            │           │
│  └────────────────────┘  └──────────────────────────┘           │
│                                                                 │
│  ┌──────────────────┐  ┌──────────────┐                         │
│  │ PostgreSQL (16)  │  │ Service Bus  │                         │
│  └──────────────────┘  └──────────────┘                         │
└─────────────────────────────────────────────────────────────────┘
         ▲                         ▲
         │                         │
    ┌────┴────┐              ┌─────┴───┐
    │  ACR    │              │Key Vault│
    └─────────┘              └─────────┘
```

**Infrastructure components:**
- **Azure Container Registry (ACR)** — Docker image storage
- **Azure Container Apps (ACA)** — Serverless container hosting (7 apps)
- **PostgreSQL 16** — Containerized in ACA (free trial doesn't support Flexible Server)
- **Azure Service Bus** — Message queue (6 queues: payments, orders saga, orders dispatch, DLX, dead-letters, notifications)
- **Azure Key Vault** — Secret management

### Container Apps

| Container App | External | Port | Scale | Purpose |
|---|---|---|---|---|
| `auth-api-{suffix}` | ✅ | 8080 | 0–2 | Identity server (Keycloak) |
| `tasks-api-{suffix}` | ✅ | 8080 | 0–2 | Task management |
| `orders-api-{suffix}` | ✅ | 8080 | 0–2 | Order orchestration + saga |
| `payments-api-{suffix}` | ✅ | 8080 | 0–2 | Payment processing |
| `users-api-{suffix}` | ✅ | 8080 | 0–2 | User CRUD operations |
| `api-gateway-{suffix}` | ✅ | 8080 | 0–3 | YARP reverse proxy + auth guard |
| `frontend-{suffix}` | ✅ | 80 | 1–1 | Vue.js SPA (nginx) |
| `postgresql-{suffix}` | ❌ Internal | 5432 | 1–1 | Database (4 DBs) |

### Azure Prerequisites

- **Azure CLI** (`az`) — [Install](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli)
- **Azure Subscription** with **Owner** or **Contributor** role (required for deploying resources)
- **Docker Desktop** running (for building and pushing images)
- **PowerShell 7+** (deployment scripts)
- **dotnet-ef CLI** (`dotnet tool install --global dotnet-ef`) — for migrations
- **psql** (PostgreSQL client) — for creating databases (optional, script will warn if missing)

### Step 1: Deploy Infrastructure

```powershell
# Login to Azure
az login

# (Optional) Select subscription
az account set --subscription "Your-Subscription-ID"

# Create resource group (if not exists)
az group create `
  --name vue-admin-demo-rg `
  --location eastus

# Deploy Bicep template
.\infra\deploy.ps1
```

The `deploy.ps1` script:
1. Compiles `main.bicep` to JSON
2. Generates a 22-character random password for PostgreSQL
3. Deploys all resources to the specified resource group
4. Saves outputs to `infra/.env.azure` for subsequent scripts
5. Prints all endpoint URLs and credentials

**What gets deployed:**
- Azure Container Registry (`vueadmin{suffix}acr`)
- Container Apps Environment (`vueadmin{suffix}env`)
- 7 Container Apps (Auth, Tasks, Orders, Payments, Users, API Gateway, Frontend)
- PostgreSQL container (internal-only, port 5432)
- Service Bus namespace with 6 queues
- Key Vault
- ACR Pull role assignments (managed identity for each container app)

**Estimated cost:** ~$50-100/month (ACA + Service Bus Standard + ACR Basic)

### Step 2: Build and Push Images

```powershell
.\infra\build-images.ps1
```

This script builds 7 Docker images and pushes them to ACR:

| Image Tag | Dockerfile | Context |
|---|---|---|
| `auth-api` | `backend/backend.Auth.Api/Dockerfile` | `backend/` |
| `tasks-api` | `backend/backend.Tasks.Api/Dockerfile` | `backend/` |
| `orders-api` | `backend/backend.Orders.Api/Dockerfile` | `backend/` |
| `payments-api` | `backend/backend.Payments.Api/Dockerfile` | `backend/` |
| `users-api` | `backend/backend.Users.Api/Dockerfile` | `backend/` |
| `api-gateway` | `backend/backend.Api/Dockerfile` | `backend/` |
| `frontend` | `frontend/Dockerfile` | `frontend/` |

**Important:** All backend Dockerfiles are multi-stage builds:
- **Build stage**: `.NET 10.0 SDK` — restores and publishes
- **Final stage**: `ASP.NET 10.0 runtime` — runs the published binary

The frontend Dockerfile is a two-stage build:
- **Build stage**: `Node 22` — `npm install && npm run build` (with VITE_* env vars)
- **Final stage**: `nginx:1.27-alpine` — serves the built SPA

### Step 3: Run Migrations

```powershell
.\infra\migrate.ps1
```

This script:
1. Creates 4 databases (`vue_demo_auth`, `vue_demo_tasks`, `vue_demo_orders`, `vue_demo_payments`) via `psql`
2. Runs EF Core migrations against each database using the `dotnet-ef` CLI tool

**Database ownership:**
| Database | Primary Context | Used By |
|---|---|---|
| `vue_demo_auth` | `AuthDbContext` | Auth API, Tasks API, Orders API, Payments API, Users API, API Gateway |
| `vue_demo_tasks` | `TasksDbContext` | Tasks API, API Gateway |
| `vue_demo_orders` | `OrdersDbContext` | Orders API, Payments API, API Gateway |
| `vue_demo_payments` | `PaymentsDbContext` | Payments API, Orders API, API Gateway |

> **Note:** The API Gateway reads from multiple databases (Auth, Orders, Payments, Users). In production, consider adding a dedicated `users` database and connection string.

### Step 4: Configure Frontend

The frontend requires two environment variables at build time:

| Variable | Source | Purpose |
|---|---|---|
| `VITE_API_URL` | `api-gateway-{suffix}.{region}.azurecontainerapps.io` | API Gateway endpoint for frontend requests |
| `VITE_AUTHORITY` | `auth-api-{suffix}.{region}.azurecontainerapps.io` | Keycloak OIDC authority URL |

**Option A: Rebuild with variables (recommended)**

```powershell
# Build frontend with production URLs
$API_URL = "https://api-gateway-{suffix}.{region}.azurecontainerapps.io"
$AUTHORITY = "https://auth-api-{suffix}.{region}.azurecontainerapps.io"

docker build -t $ACR_LOGIN/frontend:latest `
  --build-arg VITE_API_URL=$API_URL `
  --build-arg VITE_AUTHORITY=$AUTHORITY `
  -f frontend/Dockerfile frontend

docker push $ACR_LOGIN/frontend:latest
```

**Option B: ACA environment variables**

The bicep template accepts `frontendApiUrl` and `frontendAuthority` parameters. However, these are set as runtime env vars, not build-time args. The `nginx.conf` may need to proxy `/api` requests to the gateway URL for this to work.

### Step 5: Verify Deployment

```powershell
# Check all container apps are running
az containerapp list `
  --resource-group vue-admin-demo-rg `
  --query '[].{Name:name, Replicas:properties.template.scale.minReplicas, Revision:properties.latestRevisionName}' `
  --output table

# Check logs for a specific container app
az containerapp logs show `
  --name auth-api-{suffix} `
  --resource-group vue-admin-demo-rg

# Test API Gateway health
curl https://api-gateway-{suffix}.{region}.azurecontainerapps.io/health

# Test frontend
curl https://frontend-{suffix}.{region}.azurecontainerapps.io
```

---

## Standalone Deployment (Dev / AWS)

### Standalone Prerequisites

- **Docker & Docker Compose** (v24+ / v2.23+)
- **Node.js 22+** and **npm** (for frontend dev)
- **.NET 10.0 SDK** (for backend dev)
- **psql** (optional, for database management)

### Quick Start with Docker Compose

```bash
# 1. Copy environment templates
cp .env.template .env
cp backend/IdentityServer4/.env.template backend/IdentityServer4/.env

# 2. (Optional) Edit .env to customize ports, passwords, etc.

# 3. Start all services
docker compose up -d

# 4. Verify
docker compose ps

# 5. View logs
docker compose logs -f backend.auth
```

**Services started:**
| Service | Container | Port | Purpose |
|---|---|---|---|
| PostgreSQL | `postgres` | 5432 | Database |
| Keycloak | `keycloak` | 8080 | Identity provider |
| Redis | `redis` | 6379 | Cache / session store |
| Auth API | `backend.auth` | 5001 | User registration/login |
| Tasks API | `backend.tasks` | 5002 | Task management |
| Frontend | `frontend` | 80 | Vue.js SPA (nginx) |

### AWS EC2 Deployment

The `scripts/deploy.sh` script automates AWS deployment:

```bash
# 1. Configure AWS credentials
aws configure

# 2. Set deployment variables
export AWS_REGION="us-east-1"
export KEYCLOAK_ADMIN="admin"
export KEYCLOAK_ADMIN_PASSWORD="admin"

# 3. Run deployment
./scripts/deploy.sh
```

The script:
1. Launches an EC2 instance (t3.medium, Ubuntu 24.04)
2. Installs Docker, Docker Compose, and NGINX
3. Creates security group (ports 22, 80, 443)
4. Copies project files to the instance
5. Builds and starts all services via Docker Compose
6. Sets up NGINX reverse proxy
7. Configures Keycloak realm and admin user

---

## Troubleshooting

### Azure Deployment

**Container apps show "Provisioning" status**
- Check deployment logs: `az containerapp logs show --name <app> --resource-group <rg>`
- Verify ACR images were pushed: `az acr repository list --name <acr-login>`
- Check managed identity has ACR pull role: `az role assignment list --scope <acr-id>`

**PostgreSQL connection failures**
- The internal hostname is `postgresql-{suffix}` (Container Apps DNS)
- Verify the PostgreSQL container is running: `az containerapp show --name postgresql-{suffix}`
- Check the password in Key Vault matches the bicep parameter

**Frontend can't reach API Gateway**
- Ensure `VITE_API_URL` is set correctly at build time
- Check CORS is enabled on the API Gateway for the frontend URL
- Test directly: `curl https://api-gateway-{suffix}.{region}.azurecontainerapps.io/api/health`

### Docker Compose

**Services fail to start**
```bash
# Check which service failed
docker compose ps

# View logs for a specific service
docker compose logs backend.auth

# Rebuild images (after code changes)
docker compose up -d --build
```

**Database migration errors**
```bash
# Run migrations manually
docker compose exec backend.auth dotnet ef database update
```

**Keycloak not accessible**
- Wait 30-60 seconds for Keycloak to fully start
- Check logs: `docker compose logs keycloak`
- Verify the realm is imported: `docker compose exec keycloak curl http://localhost:8080/realms/vue-demo`

---

## Teardown

### Azure
```powershell
# Delete the entire resource group
az group delete --name vue-admin-demo-rg --yes --no-wait
```

### Docker Compose
```bash
docker compose down -v  # -v removes volumes (deletes data)
```
