#!/usr/bin/env bash
set -euo pipefail

REGION="${AWS_REGION:-eu-central-1}"
APP_NAME="${APP_NAME:-keycloak-demo}"
ENVIRONMENT="${ENVIRONMENT:-dev}"
PARAM_PATH="${PARAM_PATH:-/$APP_NAME/$ENVIRONMENT}"
DB_ID="${DB_ID:-$APP_NAME}"
LOCAL_FALLBACK_ENV_FILE="${LOCAL_FALLBACK_ENV_FILE:-.env.local}"
GENERATED_ENV_FILE="${GENERATED_ENV_FILE:-.env.runtime}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
GENERATED_ENV_PATH="$REPO_ROOT/$GENERATED_ENV_FILE"
LOCAL_FALLBACK_ENV_PATH="$REPO_ROOT/$LOCAL_FALLBACK_ENV_FILE"

require() {
  command -v "$1" >/dev/null 2>&1 || {
    echo "Missing dependency: $1" >&2
    exit 1
  }
}

log() {
  echo "[run-compose] $*"
}

cleanup() {
  rm -f "$GENERATED_ENV_PATH"
}

trap cleanup EXIT

mask_env_output() {
  sed -E \
    -e 's/(PASSWORD=).*/\1****/g' \
    -e 's/(Password=)[^;]*/\1****/g' \
    -e 's/(SECRET=).*/\1****/g' \
    -e 's/(TOKEN=).*/\1****/g' \
    -e 's/(KEY=).*/\1****/g'
}

get_public_ip() {
  local token

  token="$(curl -fsX PUT "http://169.254.169.254/latest/api/token" \
    -H "X-aws-ec2-metadata-token-ttl-seconds: 21600" 2>/dev/null || true)"

  if [ -n "$token" ]; then
    curl -fs -H "X-aws-ec2-metadata-token: $token" \
      "http://169.254.169.254/latest/meta-data/public-ipv4" 2>/dev/null || true
  else
    curl -fs "http://169.254.169.254/latest/meta-data/public-ipv4" 2>/dev/null || true
  fi
}

fetch_ssm_page() {
  local path="$1"
  local next_token=""

  if [ "$#" -ge 2 ]; then
    next_token="$2"
  else
    next_token=""
  fi

  if [ -n "$next_token" ]; then
    aws ssm get-parameters-by-path \
      --region "$REGION" \
      --path "$path" \
      --recursive \
      --with-decryption \
      --output json \
      --starting-token "$next_token"
  else
    aws ssm get-parameters-by-path \
      --region "$REGION" \
      --path "$path" \
      --recursive \
      --with-decryption \
      --output json
  fi
}

