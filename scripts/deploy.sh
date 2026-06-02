#!/bin/bash

set -euo pipefail

REGION="eu-central-1"
KEY_NAME="dev"
REPO_URL="https://github.com/Sh2dow/Keycloak-UI-Demo.git"
INSTANCE_NAME="keycloak-demo"
DB_ID="keycloak-demo"
DB_USERNAME_PARAMETER_NAME="/keycloak-demo/rds/master-username"
RDS_PASSWORD_PARAMETER_NAME="/keycloak-demo/rds/master-password"
KEYCLOAK_DB_USERNAME_PARAMETER_NAME="/keycloak-demo/rds/keycloak-username"
KEYCLOAK_DB_PASSWORD_PARAMETER_NAME="/keycloak-demo/rds/keycloak-password"
AUTH_DB_USERNAME_PARAMETER_NAME="/keycloak-demo/rds/auth-username"
AUTH_DB_PASSWORD_PARAMETER_NAME="/keycloak-demo/rds/auth-password"
APP_DB_USERNAME_PARAMETER_NAME="/keycloak-demo/rds/app-username"
APP_DB_PASSWORD_PARAMETER_NAME="/keycloak-demo/rds/app-password"
KEYCLOAK_DB_PARAMETER_NAME="/keycloak-demo/rds/db-name-keycloak"
AUTH_DB_PARAMETER_NAME="/keycloak-demo/rds/db-name-auth"
TASKS_DB_NAME="${TASKS_DB_NAME:-keycloak_demo_tasks}"
ORDERS_DB_NAME="${ORDERS_DB_NAME:-keycloak_demo_orders}"
PAYMENTS_DB_NAME="${PAYMENTS_DB_NAME:-keycloak_demo_payments}"
KEYCLOAK_ADMIN_PASSWORD_PARAMETER_NAME="/keycloak-demo/keycloak/admin-password"
ROLE_NAME="keycloak-demo-ec2-role"
INSTANCE_PROFILE_NAME="keycloak-demo-ec2-profile"
SSM_POLICY_NAME="keycloak-demo-ssm-parameter-read"
DB_SUBNET_GROUP_NAME="keycloak-demo-db-subnet-group"
CERT_ARN="${CERT_ARN:-}"
BASE_DOMAIN="${BASE_DOMAIN:-}"
KEYCLOAK_PUBLIC_HOSTNAME="${KEYCLOAK_PUBLIC_HOSTNAME:-}"
APP_PUBLIC_HOSTNAME="${APP_PUBLIC_HOSTNAME:-}"
API_PUBLIC_HOSTNAME="${API_PUBLIC_HOSTNAME:-}"
HOSTED_ZONE_ID="${HOSTED_ZONE_ID:-}"
LOCK_DOWN_PUBLIC_PORTS="${LOCK_DOWN_PUBLIC_PORTS:-false}"
APPLY_SSL_RUNTIME="${APPLY_SSL_RUNTIME:-false}"
ENABLE_SELF_SIGNED_HTTPS="${ENABLE_SELF_SIGNED_HTTPS:-true}"

if [ -n "$BASE_DOMAIN" ]; then
  KEYCLOAK_PUBLIC_HOSTNAME="${KEYCLOAK_PUBLIC_HOSTNAME:-auth.$BASE_DOMAIN}"
  APP_PUBLIC_HOSTNAME="${APP_PUBLIC_HOSTNAME:-app.$BASE_DOMAIN}"
  API_PUBLIC_HOSTNAME="${API_PUBLIC_HOSTNAME:-api.$BASE_DOMAIN}"
fi

