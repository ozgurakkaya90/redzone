#!/bin/bash
# AWS altyapısını kuran script
# Çalıştırmadan önce: chmod +x aws-setup.sh
# Kullanım: ./aws-setup.sh <key-pair-name> <db-password> <jwt-secret>

set -e

KEY_PAIR="${1:-risk-management-key}"
DB_PASSWORD="${2}"
JWT_SECRET="${3}"
REGION="eu-central-1"  # Frankfurt - Türkiye'ye en yakın
APP_NAME="risk-management"

if [ -z "$DB_PASSWORD" ] || [ -z "$JWT_SECRET" ]; then
  echo "Kullanım: $0 <key-pair-name> <db-password> <jwt-secret>"
  echo "Örnek: $0 my-key MySecurePass123! MyJwtSecret32CharLongString1234"
  exit 1
fi

echo "=== AWS Risk Management Deploy ==="
echo "Region: $REGION"
echo ""

# ─── 1. Key Pair ───────────────────────────────────────────────────────────────
echo "[1/6] Key pair kontrol ediliyor..."
if ! aws ec2 describe-key-pairs --key-names "$KEY_PAIR" --region $REGION &>/dev/null; then
  echo "  Key pair oluşturuluyor: $KEY_PAIR"
  aws ec2 create-key-pair \
    --key-name "$KEY_PAIR" \
    --region $REGION \
    --query 'KeyMaterial' \
    --output text > "${KEY_PAIR}.pem"
  chmod 400 "${KEY_PAIR}.pem"
  echo "  Key pair kaydedildi: ${KEY_PAIR}.pem — bunu kaybetme!"
else
  echo "  Key pair zaten mevcut: $KEY_PAIR"
fi

# ─── 2. VPC / Subnet bilgisi ───────────────────────────────────────────────────
echo "[2/6] VPC bilgisi alınıyor..."
VPC_ID=$(aws ec2 describe-vpcs --filters "Name=isDefault,Values=true" \
  --region $REGION --query 'Vpcs[0].VpcId' --output text)
SUBNET_ID=$(aws ec2 describe-subnets \
  --filters "Name=vpc-id,Values=$VPC_ID" "Name=defaultForAz,Values=true" \
  --region $REGION --query 'Subnets[0].SubnetId' --output text)
echo "  VPC: $VPC_ID | Subnet: $SUBNET_ID"

# ─── 3. Security Groups ────────────────────────────────────────────────────────
echo "[3/6] Security group oluşturuluyor..."

# RDS için SG
RDS_SG_ID=$(aws ec2 create-security-group \
  --group-name "${APP_NAME}-rds-sg" \
  --description "RDS MySQL for Risk Management" \
  --vpc-id $VPC_ID \
  --region $REGION \
  --query 'GroupId' --output text 2>/dev/null || \
  aws ec2 describe-security-groups \
    --filters "Name=group-name,Values=${APP_NAME}-rds-sg" \
    --region $REGION --query 'SecurityGroups[0].GroupId' --output text)

# EC2 için SG
EC2_SG_ID=$(aws ec2 create-security-group \
  --group-name "${APP_NAME}-ec2-sg" \
  --description "EC2 for Risk Management" \
  --vpc-id $VPC_ID \
  --region $REGION \
  --query 'GroupId' --output text 2>/dev/null || \
  aws ec2 describe-security-groups \
    --filters "Name=group-name,Values=${APP_NAME}-ec2-sg" \
    --region $REGION --query 'SecurityGroups[0].GroupId' --output text)

# EC2 SG: SSH ve HTTP izinleri
aws ec2 authorize-security-group-ingress \
  --group-id $EC2_SG_ID --protocol tcp --port 22 --cidr 0.0.0.0/0 \
  --region $REGION 2>/dev/null || true
aws ec2 authorize-security-group-ingress \
  --group-id $EC2_SG_ID --protocol tcp --port 80 --cidr 0.0.0.0/0 \
  --region $REGION 2>/dev/null || true
aws ec2 authorize-security-group-ingress \
  --group-id $EC2_SG_ID --protocol tcp --port 8080 --cidr 0.0.0.0/0 \
  --region $REGION 2>/dev/null || true