try_write_env_from_ssm() {
  local path="$1"

  : > "$GENERATED_ENV_PATH"

  local next_token=""
  local pages=0

  while :; do
    local json
    if ! json="$(fetch_ssm_page "$path" "$next_token" 2>/dev/null)"; then
      return 1
    fi

    echo "$json" | jq -r '
      .Parameters[]
      | [.Name, .Value]
      | @tsv
    ' | while IFS=$'\t' read -r name value; do
      case "$name" in
        "$path/rds/keycloak-username")
          printf 'KEYCLOAK_DB_USERNAME=%s\n' "$value" >> "$GENERATED_ENV_PATH"
          ;;
        "$path/rds/keycloak-password")
          printf 'KEYCLOAK_DB_PASSWORD=%s\n' "$value" >> "$GENERATED_ENV_PATH"
          ;;
        "$path/rds/auth-username")
          printf 'AUTH_DB_USERNAME=%s\n' "$value" >> "$GENERATED_ENV_PATH"
          ;;
        "$path/rds/auth-password")
          printf 'AUTH_DB_PASSWORD=%s\n' "$value" >> "$GENERATED_ENV_PATH"
          ;;
        "$path/rds/app-username")
          printf 'APP_DB_USERNAME=%s\n' "$value" >> "$GENERATED_ENV_PATH"
          ;;
        "$path/rds/app-password")
          printf 'APP_DB_PASSWORD=%s\n' "$value" >> "$GENERATED_ENV_PATH"
          ;;
        "$path/rds/master-username")
          printf 'RDS_MASTER_USERNAME=%s\n' "$value" >> "$GENERATED_ENV_PATH"
          ;;
        "$path/rds/master-password")
          printf 'RDS_MASTER_PASSWORD=%s\n' "$value" >> "$GENERATED_ENV_PATH"
          ;;
        "$path/rds/db-name-keycloak")
          printf 'RDS_KEYCLOAK_DB=%s\n' "$value" >> "$GENERATED_ENV_PATH"
          ;;
        "$path/rds/db-name-auth")
          printf 'RDS_AUTH_DB=%s\n' "$value" >> "$GENERATED_ENV_PATH"
          ;;
        "$path/rds/db-name-app")
          printf 'RDS_APP_DB=%s\n' "$value" >> "$GENERATED_ENV_PATH"
          ;;
        "$path/keycloak/admin-password")
          printf 'KEYCLOAK_ADMIN_PASSWORD=%s\n' "$value" >> "$GENERATED_ENV_PATH"
          ;;
      esac
    done

    next_token="$(echo "$json" | jq -r '.NextToken // empty')"
    pages=$((pages + 1))

    [ -z "$next_token" ] && break
  done

  if [ ! -s "$GENERATED_ENV_PATH" ]; then
    return 1
  fi

  log "Fetched SSM parameters from $path ($pages page(s))"
  return 0
}

write_env_from_ssm() {
  local primary_path="$PARAM_PATH"
  local fallback_path="/$APP_NAME"

  if try_write_env_from_ssm "$primary_path"; then
    PARAM_PATH="$primary_path"
    return 0
  fi

  if [ "$primary_path" != "$fallback_path" ] && try_write_env_from_ssm "$fallback_path"; then
    log "Primary path $primary_path unavailable, fell back to $fallback_path"
    PARAM_PATH="$fallback_path"
    return 0
  fi

  return 1
}

append_or_replace_env() {
  local key="$1"
  local value="$2"

  if grep -q "^${key}=" "$GENERATED_ENV_PATH" 2>/dev/null; then
    awk -v k="$key" -v v="$value" '
      BEGIN { replaced = 0 }
      $0 ~ "^" k "=" {
        print k "=" v
        replaced = 1
        next
      }
      { print }
      END {
        if (replaced == 0) {
          print k "=" v
        }
      }
    ' "$GENERATED_ENV_PATH" > "${GENERATED_ENV_PATH}.tmp"
    mv "${GENERATED_ENV_PATH}.tmp" "$GENERATED_ENV_PATH"
  else
    printf '%s=%s\n' "$key" "$value" >> "$GENERATED_ENV_PATH"
  fi
}