if [ -z "$CERT_ARN" ] && [ -n "$KEYCLOAK_PUBLIC_HOSTNAME" ] && [ -n "$APP_PUBLIC_HOSTNAME" ] && [ -n "$API_PUBLIC_HOSTNAME" ]; then
  echo "Resolving ACM certificate and hosted zone..."
  ACM_OUTPUT="$(
    AWS_REGION="$REGION" \
    APP_NAME="$INSTANCE_NAME" \
    BASE_DOMAIN="$BASE_DOMAIN" \
    KEYCLOAK_PUBLIC_HOSTNAME="$KEYCLOAK_PUBLIC_HOSTNAME" \
    APP_PUBLIC_HOSTNAME="$APP_PUBLIC_HOSTNAME" \
    API_PUBLIC_HOSTNAME="$API_PUBLIC_HOSTNAME" \
    HOSTED_ZONE_ID="$HOSTED_ZONE_ID" \
    CERT_ARN="$CERT_ARN" \
    ./scripts/request-acm-certificate.sh
  )"
  echo "$ACM_OUTPUT"

  CERT_ARN="$(printf '%s\n' "$ACM_OUTPUT" | awk -F= '/^CERT_ARN=/{print $2}' | tail -n 1)"
  HOSTED_ZONE_ID="$(printf '%s\n' "$ACM_OUTPUT" | awk -F= '/^HOSTED_ZONE_ID=/{print $2}' | tail -n 1)"
  KEYCLOAK_PUBLIC_HOSTNAME="$(printf '%s\n' "$ACM_OUTPUT" | awk -F= '/^KEYCLOAK_PUBLIC_HOSTNAME=/{print $2}' | tail -n 1)"
  APP_PUBLIC_HOSTNAME="$(printf '%s\n' "$ACM_OUTPUT" | awk -F= '/^APP_PUBLIC_HOSTNAME=/{print $2}' | tail -n 1)"
  API_PUBLIC_HOSTNAME="$(printf '%s\n' "$ACM_OUTPUT" | awk -F= '/^API_PUBLIC_HOSTNAME=/{print $2}' | tail -n 1)"
fi

echo "Finding latest Ubuntu 22.04 AMI..."

AMI_ID=$(aws ec2 describe-images \
  --region "$REGION" \
  --owners 099720109477 \
  --filters "Name=name,Values=ubuntu/images/hvm-ssd/ubuntu-jammy-22.04-amd64-server-*" \
  --query 'Images | sort_by(@, &CreationDate)[-1].ImageId' \
  --output text)

echo "Using AMI: $AMI_ID"

ACCOUNT_ID=$(aws sts get-caller-identity \
  --query "Account" \
  --output text)

echo "AWS account: $ACCOUNT_ID"

get_required_parameter() {
  local parameter_name="$1"
  local with_decryption="$2"

  if [ "$with_decryption" = "true" ]; then
    aws ssm get-parameter \
      --region "$REGION" \
      --name "$parameter_name" \
      --with-decryption \
      --query "Parameter.Value" \
      --output text
  else
    aws ssm get-parameter \
      --region "$REGION" \
      --name "$parameter_name" \
      --query "Parameter.Value" \
      --output text
  fi
}

echo "Resolving database and Keycloak parameters from SSM Parameter Store..."

DB_USERNAME=$(get_required_parameter "$DB_USERNAME_PARAMETER_NAME" "false")
KEYCLOAK_DB_USERNAME=$(get_required_parameter "$KEYCLOAK_DB_USERNAME_PARAMETER_NAME" "false")
KEYCLOAK_DB_PASSWORD=$(get_required_parameter "$KEYCLOAK_DB_PASSWORD_PARAMETER_NAME" "true")
AUTH_DB_USERNAME=$(get_required_parameter "$AUTH_DB_USERNAME_PARAMETER_NAME" "false")
AUTH_DB_PASSWORD=$(get_required_parameter "$AUTH_DB_PASSWORD_PARAMETER_NAME" "true")
APP_DB_USERNAME=$(get_required_parameter "$APP_DB_USERNAME_PARAMETER_NAME" "false")
APP_DB_PASSWORD=$(get_required_parameter "$APP_DB_PASSWORD_PARAMETER_NAME" "true")
KEYCLOAK_DB_NAME=$(get_required_parameter "$KEYCLOAK_DB_PARAMETER_NAME" "false")
AUTH_DB_NAME=$(get_required_parameter "$AUTH_DB_PARAMETER_NAME" "false")
KEYCLOAK_ADMIN_PASSWORD=$(get_required_parameter "$KEYCLOAK_ADMIN_PASSWORD_PARAMETER_NAME" "true")
echo "Detecting default VPC..."

VPC_ID=$(aws ec2 describe-vpcs \
  --region "$REGION" \
  --filters Name=isDefault,Values=true \
  --query "Vpcs[0].VpcId" \
  --output text)

echo "Default VPC: $VPC_ID"

echo "Resolving default VPC subnets for RDS..."

SUBNET_IDS=($(aws ec2 describe-subnets \
  --region "$REGION" \
  --filters Name=vpc-id,Values="$VPC_ID" \
  --query "Subnets[].SubnetId" \
  --output text))

