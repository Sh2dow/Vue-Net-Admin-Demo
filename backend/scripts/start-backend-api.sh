#!/usr/bin/env bash
set -euo pipefail

required_vars=(
  AWS_REGION
  RDS_ENDPOINT
  RDS_USERNAME
  RDS_APP_DB
  RDS_MASTER_SECRET_ARN
)

for name in "${required_vars[@]}"; do
  if [ -z "${!name:-}" ]; then
    echo "Missing required environment variable: $name" >&2
    exit 1
  fi
done

RDS_PASSWORD="$(aws secretsmanager get-secret-value \
  --region "$AWS_REGION" \
  --secret-id "$RDS_MASTER_SECRET_ARN" \
  --query 'SecretString' \
  --output text | sed -n 's/.*"password"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p')"

export ConnectionStrings__Default="Host=${RDS_ENDPOINT};Port=5432;Database=${RDS_APP_DB};Username=${RDS_USERNAME};Password=${RDS_PASSWORD}"

exec dotnet backend.Api.dll
