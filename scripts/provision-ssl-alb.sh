#!/usr/bin/env bash
set -euo pipefail

REGION="${AWS_REGION:-eu-central-1}"
APP_NAME="${APP_NAME:-keycloak-demo}"
INSTANCE_NAME="${INSTANCE_NAME:-$APP_NAME}"
CERT_ARN="${CERT_ARN:-}"
KEYCLOAK_PUBLIC_HOSTNAME="${KEYCLOAK_PUBLIC_HOSTNAME:-}"
APP_PUBLIC_HOSTNAME="${APP_PUBLIC_HOSTNAME:-}"
API_PUBLIC_HOSTNAME="${API_PUBLIC_HOSTNAME:-}"
HOSTED_ZONE_ID="${HOSTED_ZONE_ID:-}"
ALB_NAME="${ALB_NAME:-$APP_NAME-alb}"
ALB_SG_NAME="${ALB_SG_NAME:-$APP_NAME-alb-sg}"
EC2_SG_NAME="${EC2_SG_NAME:-$APP_NAME-sg}"
VPC_ID="${VPC_ID:-}"
LOCK_DOWN_PUBLIC_PORTS="${LOCK_DOWN_PUBLIC_PORTS:-false}"
APPLY_RUNTIME="${APPLY_RUNTIME:-false}"

require() {
  command -v "$1" >/dev/null 2>&1 || {
    echo "Missing dependency: $1" >&2
    exit 1
  }
}

log() {
  echo "[provision-ssl] $*"
}

require aws

if [ -z "$CERT_ARN" ]; then
  echo "CERT_ARN is required." >&2
  exit 1
fi

if [ -z "$KEYCLOAK_PUBLIC_HOSTNAME" ]; then
  echo "KEYCLOAK_PUBLIC_HOSTNAME is required." >&2
  exit 1
fi

if [ -z "$APP_PUBLIC_HOSTNAME" ]; then
  echo "APP_PUBLIC_HOSTNAME is required." >&2
  exit 1
fi

if [ -z "$API_PUBLIC_HOSTNAME" ]; then
  echo "API_PUBLIC_HOSTNAME is required." >&2
  exit 1
fi

if [ -z "$VPC_ID" ]; then
  VPC_ID="$(aws ec2 describe-vpcs \
    --region "$REGION" \
    --filters Name=isDefault,Values=true \
    --query "Vpcs[0].VpcId" \
    --output text)"
fi

if [ -z "$VPC_ID" ] || [ "$VPC_ID" = "None" ]; then
  echo "Unable to resolve VPC_ID." >&2
  exit 1
fi

INSTANCE_ID="$(aws ec2 describe-instances \
  --region "$REGION" \
  --filters "Name=tag:Name,Values=$INSTANCE_NAME" "Name=instance-state-name,Values=running" \
  --query "Reservations[0].Instances[0].InstanceId" \
  --output text)"

if [ -z "$INSTANCE_ID" ] || [ "$INSTANCE_ID" = "None" ]; then
  echo "Unable to resolve running EC2 instance with Name tag '$INSTANCE_NAME'." >&2
  exit 1
fi

EC2_SG_ID="$(aws ec2 describe-security-groups \
  --region "$REGION" \
  --filters "Name=group-name,Values=$EC2_SG_NAME" "Name=vpc-id,Values=$VPC_ID" \
  --query "SecurityGroups[0].GroupId" \
  --output text)"

if [ -z "$EC2_SG_ID" ] || [ "$EC2_SG_ID" = "None" ]; then
  echo "Unable to resolve EC2 security group '$EC2_SG_NAME'." >&2
  exit 1
fi

SUBNET_IDS=($(aws ec2 describe-subnets \
  --region "$REGION" \
  --filters Name=vpc-id,Values="$VPC_ID" \
  --query "Subnets[].SubnetId" \
  --output text))