if [ ${#SUBNET_IDS[@]} -lt 2 ]; then
  echo "RDS requires at least two subnets in the VPC." >&2
  exit 1
fi

echo "Checking DB subnet group..."

DB_SUBNET_GROUP_EXISTS=$(aws rds describe-db-subnet-groups \
  --region "$REGION" \
  --db-subnet-group-name "$DB_SUBNET_GROUP_NAME" \
  --query "DBSubnetGroups[0].DBSubnetGroupName" \
  --output text 2>/dev/null || true)

if [ "$DB_SUBNET_GROUP_EXISTS" != "$DB_SUBNET_GROUP_NAME" ]; then
  echo "Creating DB subnet group..."

  aws rds create-db-subnet-group \
    --region "$REGION" \
    --db-subnet-group-name "$DB_SUBNET_GROUP_NAME" \
    --db-subnet-group-description "Keycloak demo RDS subnet group" \
    --subnet-ids "${SUBNET_IDS[@]}" >/dev/null
fi

echo "Checking security group..."

SG_ID=$(aws ec2 describe-security-groups \
  --region "$REGION" \
  --filters Name=group-name,Values=keycloak-demo-sg \
  --query "SecurityGroups[0].GroupId" \
  --output text)

if [ "$SG_ID" = "None" ]; then
  echo "Creating security group..."

  SG_ID=$(aws ec2 create-security-group \
    --region "$REGION" \
    --group-name keycloak-demo-sg \
    --description "Keycloak demo SG" \
    --vpc-id "$VPC_ID" \
    --query GroupId \
    --output text)
fi

echo "Security group: $SG_ID"

echo "Opening instance ports..."

PORTS=(22 80 443 5173 5000 5001 8080 15672)

for PORT in "${PORTS[@]}"
do
  aws ec2 authorize-security-group-ingress \
    --region "$REGION" \
    --group-id "$SG_ID" \
    --protocol tcp \
    --port "$PORT" \
    --cidr 0.0.0.0/0 2>/dev/null || true
done

echo "Allowing PostgreSQL access from the same security group..."

aws ec2 authorize-security-group-ingress \
  --region "$REGION" \
  --group-id "$SG_ID" \
  --protocol tcp \
  --port 5432 \
  --source-group "$SG_ID" 2>/dev/null || true

echo "Ensuring EC2 IAM role and instance profile..."

ROLE_EXISTS=$(aws iam get-role \
  --role-name "$ROLE_NAME" \
  --query "Role.RoleName" \
  --output text 2>/dev/null || true)

if [ "$ROLE_EXISTS" != "$ROLE_NAME" ]; then
  cat > trust-policy.json <<'JSON'
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Principal": {
        "Service": "ec2.amazonaws.com"
      },
      "Action": "sts:AssumeRole"
    }
  ]
}
JSON

  aws iam create-role \
    --role-name "$ROLE_NAME" \
    --assume-role-policy-document file://trust-policy.json >/dev/null
fi

aws iam attach-role-policy \
  --role-name "$ROLE_NAME" \
  --policy-arn arn:aws:iam::aws:policy/AmazonSSMManagedInstanceCore >/dev/null 2>&1 || true

cat > ssm-parameter-policy.json <<JSON
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "ssm:GetParameter",
        "ssm:GetParametersByPath"
      ],
      "Resource": [
        "arn:aws:ssm:$REGION:$ACCOUNT_ID:parameter/keycloak-demo",
        "arn:aws:ssm:$REGION:$ACCOUNT_ID:parameter/keycloak-demo/*",
        "arn:aws:ssm:$REGION:$ACCOUNT_ID:parameter${DB_USERNAME_PARAMETER_NAME}",
        "arn:aws:ssm:$REGION:$ACCOUNT_ID:parameter${RDS_PASSWORD_PARAMETER_NAME}",
        "arn:aws:ssm:$REGION:$ACCOUNT_ID:parameter${KEYCLOAK_DB_USERNAME_PARAMETER_NAME}",
        "arn:aws:ssm:$REGION:$ACCOUNT_ID:parameter${KEYCLOAK_DB_PASSWORD_PARAMETER_NAME}",
        "arn:aws:ssm:$REGION:$ACCOUNT_ID:parameter${AUTH_DB_USERNAME_PARAMETER_NAME}",
        "arn:aws:ssm:$REGION:$ACCOUNT_ID:parameter${AUTH_DB_PASSWORD_PARAMETER_NAME}",
        "arn:aws:ssm:$REGION:$ACCOUNT_ID:parameter${APP_DB_USERNAME_PARAMETER_NAME}",
        "arn:aws:ssm:$REGION:$ACCOUNT_ID:parameter${APP_DB_PASSWORD_PARAMETER_NAME}",
        "arn:aws:ssm:$REGION:$ACCOUNT_ID:parameter${KEYCLOAK_DB_PARAMETER_NAME}",
        "arn:aws:ssm:$REGION:$ACCOUNT_ID:parameter${AUTH_DB_PARAMETER_NAME}",
        "arn:aws:ssm:$REGION:$ACCOUNT_ID:parameter${KEYCLOAK_ADMIN_PASSWORD_PARAMETER_NAME}"
      ]
    }
  ]
}
JSON

