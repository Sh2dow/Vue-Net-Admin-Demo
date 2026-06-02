#!/usr/bin/env bash
set -euo pipefail

REGION="${AWS_REGION:-eu-central-1}"
APP_NAME="${APP_NAME:-keycloak-demo}"
BASE_DOMAIN="${BASE_DOMAIN:-}"
KEYCLOAK_PUBLIC_HOSTNAME="${KEYCLOAK_PUBLIC_HOSTNAME:-}"
APP_PUBLIC_HOSTNAME="${APP_PUBLIC_HOSTNAME:-}"
API_PUBLIC_HOSTNAME="${API_PUBLIC_HOSTNAME:-}"
HOSTED_ZONE_ID="${HOSTED_ZONE_ID:-}"
CERT_ARN="${CERT_ARN:-}"
WAIT_FOR_ISSUED="${WAIT_FOR_ISSUED:-true}"
WAIT_TIMEOUT_SECONDS="${WAIT_TIMEOUT_SECONDS:-900}"
POLL_INTERVAL_SECONDS="${POLL_INTERVAL_SECONDS:-15}"

require() {
  command -v "$1" >/dev/null 2>&1 || {
    echo "Missing dependency: $1" >&2
    exit 1
  }
}

log() {
  echo "[request-acm] $*"
}

require aws

if [ -n "$BASE_DOMAIN" ]; then
  KEYCLOAK_PUBLIC_HOSTNAME="${KEYCLOAK_PUBLIC_HOSTNAME:-auth.$BASE_DOMAIN}"
  APP_PUBLIC_HOSTNAME="${APP_PUBLIC_HOSTNAME:-app.$BASE_DOMAIN}"
  API_PUBLIC_HOSTNAME="${API_PUBLIC_HOSTNAME:-api.$BASE_DOMAIN}"
fi

if [ -z "$KEYCLOAK_PUBLIC_HOSTNAME" ] || [ -z "$APP_PUBLIC_HOSTNAME" ] || [ -z "$API_PUBLIC_HOSTNAME" ]; then
  echo "Provide BASE_DOMAIN or all of KEYCLOAK_PUBLIC_HOSTNAME, APP_PUBLIC_HOSTNAME, API_PUBLIC_HOSTNAME." >&2
  exit 1
fi

trim_dot() {
  local value="$1"
  value="${value%.}"
  printf '%s' "$value"
}