build_database_urls() {
  set +u
  # shellcheck disable=SC1090
  . "$GENERATED_ENV_PATH"
  set -u

  : "${KEYCLOAK_DB_USERNAME:?Missing KEYCLOAK_DB_USERNAME}"
  : "${KEYCLOAK_DB_PASSWORD:?Missing KEYCLOAK_DB_PASSWORD}"
  : "${AUTH_DB_USERNAME:?Missing AUTH_DB_USERNAME}"
  : "${AUTH_DB_PASSWORD:?Missing AUTH_DB_PASSWORD}"
  : "${APP_DB_USERNAME:?Missing APP_DB_USERNAME}"
  : "${APP_DB_PASSWORD:?Missing APP_DB_PASSWORD}"
  : "${RDS_KEYCLOAK_DB:?Missing RDS_KEYCLOAK_DB}"
  : "${RDS_AUTH_DB:?Missing RDS_AUTH_DB}"
  : "${RDS_ENDPOINT:?Missing RDS_ENDPOINT}"

  local port="${RDS_PORT:-5432}"
  local app_db="${RDS_APP_DB:-keycloak_demo_app}"
  local tasks_db="${RDS_TASKS_DB:-${TASKS_DB_NAME:-keycloak_demo_tasks}}"
  local orders_db="${RDS_ORDERS_DB:-${ORDERS_DB_NAME:-keycloak_demo_orders}}"
  local payments_db="${RDS_PAYMENTS_DB:-${PAYMENTS_DB_NAME:-keycloak_demo_payments}}"

  append_or_replace_env "KEYCLOAK_DB_URL" "postgresql://${KEYCLOAK_DB_USERNAME}:${KEYCLOAK_DB_PASSWORD}@${RDS_ENDPOINT}:${port}/${RDS_KEYCLOAK_DB}?sslmode=require"
  append_or_replace_env "AUTH_DB_URL" "postgresql://${AUTH_DB_USERNAME}:${AUTH_DB_PASSWORD}@${RDS_ENDPOINT}:${port}/${RDS_AUTH_DB}?sslmode=require"
  append_or_replace_env "APP_DB_URL" "postgresql://${APP_DB_USERNAME}:${APP_DB_PASSWORD}@${RDS_ENDPOINT}:${port}/${app_db}?sslmode=require"
  append_or_replace_env "DB_HOST" "$RDS_ENDPOINT"
  append_or_replace_env "DB_PORT" "$port"
  append_or_replace_env "RDS_USERNAME" "${RDS_MASTER_USERNAME:-}"
  append_or_replace_env "RDS_PASSWORD" "${RDS_MASTER_PASSWORD:-}"
  append_or_replace_env "KEYCLOAK_DB_NAME" "$RDS_KEYCLOAK_DB"
  append_or_replace_env "AUTH_DB_NAME" "$RDS_AUTH_DB"
  append_or_replace_env "APP_DB_NAME" "$app_db"
  append_or_replace_env "RDS_TASKS_DB" "$tasks_db"
  append_or_replace_env "RDS_ORDERS_DB" "$orders_db"
  append_or_replace_env "RDS_PAYMENTS_DB" "$payments_db"
  append_or_replace_env "AUTH_DB_CONNECTION_STRING" "Host=${RDS_ENDPOINT};Port=${port};Database=${RDS_AUTH_DB};Username=${AUTH_DB_USERNAME};Password=${AUTH_DB_PASSWORD};SSLMode=Require"
  append_or_replace_env "TASKS_DB_CONNECTION_STRING" "Host=${RDS_ENDPOINT};Port=${port};Database=${tasks_db};Username=${APP_DB_USERNAME};Password=${APP_DB_PASSWORD};SSLMode=Require"
  append_or_replace_env "ORDERS_DB_CONNECTION_STRING" "Host=${RDS_ENDPOINT};Port=${port};Database=${orders_db};Username=${APP_DB_USERNAME};Password=${APP_DB_PASSWORD};SSLMode=Require"
  append_or_replace_env "PAYMENTS_DB_CONNECTION_STRING" "Host=${RDS_ENDPOINT};Port=${port};Database=${payments_db};Username=${APP_DB_USERNAME};Password=${APP_DB_PASSWORD};SSLMode=Require"
}