aws iam put-role-policy \
  --role-name "$ROLE_NAME" \
  --policy-name "$SSM_POLICY_NAME" \
  --policy-document file://ssm-parameter-policy.json >/dev/null

cat > rds-read-policy.json <<JSON
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "rds:DescribeDBInstances"
      ],
      "Resource": "arn:aws:rds:$REGION:$ACCOUNT_ID:db:$DB_ID"
    }
  ]
}
JSON

aws iam put-role-policy \
  --role-name "$ROLE_NAME" \
  --policy-name "keycloak-demo-rds-read" \
  --policy-document file://rds-read-policy.json >/dev/null

PROFILE_EXISTS=$(aws iam get-instance-profile \
  --instance-profile-name "$INSTANCE_PROFILE_NAME" \
  --query "InstanceProfile.InstanceProfileName" \
  --output text 2>/dev/null || true)

if [ "$PROFILE_EXISTS" != "$INSTANCE_PROFILE_NAME" ]; then
  aws iam create-instance-profile \
    --instance-profile-name "$INSTANCE_PROFILE_NAME" >/dev/null

  aws iam add-role-to-instance-profile \
    --instance-profile-name "$INSTANCE_PROFILE_NAME" \
    --role-name "$ROLE_NAME" >/dev/null

  echo "Waiting for instance profile propagation..."
  sleep 10
fi

echo "Checking RDS instance..."

RDS_EXISTS=$(aws rds describe-db-instances \
  --region "$REGION" \
  --db-instance-identifier "$DB_ID" \
  --query "DBInstances[0].DBInstanceIdentifier" \
  --output text 2>/dev/null || true)

if [ "$RDS_EXISTS" != "$DB_ID" ]; then
  echo "Creating RDS PostgreSQL instance..."

  aws rds create-db-instance \
    --region "$REGION" \
    --db-instance-identifier "$DB_ID" \
    --engine postgres \
    --engine-version 17.6 \
    --db-instance-class db.t4g.micro \
    --allocated-storage 20 \
    --storage-type gp2 \
    --master-username "$DB_USERNAME" \
    --master-user-password "$(get_required_parameter "$RDS_PASSWORD_PARAMETER_NAME" "true")" \
    --no-publicly-accessible \
    --db-subnet-group-name "$DB_SUBNET_GROUP_NAME" \
    --vpc-security-group-ids "$SG_ID" \
    --backup-retention-period 1 \
    --storage-encrypted \
    --enable-performance-insights \
    --performance-insights-retention-period 7 >/dev/null
else
  CURRENT_PUBLIC_ACCESS=$(aws rds describe-db-instances \
    --region "$REGION" \
    --db-instance-identifier "$DB_ID" \
    --query "DBInstances[0].PubliclyAccessible" \
    --output text)

  CURRENT_SUBNET_GROUP=$(aws rds describe-db-instances \
    --region "$REGION" \
    --db-instance-identifier "$DB_ID" \
    --query "DBInstances[0].DBSubnetGroup.DBSubnetGroupName" \
    --output text)

  if [ "$CURRENT_PUBLIC_ACCESS" != "False" ] || [ "$CURRENT_SUBNET_GROUP" != "$DB_SUBNET_GROUP_NAME" ]; then
    echo "Updating RDS instance to use private accessibility..."

    aws rds modify-db-instance \
      --region "$REGION" \
      --db-instance-identifier "$DB_ID" \
      --no-publicly-accessible \
      --db-subnet-group-name "$DB_SUBNET_GROUP_NAME" \
      --vpc-security-group-ids "$SG_ID" \
      --apply-immediately >/dev/null
  fi