discover_hosted_zone_id() {
  local hostname="$1"
  local zones_json zone_id zone_name best_id="" best_len=0

  zones_json="$(aws route53 list-hosted-zones --query 'HostedZones[*].[Id,Name]' --output text)"
  while IFS=$'\t' read -r zone_id zone_name; do
    zone_name="$(trim_dot "$zone_name")"
    zone_id="${zone_id##*/hostedzone/}"
    if [[ "$hostname" == "$zone_name" || "$hostname" == *".$zone_name" ]]; then
      if [ ${#zone_name} -gt $best_len ]; then
        best_len=${#zone_name}
        best_id="$zone_id"
      fi
    fi
  done <<< "$zones_json"

  printf '%s' "$best_id"
}

if [ -z "$HOSTED_ZONE_ID" ]; then
  HOSTED_ZONE_ID="$(discover_hosted_zone_id "$KEYCLOAK_PUBLIC_HOSTNAME")"
fi

if [ -z "$HOSTED_ZONE_ID" ]; then
  echo "Unable to auto-discover HOSTED_ZONE_ID for $KEYCLOAK_PUBLIC_HOSTNAME" >&2
  exit 1
fi

ensure_route53_validation_records() {
  local cert_arn="$1"
  local description_json validation_rows record_name record_type record_value sanitized_value

  description_json="$(aws acm describe-certificate \
    --region "$REGION" \
    --certificate-arn "$cert_arn" \
    --output json)"

  validation_rows="$(printf '%s' "$description_json" | aws --region "$REGION" acm describe-certificate --certificate-arn "$cert_arn" --query 'Certificate.DomainValidationOptions[?ResourceRecord!=null].[ResourceRecord.Name,ResourceRecord.Type,ResourceRecord.Value]' --output text)"

  if [ -z "$validation_rows" ]; then
    return 0
  fi

  while IFS=$'\t' read -r record_name record_type record_value; do
    [ -z "$record_name" ] && continue
    record_name="$(trim_dot "$record_name")"
    sanitized_value="$(trim_dot "$record_value")"
    cat > /tmp/acm-validation.json <<JSON
{
  "Comment": "ACM validation record for $record_name",
  "Changes": [
    {
      "Action": "UPSERT",
      "ResourceRecordSet": {
        "Name": "$record_name",
        "Type": "$record_type",
        "TTL": 300,
        "ResourceRecords": [
          { "Value": "$sanitized_value" }
        ]
      }
    }
  ]
}
JSON

    aws route53 change-resource-record-sets \
      --region "$REGION" \
      --hosted-zone-id "$HOSTED_ZONE_ID" \
      --change-batch file:///tmp/acm-validation.json >/dev/null
  done <<< "$validation_rows"

  rm -f /tmp/acm-validation.json
}

resolve_existing_cert_arn() {
  local domain="$1"
  aws acm list-certificates \
    --region "$REGION" \
    --certificate-statuses ISSUED PENDING_VALIDATION INACTIVE EXPIRED VALIDATION_TIMED_OUT REVOKED FAILED \
    --query "CertificateSummaryList[?DomainName=='$domain'].CertificateArn | [0]" \
    --output text
}

if [ -z "$CERT_ARN" ]; then
  CERT_ARN="$(resolve_existing_cert_arn "$KEYCLOAK_PUBLIC_HOSTNAME")"
  if [ "$CERT_ARN" = "None" ]; then
    CERT_ARN=""
  fi
fi

if [ -z "$CERT_ARN" ]; then
  log "Requesting ACM certificate..."
  CERT_ARN="$(aws acm request-certificate \
    --region "$REGION" \
    --domain-name "$KEYCLOAK_PUBLIC_HOSTNAME" \
    --subject-alternative-names "$APP_PUBLIC_HOSTNAME" "$API_PUBLIC_HOSTNAME" \
    --validation-method DNS \
    --query 'CertificateArn' \
    --output text)"
fi

log "Ensuring Route53 validation records..."
ensure_route53_validation_records "$CERT_ARN"

if [ "$WAIT_FOR_ISSUED" = "true" ]; then
  log "Waiting for ACM certificate to be issued..."
  start_time="$(date +%s)"
  while true; do
    cert_status="$(aws acm describe-certificate \
      --region "$REGION" \
      --certificate-arn "$CERT_ARN" \
      --query 'Certificate.Status' \
      --output text)"

    if [ "$cert_status" = "ISSUED" ]; then
      break
    fi

    if [ "$cert_status" = "FAILED" ] || [ "$cert_status" = "VALIDATION_TIMED_OUT" ]; then
      echo "Certificate request failed with status: $cert_status" >&2
      exit 1
    fi

    now="$(date +%s)"
    if [ $((now - start_time)) -ge "$WAIT_TIMEOUT_SECONDS" ]; then
      echo "Timed out waiting for certificate issuance." >&2
      exit 1
    fi

    sleep "$POLL_INTERVAL_SECONDS"
  done
fi

echo "CERT_ARN=$CERT_ARN"
echo "HOSTED_ZONE_ID=$HOSTED_ZONE_ID"
echo "KEYCLOAK_PUBLIC_HOSTNAME=$KEYCLOAK_PUBLIC_HOSTNAME"
echo "APP_PUBLIC_HOSTNAME=$APP_PUBLIC_HOSTNAME"
echo "API_PUBLIC_HOSTNAME=$API_PUBLIC_HOSTNAME"