configure_public_urls() {
  set +u
  # shellcheck disable=SC1090
  . "$GENERATED_ENV_PATH"
  set -u

  local self_signed_https="${ENABLE_SELF_SIGNED_HTTPS:-true}"
  local public_host="${PUBLIC_HOST:-$(get_public_ip)}"
  local public_scheme="${PUBLIC_SCHEME:-http}"
  local keycloak_hostname="${KEYCLOAK_PUBLIC_HOSTNAME:-}"
  local app_hostname="${APP_PUBLIC_HOSTNAME:-$public_host}"
  local api_hostname="${API_PUBLIC_HOSTNAME:-$public_host}"
  local keycloak_scheme="${KEYCLOAK_PUBLIC_SCHEME:-$public_scheme}"
  local app_scheme="${APP_PUBLIC_SCHEME:-$public_scheme}"
  local api_scheme="${API_PUBLIC_SCHEME:-$public_scheme}"
  local keycloak_url="${KEYCLOAK_PUBLIC_URL:-}"
  local keycloak_realm_url="${KEYCLOAK_REALM_URL:-}"
  local keycloak_client_url="http://${public_host}:8080"
  local keycloak_proxy_headers="${KEYCLOAK_PROXY_HEADERS:-xforwarded}"
  local keycloak_hostname_strict="${KEYCLOAK_HOSTNAME_STRICT:-false}"
  local app_public_url="${APP_PUBLIC_URL:-${app_scheme}://${app_hostname}}"
  local api_public_url="${API_PUBLIC_URL:-${api_scheme}://${api_hostname}}"

  if [ "$self_signed_https" = "true" ] && [ -z "${PUBLIC_SCHEME:-}" ]; then
    public_scheme="https"
    keycloak_scheme="${KEYCLOAK_PUBLIC_SCHEME:-https}"
    app_scheme="${APP_PUBLIC_SCHEME:-https}"
    api_scheme="${API_PUBLIC_SCHEME:-https}"
    app_public_url="${APP_PUBLIC_URL:-${app_scheme}://${app_hostname}}"
    api_public_url="${API_PUBLIC_URL:-${api_scheme}://${api_hostname}}"
  fi

  if [ "$self_signed_https" = "true" ] && [ -z "$keycloak_hostname" ]; then
    keycloak_hostname="$public_host"
  fi

  if [ -n "$keycloak_hostname" ] && [ -z "$keycloak_url" ]; then
    keycloak_url="${keycloak_scheme}://${keycloak_hostname}"
  fi

  if [ -n "$keycloak_url" ] && [ -z "$keycloak_realm_url" ]; then
    keycloak_realm_url="${keycloak_url}/realms/myrealm"
  elif [ -z "$keycloak_realm_url" ]; then
    keycloak_realm_url="${keycloak_client_url}/realms/myrealm"
  fi

  append_or_replace_env "PUBLIC_HOST" "$public_host"
  append_or_replace_env "PUBLIC_SCHEME" "$public_scheme"
  append_or_replace_env "KEYCLOAK_PUBLIC_HOSTNAME" "$keycloak_hostname"
  append_or_replace_env "KEYCLOAK_PUBLIC_SCHEME" "$keycloak_scheme"
  append_or_replace_env "KEYCLOAK_PUBLIC_URL" "$keycloak_url"
  append_or_replace_env "KEYCLOAK_REALM_URL" "$keycloak_realm_url"
  append_or_replace_env "KEYCLOAK_PROXY_HEADERS" "$keycloak_proxy_headers"
  append_or_replace_env "KEYCLOAK_HOSTNAME_STRICT" "$keycloak_hostname_strict"
  append_or_replace_env "ENABLE_SELF_SIGNED_HTTPS" "$self_signed_https"
  append_or_replace_env "APP_PUBLIC_HOSTNAME" "$app_hostname"
  append_or_replace_env "APP_PUBLIC_SCHEME" "$app_scheme"
  append_or_replace_env "APP_PUBLIC_URL" "$app_public_url"
  append_or_replace_env "API_PUBLIC_HOSTNAME" "$api_hostname"
  append_or_replace_env "API_PUBLIC_SCHEME" "$api_scheme"
  append_or_replace_env "API_PUBLIC_URL" "$api_public_url"
}

load_local_fallback() {
  if [ ! -f "$LOCAL_FALLBACK_ENV_PATH" ]; then
    echo "AWS config unavailable and fallback file not found: $LOCAL_FALLBACK_ENV_PATH" >&2
    exit 1
  fi

  cp "$LOCAL_FALLBACK_ENV_PATH" "$GENERATED_ENV_PATH"
  log "Using local fallback env: $LOCAL_FALLBACK_ENV_FILE"
}