fi

echo "Waiting for RDS instance to become available..."

aws rds wait db-instance-available \
  --region "$REGION" \
  --db-instance-identifier "$DB_ID"

RDS_ENDPOINT=$(aws rds describe-db-instances \
  --region "$REGION" \
  --db-instance-identifier "$DB_ID" \
  --query "DBInstances[0].Endpoint.Address" \
  --output text)

echo "RDS endpoint: $RDS_ENDPOINT"

echo "Preparing bootstrap script..."

cat <<EOF > user-data.sh
#!/bin/bash
set -euo pipefail

export DEBIAN_FRONTEND=noninteractive

apt update -y
apt remove -y docker docker-engine docker.io containerd runc || true
apt install -y ca-certificates curl gnupg git postgresql-client awscli jq

install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/ubuntu/gpg | gpg --dearmor -o /etc/apt/keyrings/docker.gpg
chmod a+r /etc/apt/keyrings/docker.gpg

echo \
  "deb [arch=\$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] \
  https://download.docker.com/linux/ubuntu \
  \$(. /etc/os-release && echo \$VERSION_CODENAME) stable" | \
  tee /etc/apt/sources.list.d/docker.list > /dev/null

apt update -y
apt install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin

systemctl enable docker
systemctl start docker

usermod -aG docker ubuntu

docker system prune -af || true
docker volume prune -f || true
docker compose version