if [ ${#SUBNET_IDS[@]} -lt 2 ]; then
  echo "ALB requires at least two subnets." >&2
  exit 1
fi

ALB_SG_ID="$(aws ec2 describe-security-groups \
  --region "$REGION" \
  --filters "Name=group-name,Values=$ALB_SG_NAME" "Name=vpc-id,Values=$VPC_ID" \
  --query "SecurityGroups[0].GroupId" \
  --output text)"

if [ -z "$ALB_SG_ID" ] || [ "$ALB_SG_ID" = "None" ]; then
  log "Creating ALB security group..."
  ALB_SG_ID="$(aws ec2 create-security-group \
    --region "$REGION" \
    --group-name "$ALB_SG_NAME" \
    --description "$APP_NAME ALB security group" \
    --vpc-id "$VPC_ID" \
    --query "GroupId" \
    --output text)"
fi

for port in 80 443; do
  aws ec2 authorize-security-group-ingress \
    --region "$REGION" \
    --group-id "$ALB_SG_ID" \
    --protocol tcp \
    --port "$port" \
    --cidr 0.0.0.0/0 2>/dev/null || true
done

for port in 8080 5000 5173; do
  aws ec2 authorize-security-group-ingress \
    --region "$REGION" \
    --group-id "$EC2_SG_ID" \
    --protocol tcp \
    --port "$port" \
    --source-group "$ALB_SG_ID" 2>/dev/null || true
done

if [ "$LOCK_DOWN_PUBLIC_PORTS" = "true" ]; then
  for port in 8080 5000 5001 5173; do
    aws ec2 revoke-security-group-ingress \
      --region "$REGION" \
      --group-id "$EC2_SG_ID" \
      --protocol tcp \
      --port "$port" \
      --cidr 0.0.0.0/0 2>/dev/null || true
  done
fi

ALB_ARN="$(aws elbv2 describe-load-balancers \
  --region "$REGION" \
  --names "$ALB_NAME" \
  --query "LoadBalancers[0].LoadBalancerArn" \
  --output text 2>/dev/null || true)"

if [ -z "$ALB_ARN" ] || [ "$ALB_ARN" = "None" ]; then
  log "Creating ALB..."
  ALB_ARN="$(aws elbv2 create-load-balancer \
    --region "$REGION" \
    --name "$ALB_NAME" \
    --subnets "${SUBNET_IDS[@]}" \
    --security-groups "$ALB_SG_ID" \
    --scheme internet-facing \
    --type application \
    --query "LoadBalancers[0].LoadBalancerArn" \
    --output text)"
fi

ALB_DNS_NAME="$(aws elbv2 describe-load-balancers \
  --region "$REGION" \
  --load-balancer-arns "$ALB_ARN" \
  --query "LoadBalancers[0].DNSName" \
  --output text)"

ALB_ZONE_ID="$(aws elbv2 describe-load-balancers \
  --region "$REGION" \
  --load-balancer-arns "$ALB_ARN" \
  --query "LoadBalancers[0].CanonicalHostedZoneId" \
  --output text)"

create_target_group() {
  local name="$1"
  local port="$2"
  local health_path="$3"
  local matcher="$4"
  local arn

  arn="$(aws elbv2 describe-target-groups \
    --region "$REGION" \
    --names "$name" \
    --query "TargetGroups[0].TargetGroupArn" \
    --output text 2>/dev/null || true)"

  if [ -z "$arn" ] || [ "$arn" = "None" ]; then
    arn="$(aws elbv2 create-target-group \
      --region "$REGION" \
      --name "$name" \
      --protocol HTTP \
      --port "$port" \
      --target-type instance \
      --vpc-id "$VPC_ID" \
      --health-check-protocol HTTP \
      --health-check-path "$health_path" \
      --matcher "HttpCode=$matcher" \
      --query "TargetGroups[0].TargetGroupArn" \
      --output text)"
  fi

  aws elbv2 register-targets \
    --region "$REGION" \
    --target-group-arn "$arn" \
    --targets "Id=$INSTANCE_ID,Port=$port" >/dev/null

  echo "$arn"
}

KEYCLOAK_TG_ARN="$(create_target_group "$APP_NAME-kc-tg" 8080 "/realms/master" "200-399")"
FRONTEND_TG_ARN="$(create_target_group "$APP_NAME-web-tg" 5173 "/" "200-399")"
API_TG_ARN="$(create_target_group "$APP_NAME-api-tg" 5000 "/" "200-499")"

HTTP_LISTENER_ARN="$(aws elbv2 describe-listeners \
  --region "$REGION" \
  --load-balancer-arn "$ALB_ARN" \
  --query "Listeners[?Port==\`80\`].ListenerArn | [0]" \
  --output text)"

if [ -z "$HTTP_LISTENER_ARN" ] || [ "$HTTP_LISTENER_ARN" = "None" ]; then
  aws elbv2 create-listener \
    --region "$REGION" \
    --load-balancer-arn "$ALB_ARN" \
    --protocol HTTP \
    --port 80 \
    --default-actions Type=redirect,RedirectConfig="{Protocol=HTTPS,Port=443,StatusCode=HTTP_301}" >/dev/null
fi

HTTPS_LISTENER_ARN="$(aws elbv2 describe-listeners \
  --region "$REGION" \
  --load-balancer-arn "$ALB_ARN" \
  --query "Listeners[?Port==\`443\`].ListenerArn | [0]" \
  --output text)"

if [ -z "$HTTPS_LISTENER_ARN" ] || [ "$HTTPS_LISTENER_ARN" = "None" ]; then
  HTTPS_LISTENER_ARN="$(aws elbv2 create-listener \
    --region "$REGION" \
    --load-balancer-arn "$ALB_ARN" \
    --protocol HTTPS \
    --port 443 \
    --certificates "CertificateArn=$CERT_ARN" \
    --default-actions "Type=forward,TargetGroupArn=$FRONTEND_TG_ARN" \
    --query "Listeners[0].ListenerArn" \
    --output text)"
fi

upsert_listener_rule() {
  local priority="$1"
  local host="$2"
  local tg_arn="$3"
  local existing_rule_arn

  existing_rule_arn="$(aws elbv2 describe-rules \
    --region "$REGION" \
    --listener-arn "$HTTPS_LISTENER_ARN" \
    --query "Rules[?Conditions[?Field=='host-header' && contains(join(',',HostHeaderConfig.Values), '$host')]].RuleArn | [0]" \
    --output text)"

  if [ -n "$existing_rule_arn" ] && [ "$existing_rule_arn" != "None" ]; then
    aws elbv2 modify-rule \
      --region "$REGION" \
      --rule-arn "$existing_rule_arn" \
      --conditions "Field=host-header,HostHeaderConfig={Values=[$host]}" \
      --actions "Type=forward,TargetGroupArn=$tg_arn" >/dev/null
  else
    aws elbv2 create-rule \
      --region "$REGION" \
      --listener-arn "$HTTPS_LISTENER_ARN" \
      --priority "$priority" \
      --conditions "Field=host-header,HostHeaderConfig={Values=[$host]}" \
      --actions "Type=forward,TargetGroupArn=$tg_arn" >/dev/null
  fi
}

upsert_listener_rule 10 "$KEYCLOAK_PUBLIC_HOSTNAME" "$KEYCLOAK_TG_ARN"
upsert_listener_rule 20 "$API_PUBLIC_HOSTNAME" "$API_TG_ARN"
upsert_listener_rule 30 "$APP_PUBLIC_HOSTNAME" "$FRONTEND_TG_ARN"

if [ -n "$HOSTED_ZONE_ID" ]; then
  log "Upserting Route53 alias records..."
  for host in "$KEYCLOAK_PUBLIC_HOSTNAME" "$APP_PUBLIC_HOSTNAME" "$API_PUBLIC_HOSTNAME"; do
    cat > /tmp/route53-alias.json <<JSON
{
  "Comment": "Alias $host to $ALB_DNS_NAME",
  "Changes": [
    {
      "Action": "UPSERT",
      "ResourceRecordSet": {
        "Name": "$host",
        "Type": "A",
        "AliasTarget": {
          "HostedZoneId": "$ALB_ZONE_ID",
          "DNSName": "$ALB_DNS_NAME",
          "EvaluateTargetHealth": false
        }
      }
    }
  ]
}
JSON

    aws route53 change-resource-record-sets \
      --region "$REGION" \
      --hosted-zone-id "$HOSTED_ZONE_ID" \
      --change-batch file:///tmp/route53-alias.json >/dev/null
  done

  rm -f /tmp/route53-alias.json
fi

if [ "$APPLY_RUNTIME" = "true" ]; then
  log "Triggering remote runtime reconfiguration over SSM..."
  aws ssm send-command \
    --region "$REGION" \
    --instance-ids "$INSTANCE_ID" \
    --document-name "AWS-RunShellScript" \
    --comment "Apply ALB HTTPS public hostnames for $APP_NAME" \
    --parameters "commands=[
      \"cd /home/ubuntu/project\",
      \"sudo -u ubuntu env PUBLIC_SCHEME=https KEYCLOAK_PUBLIC_HOSTNAME=$KEYCLOAK_PUBLIC_HOSTNAME APP_PUBLIC_HOSTNAME=$APP_PUBLIC_HOSTNAME API_PUBLIC_HOSTNAME=$API_PUBLIC_HOSTNAME bash ./scripts/run-compose-from-aws.sh up -d --build --force-recreate\"
    ]" >/dev/null
fi

echo
echo "ALB DNS: $ALB_DNS_NAME"
echo "Keycloak URL: https://$KEYCLOAK_PUBLIC_HOSTNAME"
echo "Frontend URL: https://$APP_PUBLIC_HOSTNAME"
echo "API URL: https://$API_PUBLIC_HOSTNAME"
echo
echo "If you are not using APPLY_RUNTIME=true, apply the runtime hostnames manually on the instance with:"
echo "PUBLIC_SCHEME=https KEYCLOAK_PUBLIC_HOSTNAME=$KEYCLOAK_PUBLIC_HOSTNAME APP_PUBLIC_HOSTNAME=$APP_PUBLIC_HOSTNAME API_PUBLIC_HOSTNAME=$API_PUBLIC_HOSTNAME ./scripts/run-compose-from-aws.sh up -d --build --force-recreate"
