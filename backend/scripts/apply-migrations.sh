#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/.."

echo "Applying auth database migrations..."
export ConnectionStrings__Auth="${AUTH_DB_CONNECTION_STRING:?AUTH_DB_CONNECTION_STRING is required}"
dotnet ef database update --project backend.Domain/backend.Domain.csproj --context AuthDbContext

echo "Applying tasks database migrations..."
export ConnectionStrings__Default="${TASKS_DB_CONNECTION_STRING:?TASKS_DB_CONNECTION_STRING is required}"
dotnet ef database update --project backend.Domain/backend.Domain.csproj --context TasksDbContext

echo "Applying orders database migrations..."
export ConnectionStrings__Default="${ORDERS_DB_CONNECTION_STRING:?ORDERS_DB_CONNECTION_STRING is required}"
dotnet ef database update --project backend.Domain/backend.Domain.csproj --context OrdersDbContext

echo "Applying payments database migrations..."
export ConnectionStrings__Default="${PAYMENTS_DB_CONNECTION_STRING:?PAYMENTS_DB_CONNECTION_STRING is required}"
dotnet ef database update --project backend.Domain/backend.Domain.csproj --context PaymentsDbContext