TOKEN=\$(curl -fsX PUT "http://169.254.169.254/latest/api/token" -H "X-aws-ec2-metadata-token-ttl-seconds: 21600")
INSTANCE_PUBLIC_IP=\$(curl -fs -H "X-aws-ec2-metadata-token: \$TOKEN" http://169.254.169.254/latest/meta-data/public-ipv4)
DB_USERNAME=\$(aws ssm get-parameter --region "$REGION" --name "$DB_USERNAME_PARAMETER_NAME" --query "Parameter.Value" --output text)
DB_PASSWORD=\$(aws ssm get-parameter --region "$REGION" --name "$RDS_PASSWORD_PARAMETER_NAME" --with-decryption --query "Parameter.Value" --output text)
KEYCLOAK_DB_USERNAME=\$(aws ssm get-parameter --region "$REGION" --name "$KEYCLOAK_DB_USERNAME_PARAMETER_NAME" --query "Parameter.Value" --output text)
KEYCLOAK_DB_PASSWORD=\$(aws ssm get-parameter --region "$REGION" --name "$KEYCLOAK_DB_PASSWORD_PARAMETER_NAME" --with-decryption --query "Parameter.Value" --output text)
AUTH_DB_USERNAME=\$(aws ssm get-parameter --region "$REGION" --name "$AUTH_DB_USERNAME_PARAMETER_NAME" --query "Parameter.Value" --output text)
AUTH_DB_PASSWORD=\$(aws ssm get-parameter --region "$REGION" --name "$AUTH_DB_PASSWORD_PARAMETER_NAME" --with-decryption --query "Parameter.Value" --output text)
APP_DB_USERNAME=\$(aws ssm get-parameter --region "$REGION" --name "$APP_DB_USERNAME_PARAMETER_NAME" --query "Parameter.Value" --output text)
APP_DB_PASSWORD=\$(aws ssm get-parameter --region "$REGION" --name "$APP_DB_PASSWORD_PARAMETER_NAME" --with-decryption --query "Parameter.Value" --output text)
KEYCLOAK_DB_NAME=\$(aws ssm get-parameter --region "$REGION" --name "$KEYCLOAK_DB_PARAMETER_NAME" --query "Parameter.Value" --output text)
AUTH_DB_NAME=\$(aws ssm get-parameter --region "$REGION" --name "$AUTH_DB_PARAMETER_NAME" --query "Parameter.Value" --output text)
TASKS_DB_NAME="$TASKS_DB_NAME"
ORDERS_DB_NAME="$ORDERS_DB_NAME"
PAYMENTS_DB_NAME="$PAYMENTS_DB_NAME"

ensure_role_and_database() {
  local db_name="\$1"
  local app_username="\$2"
  local app_password="\$3"
  local escaped_password

  if [[ ! "\$app_username" =~ ^[A-Za-z_][A-Za-z0-9_]*$ ]]; then
    echo "Invalid database username: \$app_username" >&2
    exit 1
  fi

  escaped_password=\${app_password//\'/\'\'}

  PGPASSWORD="\$DB_PASSWORD" psql -h "$RDS_ENDPOINT" -U "\$DB_USERNAME" -d postgres \
    -c "DO \\\$do\\\$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = '\$app_username') THEN EXECUTE 'CREATE ROLE \"\$app_username\" LOGIN PASSWORD ''\$escaped_password'''; ELSE EXECUTE 'ALTER ROLE \"\$app_username\" WITH LOGIN PASSWORD ''\$escaped_password'''; END IF; END \\\$do\\\$;"

  EXISTS=\$(PGPASSWORD="\$DB_PASSWORD" psql -h "$RDS_ENDPOINT" -U "\$DB_USERNAME" -d postgres -tAc "SELECT 1 FROM pg_database WHERE datname = '\$db_name'")
  if [ "\$EXISTS" != "1" ]; then
    PGPASSWORD="\$DB_PASSWORD" psql -h "$RDS_ENDPOINT" -U "\$DB_USERNAME" -d postgres -c "CREATE DATABASE \\"\$db_name\\""
  fi

  PGPASSWORD="\$DB_PASSWORD" psql -h "$RDS_ENDPOINT" -U "\$DB_USERNAME" -d "\$db_name" \
    -c "GRANT CONNECT, TEMP ON DATABASE \"\$db_name\" TO \"\$app_username\"; GRANT USAGE, CREATE ON SCHEMA public TO \"\$app_username\"; GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO \"\$app_username\"; GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public TO \"\$app_username\"; ALTER DEFAULT PRIVILEGES FOR USER \"\$DB_USERNAME\" IN SCHEMA public GRANT ALL ON TABLES TO \"\$app_username\"; ALTER DEFAULT PRIVILEGES FOR USER \"\$DB_USERNAME\" IN SCHEMA public GRANT ALL ON SEQUENCES TO \"\$app_username\";"
}

ensure_role_and_database "\$KEYCLOAK_DB_NAME" "\$KEYCLOAK_DB_USERNAME" "\$KEYCLOAK_DB_PASSWORD"
ensure_role_and_database "\$AUTH_DB_NAME" "\$AUTH_DB_USERNAME" "\$AUTH_DB_PASSWORD"
ensure_role_and_database "\$TASKS_DB_NAME" "\$APP_DB_USERNAME" "\$APP_DB_PASSWORD"
ensure_role_and_database "\$ORDERS_DB_NAME" "\$APP_DB_USERNAME" "\$APP_DB_PASSWORD"
ensure_role_and_database "\$PAYMENTS_DB_NAME" "\$APP_DB_USERNAME" "\$APP_DB_PASSWORD"

sudo -u ubuntu bash <<INNER
set -euo pipefail

cd /home/ubuntu
git clone "$REPO_URL" project
cd project

rm -f .env

PUBLIC_HOST="\$INSTANCE_PUBLIC_IP" ENABLE_SELF_SIGNED_HTTPS="$ENABLE_SELF_SIGNED_HTTPS" bash ./scripts/run-compose-from-aws.sh up -d --build
INNER
EOF

USER_DATA=$(base64 -w 0 user-data.sh)

echo "Launching EC2 instance..."

INSTANCE_ID=$(aws ec2 run-instances \
  --region "$REGION" \
  --image-id "$AMI_ID" \
  --instance-type c7i-flex.large \
  --block-device-mappings '[{"DeviceName":"/dev/sda1","Ebs":{"VolumeSize":30,"VolumeType":"gp3","DeleteOnTermination":true}}]' \
  --key-name "$KEY_NAME" \
  --security-group-ids "$SG_ID" \
  --associate-public-ip-address \
  --iam-instance-profile Name="$INSTANCE_PROFILE_NAME" \
  --user-data "$USER_DATA" \
  --tag-specifications "ResourceType=instance,Tags=[{Key=Name,Value=$INSTANCE_NAME}]" \
  --query "Instances[0].InstanceId" \
  --output text)

echo "Instance launched: $INSTANCE_ID"

echo "Waiting for instance to start..."

aws ec2 wait instance-running \
  --region "$REGION" \
  --instance-ids "$INSTANCE_ID"

PUBLIC_IP=$(aws ec2 describe-instances \
  --region "$REGION" \
  --instance-ids "$INSTANCE_ID" \
  --query "Reservations[0].Instances[0].PublicIpAddress" \
  --output text)

rm -f user-data.sh trust-policy.json ssm-parameter-policy.json rds-read-policy.json

ALB_DNS_NAME=""
if [ -n "$CERT_ARN" ] && [ -n "$KEYCLOAK_PUBLIC_HOSTNAME" ] && [ -n "$APP_PUBLIC_HOSTNAME" ] && [ -n "$API_PUBLIC_HOSTNAME" ]; then
  echo ""
  echo "Provisioning HTTPS / ALB layer..."

  ALB_OUTPUT="$(
    CERT_ARN="$CERT_ARN" \
    KEYCLOAK_PUBLIC_HOSTNAME="$KEYCLOAK_PUBLIC_HOSTNAME" \
    APP_PUBLIC_HOSTNAME="$APP_PUBLIC_HOSTNAME" \
    API_PUBLIC_HOSTNAME="$API_PUBLIC_HOSTNAME" \
    HOSTED_ZONE_ID="$HOSTED_ZONE_ID" \
    LOCK_DOWN_PUBLIC_PORTS="$LOCK_DOWN_PUBLIC_PORTS" \
    APPLY_RUNTIME="$APPLY_SSL_RUNTIME" \
    INSTANCE_NAME="$INSTANCE_NAME" \
    APP_NAME="$INSTANCE_NAME" \
    AWS_REGION="$REGION" \
    ./scripts/provision-ssl-alb.sh
  )"
  echo "$ALB_OUTPUT"

  ALB_DNS_NAME="$(printf '%s\n' "$ALB_OUTPUT" | awk -F': ' '/^ALB DNS:/ {print $2}' | tail -n 1)"
fi

echo ""
echo "Server IP: $PUBLIC_IP"
echo "RDS endpoint: $RDS_ENDPOINT"
echo "SSM username parameter: $DB_USERNAME_PARAMETER_NAME"
echo "SSM password parameter: $RDS_PASSWORD_PARAMETER_NAME"
echo "SSM Keycloak DB username parameter: $KEYCLOAK_DB_USERNAME_PARAMETER_NAME"
echo "SSM Keycloak DB password parameter: $KEYCLOAK_DB_PASSWORD_PARAMETER_NAME"
echo "SSM auth DB username parameter: $AUTH_DB_USERNAME_PARAMETER_NAME"
echo "SSM auth DB password parameter: $AUTH_DB_PASSWORD_PARAMETER_NAME"
echo "SSM service DB username parameter: $APP_DB_USERNAME_PARAMETER_NAME"
echo "SSM service DB password parameter: $APP_DB_PASSWORD_PARAMETER_NAME"
echo "SSM Keycloak DB parameter: $KEYCLOAK_DB_PARAMETER_NAME"
echo "SSM auth DB parameter: $AUTH_DB_PARAMETER_NAME"
echo "Tasks DB name: $TASKS_DB_NAME"
echo "Orders DB name: $ORDERS_DB_NAME"
echo "Payments DB name: $PAYMENTS_DB_NAME"
echo "SSM Keycloak admin password parameter: $KEYCLOAK_ADMIN_PASSWORD_PARAMETER_NAME"
echo ""

echo "Services will be available soon (Docker may take ~2–4 minutes):"
if [ "$ENABLE_SELF_SIGNED_HTTPS" = "true" ]; then
  echo "Frontend:  https://$PUBLIC_IP"
  echo "Backend:   https://$PUBLIC_IP/api"
  echo "Auth API:  https://$PUBLIC_IP/auth-api"
  echo "Keycloak:  https://$PUBLIC_IP/admin/master/console/"
else
  echo "Frontend:  http://$PUBLIC_IP:5173"
  echo "Backend:   http://$PUBLIC_IP:5000"
  echo "Auth API:  http://$PUBLIC_IP:5001"
  echo "Keycloak:  http://$PUBLIC_IP:8080"
fi
echo "RabbitMQ:  http://$PUBLIC_IP:15672"

echo ""
echo "SSH command:"
echo "ssh -i $KEY_NAME.pem ubuntu@$PUBLIC_IP"

if [ -n "$ALB_DNS_NAME" ]; then
  echo ""
  echo "ALB DNS: $ALB_DNS_NAME"
  echo "Keycloak HTTPS: https://$KEYCLOAK_PUBLIC_HOSTNAME"
  echo "Frontend HTTPS: https://$APP_PUBLIC_HOSTNAME"
  echo "API HTTPS: https://$API_PUBLIC_HOSTNAME"
fi