# RDS SG: sadece EC2 SG'den 3306
aws ec2 authorize-security-group-ingress \
  --group-id $RDS_SG_ID --protocol tcp --port 3306 \
  --source-group $EC2_SG_ID \
  --region $REGION 2>/dev/null || true

echo "  EC2 SG: $EC2_SG_ID | RDS SG: $RDS_SG_ID"

# ─── 4. RDS MySQL ──────────────────────────────────────────────────────────────
echo "[4/6] RDS MySQL oluşturuluyor (bu ~5 dakika sürebilir)..."
aws rds create-db-instance \
  --db-instance-identifier "${APP_NAME}-db" \
  --db-instance-class db.t3.micro \
  --engine mysql \
  --engine-version "8.0" \
  --master-username riskapp \
  --master-user-password "$DB_PASSWORD" \
  --db-name RiskManagement \
  --allocated-storage 20 \
  --vpc-security-group-ids $RDS_SG_ID \
  --no-publicly-accessible \
  --no-multi-az \
  --region $REGION 2>/dev/null || echo "  RDS zaten mevcut, devam ediliyor..."

echo "  RDS oluşturuluyor, EC2 kurulumuna geçiliyor..."

# ─── 5. EC2 Instance ───────────────────────────────────────────────────────────
echo "[5/6] EC2 instance oluşturuluyor..."
AMI_ID=$(aws ec2 describe-images \
  --owners amazon \
  --filters "Name=name,Values=al2023-ami-*-arm64" "Name=state,Values=available" \
  --query 'sort_by(Images, &CreationDate)[-1].ImageId' \
  --region $REGION --output text)
echo "  AMI: $AMI_ID (Amazon Linux 2023 ARM)"

INSTANCE_ID=$(aws ec2 run-instances \
  --image-id $AMI_ID \
  --instance-type t4g.small \
  --key-name "$KEY_PAIR" \
  --security-group-ids $EC2_SG_ID \
  --subnet-id $SUBNET_ID \
  --associate-public-ip-address \
  --user-data file://deploy/ec2-userdata.sh \
  --tag-specifications "ResourceType=instance,Tags=[{Key=Name,Value=${APP_NAME}}]" \
  --region $REGION \
  --query 'Instances[0].InstanceId' --output text)

echo "  EC2 başlatıldı: $INSTANCE_ID"

# ─── 6. Bilgileri kaydet ───────────────────────────────────────────────────────
echo "[6/6] Bekleniyor ve bilgiler kaydediliyor..."

# EC2 IP bekle
echo "  EC2 IP bekleniyor..."
aws ec2 wait instance-running --instance-ids $INSTANCE_ID --region $REGION
EC2_IP=$(aws ec2 describe-instances \
  --instance-ids $INSTANCE_ID \
  --region $REGION \
  --query 'Reservations[0].Instances[0].PublicIpAddress' --output text)

# RDS endpoint bekle (arka planda devam ediyor, bilgisi için sorgula)
RDS_ENDPOINT=$(aws rds describe-db-instances \
  --db-instance-identifier "${APP_NAME}-db" \
  --region $REGION \
  --query 'DBInstances[0].Endpoint.Address' --output text 2>/dev/null || echo "RDS henüz hazır değil")

cat > deploy/env-production.txt << EOF
EC2_IP=$EC2_IP
EC2_INSTANCE_ID=$INSTANCE_ID
RDS_ENDPOINT=$RDS_ENDPOINT
DB_NAME=RiskManagement
DB_USER=riskapp
DB_PASSWORD=$DB_PASSWORD
JWT_SECRET=$JWT_SECRET
REGION=$REGION
KEY_PAIR=$KEY_PAIR
EOF

echo ""
echo "=== KURULUM TAMAMLANDI ==="
echo "EC2 IP: $EC2_IP"
echo "RDS Endpoint: $RDS_ENDPOINT (henüz hazır olmayabilir, ~5dk bekle)"
echo ""
echo "Sonraki adım:"
echo "  ./deploy/deploy-app.sh"