main() {
  require aws
  require jq
  require docker
  require curl

  if docker compose version >/dev/null 2>&1; then
    COMPOSE_CMD=(docker compose)
  else
    echo "Missing Docker Compose v2 plugin. Install Docker from the official Docker repository so 'docker compose' is available." >&2
    exit 1
  fi

  log "Resolving config for app=$APP_NAME env=$ENVIRONMENT region=$REGION"
  log "SSM path: $PARAM_PATH"

  if write_env_from_ssm; then
    local endpoint
    endpoint="$(aws rds describe-db-instances \
      --region "$REGION" \
      --db-instance-identifier "$DB_ID" \
      --query 'DBInstances[0].Endpoint.Address' \
      --output text)"

    if [ -z "$endpoint" ] || [ "$endpoint" = "None" ]; then
      echo "Failed to resolve RDS endpoint for DB instance: $DB_ID" >&2
      exit 1
    fi

    append_or_replace_env "AWS_REGION" "$REGION"
    append_or_replace_env "APP_NAME" "$APP_NAME"
    append_or_replace_env "ENVIRONMENT" "$ENVIRONMENT"
    append_or_replace_env "RDS_ENDPOINT" "$endpoint"
    append_or_replace_env "RDS_PORT" "${RDS_PORT:-5432}"
    append_or_replace_env "KEYCLOAK_START_COMMAND" "${KEYCLOAK_START_COMMAND:-start}"

    build_database_urls
    configure_public_urls
  else
    log "Could not load parameters from AWS SSM path: $PARAM_PATH"
    load_local_fallback
  fi

  chmod 600 "$GENERATED_ENV_PATH"

  log "Generated env file: $GENERATED_ENV_FILE"
  cat "$GENERATED_ENV_PATH" | mask_env_output

  cd "$REPO_ROOT"
  if [ "$#" -eq 0 ]; then
    set -- up -d --build
  fi

  env \
    -u RDS_ENDPOINT \
    -u RDS_USERNAME \
    -u RDS_PASSWORD \
    -u RDS_KEYCLOAK_DB \
    -u RDS_AUTH_DB \
    -u RDS_APP_DB \
    -u RDS_TASKS_DB \
    -u RDS_ORDERS_DB \
    -u RDS_PAYMENTS_DB \
    -u KEYCLOAK_ADMIN_PASSWORD \
    -u KEYCLOAK_DB_USERNAME \
    -u KEYCLOAK_DB_PASSWORD \
    -u AUTH_DB_USERNAME \
    -u AUTH_DB_PASSWORD \
    -u APP_DB_USERNAME \
    -u APP_DB_PASSWORD \
    -u AUTH_DB_CONNECTION_STRING \
    -u TASKS_DB_CONNECTION_STRING \
    -u ORDERS_DB_CONNECTION_STRING \
    -u PAYMENTS_DB_CONNECTION_STRING \
    -u PUBLIC_HOST \
    -u PUBLIC_SCHEME \
    -u ENABLE_SELF_SIGNED_HTTPS \
    -u KEYCLOAK_PUBLIC_HOSTNAME \
    -u KEYCLOAK_PUBLIC_SCHEME \
    -u KEYCLOAK_PUBLIC_URL \
    -u KEYCLOAK_REALM_URL \
    -u APP_PUBLIC_HOSTNAME \
    -u APP_PUBLIC_SCHEME \
    -u APP_PUBLIC_URL \
    -u API_PUBLIC_HOSTNAME \
    -u API_PUBLIC_SCHEME \
    -u API_PUBLIC_URL \
    "${COMPOSE_CMD[@]}" --env-file "$GENERATED_ENV_PATH" "$@"
}

main "$@"
